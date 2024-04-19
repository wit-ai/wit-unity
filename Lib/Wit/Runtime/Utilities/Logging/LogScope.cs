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
    /// A logging scope to be used in "using" blocks.
    /// </summary>
    public class LogScope : ILogScope
    {
        private readonly ICoreLogger _logger;
        private readonly int _sequenceId;

        /// <summary>
        /// Constructs a logging scope to be used in "using" blocks.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <param name="correlationID">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public LogScope(ICoreLogger logger, VLoggerVerbosity verbosity, CorrelationID correlationID, string message, object [] parameters)
        {
            CorrelationID = correlationID;
            _logger = logger;
            _sequenceId = _logger.Start(correlationID, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public CorrelationID CorrelationID { get; set; }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Verbose, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Verbose, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Error, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Error, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Verbose, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Verbose, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            return _logger.Start(correlationId, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return _logger.Start(verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            _logger.End(sequenceId);
        }

        public void Correlate(CorrelationID newCorrelationId, CorrelationID rootCorrelationId)
        {
            _logger.Correlate(newCorrelationId, rootCorrelationId);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, message, parameters);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, Exception exception,
            ErrorCode errorCode,
            string message, params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, exception, errorCode, message, parameters);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, errorCode, message, parameters);
        }

        /// <summary>
        /// Disposes the scope.
        /// </summary>
        public void Dispose()
        {
            _logger.End(_sequenceId);
        }
    }
}
