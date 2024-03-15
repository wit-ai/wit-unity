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
        /// Debug logs. Useful for debugging specific things.
        /// </summary>
        Log = 3,

        /// <summary>
        /// Information logs. Normal tracing.
        /// </summary>
        Info = 2,

        /// <summary>
        /// High verbosity information. Helpful for detailed tracing.
        /// </summary>
        Verbose = 1
    }
}
