/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// All wit specific settings required by a web socket client in order to connect to a server
    /// </summary>
    public class WitWebSocketSettings
    {
        /// <summary>
        /// The url to connect with on client.Connect()
        /// </summary>
        public string ServerUrl { get; set; } = WitConstants.WIT_SOCKET_URL;

        /// <summary>
        /// The interval in seconds before a connection is timed out
        /// </summary>
        public int ServerConnectionTimeoutMs { get; set; } = WitConstants.WIT_SOCKET_CONNECT_TIMEOUT;

        /// <summary>
        /// The total amount of reconnects that will be attempted if disconnected from the server.
        /// If 0, it will not attempt to reconnect.
        /// If -1, it will continuously attempt to reconnect.
        /// </summary>
        public int ReconnectAttempts { get; set; } = WitConstants.WIT_SOCKET_RECONNECT_ATTEMPTS;

        /// <summary>
        /// The interval in seconds between reconnects when disconnected
        /// </summary>
        public float ReconnectInterval { get; set; } = WitConstants.WIT_SOCKET_RECONNECT_INTERVAL;

        /// <summary>
        /// The configuration used for wit web socket communication
        /// </summary>
        public IWitRequestConfiguration Configuration { get; }

        /// <summary>
        /// Request timeout in milliseconds
        /// </summary>
        public int RequestTimeoutMs => Configuration.RequestTimeoutMs;

        /// <summary>
        /// Constructor that takes in configuration
        /// </summary>
        public WitWebSocketSettings(IWitRequestConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
