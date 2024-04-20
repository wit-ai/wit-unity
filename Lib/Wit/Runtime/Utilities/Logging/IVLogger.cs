/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Logging
{
    /// <summary>
    /// The VSDK Logger. Each class should have its own instance of the logger.
    /// Instances should be created via <see cref="ILoggerRegistry"/>.
    /// </summary>
    public interface IVLogger : ICoreLogger
    {
        /// <summary>
        /// The minimum verbosity this logger will log.
        /// </summary>
        public VLoggerVerbosity MinimumVerbosity { get; set; }

        /// <summary>
        /// The verbosity level at which we suppress logs until they rare flushed.
        /// Any logs at or below this verbosity level will be suppressed.
        /// Anything higher will be written out right away.
        /// </summary>
        public VLoggerVerbosity SuppressionLevel { get; set; }

        /// <summary>
        /// Writes out any high verbosity logs that have been suppressed as part of the specified correlation ID.
        /// </summary>
        public void Flush(CorrelationID correlationID);

        /// <summary>
        /// Writes out any high verbosity logs that have been suppressed.
        /// </summary>
        public void Flush();
    }
}
