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
    /// A log writer is a sink to be used by VLogger to write the logs to a destination.
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// Writes a verbose message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteVerbose(string message);

        /// <summary>
        /// Writes debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteDebug(string message);

        /// <summary>
        /// Writes an info message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteInfo(string message);

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteWarning(string message);

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        void WriteError(string message);
    }
}
