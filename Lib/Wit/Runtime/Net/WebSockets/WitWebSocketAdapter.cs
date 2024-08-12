/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Logging;
using Meta.Voice.Net.Encoding.Wit;
using Meta.Voice.Net.PubSub;
using UnityEngine;
using Meta.WitAi.Attributes;
using UnityEngine.Events;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// A publish/subscribe MonoBehaviour adapter for WitWebSocketClients
    /// </summary>
    [LogCategory(LogCategory.Network, LogCategory.WebSockets)]
    public class WitWebSocketAdapter : MonoBehaviour, IPubSubAdapter, ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.WebSockets);

        /// <summary>
        /// The script used to provide the WitWebSocketClient
        /// </summary>
        public IWitWebSocketClientProvider WebSocketProvider => _webSocketProvider as IWitWebSocketClientProvider;
        [ObjectType(typeof(IWitWebSocketClientProvider))] [SerializeField]
        private UnityEngine.Object _webSocketProvider;

        /// <summary>
        /// The current web socket client
        /// </summary>
        public IWitWebSocketClient WebSocketClient { get; private set; }

        /// <summary>
        /// The various pub sub settings available
        /// </summary>
        public PubSubSettings Settings
        {
            get => _settings;
            set => SetSettings(value);
        }
        private PubSubSettings _settings;

        /// <summary>
        /// The current subscription state of the adapter
        /// </summary>
        public PubSubSubscriptionState SubscriptionState { get; private set; }

        /// <summary>
        /// Event callback for subscription state change
        /// </summary>
        public event Action<PubSubSubscriptionState> OnTopicSubscriptionStateChange;

        /// <summary>
        /// Callback when successfully subscribed to the current topic
        /// </summary>
        public UnityEvent OnSubscribed { get; } = new UnityEvent();

        /// <summary>
        /// Callback when successfully unsubscribed from the current topic
        /// </summary>
        public UnityEvent OnUnsubscribed { get; } = new UnityEvent();

        /// <summary>
        /// An event callback for processing a response for a request originating
        /// on a different client with a topic this client adapter has subscribed to.
        /// </summary>
        public event WitWebSocketResponseProcessor OnProcessForwardedResponse;

        /// <summary>
        /// Callback when a request is generated for the subscribed topic
        /// </summary>
        public event Action<IWitWebSocketRequest> OnRequestGenerated;

        // Whether or not connection to server has been requested
        private bool _connected = false;
        // Whether or not currently active in heirarchy
        private bool _active = false;
        // Current subscriptions per topic
        private ConcurrentDictionary<string, PubSubSubscriptionState> _subscriptionsPerTopic =
            new ConcurrentDictionary<string, PubSubSubscriptionState>();

        #region LIFECYCLE
        protected virtual void OnEnable()
        {
            _active = true;
            SetClientProvider(WebSocketProvider);
            Connect();
        }
        protected virtual bool RaiseProcessForwardedResponse(string topicId,
            string requestId,
            string clientUserId,
            WitChunk responseChunk)
        {
            if (!Settings.IsSubscribedTopicId(topicId))
            {
                return false;
            }
            return OnProcessForwardedResponse?.Invoke(topicId, requestId, clientUserId, responseChunk) ?? false;
        }
        protected virtual void HandleRequestGenerated(string topicId,
            IWitWebSocketRequest request)
        {
            if (!Settings.IsSubscribedTopicId(topicId))
            {
                return;
            }
            OnRequestGenerated?.Invoke(request);
        }
        protected virtual void OnDisable()
        {
            _active = false;
            Disconnect();
        }
        protected virtual void OnDestroy()
        {
            WebSocketClient = null;
            Disconnect();
        }
        #endregion LIFECYCLE

        #region CONNECT & DISCONNECT
        /// <summary>
        /// Safely sets the new web socket client provider if possible
        /// </summary>
        public void SetClientProvider(IWitWebSocketClientProvider clientProvider)
        {
            // Ignore if already set
            var newClient = clientProvider?.WebSocketClient;
            if (WebSocketClient != null && WebSocketClient.Equals(newClient))
            {
                return;
            }

            // Disconnect previous web socket client if active
            if (_active)
            {
                Disconnect();
            }

            // Apply new providers if possible
            _webSocketProvider = clientProvider as UnityEngine.Object;
            WebSocketClient = newClient;

            // Log warning for non UnityEngine.Objects
            if (clientProvider != null && _webSocketProvider == null)
            {
                Logger.Warning("SetClientProvider failed\nReason: {0} does not inherit from UnityEngine.Object", clientProvider.GetType());
            }

            // Connect new web socket client if active
            if (_active)
            {
                Connect();
            }
        }

        /// <summary>
        /// Connects to server if possible
        /// </summary>
        private void Connect()
        {
            if (WebSocketClient == null || _connected)
            {
                return;
            }
            _connected = true;

            // Connect to server if possible
            WebSocketClient.OnTopicSubscriptionStateChange += ApplySubscriptionPerTopic;
            WebSocketClient.OnProcessForwardedResponse += RaiseProcessForwardedResponse;
            WebSocketClient.OnTopicRequestTracked += HandleRequestGenerated;
            WebSocketClient.Connect();

            // Subscribe to current topic
            Subscribe();
        }

        /// <summary>
        /// Disconnects from server if possible
        /// </summary>
        private void Disconnect()
        {
            if (WebSocketClient == null || !_connected)
            {
                return;
            }

            // Unsubscribe from current topic
            Unsubscribe();

            // Disconnect if possible
            _connected = false;
            WebSocketClient.Disconnect();
            WebSocketClient.OnTopicSubscriptionStateChange -= ApplySubscriptionPerTopic;
            WebSocketClient.OnProcessForwardedResponse -= RaiseProcessForwardedResponse;
            WebSocketClient.OnTopicRequestTracked -= HandleRequestGenerated;
        }
        #endregion CONNECT & DISCONNECT

        #region SEND & PUBLISH
        /// <summary>
        /// Send a request with a specified topic
        /// </summary>
        public void SendRequest(IWitWebSocketRequest request)
        {
            // Append settings
            request.TopicId = Settings.PubSubTopicId;
            request.PublishOptions = Settings.PublishOptions;

            // Send request
            WebSocketClient.SendRequest(request);
        }
        #endregion SEND & PUBLISH

        #region SUBSCRIBE & UNSUBSCRIBE
        /// <summary>
        /// Set the pubsub settings, unsubscribe to the previous & subscribe to a new
        /// </summary>
        public void SetSettings(PubSubSettings settings)
        {
            // Ensure spamming of subscriptions does not occur
            if (Settings.Equals(settings))
            {
                return;
            }

            // Unsubscribe from old topic ids
            Unsubscribe();

            // Set new topic
            Logger.Verbose("Topic set to {0}\nFrom: {1}", settings.PubSubTopicId ?? "Null", Settings.PubSubTopicId ?? "Null");
            _settings = settings;

            // Subscribe to new topic ids
            Subscribe();
        }

        /// <summary>
        /// Unsubscribe if topic id exists and connected
        /// </summary>
        private void Unsubscribe()
        {
            // Ignore if null or not connected
            var topicId = Settings.PubSubTopicId;
            if (string.IsNullOrEmpty(topicId) || !_connected)
            {
                return;
            }

            // Unsubscribe from topic id
            Logger.Verbose("Unsubscribe from topic: {0}", topicId);

            // Iterate each subscription topic
            var topics = Settings.GetSubscribeTopics();
            foreach (var topicValue in topics.Values)
            {
                ApplySubscriptionPerTopic(topicValue, PubSubSubscriptionState.Unsubscribing);
            }
            foreach (var topicValue in topics.Values)
            {
                WebSocketClient.Unsubscribe(topicValue);
                ApplySubscriptionPerTopic(topicValue, PubSubSubscriptionState.NotSubscribed);
            }
            // Clear existing topic ids
            _subscriptionsPerTopic.Clear();
        }

        /// <summary>
        /// Subscribe if topic id exists and connected
        /// </summary>
        private void Subscribe()
        {
            // Ignore if null or not connected
            var topicId = Settings.PubSubTopicId;
            if (string.IsNullOrEmpty(topicId) || !_connected)
            {
                return;
            }

            // Begin subscribing
            Logger.Verbose("Subscribe to topic: {0}", topicId);

            // Iterate each subscription topic
            var topics = Settings.GetSubscribeTopics();
            foreach (var topicValue in topics.Values)
            {
                ApplySubscriptionPerTopic(topicValue, PubSubSubscriptionState.Subscribing);
                WebSocketClient.Subscribe(topicValue);
            }
        }

        /// <summary>
        /// Handle currently set topic id subscription changes only
        /// </summary>
        protected virtual void ApplySubscriptionPerTopic(string topicId,
            PubSubSubscriptionState subscriptionState)
        {
            // Only check if currently subscribed
            if (!Settings.IsSubscribedTopicId(topicId))
            {
                return;
            }
            // Ignore if the same
            if (_subscriptionsPerTopic.ContainsKey(topicId)
                && _subscriptionsPerTopic[topicId] == subscriptionState)
            {
                return;
            }

            // Set the state
            _subscriptionsPerTopic[topicId] = subscriptionState;

            // Refresh current subscription state
            RefreshSubscriptionState();
        }

        /// <summary>
        /// Set the current subscription state to the determined state
        /// </summary>
        private void RefreshSubscriptionState()
            => SetSubscriptionState(DetermineSubscriptionState());

        /// <summary>
        /// Determines the current subscription state
        /// </summary>
        protected PubSubSubscriptionState DetermineSubscriptionState()
        {
            var state = PubSubSubscriptionState.NotSubscribed;
            var subscribed = _subscriptionsPerTopic.Keys.Count > 0;
            foreach (var key in _subscriptionsPerTopic.Keys)
            {
                if (!_subscriptionsPerTopic.TryGetValue(key, out var topicState))
                {
                    continue;
                }
                // Error
                if (topicState == PubSubSubscriptionState.SubscribeError
                    || topicState == PubSubSubscriptionState.UnsubscribeError)
                {
                    return topicState;
                }
                // Not subscribed
                if (subscribed && topicState != PubSubSubscriptionState.Subscribed)
                {
                    subscribed = false;
                }
                // Set as most recent subscribing or unsubscribing
                if (topicState == PubSubSubscriptionState.Subscribing
                    || topicState == PubSubSubscriptionState.Unsubscribing)
                {
                    state = topicState;
                }
            }
            // If still subscribed
            if (subscribed)
            {
                return PubSubSubscriptionState.Subscribed;
            }
            return state;
        }

        /// <summary>
        /// Set the current subscription state
        /// </summary>
        private void SetSubscriptionState(PubSubSubscriptionState newSubState)
        {
            // Ignore if the same state
            if (SubscriptionState == newSubState)
            {
                return;
            }

            // Set the new state
            SubscriptionState = newSubState;
            OnTopicSubscriptionStateChange?.Invoke(SubscriptionState);

            // Subscribed if set
            if (SubscriptionState == PubSubSubscriptionState.Subscribed)
            {
                OnSubscribed?.Invoke();
            }
            // Unsubscribed if set after unsubscribing or error
            else if (SubscriptionState == PubSubSubscriptionState.NotSubscribed)
            {
                OnUnsubscribed?.Invoke();
            }
        }
        #endregion SUBSCRIBE & UNSUBSCRIBE
    }
}
