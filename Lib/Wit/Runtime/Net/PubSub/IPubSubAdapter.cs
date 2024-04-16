/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.Voice.Net.PubSub
{
    /// <summary>
    /// An interface for publishing and subscribing to a specific topic
    /// </summary>
    public interface IPubSubAdapter
    {
        /// <summary>
        /// The topic id to be published to or received
        /// </summary>
        string TopicId { get; set; }

        /// <summary>
        /// The current topic subscription state
        /// </summary>
        PubSubSubscriptionState SubscriptionState { get; }

        /// <summary>
        /// A callback when topic subscription state changes
        /// </summary>
        event Action<PubSubSubscriptionState> OnTopicSubscriptionStateChange;

        /// <summary>
        /// Unity event when subscribed to a topic
        /// </summary>
        UnityEvent OnSubscribed { get; }

        /// <summary>
        /// Unity event when unsubscribing from a topic
        /// </summary>
        UnityEvent OnUnsubscribed { get; }
    }
}
