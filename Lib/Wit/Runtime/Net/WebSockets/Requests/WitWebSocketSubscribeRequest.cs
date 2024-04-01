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
    /// Performs a single subscribe or unsubscribe request for pubsub integration
    /// </summary>
    public class WitWebSocketSubscribeRequest : WitWebSocketJsonRequest
    {
        /// <summary>
        /// Generates subscribe/unsubscribe request using a specific topic
        /// </summary>
        public WitWebSocketSubscribeRequest(string topicId, bool unsubscribe = false) : base(GetSubscribeNode(topicId, unsubscribe)) { }

        /// <summary>
        /// Gets a static json node for pubsub subscribe or unsubscribe
        /// </summary>
        private static WitResponseNode GetSubscribeNode(string topicId, bool unsubscribe)
        {
            var root = new WitResponseClass();
            var data = new WitResponseClass();
            var subscribe = new WitResponseClass();
            subscribe[WitConstants.WIT_SOCKET_PUBSUB_TOPIC_KEY] = topicId;
            var key = unsubscribe
                ? WitConstants.WIT_SOCKET_PUBSUB_UNSUBSCRIBE_KEY
                : WitConstants.WIT_SOCKET_PUBSUB_SUBSCRIBE_KEY;
            data[key] = subscribe;
            root[WitConstants.WIT_SOCKET_DATA_KEY] = data;
            return root;
        }
    }
}
