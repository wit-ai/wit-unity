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
    public class LogScope : IDisposable
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
            _logger = logger;
            _sequenceId = _logger.Start(verbosity, correlationId:correlationID, message, parameters);
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
