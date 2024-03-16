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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    internal class VLogger : IVLogger
    {
        private readonly Dictionary<int, LogEntry> _logEntries = new Dictionary<int, LogEntry>();
        private int _nextSequenceId = 1;
        private static readonly ThreadLocal<string> CorrelationIDThreadLocal = new ThreadLocal<string>();
        private static readonly ErrorMitigator _errorMitigator = ErrorMitigator.Instance;

        /// <inheritdoc/>
        public CorrelationID CorrelationID
        {
            get
            {
                if (!CorrelationIDThreadLocal.IsValueCreated)
                {
                    CorrelationIDThreadLocal.Value = Guid.NewGuid().ToString();
                }

                return (CorrelationID)CorrelationIDThreadLocal.Value;
            }
            set => CorrelationIDThreadLocal.Value = value;
        }

        private readonly string _category;

        public VLogger(string category)
        {
            _category = category;
        }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Verbose, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Verbose, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Info, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Info, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Log, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Log, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Warning, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Error, correlationId, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Error, CorrelationID, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, Exception exception, string message, params object[] parameters)
        {
            Log(VLoggerVerbosity.Error, correlationId, errorCode, exception, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters)
        {
            Log(VLoggerVerbosity.Error, CorrelationID, errorCode, exception, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLoggerVerbosity.Warning, correlationId, message, parameters);
        }

        private void Log(VLoggerVerbosity verbosity, CorrelationID correlationID, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationID, message, parameters);
            Write(logEntry);
        }

        private void Log(VLoggerVerbosity verbosity, CorrelationID correlationID, ErrorCode errorCode, Exception exception, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationID, errorCode, exception, message, parameters);
            Write(logEntry);
        }

        private void Log(VLoggerVerbosity verbosity, CorrelationID correlationID, ErrorCode errorCode, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationID, errorCode, message, parameters);
            Write(logEntry);
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
        public int Start(VLoggerVerbosity verbosity, CorrelationID correlationId, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationId, message, parameters);
            _logEntries.Add(_nextSequenceId, logEntry);

            Write(logEntry, "Started: ");

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, CorrelationID, message, parameters);
            _logEntries.Add(_nextSequenceId, logEntry);

            Write(logEntry, "Started: ");

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            if (!_logEntries.ContainsKey(sequenceId))
            {
                Error(KnownErrorCode.Logging, "Attempted to end a scope that was not started. Scope ID: {0}", sequenceId);
                return;
            }

            var openingEntry = _logEntries[sequenceId];
            Write(openingEntry, "Finished: ");

            _logEntries.Remove(sequenceId);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            foreach (var logEntry in _logEntries)
            {
                Write(logEntry.Value);
            }
        }

        /// <summary>
        /// Applies any relevant filtering and formatting, then writes to the log sink.
        /// </summary>
        /// <param name="logEntry">The entry to write.</param>
        /// <param name="prefix">Any prefix that should go before the log.</param>
        private void Write(LogEntry logEntry, string prefix)
        {
#if UNITY_EDITOR
            // Skip logs with higher log type then global log level
            if ((int) logEntry.Verbosity < (int)VLog.EditorLogLevel)
            {
                return;
            }

            if (VLog.FilteredTagSet.Contains(logEntry.Category))
            {
                return;
            }
#endif

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
            WrapWithLogColor(sb, start, logEntry.Verbosity);

            // Append VDSK & Category
            start = sb.Length;
            if (!string.IsNullOrEmpty(logEntry.Category))
            {
                sb.Append($"[{logEntry.Category}] ");
            }
            WrapWithCallingLink(sb, start);

            // Append the actual log
            sb.Append(logEntry.Message == null ? string.Empty : string.Format(logEntry.Message, logEntry.Parameters));

            if (logEntry.ErrorCode.HasValue && logEntry.ErrorCode.Value != null)
            {
                // The mitigator may not be available if the error is coming from the mitigator constructor itself.
                if (_errorMitigator != null)
                {
                    sb.Append("\nMitigation: ");
                    sb.Append(_errorMitigator.GetMitigation(logEntry.ErrorCode.Value));
                }
            }

            var message = sb.ToString();
            if (logEntry.Exception != null)
            {
#if UNITY_EDITOR
                message = string.Format("{0}\n<color=\"#ff6666\"><b>{1}:</b> {2}</color>\n=== STACK TRACE ===\n{3}\n=====", sb, logEntry.Exception.GetType().Name, logEntry.Exception.Message, FormatStackTrace(logEntry.Exception.StackTrace));
#endif
            }

            switch (logEntry.Verbosity)
            {
                case VLoggerVerbosity.Error:
#if UNITY_EDITOR
                    if (VLog.LogErrorsAsWarnings)
                    {
                        UnityEngine.Debug.LogWarning($"{prefix}{message}");
                        return;
                    }
#endif
                    UnityEngine.Debug.LogError($"{prefix}{message}");
                    break;
                case VLoggerVerbosity.Warning:
                    UnityEngine.Debug.LogWarning($"{prefix}{message}");
                    break;
                default:
                    UnityEngine.Debug.Log($"{prefix}{message}");
                    break;
            }
        }

        private void Write(LogEntry logEntry)
        {
            Write(logEntry, String.Empty);
        }

        private static void WrapWithCallingLink(StringBuilder builder, int startIndex)
        {
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            var stackTrace = new StackTrace(true);
            var stackFrame = stackTrace.GetFrame(3);
            var callingFileName = stackFrame.GetFileName()?.Replace('\\', '/');
            var callingFileLine = stackFrame.GetFileLineNumber();
            builder.Insert(startIndex, $"<a href=\"{callingFileName}\" line=\"{callingFileLine}\">");
            builder.Append("</a>");
#endif
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

        private struct LogEntry
        {
            public string Category { get; }
            public DateTime TimeStamp { get; }
            public string Message { get; }
            public object[] Parameters { get; }
            public CorrelationID CorrelationID { get; }
            public VLoggerVerbosity Verbosity { get; }

            public Exception Exception { get; }

            public ErrorCode? ErrorCode { get; }

            public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, string message, object [] parameters)
            {
                Category = category;
                TimeStamp = DateTime.UtcNow;
                Message = message;
                Parameters = parameters;
                Verbosity = verbosity;
                CorrelationID = correlationId;
                Exception = null;
                ErrorCode = (ErrorCode)null;
            }

            public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, ErrorCode errorCode, Exception exception, string message, object [] parameters)
            {
                Category = category;
                TimeStamp = DateTime.UtcNow;
                Message = message;
                Parameters = parameters;
                Verbosity = verbosity;
                CorrelationID = correlationId;
                Exception = exception;
                ErrorCode = errorCode;
            }

            public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, ErrorCode errorCode, string message, object [] parameters)
            {
                Category = category;
                TimeStamp = DateTime.UtcNow;
                Message = message;
                Parameters = parameters;
                Verbosity = verbosity;
                CorrelationID = correlationId;
                Exception = null;
                ErrorCode = errorCode;
            }

            public override string ToString()
            {
                return string.Format(Message, Parameters) + $" [{CorrelationID}]";
            }
        }
    }
}
