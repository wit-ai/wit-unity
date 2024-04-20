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
    /// The various logging options for VLog
    /// </summary>
    public enum VLoggerVerbosity
    {
        /// <summary>
        /// Error log. Usually indicates a bug in the code.
        /// </summary>
        Error = 5,

        /// <summary>
        /// Something that is a red flag and could potentially be a problem, but not necessarily.
        /// </summary>
        Warning = 4,

        /// <summary>
        /// Information logs. Normal tracing.
        /// </summary>
        Info = 3,

        /// <summary>
        /// Debug logs. Useful for debugging specific things.
        /// </summary>
        Debug = 2,

        /// <summary>
        /// High verbosity information. Helpful for detailed tracing.
        /// </summary>
        Verbose = 1,

        /// <summary>
        /// This level means the information will not be written at all.
        /// This is typically used for disabling suppression, rather than as an actual logging level.
        /// </summary>
        None = 0,
    }
}
