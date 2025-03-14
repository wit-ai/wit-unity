/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Various errors that can be simulated for a request
    /// </summary>
    public enum VoiceErrorSimulationType
    {
        /// <summary>
        /// Simulate server returning a 500 error
        /// </summary>
        Server,

        /// <summary>
        /// Simulate server not responding
        /// </summary>
        Timeout,

        /// <summary>
        /// Simulate web socket server disconnection
        /// </summary>
        Disconnect
    }
}
