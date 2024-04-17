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
    /// A common interface to core logging functionality
    /// </summary>
    public interface ICoreLogger
    {
        /// <summary>
        /// The correlation ID allows the tracing of an operation from beginning to end.
        /// It can be linked to other IDs to form a full chain when it branches out or moves to other domains.
        /// If not supplied explicitly while logging, it will be inherited from the thread storage or a
        /// new one will be generated if none exist.
        /// </summary>
        CorrelationID CorrelationID { get; set; }

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Verbose(string message, params object [] parameters);

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Verbose(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Info(string message, params object [] parameters);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Info(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Debug(string message, params object [] parameters);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Debug(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Warning(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Warning(string message, params object [] parameters);

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object[] parameters);

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(ErrorCode errorCode, string message, params object[] parameters);

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, Exception exception, string message,
            params object[] parameters);

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters);
    }
}
