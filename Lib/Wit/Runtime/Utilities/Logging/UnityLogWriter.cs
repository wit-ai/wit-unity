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
        /// <inheritdoc/>
        public void WriteVerbose(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <inheritdoc/>
        public void WriteDebug(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <inheritdoc/>
        public void WriteInfo(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <inheritdoc/>
        public void WriteWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        /// <inheritdoc/>
        public void WriteError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}
