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
        public VLoggerVerbosity MinimumVerbosity;

        /// <summary>
        /// Suppression level.
        /// </summary>
        public VLoggerVerbosity SuppressionLevel;

        /// <summary>
        /// StackTrace inclusion level.
        /// </summary>
        public VLoggerVerbosity StackTraceLevel;

        /// <summary>
        /// Whether or not to color the logs.
        /// </summary>
        public bool ColorLogs;

        /// <summary>
        /// Whether or not to link to the call site.
        /// </summary>
        public bool LinkToCallSite;

        /// <summary>
        /// Initialize the options class/
        /// </summary>
        public LoggerOptions(VLoggerVerbosity minimumVerbosity,
            VLoggerVerbosity suppressionLevel,
            VLoggerVerbosity stackTraceLevel,
#if UNITY_EDITOR
            bool colorLogs = true,
            bool linkToCallSite = true
#else
            bool colorLogs = false,
            bool linkToCallSite = false
#endif
            )
        {
            MinimumVerbosity = minimumVerbosity;
            ColorLogs = colorLogs;
            LinkToCallSite = linkToCallSite;
            SuppressionLevel = suppressionLevel;
            StackTraceLevel = stackTraceLevel;
        }

        public void CopyFrom(LoggerOptions other)
        {
            MinimumVerbosity = other.MinimumVerbosity;
            ColorLogs = other.ColorLogs;
            LinkToCallSite = other.LinkToCallSite;
            SuppressionLevel = other.SuppressionLevel;
            StackTraceLevel = other.StackTraceLevel;
        }
    }
}
