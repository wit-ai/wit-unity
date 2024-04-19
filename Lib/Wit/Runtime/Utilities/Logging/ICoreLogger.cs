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
        /// <param name="exception">The exception to log</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(CorrelationID correlationId, Exception exception, ErrorCode errorCode, string message,
            params object[] parameters);

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters);

        /// <summary>
        /// Returns a logging scope to be used in a "using" block.
        /// </summary>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns>The scope.</returns>
        public ILogScope Scope(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, CorrelationID, message, parameters);
        }

        /// <summary>
        /// Returns a logging scope to be used in a "using" block.
        /// </summary>
        /// <param name="correlationId">The correlation ID to use for the scope.</param>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns>The scope.</returns>
        public ILogScope Scope(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            return new LogScope(this, verbosity, correlationId, message, parameters);
        }

        /// <summary>
        /// Explicitly start a scope.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns></returns>
        public int Start(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters);

        /// <summary>
        /// Explicitly start a scope.
        /// </summary>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns></returns>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters);

        /// <summary>
        /// Explicitly end a scope. Must have been started already.
        /// </summary>
        /// <param name="sequenceId"></param>
        void End(int sequenceId);

        /// <summary>
        /// Correlates two correlation IDs. This is used to indicate nesting, branching, or exchanges.
        /// The purpose is to allow a complete trail up to the source when needed.
        /// </summary>
        /// <param name="newCorrelationId">The source correlation ID (eg. child operation).</param>
        /// <param name="rootCorrelationId">The target correlation ID (eg. root operation).</param>
        public void Correlate(CorrelationID newCorrelationId, CorrelationID rootCorrelationId);

        /// <summary>
        /// Logs a message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters);

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity,
            Exception exception, ErrorCode errorCode,
            string message, params object[] parameters);


        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters);
    }
}
