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
    /// A delegate callback for topic id and subscription state
    /// </summary>
    public delegate void PubSubTopicSubscriptionDelegate(string topicId, PubSubSubscriptionState subscriptionState);

    /// <summary>
    /// An interface for subscribing and unsubscribing from a specific topic
    /// </summary>
    public interface IPubSubSubscriber
    {
        /// <summary>
        /// Callback when subscription state changes for a specific topic id
        /// </summary>
        public event PubSubTopicSubscriptionDelegate OnTopicSubscriptionStateChange;

        /// <summary>
        /// Obtains the current subscription state for a specific topic
        /// </summary>
        public PubSubSubscriptionState GetTopicSubscriptionState(string topicId);

        /// <summary>
        /// Method to subscribe to a specific topic id
        /// </summary>
        public void Subscribe(string topicId);

        /// <summary>
        /// Method to unsubscribe from a specific topic id
        /// </summary>
        public void Unsubscribe(string topicId);
    }
}
