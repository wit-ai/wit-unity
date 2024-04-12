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
        NotSubscribed,
        Subscribing,
        Subscribed,
        Unsubscribing,
        Error
    }
}
