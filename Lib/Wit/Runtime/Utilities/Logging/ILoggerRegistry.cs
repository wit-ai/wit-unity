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
    /// Used to create VSDK loggers.
    /// </summary>
    public interface ILoggerRegistry
    {
        /// <summary>
        /// Gets a logger with an inferred category.
        /// </summary>
        /// <param name="logWriter">An optional log writer.</param>
        /// <param name="verbosity">Minimum verbosity that will be logged.</param>
        /// <returns>The logger</returns>
        IVLogger GetLogger(ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null);

        /// <summary>
        /// Gets a logger with an explicitly specified category.
        /// </summary>
        /// <param name="category">The category of the logs written by this logger.</param>
        /// <param name="logWriter">An optional log writer.</param>
        /// /// <param name="verbosity">Minimum verbosity that will be logged.</param>
        /// <returns>The logger</returns>
        IVLogger GetLogger(string category, ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null);
    }
}
