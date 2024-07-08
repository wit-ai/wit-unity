/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Logging;
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
        /// The topic to be used for publishing/subscribing to the current client provider
        /// </summary>
        public string TopicId
        {
            get => _topicId;
            set => SetTopicId(value);
        }
        [SerializeField] private string _topicId;

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
        /// Callback when a request is generated for the subscribed topic
        /// </summary>
        public event Action<IWitWebSocketRequest> OnRequestGenerated;

        // Whether or not connection to server has been requested
        private bool _connected = false;
        // Whether or not currently active in heirarchy
        private bool _active = false;

        #region LIFECYCLE
        protected virtual void OnEnable()
        {
            _active = true;
            SetClientProvider(WebSocketProvider);
            Connect();
        }
        protected virtual void HandleRequestGenerated(string topicId,
            IWitWebSocketRequest request)
        {
            if (!string.Equals(TopicId, topicId))
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
            WebSocketClient.OnTopicSubscriptionStateChange += HandleSubscriptionStateChange;
            WebSocketClient.OnTopicRequestTracked += HandleRequestGenerated;
            WebSocketClient.Connect();

            // Subscribe to current topic
            Subscribe(TopicId);
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
            Unsubscribe(TopicId);

            // Disconnect if possible
            _connected = false;
            WebSocketClient.Disconnect();
            WebSocketClient.OnTopicSubscriptionStateChange -= HandleSubscriptionStateChange;
            WebSocketClient.OnTopicRequestTracked -= HandleRequestGenerated;
        }
        #endregion CONNECT & DISCONNECT

        #region SEND & PUBLISH
        /// <summary>
        /// Send a request with a specified topic
        /// </summary>
        public void SendRequest(IWitWebSocketRequest request)
        {
            // Append topic
            Publish(request, TopicId);

            // Send request
            WebSocketClient.SendRequest(request);
        }

        /// <summary>
        /// Attempt to publish request if applicable
        /// </summary>
        private void Publish(IWitWebSocketRequest request, string topicId)
        {
            // Ignore if topic is null
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }
            // Set request's topic id
            request.TopicId = topicId;
        }
        #endregion SEND & PUBLISH

        #region SUBSCRIBE & UNSUBSCRIBE
        /// <summary>
        /// Set the topic, unsubscribe to the previous & subscribe to a new
        /// </summary>
        public void SetTopicId(string newTopicId)
        {
            // Ignore if same topic
            if (string.Equals(TopicId, newTopicId, StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            // Unsubscribe from old topic id
            Unsubscribe(TopicId);

            // Set new topic
            Logger.Verbose("PubSub Topic ID Set from {0} to {1}", TopicId, newTopicId);
            _topicId = newTopicId;

            // Subscribe to new topic
            Subscribe(TopicId);
        }

        /// <summary>
        /// Unsubscribe if topic id exists and connected
        /// </summary>
        private void Unsubscribe(string topicId)
        {
            // Ignore if null or not connected
            if (string.IsNullOrEmpty(topicId) || !_connected)
            {
                return;
            }

            // Unsubscribe from topic id
            Logger.Verbose("Unsubscribe from topic: {0}", topicId);
            WebSocketClient.Unsubscribe(topicId);

            // Immediately set state as not subscribed
            SetSubscriptionState(PubSubSubscriptionState.Unsubscribing);
            SetSubscriptionState(PubSubSubscriptionState.NotSubscribed);
        }

        /// <summary>
        /// Subscribe if topic id exists and connected
        /// </summary>
        private void Subscribe(string topicId)
        {
            // Ignore if null or not connected
            if (string.IsNullOrEmpty(topicId) || !_connected)
            {
                return;
            }

            // Get current state (in case already subscribing elsewhere)
            SetSubscriptionState(WebSocketClient.GetTopicSubscriptionState(topicId));

            // Begin subscribing
            Logger.Verbose("Subscribe to topic: {0}", topicId);
            WebSocketClient.Subscribe(topicId);
        }

        /// <summary>
        /// Handle currently set topic id subscription changes only
        /// </summary>
        protected virtual void HandleSubscriptionStateChange(string topicId,
            PubSubSubscriptionState subscriptionState)
        {
            // Only check if currently subscribed
            if (!string.Equals(TopicId, topicId))
            {
                return;
            }
            // Set state
            SetSubscriptionState(subscriptionState);
        }

        /// <summary>
        /// Set the current subscription state
        /// </summary>
        protected void SetSubscriptionState(PubSubSubscriptionState subscriptionState)
        {
            // Ignore if the same state
            if (SubscriptionState == subscriptionState)
            {
                return;
            }

            // Set the new state
            SubscriptionState = subscriptionState;

            // Invoke all callbacks
            OnTopicSubscriptionStateChange?.Invoke(SubscriptionState);
            if (subscriptionState == PubSubSubscriptionState.Subscribed)
            {
                OnSubscribed?.Invoke();
            }
            else if (subscriptionState == PubSubSubscriptionState.NotSubscribed)
            {
                OnUnsubscribed?.Invoke();
            }
        }
        #endregion SUBSCRIBE & UNSUBSCRIBE
    }
}
