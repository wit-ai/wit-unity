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
        public WitWebSocketAuthRequest(string clientAccessToken, string versionTag) : base(GetAuthNode(clientAccessToken, versionTag)) { }

        /// <summary>
        /// Gets a static response node from the client access token
        /// </summary>
        private static WitResponseNode GetAuthNode(string clientAccessToken, string versionTag)
        {
            WitResponseClass authNode = new WitResponseClass();
            authNode[WitConstants.WIT_SOCKET_AUTH_TOKEN] = clientAccessToken;
            authNode[WitConstants.WIT_SOCKET_API_KEY] = WitConstants.API_VERSION;
            if (!string.IsNullOrEmpty(versionTag))
            {
                authNode[WitConstants.HEADER_TAG_ID] = versionTag;
            }
            return authNode;
        }

        /// <summary>
        /// Apply response data but add an additional check for authentication errors
        /// </summary>
        /// <param name="newResponseData">New response data received</param>
        protected override void SetResponseData(WitResponseNode newResponseData)
        {
            base.SetResponseData(newResponseData);

            // Set error using auth error if failed
            var authText = newResponseData[WitConstants.WIT_SOCKET_AUTH_RESPONSE_KEY];
            if (!string.Equals(authText, WitConstants.WIT_SOCKET_AUTH_RESPONSE_VAL))
            {
                Error = WitConstants.WIT_SOCKET_AUTH_RESPONSE_ERROR;
            }
        }
    }
}
