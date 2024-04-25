/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A log writer that writes to Unity's console.
    /// </summary>
    internal class UnityLogWriter : ILogWriter
    {
        public void WriteEntry(LogEntry logEntry)
        {
            switch (logEntry.Verbosity)
            {
                case VLoggerVerbosity.Error:
#if UNITY_EDITOR
                    if (VLog.LogErrorsAsWarnings)
                    {
                        WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                        return;
                    }
#endif
                    WriteError($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Warning:
                    WriteWarning($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Info:
                    WriteInfo($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                case VLoggerVerbosity.Debug:
                    WriteDebug($"{logEntry.Prefix}{logEntry.Message}");
                    break;
                default:
                    WriteVerbose($"{logEntry.Prefix}{logEntry.Message}");
                    break;
            }
        }

        /// <inheritdoc/>
        public void WriteVerbose(string message)
        {
            _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
        }

        /// <inheritdoc/>
        public void WriteDebug(string message)
        {
            _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
        }

        /// <inheritdoc/>
        public void WriteInfo(string message)
        {
            _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
        }

        /// <inheritdoc/>
        public void WriteWarning(string message)
        {
            _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.LogWarning(message));
        }

        /// <inheritdoc/>
        public void WriteError(string message)
        {
            _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.LogError(message));
        }
    }
}
