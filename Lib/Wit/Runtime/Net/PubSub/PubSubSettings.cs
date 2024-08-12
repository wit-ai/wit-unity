/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi;
using UnityEngine;

namespace Meta.Voice.Net.PubSub
{
    /// <summary>
    /// All data required for publishing and subscribing to a specific web socket topic.
    /// This includes publish and subscription specific options.
    /// </summary>
    [Serializable]
    public struct PubSubSettings
    {
        [Tooltip("The unique pubsub topic id to publish and/or subscribe to")]
        public string PubSubTopicId;

        [Tooltip("Toggles for publishing per response type.")]
        public PubSubResponseOptions PublishOptions;

        [Tooltip("Toggles for subscribing per response type.")]
        public PubSubResponseOptions SubscribeOptions;

        /// <summary>
        /// Default empty constructor
        /// </summary>
        public PubSubSettings(string pubSubTopicId = "")
        {
            PubSubTopicId = pubSubTopicId;
            PublishOptions = GetDefaultOptions();
            SubscribeOptions = GetDefaultOptions();
        }

        /// <summary>
        /// Defined equal check for pubsub settings
        /// </summary>
        public bool Equals(PubSubSettings other)
            => string.Equals(PubSubTopicId, other.PubSubTopicId)
               && PublishOptions.Equals(other.PublishOptions)
               && SubscribeOptions.Equals(other.SubscribeOptions);

        /// <summary>
        /// Gets all keys and topic ids for publishing
        /// </summary>
        public void GetPublishTopics(Dictionary<string, string> topics)
            => GetTopics(topics, PubSubTopicId, PublishOptions);
        public Dictionary<string, string> GetPublishTopics()
            => GetTopics(PubSubTopicId, PublishOptions);

        /// <summary>
        /// Gets all keys and topic ids for subscription
        /// </summary>
        public void GetSubscribeTopics(Dictionary<string, string> topics)
            => GetTopics(topics, PubSubTopicId, SubscribeOptions);
        public Dictionary<string, string> GetSubscribeTopics()
            => GetTopics(PubSubTopicId, SubscribeOptions);

        /// <summary>
        /// Get key and topic ids depending on response options
        /// </summary>
        public static void GetTopics(Dictionary<string, string> topics,
            string topicId,
            PubSubResponseOptions options)
        {
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }
            if (options.transcriptionResponses)
            {
                SetTopicKey(topics, topicId,
                    WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_TRANSCRIPTION_KEY,
                    WitConstants.WIT_SOCKET_PUBSUB_TOPIC_TRANSCRIPTION_KEY);
            }
            if (options.composerResponses)
            {
                SetTopicKey(topics, topicId,
                    WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_COMPOSER_KEY,
                    WitConstants.WIT_SOCKET_PUBSUB_TOPIC_COMPOSER_KEY);
            }
        }
        public static Dictionary<string, string> GetTopics(string topicId,
            PubSubResponseOptions options)
        {
            var dictionary = new Dictionary<string, string>();
            GetTopics(dictionary, topicId, options);
            return dictionary;
        }
        /// <summary>
        /// Apply publish key/values to a dictionary using specified key
        /// </summary>
        private static void SetTopicKey(Dictionary<string, string> topics, string topicId, string key, string append)
        {
            topics[key] = $"{topicId}{append}";
        }

        /// <summary>
        /// Whether the specified topic is a topic generated using the specified topic id and selected subscription options.
        /// </summary>
        public bool IsSubscribedTopicId(string topicId)
        {
            if (string.IsNullOrEmpty(PubSubTopicId) || string.IsNullOrEmpty(topicId))
            {
                return false;
            }
            if (SubscribeOptions.transcriptionResponses && topicId.Equals($"{PubSubTopicId}{WitConstants.WIT_SOCKET_PUBSUB_TOPIC_TRANSCRIPTION_KEY}"))
            {
                return true;
            }
            if (SubscribeOptions.composerResponses && topicId.Equals($"{PubSubTopicId}{WitConstants.WIT_SOCKET_PUBSUB_TOPIC_COMPOSER_KEY}"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Method for obtaining default pub sub response options
        /// </summary>
        private static PubSubResponseOptions GetDefaultOptions() => new PubSubResponseOptions()
        {
            transcriptionResponses = true,
            composerResponses = true
        };
    }

    /// <summary>
    /// A serialized struct that consists of all response options that can either be published for or subscribed to.
    /// </summary>
    [Serializable]
    public struct PubSubResponseOptions
    {
        [Header("Responses returned from audio interactions.")]
        public bool transcriptionResponses;

        [Header("Responses returned from composer results.")]
        public bool composerResponses;

        public bool Equals(PubSubResponseOptions other)
        {
            return transcriptionResponses == other.transcriptionResponses
                   && composerResponses == other.composerResponses;
        }
    }
}
