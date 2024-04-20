/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    internal class VLogger : IVLogger
    {
        private int _nextSequenceId = 1;

        /// <summary>
        /// Tracks open scopes by associating sequence ID to log entries.
        /// </summary>
        private readonly Dictionary<int, LogEntry> _scopeEntries = new Dictionary<int, LogEntry>();

        /// <summary>
        /// Holds the Correlation ID in the same thread.
        /// </summary>
        private static readonly ThreadLocal<string> CorrelationIDThreadLocal = new ThreadLocal<string>();

        /// <summary>
        /// Used to get mitigations for errors.
        /// </summary>
        private static readonly ErrorMitigator ErrorMitigator = ErrorMitigator.Instance;

        /// <summary>
        /// Tracks log entries that are part of a specific correlation ID.
        /// </summary>
        private static readonly RingDictionaryBuffer<CorrelationID, LogEntry> LogBuffer = new RingDictionaryBuffer<CorrelationID, LogEntry>(1000);

        /// <summary>
        /// Caches the last few messages so we can omit repeated correlation IDs.
        /// </summary>
        private readonly RingDictionaryBuffer<string, CorrelationID> _messagesCache =
            new RingDictionaryBuffer<string, CorrelationID>(100);

        /// <summary>
        /// Caches correlations between IDs. When something branches or exchanges identity, we mark the correlation.
        /// This allows us to bring everything related when correlating.
        /// The key is the thing we are correlating (eg. child operation) and the value is the target (eg. root).
        /// This the value list should contain exactly one element given the current implementation.
        /// </summary>
        private readonly RingDictionaryBuffer<CorrelationID, CorrelationID> _correlations =
            new RingDictionaryBuffer<CorrelationID, CorrelationID>(100);

        /// <summary>
        /// This is an inverse correlation dictionary holding the "child" correlations.
        /// The key is the root operation and the value is the target (eg. child operation).
        /// </summary>
        private readonly RingDictionaryBuffer<CorrelationID, CorrelationID> _downStreamCorrelations =
            new RingDictionaryBuffer<CorrelationID, CorrelationID>(100);

        /// <summary>
        /// The final log sink where log data is written.
        /// </summary>
        private readonly ILogWriter _logWriter;

        /// <summary>
        /// The category of the logger.
        /// </summary>
        private readonly string _category;

        /// <summary>
        /// The options that control the logging.
        /// </summary>
        private readonly Lazy<LoggerOptions> _options;

        /// <inheritdoc/>
        /// <inheritdoc/>
        public VLoggerVerbosity MinimumVerbosity
        {
            get => _options.Value.MinimumVerbosity;
            set => _options.Value.MinimumVerbosity = value;
        }

        /// <inheritdoc/>
        public VLoggerVerbosity SuppressionLevel
        {
            get => _options.Value.SuppressionLevel;
            set => _options.Value.SuppressionLevel = value;
        }

        /// <summary>
        /// The current correlation ID of this logger. This can be changed at will.
        /// </summary>
        private CorrelationID _correlationID;

        /// <inheritdoc/>
        public CorrelationID CorrelationID
        {
            get
            {
                if (_correlationID.IsAssigned)
                {
                    return _correlationID;
                }

                if (!CorrelationIDThreadLocal.IsValueCreated)
                {
                    CorrelationIDThreadLocal.Value = Guid.NewGuid().ToString();
                }

                _correlationID = (CorrelationID)CorrelationIDThreadLocal.Value;

                return _correlationID;
            }
            set
            {
                _correlationID = value;
                CorrelationIDThreadLocal.Value = _correlationID;
            }
        }

        internal VLogger(string category, ILogWriter logWriter, Lazy<LoggerOptions> options)
        {
            _category = category;
            _logWriter = logWriter;
            _options = options;
        }

        /// <summary>
        /// Clears the log buffer.
        /// </summary>
        public static void ClearBuffer()
        {
            LogBuffer.Clear();
        }

        /// <summary>
        /// Sets the correlation ID if it's not already set. If it's set and different, will correlate the two.
        /// </summary>
        private void CorrelateIds(CorrelationID correlationId)
        {
            if (!_correlationID.IsAssigned)
            {
                CorrelationID = correlationId;
            }

            if (CorrelationID != correlationId)
            {
                if (_correlations.ContainsKey(correlationId))
                {
                    // We only correlate if the ID hasn't already been correlated.
                    return;
                }
                Correlate(correlationId, CorrelationID);
            }
        }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Verbose, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Verbose, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object [] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Error, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object [] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Error, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Error, errorCode, exception, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters)
        {
            Log(CorrelationID, VLoggerVerbosity.Error, errorCode, exception, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            CorrelateIds(correlationId);
            Log(correlationId, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Correlate(CorrelationID newCorrelationId, CorrelationID rootCorrelationId)
        {
            var correlated = _correlations.Add(newCorrelationId, rootCorrelationId, true);
            correlated |= _downStreamCorrelations.Add(rootCorrelationId, newCorrelationId, true);

            if (!correlated)
            {
                // Attempted to correlate already correlated entries.
                return;
            }

            Log(newCorrelationId, VLoggerVerbosity.Verbose, "Correlated: {0}", newCorrelationId);
        }

        /// <inheritdoc/>
        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationId, String.Empty, message, parameters);

            if (IsSuppressed(logEntry))
            {
                LogBuffer.Add(correlationId, logEntry);
            }
            else
            {
                if (IsFiltered(logEntry))
                {
                    return;
                }

                Write(logEntry);
            }
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity,
            Exception exception, ErrorCode errorCode,
            string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationId, errorCode, exception, string.Empty, message, parameters);

            if (IsFiltered(logEntry))
            {
                return;
            }

            if (IsSuppressed(logEntry))
            {
                LogBuffer.Add(correlationId, logEntry);
            }
            else
            {
                Write(logEntry);
            }
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationId, errorCode, message, parameters);

            if (IsFiltered(logEntry))
            {
                return;
            }

            if (IsSuppressed(logEntry))
            {
                LogBuffer.Add(correlationId, logEntry);
            }
            else
            {
                Write(logEntry);
            }
        }

        /// <summary>
        /// Returns true if the log should be filtered out and not written.
        /// </summary>
        /// <param name="logEntry">The log entry.</param>
        /// <returns>True if it is filtered/suppressed. False otherwise.</returns>
        private bool IsFiltered(LogEntry logEntry)
        {
#if UNITY_EDITOR
            if (logEntry.Verbosity < MinimumVerbosity)
            {
                return true;
            }

            if (VLog.FilteredTagSet.Contains(logEntry.Category))
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsSuppressed(LogEntry logEntry)
        {
#if UNITY_EDITOR
            if (logEntry.Verbosity <= SuppressionLevel)
            {
                return true;
            }
#endif
            return false;
        }

        /// <inheritdoc/>
        public LogScope Scope(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public LogScope Scope(VLoggerVerbosity verbosity, CorrelationID correlationId, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            CorrelateIds(correlationId);
            var logEntry = new LogEntry(_category, verbosity, correlationId, "Started: ", message, parameters);
            LogBuffer.Add(correlationId, logEntry);
            _scopeEntries.Add(_nextSequenceId, logEntry);

            if (!IsFiltered(logEntry))
            {
                Write(logEntry);
            }

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, CorrelationID, "Started: ", message, parameters);
            LogBuffer.Add(CorrelationID, logEntry);
            _scopeEntries.Add(_nextSequenceId, logEntry);

            if (!IsFiltered(logEntry))
            {
                Write(logEntry);
            }

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            if (!_scopeEntries.ContainsKey(sequenceId))
            {
                Error(KnownErrorCode.Logging, "Attempted to end a scope that was not started. Scope ID: {0}", sequenceId);
                return;
            }

            var openingEntry = _scopeEntries[sequenceId];
            if (!IsFiltered(openingEntry))
            {
                openingEntry.Prefix = "Finished: ";
                Write(openingEntry);
            }

            _scopeEntries.Remove(sequenceId);
        }

        /// <inheritdoc/>
        public void Flush(CorrelationID correlationID)
        {
            var allRelatedEntries = ExtractRelatedEntries(correlationID);

            allRelatedEntries.Sort();

            foreach (var logEntry in allRelatedEntries)
            {
                Write(logEntry);
            }
        }

        /// <summary>
        /// Obtains and removes related entries for the specified correlation ID.
        /// </summary>
        /// <param name="correlationID">The correlation ID.</param>
        /// <returns>The entries extracted.</returns>
        private List<LogEntry> ExtractRelatedEntries(CorrelationID correlationID)
        {
            var allRelatedEntries = new List<LogEntry>();
            var currentId = correlationID;

            // First we get upstream correlation IDs from the parents chain.
            allRelatedEntries.AddRange(LogBuffer.Extract(currentId));
            while (_correlations.ContainsKey(currentId))
            {
                if (_correlations[currentId].Count > 1)
                {
                    Warning(correlationID, KnownErrorCode.Logging, "Correlation ID {0} had multiple parent IDs. Found: {1} IDs.", correlationID, _correlations[currentId].Count );
                }

                currentId = _correlations[currentId].First();
                allRelatedEntries.AddRange(LogBuffer.Extract(currentId));
            }

            // Then we follow the downstream tree recursively.
            ExtractDownstreamRelatedEntries(correlationID, ref allRelatedEntries);

            return allRelatedEntries;
        }

        private void ExtractDownstreamRelatedEntries(CorrelationID correlationID, ref List<LogEntry> entries)
        {
            if (!_downStreamCorrelations.ContainsKey(correlationID))
            {
                return;
            }

            foreach (var relatedId in _downStreamCorrelations[correlationID])
            {
                entries.AddRange(LogBuffer.Extract(relatedId));
                ExtractDownstreamRelatedEntries(relatedId, ref entries);
            }
        }


        /// <inheritdoc/>
        public void Flush()
        {
            foreach (var logEntry in LogBuffer.ExtractAll())
            {
                Write(logEntry);
            }
        }

        /// <summary>
        /// Extract all the suppressed entries and returns them.
        /// </summary>
        /// <returns>All the log entries.</returns>
        public IEnumerable<LogEntry> ExtractAllEntries()
        {
            return LogBuffer.ExtractAll();
        }

        /// <summary>
        /// Applies any relevant filtering and formatting, then writes to the log sink.
        /// </summary>
        /// <param name="logEntry">The entry to write.</param>
        /// <param name="prefix">Any prefix that should go before the log.</param>
        private void Write(LogEntry logEntry)
        {
            if (logEntry.Verbosity == VLoggerVerbosity.Error)
            {
                Flush(logEntry.CorrelationID);
            }

            // Suppress all except errors if needed
            if (VLog.SuppressLogs && (int)logEntry.Verbosity < (int)VLoggerVerbosity.Error)
            {
                return;
            }

            var sb = new StringBuilder();

#if !UNITY_EDITOR && !UNITY_ANDROID
            {
                // Start with datetime if not done so automatically
                DateTime now = DateTime.Now;
                sb.Append($"[{now.ToShortDateString()} {now.ToShortTimeString()}] ");
            }
#endif

            // Insert log type
            var start = sb.Length;
            sb.Append($"[VSDK {logEntry.Verbosity.ToString().ToUpper()}] ");

            if (_options.Value.ColorLogs)
            {
                WrapWithLogColor(sb, start, logEntry.Verbosity);
            }

            // Append VDSK & Category
            start = sb.Length;
            if (!string.IsNullOrEmpty(logEntry.Category))
            {
                sb.Append($"[{logEntry.Category}] ");
            }

            if (_options.Value.LinkToCallSite)
            {
                WrapWithCallingLink(sb, start);
            }

            var formattedCoreMessage =
                (!string.IsNullOrEmpty(logEntry.Message) && logEntry.Parameters != null &&
                 logEntry.Parameters.Length != 0)
                    ? string.Format(logEntry.Message, logEntry.Parameters)
                    : logEntry.Message;

            sb.Append(formattedCoreMessage);

            // Append the correlation ID if not repeated.
            // We use the formatted message so we split on different parameter values even for same format string.
            if (_messagesCache.ContainsKey(formattedCoreMessage))
            {
                // Move it to the top of the cache.
                var lastId = _messagesCache.Extract(logEntry.Message);
                _messagesCache.Add(logEntry.Message, logEntry.CorrelationID);

                if (lastId.First() == logEntry.CorrelationID)
                {
                    sb.Append($" [{logEntry.CorrelationID}]");
                }
                else
                {
                    sb.Append($" [...]");
                }
            }
            else
            {
                sb.Append($" [{logEntry.CorrelationID}]");
                _messagesCache.Add(logEntry.Message, logEntry.CorrelationID);
            }


            if (logEntry.ErrorCode.HasValue && logEntry.ErrorCode.Value != null)
            {
                // The mitigator may not be available if the error is coming from the mitigator constructor itself.
                if (ErrorMitigator != null)
                {
                    sb.Append("\nMitigation: ");
                    sb.Append(ErrorMitigator.GetMitigation(logEntry.ErrorCode.Value));
                }
            }

            var message = sb.ToString();
            if (logEntry.Exception != null)
            {
#if UNITY_EDITOR
                message = string.Format("{0}\n<color=\"#ff6666\"><b>{1}:</b> {2}</color>\n=== STACK TRACE ===\n{3}\n=====", sb, logEntry.Exception.GetType().Name, logEntry.Exception.Message, FormatStackTrace(logEntry.Exception.StackTrace));
#endif
            }

            logEntry.Message = message;

            WriteToSink(logEntry);
        }

        /// <summary>
        /// Produce a structure of the correlations.
        /// </summary>
        /// <param name="correlationID">The starting correlation ID. If null, will start at current ID.</param>
        /// <param name="depth">The depth in the dependencies tree.</param>
        /// <returns>A string showing the tree structure of the correlation IDs.</returns>
        internal string GetDependenciesStructure(CorrelationID? correlationID = null, int depth = 0)
        {
            var currentId = correlationID ?? CorrelationID;
            var output = new string(' ', depth * 2) + currentId;

            if (_downStreamCorrelations.ContainsKey(currentId))
            {
                foreach (var downstreamCorrelation in _downStreamCorrelations[currentId])
                {
                    output = output + "\n" + GetDependenciesStructure(downstreamCorrelation, depth + 1);
                }
            }

            return output;
        }

        private void WriteToSink(LogEntry logEntry)
        {
            _logWriter.WriteEntry(logEntry);
        }

        private static void WrapWithCallingLink(StringBuilder builder, int startIndex)
        {
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            var stackTrace = new StackTrace(true);
            for (int i = 3; i < stackTrace.FrameCount; i++)
            {
                var stackFrame = stackTrace.GetFrame(i);
                var method = stackFrame.GetMethod();
                if (IsLoggingClass(method.DeclaringType))
                {
                    continue;
                }

                var callingFileName = stackFrame.GetFileName()?.Replace('\\', '/');
                var callingFileLine = stackFrame.GetFileLineNumber();
                builder.Insert(startIndex, $"<a href=\"{callingFileName}\" line=\"{callingFileLine}\">");
                builder.Append("</a>");
                return;
            }
#endif
        }

        private static bool IsLoggingClass(Type type)
        {
            return typeof(IVLogger).IsAssignableFrom(type) || typeof(ILogWriter).IsAssignableFrom(type) || type == typeof(VLog);
        }

        /// <summary>
        /// Get hex value for each log type
        /// </summary>
        private static void WrapWithLogColor(StringBuilder builder, int startIndex, VLoggerVerbosity logType)
        {
#if UNITY_EDITOR
            string hex;
            switch (logType)
            {
                case VLoggerVerbosity.Error:
                    hex = "FF0000";
                    break;
                case VLoggerVerbosity.Warning:
                    hex = "FFFF00";
                    break;
                default:
                    hex = "00FF00";
                    break;
            }
            builder.Insert(startIndex, $"<color=#{hex}>");
            builder.Append("</color>");
#endif
        }

        private static string FormatStackTrace(string stackTrace)
        {
            // Get the project's working directory
            var workingDirectory = Directory.GetCurrentDirectory();
            // Use a regular expression to match lines with a file path and line number
            var regex = new Regex(@"at (.+) in (.*):(\d+)");
            // Use the MatchEvaluator delegate to format the matched lines
            string Evaluator(Match match)
            {
                var method = match.Groups[1].Value;
                var filePath = match.Groups[2].Value.Replace(workingDirectory, "");
                var lineNumber = match.Groups[3].Value;
                // Only format the line as a clickable link if the file exists
                if (File.Exists(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    return $"at {method} in <a href=\"{filePath}\" line=\"{lineNumber}\">{fileName}:<b>{lineNumber}</b></a>";
                }
                else
                {
                    return match.Value;
                }
            }

            // Replace the matched lines in the stack trace
            var formattedStackTrace = regex.Replace(stackTrace, (MatchEvaluator)Evaluator);
            return formattedStackTrace;
        }

        /// <summary>
        /// This class will maintain a cache of entries and the oldest ones will expire when it runs out of space.
        /// Each time an item is added to a key, that key's freshness is refreshed.
        /// Each key is associated with a list of entries.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        private class RingDictionaryBuffer<TKey, TValue>
        {
            private readonly int _capacity;
            private readonly Dictionary<TKey, LinkedList<TValue>> _dictionary;
            private readonly LinkedList<ValueTuple<TKey, TValue>> _order;
            public RingDictionaryBuffer(int capacity)
            {
                _capacity = capacity;
                _dictionary = new Dictionary<TKey, LinkedList<TValue>>();
                _order = new LinkedList<ValueTuple<TKey, TValue>>();
            }

            public ICollection<TValue> this[TKey key] => _dictionary[key];

            /// <summary>
            /// Adds an entry to the key. This also updates the "freshness" of the entry.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="value">The value to add.</param>
            /// <param name="unique">Will only add the value if it does not already exist.</param>
            /// <returns>True if the key value was added. False otherwise.</returns>
            public bool Add(TKey key, TValue value, bool unique = false)
            {
                if (!_dictionary.ContainsKey(key))
                {
                    _dictionary[key] = new LinkedList<TValue>();
                }

                if (unique && _dictionary[key].Contains(value))
                {
                    return false;
                }

                _dictionary[key].AddLast(value);
                _order.AddLast(ValueTuple.Create(key, value));
                if (_order.Count > _capacity)
                {
                    var oldest = _order.First.Value;
                    _order.RemoveFirst();
                    _dictionary[oldest.Item1].RemoveFirst();
                    if (_dictionary[oldest.Item1].Count == 0)
                    {
                        _dictionary.Remove(oldest.Item1);
                    }
                }

                return true;
            }

            /// <summary>
            /// Returns true if the key exists in the buffer.
            /// </summary>
            /// <param name="key">The key to check.</param>
            /// <returns>True if the key exists in the buffer. False otherwise.</returns>
            public bool ContainsKey(TKey key)
            {
                return _dictionary.ContainsKey(key);
            }

            /// <summary>
            /// Drain all the entries from the buffer that match a given key and return them.
            /// </summary>
            /// <param name="key">The key we are extracting.</param>
            /// <returns>All the entries in the buffer for that specific key.</returns>
            public IEnumerable<TValue> Extract(TKey key)
            {
                if (_dictionary.ContainsKey(key))
                {
                    var values = new List<TValue>(_dictionary[key]);
                    _dictionary.Remove(key);
                    var node = _order.First;
                    while (node != null)
                    {
                        var nextNode = node.Next; // Save next node
                        if (node.Value.Item1.Equals(key))
                        {
                            _order.Remove(node); // Remove current node
                        }
                        node = nextNode; // Move to next node
                    }
                    return values;
                }
                else
                {
                    return new List<TValue>();
                }
            }

            /// <summary>
            /// Drain all the entries from the buffer and return them.
            /// </summary>
            /// <returns>All the entries in the buffer ordered by the key (e.g. correlation IDs).</returns>
            public IEnumerable<TValue> ExtractAll()
            {
                var allValues = new List<TValue>();
                foreach (var correlationId in new List<TKey>(_dictionary.Keys))
                {
                    allValues.AddRange(Extract(correlationId));
                }
                return allValues;
            }

            /// <summary>
            /// Clears the buffer.
            /// </summary>
            public void Clear()
            {
                _dictionary.Clear();
                _order.Clear();
            }
        }
    }
}
