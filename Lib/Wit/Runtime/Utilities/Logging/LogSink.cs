/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Meta.WitAi;
using ThreadState = System.Threading.ThreadState;

namespace Meta.Voice.Logging
{
    /// <inheritdoc/>
    internal class LogSink : ILogSink
    {
        private static Thread mainThread;

        static LogSink()
        {
            _ = ThreadUtility.CallOnMainThread(() => mainThread = Thread.CurrentThread);
        }

        private static IErrorMitigator _errorMitigator;

        readonly string _workingDirectory = Directory.GetCurrentDirectory();

        /// <inheritdoc/>
        public IErrorMitigator ErrorMitigator
        {
            get
            {
                if (_errorMitigator == null)
                {
                    _errorMitigator = new ErrorMitigator();
                }

                return _errorMitigator;
            }
            set => _errorMitigator = value;
        }

        /// <summary>
        /// The log writer where all the outputs will be written.
        /// </summary>
        public ILogWriter LogWriter { get; set; }

        /// <summary>
        /// The logging options.
        /// </summary>
        public LoggerOptions Options { get; set; }

        /// <summary>
        /// Caches the last few messages so we can omit repeated correlation IDs.
        /// </summary>
        private readonly RingDictionaryBuffer<string, CorrelationID> _messagesCache = new(100);

        internal LogSink(ILogWriter logWriter, LoggerOptions options, IErrorMitigator errorMitigator = null)
        {
            LogWriter = logWriter;
            if (errorMitigator != null)
            {
                _errorMitigator = errorMitigator;
            }

            Options = options;
        }

        /// <summary>
        /// Write a log entry to the sink.
        /// </summary>
        /// <param name="logEntry">The entry to write.</param>
        public void WriteEntry(LogEntry logEntry)
        {
            var sb = new StringBuilder();

#if !UNITY_EDITOR && !UNITY_ANDROID
            {
                // Start with datetime if not done so automatically
                sb.Append($"[{logEntry.TimeStamp.ToShortDateString()} {logEntry.TimeStamp.ToShortTimeString()}] ");
            }
#endif

            // Insert log type
            var start = sb.Length;
            sb.Append($"[VSDK] ");

            if (Options.ColorLogs)
            {
                WrapWithLogColor(sb, start, logEntry.Verbosity);
            }

            Annotate(sb, logEntry);

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
                if (_errorMitigator != null)
                {
                    sb.Append("\nMitigation: ");
                    sb.Append(_errorMitigator.GetMitigation(logEntry.ErrorCode.Value));
                }
            }

            if (logEntry.Verbosity >= Options.StackTraceLevel && logEntry.StackTrace != null)
            {
                sb.Append("\n");
                AppendStackTrace(sb, logEntry);
            }

            var message = sb.ToString();
            if (logEntry.Exception != null)
            {
#if UNITY_EDITOR
                message = string.Format(
                    "{0}\n<color=\"#ff6666\"><b>{1}:</b> {2}</color>\n=== STACK TRACE ===\n{3}\n=====", sb,
                    logEntry.Exception.GetType().Name, logEntry.Exception.Message,
                    FormatStackTrace(logEntry.Exception.StackTrace));
#endif
            }

            logEntry.Message = message;

            SendEntryToLogWriter(logEntry);
        }

