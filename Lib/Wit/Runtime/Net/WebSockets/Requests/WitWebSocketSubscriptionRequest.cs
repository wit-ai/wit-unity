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
    /// The various subscription request types available
    /// </summary>
    public enum WitWebSocketSubscriptionType
    {
        Subscribe,
        Unsubscribe
    }

    /// <summary>
    /// Performs a web socket request to subscribe or unsubscribe to a specified topic
    /// </summary>
    public class WitWebSocketSubscriptionRequest : WitWebSocketJsonRequest
    {
        /// <summary>
        /// Whether to subscribe or unsubscribe
        /// </summary>
        public WitWebSocketSubscriptionType SubscriptionType { get; }

        /// <summary>
        /// Generates subscribe/unsubscribe request using a specific topic
        /// </summary>
        public WitWebSocketSubscriptionRequest(string topicId, WitWebSocketSubscriptionType subscriptionType) : base(GetSubscriptionNode(topicId, subscriptionType))
        {
            TopicId = topicId;
            SubscriptionType = subscriptionType;
        }

        /// <summary>
        /// Appends request data to log
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}\nSubscription Type: {SubscriptionType}";
        }

        /// <summary>
        /// Gets a static json node for pubsub subscribe or unsubscribe
        /// </summary>
        private static WitResponseNode GetSubscriptionNode(string topicId, WitWebSocketSubscriptionType subscriptionType)
        {
            var root = new WitResponseClass();
            var data = new WitResponseClass();
            var subscription = new WitResponseClass();
            subscription[WitConstants.WIT_SOCKET_PUBSUB_TOPIC_KEY] = topicId;
            data[GetSubscriptionNodeKey(subscriptionType)] = subscription;
            root[WitConstants.WIT_SOCKET_DATA_KEY] = data;
            return root;
        }

        /// <summary>
        /// Gets the subscription node name for the specified type
        /// </summary>
        private static string GetSubscriptionNodeKey(WitWebSocketSubscriptionType subscriptionType)
        {
            switch (subscriptionType)
            {
                case WitWebSocketSubscriptionType.Subscribe:
                    return WitConstants.WIT_SOCKET_PUBSUB_SUBSCRIBE_KEY;
                case WitWebSocketSubscriptionType.Unsubscribe:
                    return WitConstants.WIT_SOCKET_PUBSUB_UNSUBSCRIBE_KEY;
            }
            return string.Empty;
        }
    }
}
