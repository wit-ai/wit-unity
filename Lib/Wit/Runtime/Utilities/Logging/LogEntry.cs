/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A struct representing a single log entry.
    /// </summary>
    public struct LogEntry : IComparable<LogEntry>
    {
        public string Category { get; }
        public DateTime TimeStamp { get; }
        public string Prefix { get; set; }
        public string Message { get; set; }
        public object[] Parameters { get; }
        public CorrelationID CorrelationID { get; }
        public VLoggerVerbosity Verbosity { get; }

        public Exception Exception { get; }

        public ErrorCode? ErrorCode { get; }

        public string CallSiteFileName { get; }
        public int CallSiteLineNumber { get; }

        public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, int callSiteLineNumber, string callSiteFileName, string prefix, string message, object [] parameters)
        {
            Category = category;
            TimeStamp = DateTime.UtcNow;
            Prefix = prefix;
            Message = message;
            Parameters = parameters;
            CallSiteLineNumber = callSiteLineNumber;
            CallSiteFileName = callSiteFileName;
            Verbosity = verbosity;
            CorrelationID = correlationId;
            Exception = null;
            ErrorCode = (ErrorCode)null;
        }

        public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, ErrorCode errorCode, Exception exception, int callSiteLineNumber, string callSiteFileName, string prefix, string message, object [] parameters)
        {
            Category = category;
            TimeStamp = DateTime.UtcNow;
            Prefix = prefix;
            Message = message;
            Parameters = parameters;
            CallSiteLineNumber = callSiteLineNumber;
            CallSiteFileName = callSiteFileName;
            Verbosity = verbosity;
            CorrelationID = correlationId;
            Exception = exception;
            ErrorCode = errorCode;
        }

        public LogEntry(string category, VLoggerVerbosity verbosity, CorrelationID correlationId, ErrorCode errorCode, int callSiteLineNumber, string callSiteFileName, string prefix, string message, object [] parameters)
        {
            Category = category;
            TimeStamp = DateTime.UtcNow;
            Prefix = prefix;
            Message = message;
            Parameters = parameters;
            CallSiteLineNumber = callSiteLineNumber;
            CallSiteFileName = callSiteFileName;
            Verbosity = verbosity;
            CorrelationID = correlationId;
            Exception = null;
            ErrorCode = errorCode;
        }

        public override string ToString()
        {
            return string.Format(Message, Parameters) + $" [{CorrelationID}]";
        }

        public int CompareTo(LogEntry other)
        {
            return TimeStamp.CompareTo(other.TimeStamp);
        }
    }
}
