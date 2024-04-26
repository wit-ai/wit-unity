/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    /// <inheritdoc/>
    internal class LogSink : ILogSink
    {
        private static IErrorMitigator _errorMitigator;

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
        public Lazy<LoggerOptions> Options { get; }

        /// <summary>
        /// Caches the last few messages so we can omit repeated correlation IDs.
        /// </summary>
        private readonly RingDictionaryBuffer<string, CorrelationID> _messagesCache = new(100);

        internal LogSink(ILogWriter logWriter, Lazy<LoggerOptions> options, IErrorMitigator errorMitigator = null)
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

            if (Options.Value.ColorLogs)
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
                        LogWriter.WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                        return;
                    }
#endif
                    LogWriter.WriteError($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Warning:
                    LogWriter.WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Info:
                    LogWriter.WriteInfo($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Debug:
                    LogWriter.WriteDebug($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                default:
                    LogWriter.WriteVerbose($"{logEntry.Prefix}{logEntry.Message}");
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

        private string FormatStackTrace(string stackTrace)
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
        /// Adds the VSDK tag and, optionally, call site info to the string builder.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        /// <param name="logEntry">The log entry.</param>
        private void Annotate(StringBuilder sb, LogEntry logEntry)
        {
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            if (!Options.Value.LinkToCallSite)
            {
#endif
                if (!string.IsNullOrEmpty(logEntry.Category))
                {
                    sb.Append($"[<b>{logEntry.Category}</b>] ");
                }

                return;
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            }
#endif
            var fileName = Path.GetFileNameWithoutExtension(logEntry.CallSiteFileName);
            if (fileName == logEntry.Category)
            {
                sb.Append($"<a href=\"{logEntry.CallSiteFileName}\" line=\"{logEntry.CallSiteLineNumber}\">[{fileName}.cs:{logEntry.CallSiteLineNumber}]</a> ");
            }
            else
            {
                sb.Append($"[<b>{logEntry.Category}</b>] ");
                sb.Append($"<a href=\"{logEntry.CallSiteFileName}\" line=\"{logEntry.CallSiteLineNumber}\">[{fileName}.cs:{logEntry.CallSiteLineNumber}]</a> ");
            }
        }

        public void WriteError(string message)
        {
            LogWriter.WriteError(message);
        }
    }
}
