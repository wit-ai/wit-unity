/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// Various connection state options for a web socket client
    /// </summary>
    public enum WitWebSocketConnectionState
    {
        /// <summary>
        /// Not connected to a server
        /// </summary>
        Disconnected,

        /// <summary>
        /// Currently connecting or authenticating with a server
        /// </summary>
        Connecting,

        /// <summary>
        /// Connected and ready to send data
        /// </summary>
        Connected,

        /// <summary>
        /// Currently disconnecting from a server
        /// </summary>
        Disconnecting
    }
}