        private void SendEntryToLogWriter(LogEntry logEntry)
        {
            switch (logEntry.Verbosity)
            {
                case VLoggerVerbosity.Error:
#if UNITY_EDITOR
                    if (VLog.LogErrorsAsWarnings)
                    {
                        WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                        return;
                    }
#endif
                    WriteError($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Warning:
                    WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Info:
                    WriteInfo($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Debug:
                    WriteDebug($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                default:
                    WriteVerbose($"{logEntry.Prefix}{logEntry.Message}");
                    break;
            }
        }

        /// <summary>
        /// Get hex value for each log type
        /// </summary>
        private void WrapWithLogColor(StringBuilder builder, int startIndex, VLoggerVerbosity logType)
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
                case VLoggerVerbosity.Debug:
                    hex = "FF80FF";
                    break;
                case VLoggerVerbosity.Verbose:
                    hex = "80FF80";
                    break;
                case VLoggerVerbosity.None:
                    hex = "FFFFFF";
                    break;
                case VLoggerVerbosity.Info:
                default:
                    hex = "00FF00";
                    break;
            }
            builder.Insert(startIndex, $"<color=#{hex}>");
            builder.Append("</color>");
#endif
        }

        private void AppendStackTrace(StringBuilder sb, LogEntry logEntry)
        {
            sb.Append("=== STACK TRACE ===\n");
            foreach (var frame in logEntry.StackTrace.GetFrames())
            {
                var method = frame.GetMethod();
                var declaringType = method.DeclaringType;
                if (declaringType == null || IsLoggingClass(method.DeclaringType) || IsSystemClass(method.DeclaringType))
                {
                    continue;
                }


                var filePath = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name}"));

                if (!string.IsNullOrEmpty(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    var relativeFilePath = filePath.Replace(_workingDirectory, "");

#if UNITY_EDITOR
                    sb.Append($"<a href=\"{relativeFilePath}\" line=\"{lineNumber}\">[{fileName}:{lineNumber}] </a>");
#else
                    sb.Append($"[{fileName}:{lineNumber}] ");
#endif
                }

                var methodName = $"{method.Name}";
                sb.Append(declaringType?.Name);
                sb.Append('.');
                sb.Append(Options.ColorLogs ? $"<color=#39CC8F>{method.Name}</color>" : $"{methodName}");
                sb.Append($"({parameters})\n");
            }
        }

        private string FormatStackTrace(string stackTrace)
        {
            // Use a regular expression to match lines with a file path and line number
            var regex = new Regex(@"at (.+) in (.*):(\d+)");
            // Use the MatchEvaluator delegate to format the matched lines
            string Evaluator(Match match)
            {
                var method = match.Groups[1].Value;
                var filePath = match.Groups[2].Value.Replace(_workingDirectory, "");
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
        /// Adds the VSDK tag and, optionally, call site info to the string builder.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="logEntry">The log entry.</param>
        private void Annotate(StringBuilder sb, LogEntry logEntry)
        {
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            if (!Options.LinkToCallSite)
            {
#endif
                if (!string.IsNullOrEmpty(logEntry.Category))
                {
#if UNITY_EDITOR
                    sb.Append($"[<b>{logEntry.Category}</b>] ");
#else
                    sb.Append($"[{logEntry.Category}] ");
#endif
                }
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
              return;
            }
#endif
            var (callSiteFileName, callSiteLineNumber) = GetCallSite(logEntry.StackTrace);
            var fileName = Path.GetFileNameWithoutExtension(callSiteFileName);
            if (fileName == logEntry.Category)
            {
#if UNITY_EDITOR
                sb.Append($"<a href=\"{callSiteFileName}\" line=\"{callSiteLineNumber}\">[{fileName}.cs:{callSiteLineNumber}]</a> ");
#else
                sb.Append($"[{fileName}.cs:{callSiteLineNumber}] ");
#endif
            }
            else
            {
#if UNITY_EDITOR
                sb.Append($"[<b>{logEntry.Category}</b>] ");
                sb.Append($"<a href=\"{callSiteFileName}\" line=\"{callSiteLineNumber}\">[{fileName}.cs:{callSiteLineNumber}]</a> ");
#else
                sb.Append($"[{logEntry.Category}] ");
                sb.Append($"[{fileName}.cs:{callSiteLineNumber}] ");
#endif
            }
        }

        public void WriteVerbose(string message)
        {
            if (IsSafeToLog())
            {
                LogWriter.WriteVerbose(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => LogWriter.WriteVerbose(message));
            }
        }

        public void WriteDebug(string message)
        {
            if (IsSafeToLog())
            {
                LogWriter.WriteDebug(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => LogWriter.WriteDebug(message));
            }
        }

        public void WriteInfo(string message)
        {
            if (IsSafeToLog())
            {
                LogWriter.WriteInfo(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => LogWriter.WriteInfo(message));
            }
        }

        public void WriteWarning(string message)
        {
            if (IsSafeToLog())
            {
                LogWriter.WriteWarning(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => LogWriter.WriteWarning(message));
            }
        }

        public void WriteError(string message)
        {
            if (IsSafeToLog())
            {
                LogWriter.WriteError(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => LogWriter.WriteError(message));
            }
        }

        private (string fileName, int lineNumber) GetCallSite(StackTrace stackTrace)
        {
            for (int i = 1; i < stackTrace.FrameCount; i++)
            {
                var stackFrame = stackTrace.GetFrame(i);
                var method = stackFrame.GetMethod();
                if (method.DeclaringType == null || IsLoggingClass(method.DeclaringType) || IsSystemClass(method.DeclaringType))
                {
                    continue;
                }

                var callingFileName = stackFrame.GetFileName()?.Replace('\\', '/');
                var callingFileLineNumber = stackFrame.GetFileLineNumber();
                return (callingFileName, callingFileLineNumber);
            }

            WriteError("Failed to get call site information.");
            return (string.Empty, 0);
        }

        private static bool IsLoggingClass(Type type)
        {
            return typeof(ICoreLogger).IsAssignableFrom(type) || typeof(ILogWriter).IsAssignableFrom(type) || type == typeof(VLog);
        }

        private static bool IsSystemClass(Type type)
        {
            var nameSpace = type.Namespace;
            if (nameSpace == null)
            {
                return false;
            }
            return nameSpace.StartsWith("Unity") ||
                   nameSpace.StartsWith("System") ||
                   nameSpace.StartsWith("Microsoft");
        }

        private bool IsSafeToLog()
        {
            return (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested & ThreadState.Aborted) == 0 || Thread.CurrentThread == mainThread;
        }
    }
}
