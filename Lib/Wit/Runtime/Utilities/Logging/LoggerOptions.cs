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
    /// Holds options to customize logging.
    /// </summary>
    public class LoggerOptions
    {
        /// <summary>
        /// Minimum verbosity to write.
        /// </summary>
        public VLoggerVerbosity MinimumVerbosity = VLoggerVerbosity.Debug;

        /// <summary>
        /// Suppression level.
        /// </summary>
        public VLoggerVerbosity SuppressionLevel = VLoggerVerbosity.Debug;

        /// <summary>
        /// Whether or not to color the logs.
        /// </summary>
        public readonly bool ColorLogs = true;

        /// <summary>
        /// Whether or not to link to the call site.
        /// </summary>
        public readonly bool LinkToCallSite = true;

        internal LoggerOptions(VLoggerVerbosity minimumVerbosity = VLoggerVerbosity.Debug, VLoggerVerbosity suppressionLevel = VLoggerVerbosity.Verbose, bool colorLogs = true, bool linkToCallSite = true)
        {
            MinimumVerbosity = minimumVerbosity;
            ColorLogs = colorLogs;
            LinkToCallSite = linkToCallSite;
            SuppressionLevel = suppressionLevel;
        }
    }
}
