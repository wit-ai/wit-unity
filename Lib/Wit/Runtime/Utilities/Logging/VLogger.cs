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
using System.Linq;
using System.Threading;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    internal class VLogger : IVLogger
    {
        /// <summary>
        /// The sequence ID of the next scope to open.
        /// </summary>
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
        /// Tracks log entries that are part of a specific correlation ID.
        /// </summary>
        private static readonly RingDictionaryBuffer<CorrelationID, LogEntry> LogBuffer = new RingDictionaryBuffer<CorrelationID, LogEntry>(1000);

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
        /// The log sink where log data is written.
        /// </summary>
        private readonly ILogSink _logSink;

        /// <summary>
        /// The category of the logger.
        /// </summary>
        private readonly string _category;

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

        internal VLogger(string category, ILogSink logSink)
        {
            _category = category;
            _logSink = logSink;
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
            var stackTrace = new StackTrace(true);
            LogEntry(new LogEntry(_category, verbosity, correlationId, stackTrace, String.Empty, message, parameters));
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity,
            Exception exception, ErrorCode errorCode,
            string message, params object[] parameters)
        {
            var stackTrace = new StackTrace(true);
            LogEntry(new LogEntry(_category, verbosity, correlationId, errorCode, exception, stackTrace, string.Empty, message,
                parameters));
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            var stackTrace = new StackTrace(true);
            LogEntry(new LogEntry(_category, verbosity, correlationId, errorCode, stackTrace, string.Empty, message, parameters));
        }

        private void LogEntry(LogEntry logEntry)
        {
            if (IsSuppressed(logEntry))
            {
                LogBuffer.Add(logEntry.CorrelationID, logEntry);
                return;
            }

            if (IsFiltered(logEntry))
            {
                LogBuffer.Add(logEntry.CorrelationID, logEntry);
                return;
            }

            Write(logEntry);
        }

        /// <summary>
        /// Returns true if the log should be filtered out and not written.
        /// </summary>
        /// <param name="logEntry">The log entry.</param>
        /// <returns>True if it is filtered/suppressed. False otherwise.</returns>
        private bool IsFiltered(LogEntry logEntry)
        {
#if UNITY_EDITOR
            if (logEntry.Verbosity < _logSink.Options.MinimumVerbosity)
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
            if (VLog.SuppressLogs && (int)logEntry.Verbosity < (int)VLoggerVerbosity.Error)
            {
                return true;
            }

#if UNITY_EDITOR
            if (logEntry.Verbosity <= _logSink.Options.SuppressionLevel)
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
            var stackTrace = new StackTrace(true);
            var logEntry = new LogEntry(_category, verbosity, correlationId, stackTrace, "Started: ", message, parameters);
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
            var stackTrace = new StackTrace(true);
            var logEntry = new LogEntry(_category, verbosity, CorrelationID, stackTrace, "Started: ", message, parameters);
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
                Write(logEntry, true);
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
                Write(logEntry, true);
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
        /// <param name="force">When true will not suppress.</param>
        private void Write(LogEntry logEntry, bool force = false)
        {
            if (logEntry.Verbosity == VLoggerVerbosity.Error)
            {
                Flush(logEntry.CorrelationID);
            }

            // Suppress all except errors if needed
            if (!force & IsSuppressed(logEntry))
            {
                return;
            }

            _logSink.WriteEntry(logEntry);
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
    }
}
