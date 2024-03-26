/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Logging
{
    internal class UnityLogWriter : ILogWriter
    {
        public void WriteWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void WriteError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public void WriteVerbose(string message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}
