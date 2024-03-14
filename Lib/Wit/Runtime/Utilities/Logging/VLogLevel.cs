/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi
{
    /// <summary>
    /// The various logging options for VLog
    /// </summary>
    public enum VLogLevel
    {
        /// <summary>
        /// Error log. Usually indicates a bug in the code.
        /// </summary>
        Error = 0,

        /// <summary>
        /// Something that is a red flag and could potentially be a problem, but not necessarily.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Debug logs. Useful for debugging specific things.
        /// </summary>
        Log = 2,

        /// <summary>
        /// Information logs. Normal tracing.
        /// </summary>
        Info = 3,

        /// <summary>
        /// High verbosity information. Helpful for detailed tracing.
        /// </summary>
        Verbose = 4
    }
}
