/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// Used to create VSDK loggers.
    /// </summary>
    public interface ILoggerRegistry
    {
        /// <summary>
        /// The logger options. All loggers created by this factory will share these options.
        /// </summary>
        LoggerOptions Options { get; }

        /// <summary>
        /// The log sink loggers will write to by default.
        /// </summary>
        public ILogSink LogSink { get; set; }

        /// <summary>
        /// Ignores logs in editor if less than log level.
        /// Changing this value at runtime will update it for all existing VLoggers.
        /// </summary>
        public VLoggerVerbosity EditorLogFilteringLevel { get; set; }

        /// <summary>
        /// Logs that are lower than this level will be suppressed by default.
        /// Suppressed logs will not be written unless an error occurs with a related correlation ID or
        /// they are explicitly flushed.
        /// Changing this value at runtime will update it for all existing VLoggers.
        /// </summary>
        public VLoggerVerbosity LogSuppressionLevel { get; set; }

        /// <summary>
        /// When true, caches the loggers and reuse them for the same category.
        /// This should always be set to true, except in rare circumstances (such as unit tests).
        /// </summary>
        bool PoolLoggers { get; set; }

        /// <summary>
        /// Gets a logger with an inferred category.
        /// </summary>
        /// <param name="logSync">An optional log sink.</param>
        /// <returns>The logger</returns>
        IVLogger GetLogger(ILogSink logSink = null);

        /// <summary>
        /// Gets a logger with an explicitly specified category.
        /// </summary>
        /// <param name="category">The category of the logs written by this logger.</param>
        /// <param name="logSink"></param>
        /// <returns>The logger</returns>
        IVLogger GetLogger(string category, ILogSink logSink = null);

        /// <summary>
        /// Returns a list of all loggers the registry created.
        /// </summary>
        IEnumerable<IVLogger> AllLoggers { get; }
    }
}
