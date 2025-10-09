/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
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
        public WitWebSocketAuthRequest(string clientAccessToken, string versionTag)
          : base(GetAuthNode(clientAccessToken, versionTag)) { }

        /// <summary>
        /// Gets a static response node from the client access token
        /// </summary>
        private static WitResponseNode GetAuthNode(string clientAccessToken, string versionTag)
        {
            WitResponseClass authNode = new WitResponseClass();
            authNode[WitConstants.WIT_SOCKET_AUTH_TOKEN] = new WitResponseData(clientAccessToken);
            authNode[WitConstants.WIT_SOCKET_API_KEY] = new WitResponseData(WitConstants.API_VERSION);
            if (!string.IsNullOrEmpty(versionTag))
            {
                authNode[WitConstants.HEADER_TAG_ID] = new WitResponseData(versionTag);
            }
            return authNode;
        }

        /// <summary>
        /// Option to set an auth setting and returns true if different from the previous value
        /// </summary>
        public bool SetAuthSetting(string key, string val)
          => TrySetDictionary(PostData.AsObject, key, val);

        /// <summary>
        /// Option to set an auth setting context and returns true if different from the previous value
        /// </summary>
        public bool SetAuthContext(Dictionary<string, string> authContext)
        {
            var context = PostData.AsObject.HasChild(WitConstants.ENDPOINT_CONTEXT_PARAM)
              ? PostData[WitConstants.ENDPOINT_CONTEXT_PARAM].AsObject
              : new WitResponseClass();
            var changed = false;
            foreach (var (key, value) in authContext)
            {
                changed |= TrySetDictionary(context, key, value);
            }
            PostData[WitConstants.ENDPOINT_CONTEXT_PARAM] = context;
            return changed;
        }

        private bool TrySetDictionary(WitResponseClass dictionary, string key, string val)
        {
            var hasKey = dictionary.HasChild(key);
            if (string.IsNullOrEmpty(val))
            {
                if (hasKey)
                {
                    dictionary.Remove(key);
                    return true;
                }
                return false;
            }
            if (hasKey && dictionary[key].Value.Equals(val))
            {
                return false;
            }
            dictionary[key] = new WitResponseData(val);
            return true;
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
