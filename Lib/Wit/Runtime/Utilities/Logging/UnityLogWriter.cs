/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Threading;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A log writer that writes to Unity's console.
    /// </summary>
    internal class UnityLogWriter : ILogWriter
    {
        private static Thread mainThread;

        static UnityLogWriter()
        {
            _ = ThreadUtility.CallOnMainThread(() => mainThread = Thread.CurrentThread);
        }

        /// <inheritdoc/>
        public void WriteVerbose(string message)
        {
            if (IsSafeToLog())
            {
                UnityEngine.Debug.Log(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
            }
        }

        /// <inheritdoc/>
        public void WriteDebug(string message)
        {
            if (IsSafeToLog())
            {
                UnityEngine.Debug.Log(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
            }
        }

        /// <inheritdoc/>
        public void WriteInfo(string message)
        {
            if (IsSafeToLog())
            {
                UnityEngine.Debug.Log(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.Log(message));
            }
        }

        /// <inheritdoc/>
        public void WriteWarning(string message)
        {
            if (IsSafeToLog())
            {
                UnityEngine.Debug.Log(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.LogWarning(message));
            }
        }

        /// <inheritdoc/>
        public void WriteError(string message)
        {
            if (IsSafeToLog())
            {
                UnityEngine.Debug.Log(message);
            }
            else
            {
                _ = ThreadUtility.CallOnMainThread(() => UnityEngine.Debug.LogError(message));
            }
        }

        private bool IsSafeToLog()
        {
            return (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested & ThreadState.Aborted) == 0 || Thread.CurrentThread == mainThread;
        }
    }
}
