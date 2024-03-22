/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Attributes;
using UnityEngine;

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

        // Whether or not connection to server has been requested
        private bool _connectRequested = false;

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
            // Send request
            WebSocketClient.SendRequest(request);
        }
        #endregion SEND & PUBLISH
    }
}
