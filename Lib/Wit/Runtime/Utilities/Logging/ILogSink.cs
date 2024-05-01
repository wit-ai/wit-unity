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
    /// A log sink is a target where we write log entries.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// The error mitigator. This is mainly used to supplement the internal error mitigator or replace it.
        /// </summary>
        public IErrorMitigator ErrorMitigator { get; set; }

        /// <summary>
        /// The logging options.
        /// </summary>
        public LoggerOptions Options { get; set; }

        /// <summary>
        /// The log writer to which this sink writes.
        /// </summary>
        ILogWriter LogWriter { get; set; }

        /// <summary>
        /// Write a log entry to the sink.
        /// </summary>
        /// <param name="logEntry">The entry to write.</param>
        void WriteEntry(LogEntry logEntry);

        /// <summary>
        /// Writes an error message directly without any processing.
        /// This is used for logging errors with the logger itself.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteError(string message);
    }
}
