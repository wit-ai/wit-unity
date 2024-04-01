/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Attributes;
using Meta.Voice.Net.WebSockets.Requests;

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
        private Object _webSocketProvider;

        /// <summary>
        /// The current web socket client
        /// </summary>
        public WitWebSocketClient WebSocketClient => WebSocketProvider?.WebSocketClient;

        /// <summary>
        /// The topic to be used for publishing/subscribing to the current client provider
        /// </summary>
        public string TopicId => _topicId;
        [SerializeField] private string _topicId;

        // Whether or not connection to server has been requested
        private bool _connectRequested = false;
        // Whether or not subscription to a topic has been requested
        private string _subscribedTopicId;

        #region LIFECYCLE
        protected virtual void OnEnable()
        {
            Connect();
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
        public void SetClientProvider(IWitWebSocketClientProvider newProvider)
        {
            // Disconnect previous web socket client if active
            if (gameObject.activeInHierarchy)
            {
                Disconnect();
            }

            // Apply new provider if possible
            if (newProvider is Object obj)
            {
                _webSocketProvider = obj;
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
            if (WebSocketClient == null || _connectRequested)
            {
                return;
            }
            _connectRequested = true;

            // Connect to server if possible
            WebSocketClient.Connect();

            // Subscribe to current topic
            Subscribe();
        }

        /// <summary>
        /// Disconnects from server if possible
        /// </summary>
        private void Disconnect()
        {
            if (WebSocketClient == null || !_connectRequested)
            {
                return;
            }
            _connectRequested = false;

            // Unsubscribe from current topic
            Unsubscribe();

            // Disconnect if possible
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
            // Append if json request
            if (request is WitWebSocketJsonRequest jsonRequest)
            {
                AppendPublishNode(jsonRequest.PostData, topicId);
            }
        }

        /// <summary>
        /// Append publish topic to an existing post node
        /// </summary>
        private static void AppendPublishNode(WitResponseNode postNode, string topicId)
        {
            var publish = new WitResponseClass();
            publish[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_TRANSCRIPTION_KEY] = topicId;
            publish[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_COMPOSER_KEY] = topicId;
            postNode[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_KEY] = publish;
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

            // Subscribe to new topic if connected
            if (_connectRequested)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Begin subscribing
        /// </summary>
        private void Subscribe()
        {
            // Unsubscribe previous topic if not already done
            if (!string.IsNullOrEmpty(_subscribedTopicId))
            {
                Unsubscribe();
            }

            // Ignore without a topic id
            var topicId = TopicId;
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }

            // Set subscribed topic
            _subscribedTopicId = topicId;

            // Get & send subscribe request
            VLog.I(GetType().Name, $"Subscribe\nTopic Id: {topicId}");
            var subscribeRequest = new WitWebSocketSubscribeRequest(topicId);
            WebSocketClient.SendRequest(subscribeRequest);
        }

        /// <summary>
        /// Stop subscribing
        /// </summary>
        private void Unsubscribe()
        {
            // Ignore if not subscribed
            var topicId = _subscribedTopicId;
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }

            // Remove subscribed topic
            _subscribedTopicId = null;

            // Get & send unsubscribe request
            VLog.I(GetType().Name, $"Unsubscribe\nTopic Id: {topicId}");
            var unsubscribeRequest = new WitWebSocketSubscribeRequest(topicId, true);
            WebSocketClient.SendRequest(unsubscribeRequest);
        }
        #endregion SUBSCRIBE & UNSUBSCRIBE
    }
}
