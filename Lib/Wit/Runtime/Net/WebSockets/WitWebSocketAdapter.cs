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
    public class WitWebSocketAdapter : MonoBehaviour
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
        public string TopicId => _topicId;
        [SerializeField] private string _topicId;

        /// <summary>
        /// Callback when successfully subscribed to the current topic
        /// </summary>
        public UnityEvent OnSubscribed { get; private set; } = new UnityEvent();

        /// <summary>
        /// Callback when successfully unsubscribed from the current topic
        /// </summary>
        public UnityEvent OnUnsubscribed { get; private set; } = new UnityEvent();

        /// <summary>
        /// Callback when a request is generated for the subscribed topic
        /// </summary>
        public event Action<IWitWebSocketRequest> OnRequestGenerated;

        // Whether or not connection to server has been requested
        private bool _connected = false;

        #region LIFECYCLE
        protected virtual void OnEnable()
        {
            Connect();
        }
        protected virtual void HandleSubscriptionStateChange(string topicId,
            PubSubSubscriptionState state)
        {
            if (!string.Equals(TopicId, topicId))
            {
                return;
            }
            if (state == PubSubSubscriptionState.Subscribed)
            {
                OnSubscribed?.Invoke();
            }
            else if (state == PubSubSubscriptionState.NotSubscribed)
            {
                OnUnsubscribed?.Invoke();
            }
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
            WebSocketClient.Subscribe(TopicId);
        }

        /// <summary>
        /// Unsubscribe if topic id exists and connected
        /// </summary>
        private void Unsubscribe()
        {
            if (string.IsNullOrEmpty(TopicId) || !_connected)
            {
                return;
            }
            WebSocketClient.Unsubscribe(TopicId);
        }
        #endregion SUBSCRIBE & UNSUBSCRIBE
    }
}
