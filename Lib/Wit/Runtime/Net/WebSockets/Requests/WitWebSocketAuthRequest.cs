/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;
using Meta.WitAi.Json;

namespace Meta.Voice.Net.WebSockets.Requests
{
    /// <summary>
    /// Performs a single authentication request
    /// </summary>
    public class WitWebSocketAuthRequest : WitWebSocketJsonRequest
    {
        /// <summary>
        /// Generates request with client access token
        /// </summary>
        public WitWebSocketAuthRequest(string clientAccessToken) : base(GetAuthNode(clientAccessToken)) { }

        /// <summary>
        /// Gets a static response node from the client access token
        /// </summary>
        private static WitResponseNode GetAuthNode(string clientAccessToken)
        {
            WitResponseClass authNode = new WitResponseClass();
            authNode[WitConstants.WIT_SOCKET_AUTH_TOKEN] = clientAccessToken;
            authNode[WitConstants.WIT_SOCKET_API_KEY] = WitConstants.API_VERSION;
            return authNode;
        }

        /// <summary>
        /// Called on server response.  Sets an error if a successful auth response was not received.
        /// </summary>
        /// <param name="jsonString">Raw json string.</param>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which should be empty.</param>
        public override void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData)
        {
            base.HandleDownload(jsonString, jsonData, binaryData);

            // Ignore if error already occured
            if (!string.IsNullOrEmpty(Error))
            {
                return;
            }

            // Ensure auth response matches
            var authText = jsonData[WitConstants.WIT_SOCKET_AUTH_RESPONSE_KEY];
            if (!string.Equals(authText, WitConstants.WIT_SOCKET_AUTH_RESPONSE_VAL))
            {
                Error = WitConstants.WIT_SOCKET_AUTH_RESPONSE_ERROR;
            }
        }
    }
}
