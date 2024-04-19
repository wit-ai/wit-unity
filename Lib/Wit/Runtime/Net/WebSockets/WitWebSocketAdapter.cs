/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice.Net.PubSub;
using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Attributes;
using UnityEngine.Events;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// A publish/subscribe MonoBehaviour adapter for WitWebSocketClients
    /// </summary>
    public class WitWebSocketAdapter : MonoBehaviour, IPubSubAdapter
    {
        /// <summary>
        /// The script used to provide the WitWebSocketClient
        /// </summary>
        public IWitWebSocketClientProvider WebSocketProvider => _webSocketProvider as IWitWebSocketClientProvider;
        [ObjectType(typeof(IWitWebSocketClientProvider))] [SerializeField]
        private UnityEngine.Object _webSocketProvider;

        /// <summary>
        /// The current web socket client
        /// </summary>
        public WitWebSocketClient WebSocketClient => WebSocketProvider?.WebSocketClient;

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
        // The actually subscribed topic
        private string _subscribedTopicId;

        #region LIFECYCLE
        protected virtual void OnEnable()
        {
            Connect();
        }

        protected void SetSubscriptionState(PubSubSubscriptionState subscriptionState)
        {
            if (SubscriptionState == subscriptionState)
            {
                return;
            }
            SubscriptionState = subscriptionState;
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
        protected virtual void HandleSubscriptionStateChange(string topicId,
            PubSubSubscriptionState subscriptionState)
        {
            if (!string.Equals(TopicId, topicId))
            {
                return;
            }
            SetSubscriptionState(subscriptionState);
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
            Disconnect();
        }
        protected virtual void OnDestroy()
        {
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
            if (_webSocketProvider != null && _webSocketProvider.Equals(clientProvider))
            {
                return;
            }

            // Disconnect previous web socket client if active
            if (gameObject.activeInHierarchy)
            {
                Disconnect();
            }

            // Apply new providers if possible
            _webSocketProvider = clientProvider as UnityEngine.Object;

            // Log warning for non UnityEngine.Objects
            if (clientProvider != null && _webSocketProvider == null)
            {
                VLog.W(GetType().Name, $"SetClientProvider failed\nReason: {clientProvider.GetType()} does not inherit from UnityEngine.Object");
            }

            // Connect new web socket client if active
            if (gameObject.activeInHierarchy)
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
            WebSocketClient.OnTopicSubscriptionStateChange -= HandleSubscriptionStateChange;
            WebSocketClient.OnTopicRequestTracked -= HandleRequestGenerated;
            WebSocketClient.Disconnect();
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

            // Unsubscribe previous topic
            Unsubscribe();

            // Set new topic
            _topicId = newTopicId;

            // Subscribe to new topic
            Subscribe();
        }

        /// <summary>
        /// Subscribe if topic id exists and connected
        /// </summary>
        private void Subscribe()
        {
            if (string.IsNullOrEmpty(TopicId) || !_connected)
            {
                return;
            }
            _subscribedTopicId = TopicId;
            WebSocketClient.Subscribe(_subscribedTopicId);
        }

        /// <summary>
        /// Unsubscribe if topic id exists and connected
        /// </summary>
        private void Unsubscribe()
        {
            if (string.IsNullOrEmpty(_subscribedTopicId) || !_connected)
            {
                return;
            }

            // Unsubscribe
            var oldTopicId = _subscribedTopicId;
            _subscribedTopicId = null;
            WebSocketClient.Unsubscribe(oldTopicId);

            // Immediately set not subscribed since no longer receive updates for current topic id
            SetSubscriptionState(PubSubSubscriptionState.NotSubscribed);
        }
        #endregion SUBSCRIBE & UNSUBSCRIBE
    }
}
