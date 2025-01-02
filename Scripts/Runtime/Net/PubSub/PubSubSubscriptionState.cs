/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Net.PubSub
{
    /// <summary>
    /// An enum used to represent the current state of a topic's subscription
    /// </summary>
    public enum PubSubSubscriptionState
    {
        /// <summary>
        /// Not subscribed to a topic whatsoever
        /// </summary>
        NotSubscribed,

        /// <summary>
        /// Currently attempting to subscribe to a topic
        /// </summary>
        Subscribing,

        /// <summary>
        /// Currently subscribed to a topic
        /// </summary>
        Subscribed,

        /// <summary>
        /// Currently sending an unsubscribe request to the server
        /// </summary>
        Unsubscribing,

        /// <summary>
        /// An error occured while subscribing, will attempt again if connected
        /// </summary>
        SubscribeError,

        /// <summary>
        /// An error occured while unsubscribing, will attempt again if connected
        /// </summary>
        UnsubscribeError
    }
}
