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
    public class LogScope : IDisposable, ICoreLogger
    {
        private readonly IVLogger _logger;
        private readonly int _sequenceId;

        /// <summary>
        /// Constructs a logging scope to be used in "using" blocks.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <param name="correlationID">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public LogScope(IVLogger logger, VLoggerVerbosity verbosity, CorrelationID correlationID, string message, object [] parameters)
        {
            CorrelationID = correlationID;
            _logger = logger;
            _sequenceId = _logger.Start(verbosity, correlationID, message, parameters);
        }

        /// <inheritdoc/>
        public CorrelationID CorrelationID { get; set; }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            _logger.Verbose(CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            _logger.Verbose(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            _logger.Info(CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            _logger.Info(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            _logger.Debug(CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            _logger.Debug(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            _logger.Warning(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            _logger.Warning(CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object [] parameters)
        {
            _logger.Error(correlationId, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object [] parameters)
        {
            _logger.Error(CorrelationID, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, Exception exception, string message, params object[] parameters)
        {
            _logger.Error(correlationId, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters)
        {
            _logger.Error(CorrelationID, errorCode, exception, message, parameters);
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
