/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.Voice.Logging;
using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.Voice.Net.Encoding.Wit;
using Meta.Voice.Net.PubSub;
using Meta.Voice.Net.WebSockets.Requests;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An class for performing multi-threaded web socket communication to a Wit endpoint
    /// WitWebSocketClient is a wrapper for web socket communication that facilitates the safe upload and
    /// download of data between the Voice SDK and Wit.ai.  The class handles connection, transmission and response handling
    /// for all socket communication.  Three background threads are spawned by each WitWebSocketClient in order to handle
    /// encoding/uploading, downloading/decoding and response handling respectively.  WitWebSocketClients contain a list of
    /// IWitWebSocketRequests which are used to facilitate upload and download of data.  When sending a WitChunk the IWitWebSocketRequest’s
    /// request id will also be sent within the json data.  When receiving a WitChunk, request id will be checked within the json data and
    /// the appropriate IWitWebSocketRequest will handle the response.  If no matching request is found, the WitChunk’s topic id will be
    /// used to find the appropriate IWitWebSocketSubscriber which then will generate a request to handle the response.
    /// </summary>
    [LogCategory(LogCategory.Network)]
    public class WitWebSocketClient : IPubSubSubscriber
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        /// <summary>
        /// The settings required to connect, authenticate and drive server/client communication.
        /// </summary>
        public WitWebSocketSettings Settings { get; }

        /// <summary>
        /// Whether the web socket is disconnected, connecting, connected, or disconnecting.
        /// </summary>
        public WitWebSocketConnectionState ConnectionState { get; private set; } =
            WitWebSocketConnectionState.Disconnected;

        /// <summary>
        /// Whether authentication had completed successfully or not
        /// </summary>
        public bool IsAuthenticated { get; private set; }

        /// <summary>
        /// Whether there currently is data being encoded and/or queued to be sent from the web socket.
        /// </summary>
        public bool IsUploading => _uploadCount > 0;

        /// <summary>
        /// Whether there currently is data being received and/or decoded from the web socket.
        /// </summary>
        public bool IsDownloading => _downloadCount > 0;

        /// <summary>
        /// Whether there currently are any scripts that have called Connect()
        /// and not yet requested a Disconnect().
        /// </summary>
        public bool IsReferenced => ReferenceCount > 0;

        /// <summary>
        /// Whether will be reconnecting
        /// </summary>
        public bool IsReconnecting => IsReferenced
                                      && ConnectionState == WitWebSocketConnectionState.Disconnected
                                      && (Settings.ReconnectAttempts < 0
                                          || FailedConnectionAttempts <= Settings.ReconnectAttempts);

        /// <summary>
        /// Total amount of scripts that have called Connect()
        /// and have not yet called Disconnect().  Used to ensure
        /// WebSocketClient is only disconnected once no scripts are still referenced.
        /// </summary>
        public int ReferenceCount { get; private set; }

        /// <summary>
        /// Total amount of failed connection attempts made
        /// </summary>
        public int FailedConnectionAttempts { get; private set; }

        /// <summary>
        /// The utc time of the last response from the server
        /// </summary>
        public DateTime LastResponseTime { get; private set; }

        /// <summary>
        /// Callback on connection state change.
        /// </summary>
        public event Action<WitWebSocketConnectionState> OnConnectionStateChanged;

        // Stores last request id to handle binary data without headers
        private string _lastRequestId;
        // Total number of chunks currently being uploaded
        private int _uploadCount;
        // Total number of responses currently being decoded
        private int _downloadCount;

        /// <summary>
        /// The requests currently being tracked by this client. Each access generates
        /// a new dictionary and should be cached.
        /// </summary>
        public Dictionary<string, IWitWebSocketRequest> Requests
            => new Dictionary<string, IWitWebSocketRequest>(_requests);
        private Dictionary<string, IWitWebSocketRequest> _requests = new Dictionary<string, IWitWebSocketRequest>();
        private List<string> _untrackedRequests = new List<string>();

        // The web socket client handling all communication
        private IWebSocket _socket;

        // Script used for decoding server responses
        private WitChunkConverter _decoder = new WitChunkConverter();

#if UNITY_EDITOR
        /// <summary>
        /// Editor only option to get a custom web socket
        /// </summary>
        public Func<string, Dictionary<string, string>, IWebSocket> GetWebSocket;
#endif

        /// <summary>
        /// Constructor with settings data
        /// </summary>
        public WitWebSocketClient(WitWebSocketSettings settings)
        {
            Settings = settings;
        }
        /// <summary>
        /// Constructor that takes in IWitRequestConfiguration and generates default settings use it
        /// </summary>
        public WitWebSocketClient(IWitRequestConfiguration configuration)
            : this(new WitWebSocketSettings(configuration)) { }

        /// <summary>
        /// Sets the connection state and performs callback
        /// </summary>
        private void SetConnectionState(WitWebSocketConnectionState newConnectionState)
        {
            if (newConnectionState == ConnectionState)
            {
                return;
            }
            ConnectionState = newConnectionState;
            _log.Info(ConnectionState.ToString());
            OnConnectionStateChanged?.Invoke(ConnectionState);
        }

        #region CONNECT
        /// <summary>
        /// Attempts to connect to the specified
        /// </summary>
        public void Connect()
        {
            // Increment reference count
            ReferenceCount++;
            if (!IsReferenced)
            {
                return;
            }

            // Ignore if already active
            if (ConnectionState == WitWebSocketConnectionState.Connecting
                || ConnectionState == WitWebSocketConnectionState.Connected)
            {
                return;
            }

            // Connect
            _ = ConnectAsync();
        }

        /// <summary>
        /// Performs connection asynchronously
        /// </summary>
        private async Task ConnectAsync()
        {
            // Begin connecting
            SetConnectionState(WitWebSocketConnectionState.Connecting);

            // Connect async to specified server url with specified options
            try
            {
                // Obtain all data required for socket connection
                var requestId = WitConstants.GetUniqueId();
                var headers = WitRequestSettings.GetHeaders(Settings.Configuration, requestId, false);
                if (headers.ContainsKey(WitConstants.HEADER_AUTH))
                {
                    headers.Remove(WitConstants.HEADER_AUTH);
                }

                // Generate socket wrapper and assign message callback method
                _socket = GenerateWebSocket(Settings.ServerUrl, headers);
                _socket.OnOpen += HandleSocketConnected;
                _socket.OnMessage += HandleSocketResponse;
                _socket.OnError += HandleSocketError;
                _socket.OnClose += HandleSocketDisconnect;

                // Begin a timeout check
                _ = WaitForConnectionTimeout();

                // Connect and wait until connection completes
                await _socket.Connect();
            }
            // Timeout handling
            catch (OperationCanceledException)
            {
                HandleSetupFailed(WitConstants.ERROR_RESPONSE_TIMEOUT);
            }
            // Additional exception handling
            catch (Exception e)
            {
                HandleSetupFailed($"Connection connect error caught\n{e}");
            }
        }

        /// <summary>
        /// Wait for connection timeout to ensure it occured
        /// </summary>
        private async Task WaitForConnectionTimeout()
        {
            // Wait for connection timeout
            await Task.Delay(Settings.ServerConnectionTimeoutMs);

            // Invalid socket or connected
            if (_socket == null || _socket.State != WitWebSocketConnectionState.Connecting)
            {
                return;
            }

            // Consider a timeout
            HandleSetupFailed(WitConstants.ERROR_RESPONSE_TIMEOUT);
        }

        /// <summary>
        /// Generates a new web socket using specified url and headers
        /// </summary>
        private IWebSocket GenerateWebSocket(string url, Dictionary<string, string> headers)
        {
            #if UNITY_EDITOR
            if (GetWebSocket != null)
            {
                var socket = GetWebSocket.Invoke(url, headers);
                if (socket != null)
                {
                    return socket;
                }
            }
            #endif
            return new NativeWebSocketWrapper(url, headers);
        }

        /// <summary>
        /// Debugs errors returned from the socket
        /// </summary>
        private void HandleSocketError(string errorMessage)
        {
            if (ConnectionState == WitWebSocketConnectionState.Connecting)
            {
                HandleSetupFailed(errorMessage);
            }
            else
            {
                VLog.W(GetType().Name, $"Socket Error\nMessage: {errorMessage}");
            }
        }

        /// <summary>
        /// Callback on socket connection, performs setup if connected successfully
        /// </summary>
        private void HandleSocketConnected()
        {
            // Ensure not disconnected
            if (ConnectionState != WitWebSocketConnectionState.Connecting)
            {
                HandleSetupFailed($"State changed to {ConnectionState} during connection.");
                return;
            }
            // Ensure socket exists
            if (_socket == null)
            {
                HandleSetupFailed("WebSocket client no longer exists.");
                return;
            }
            // Ensure socket is open
            if (_socket.State != WitWebSocketConnectionState.Connected)
            {
                HandleSetupFailed($"Socket is {_socket.State}");
                return;
            }
            // Already connected
            if (ConnectionState == WitWebSocketConnectionState.Connected)
            {
                return;
            }

            // Perform final setup
            _ = SetupAsync();
        }

        /// <summary>
        /// Option to perform additional setup for the socket, including authentication
        /// </summary>
        private async Task SetupAsync()
        {
            // Move async
            await Task.Yield();

            // Get client access token
            string clientAccessToken = Settings?.Configuration?.GetClientAccessToken();
            if (string.IsNullOrEmpty(clientAccessToken))
            {
                HandleSetupFailed("Cannot connect to Wit server without client access token");
                return;
            }

            // Make authentication request and return any encountered error
            var authRequest = new WitWebSocketAuthRequest(clientAccessToken);
            SendRequest(authRequest);
            await TaskUtility.WaitWhile(() => !authRequest.IsComplete);

            // Auth error
            IsAuthenticated = string.IsNullOrEmpty(authRequest.Error);
            if (!IsAuthenticated)
            {
                Settings.ReconnectAttempts = 0; // Don't retry
                HandleSetupFailed(authRequest.Error);
                return;
            }
            // Cancelled elsewhere
            if (ConnectionState != WitWebSocketConnectionState.Connecting)
            {
                HandleSetupFailed($"State changed to {ConnectionState} during authentication.");
                return;
            }

#if UNITY_EDITOR
            // Add disconnect on playmode exit
            UnityEditor.EditorApplication.playModeStateChanged += DisconnectIfExitingPlayMode;
#endif

            // Connected successfully
            FailedConnectionAttempts = 0;
            SetConnectionState(WitWebSocketConnectionState.Connected);

            // Attempt to subscribe to all existing subscriptions
            var topicIds = _subscriptions.Keys.ToArray();
            foreach (var topicId in topicIds)
            {
                Subscribe(topicId, true);
            }
        }

        /// <summary>
        /// Disconnect if connection failed, warn otherwise
        /// </summary>
        private void HandleSetupFailed(string error)
        {
            if (ConnectionState == WitWebSocketConnectionState.Connecting)
            {
                VLog.E(GetType().Name, $"Connection Failed\n{error}");
                FailedConnectionAttempts++;
                ForceDisconnect();
            }
            else
            {
                VLog.W(GetType().Name, $"Connection Cancelled\n{error}");
            }
        }
        #endregion CONNECT

        #region DISCONNECT
        /// <summary>
        /// Handles a server disconnect if not started locally
        /// </summary>
        private void HandleSocketDisconnect(WebSocketCloseCode closeCode)
        {
            if (ConnectionState == WitWebSocketConnectionState.Connected)
            {
                VLog.W(GetType().Name, $"Socket Closed\nReason: {closeCode}");
                ForceDisconnect();
            }
        }

        /// <summary>
        /// Disconnects socket after checking state
        /// </summary>
        public void Disconnect()
        {
            // Decrement
            ReferenceCount--;
            if (IsReferenced)
            {
                return;
            }

            // Remove reference count
            ReferenceCount = 0;

            // Disconnect without reference count
            ForceDisconnect();
        }

        /// <summary>
        /// Forces a disconnect independent from reference count
        /// </summary>
        private void ForceDisconnect()
        {
            // Ignore if already disconnecting/disconnected
            if (ConnectionState == WitWebSocketConnectionState.Disconnecting
                || ConnectionState == WitWebSocketConnectionState.Disconnected)
            {
                return;
            }

            // Perform disconnection
            _ = DisconnectAsync();
        }

        /// <summary>
        /// Disconnects without checking state
        /// </summary>
        private async Task DisconnectAsync()
        {
            // Sets disconnecting state
            SetConnectionState(WitWebSocketConnectionState.Disconnecting);

            // Perform breakdown if possible
            await BreakdownAsync();

            // Fail if changed during breakdown
            if (ConnectionState != WitWebSocketConnectionState.Disconnecting)
            {
                VLog.W(GetType().Name, $"State changed to {ConnectionState} during breakdown.");
                return;
            }

            // Disconnected successfully
            SetConnectionState(WitWebSocketConnectionState.Disconnected);

            // Attempt to reconnect
            if (IsReferenced)
            {
                Reconnect();
            }
        }

        /// <summary>
        /// Unloads all requests & ensures socket disconnection
        /// </summary>
        private async Task BreakdownAsync()
        {
            // No longer authenticated
            IsAuthenticated = false;

            // Untrack all running requests
            var requestIds = _requests.Keys.ToArray();
            foreach (var requestId in requestIds)
            {
                UntrackRequest(requestId);
            }
            // Clear untracked list
            _untrackedRequests.Clear();

            // Sets all currently subscribed topics to 'Subscribing' state
            var topicIds = _subscriptions.Keys.ToArray();
            foreach (var topicId in topicIds)
            {
                var subState = GetTopicSubscriptionState(topicId);
                if (subState == PubSubSubscriptionState.Subscribing
                    || subState == PubSubSubscriptionState.Subscribed
                    || subState == PubSubSubscriptionState.SubscribeError)
                {
                    Subscribe(topicId, true);
                }
                else
                {
                    Unsubscribe(topicId, true);
                }
            }

            // Close socket connection
            if (_socket != null)
            {
                _socket.OnOpen -= HandleSocketConnected;
                _socket.OnMessage -= HandleSocketResponse;
                _socket.OnError -= HandleSocketError;
                _socket.OnClose -= HandleSocketDisconnect;
                await _socket.Close();
                _socket = null;
            }

            // Clear counts
            _uploadCount = 0;
            _downloadCount = 0;

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= DisconnectIfExitingPlayMode;
            #endif
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor callback to ensure web sockets are always closed in editor
        /// </summary>
        private void DisconnectIfExitingPlayMode(UnityEditor.PlayModeStateChange playModeState)
        {
            if (playModeState == UnityEditor.PlayModeStateChange.ExitingPlayMode
                && (ConnectionState == WitWebSocketConnectionState.Connecting
                    || ConnectionState == WitWebSocketConnectionState.Connected))
            {
                ReferenceCount = 0;
                ForceDisconnect();
            }
        }
        #endif
        #endregion DISCONNECT

        #region RECONNECT
        /// <summary>
        /// Waits an interval and then attempts to connect once again
        /// </summary>
        private void Reconnect()
        {
            // Ignore if not referenced or disconnected
            if (!IsReferenced || ConnectionState != WitWebSocketConnectionState.Disconnected)
            {
                return;
            }
            // Ignore if failed too many times
            if (Settings.ReconnectAttempts >= 0 && FailedConnectionAttempts > Settings.ReconnectAttempts)
            {
                VLog.E(GetType().Name, $"Reconnect Failed\nToo many failed reconnect attempts\nFailures: {FailedConnectionAttempts}\nAttempts Allowed: {Settings.ReconnectAttempts}");
                return;
            }
            // Wait and reconnect
            _ = WaitAndConnect();
        }
        /// <summary>
        /// Wait and attempt to connect
        /// </summary>
        private async Task WaitAndConnect()
        {
            // Wait for reconnect interval
            var delay = Mathf.RoundToInt(Settings.ReconnectInterval * 1000f);
            if (delay > 0f)
            {
                await Task.Delay(delay);
            }
            // Don't allow immediate reconnect
            else
            {
                await TaskUtility.Wait();
            }

            // Ignore should no longer reconnect
            if (!IsReconnecting)
            {
                return;
            }

            // Wait for reconnection
            _log.Info($"Reconnect Attempt {FailedConnectionAttempts}");
            await ConnectAsync();
        }
        #endregion RECONNECT

        #region UPLOAD
        /// <summary>
        /// Send a request via this client if possible
        /// </summary>
        public bool SendRequest(IWitWebSocketRequest request)
        {
            // Begin tracking request
            if (!TrackRequest(request))
            {
                return false;
            }

            // Begin upload and send method for chunks when ready to be sent
            request.HandleUpload(SendChunk);
            return true;
        }

        /// <summary>
        /// Safely builds WitChunk with request id and then enqueues
        /// </summary>
        private void SendChunk(string requestId, WitResponseNode requestJsonData, byte[] requestBinaryData)
        {
            _ = SendChunkAsync(requestId, requestJsonData, requestBinaryData);
        }

        /// <summary>
        /// Performs upload and waits for completion
        /// </summary>
        private async Task SendChunkAsync(string requestId, WitResponseNode requestJsonData, byte[] requestBinaryData)
        {
            // Move async
            await Task.Yield();

            // If not authorization chunk, wait while connecting or reconnecting
            bool isAuth = _requests.TryGetValue(requestId, out var request) && request is Requests.WitWebSocketAuthRequest;
            if (!isAuth)
            {
                await TaskUtility.WaitWhile(() => ConnectionState == WitWebSocketConnectionState.Connecting || IsReconnecting);
                if (ConnectionState != WitWebSocketConnectionState.Connected && !IsReconnecting)
                {
                    return;
                }
            }
            // If auth chunk, ignore connecting or connected
            else if (ConnectionState != WitWebSocketConnectionState.Connecting
                     && ConnectionState != WitWebSocketConnectionState.Connected)
            {
                return;
            }
            // If request was cancelled, do not send
            if (!_requests.ContainsKey(requestId))
            {
                return;
            }

            // Increment upload count
            _uploadCount++;

            // Get json data if needed
            if (requestJsonData == null)
            {
                requestJsonData = new WitResponseClass();
            }
            // Sets request id
            requestJsonData[WitConstants.WIT_SOCKET_REQUEST_ID_KEY] = requestId;
            // Get chunk
            var chunk = new WitChunk()
            {
                jsonData = requestJsonData,
                binaryData = requestBinaryData
            };
            // Encode chunk
            byte[] rawData = EncodeChunk(chunk);

            // Perform send
            if (rawData != null)
            {
                await _socket.Send(rawData);
            }

            // Decrement upload count
            _uploadCount--;
        }

        /// <summary>
        /// Encodes provided WitChunk into bytes for submission
        /// </summary>
        private byte[] EncodeChunk(WitChunk chunk) =>
            WitChunkConverter.Encode(chunk);
        #endregion UPLOAD

        #region DOWNLOAD
        /// <summary>
        /// When dispatched, begins async decoding of response bytes
        /// </summary>
        private void HandleSocketResponse(byte[] rawBytes)
        {
            _ = DecodeChunkAsync(rawBytes);
        }

        /// <summary>
        /// Performs a decode on a background thread
        /// </summary>
        private async Task DecodeChunkAsync(byte[] rawBytes)
        {
            // Increment upload count
            _downloadCount++;

            // Move async
            await Task.Yield();

            // Decode one or more chunks
            WitChunk[] chunks = DecodeChunk(rawBytes, 0, rawBytes.Length);

            // Perform send
            if (chunks != null && chunks.Length > 0)
            {
                foreach (var chunk in chunks)
                {
                    HandleChunk(chunk);
                }
            }

            // Decrement download count
            _downloadCount--;
        }

        /// <summary>
        /// Decodes the provided buffer data into one or more WitChunks
        /// </summary>
        private WitChunk[] DecodeChunk(byte[] rawBytes, int start, int length) =>
            _decoder.Decode(rawBytes, start, length);

        /// <summary>
        /// Determines request id and applies chunk to the applicable request for handling
        /// </summary>
        private void HandleChunk(WitChunk chunk)
        {
            // Iterate safely due to background thread
            var requestId = chunk.jsonData?[WitConstants.WIT_SOCKET_REQUEST_ID_KEY];
            if (string.IsNullOrEmpty(requestId))
            {
                if (string.IsNullOrEmpty(_lastRequestId))
                {
                    VLog.E(GetType().Name,
                        $"Download Chunk - Failed\nNo request id found in chunk\nJson:\n{(chunk.jsonData?.ToString() ?? "Null")}");
                    return;
                }
                requestId = _lastRequestId;
                if (chunk.jsonData == null)
                {
                    chunk.jsonData = new WitResponseClass();
                    chunk.jsonData[WitConstants.WIT_SOCKET_REQUEST_ID_KEY] = requestId;
                }
            }
            // Store previous request id
            else
            {
                _lastRequestId = requestId;
            }

            // Returned untracked request, generate if needed
            if (!_requests.TryGetValue(requestId, out var request))
            {
                request = GenerateRequest(requestId, chunk.jsonData);
            }
            if (request == null)
            {
                return;
            }

            // Handle download synchronously
            try
            {
                request.HandleDownload(chunk.jsonString, chunk.jsonData, chunk.binaryData);
            }
            // Catch exceptions or else they will be ignored
            catch (Exception e)
            {
                VLog.E(GetType().Name, $"Request HandleDownload method exception caught\n{request}\n\n{e}\n");
                UntrackRequest(request);
            }
        }
        #endregion DOWNLOAD

        #region REQUESTS
        /// <summary>
        /// Safely adds a request to the current request list
        /// </summary>
        public bool TrackRequest(IWitWebSocketRequest request)
        {
            // Ignore null requests
            if (request == null)
            {
                return false;
            }
            // Ensure not already tracked
            if (_requests.ContainsValue(request))
            {
                VLog.E(GetType().Name, $"Track Request - Failed\nReason: Already tracking this request\n{request}");
                return false;
            }

            // Begin tracking
            request.TimeoutMs = Settings.RequestTimeoutMs;
            _requests[request.RequestId] = request;
            request.OnComplete += CompleteRequestTracking;
            _log.Info($"Track Request\n{request}");

            // Invoke callback for subscribed topics
            var topicId = request.TopicId;
            if (GetTopicSubscriptionState(topicId) != PubSubSubscriptionState.NotSubscribed)
            {
                OnTopicRequestTracked?.Invoke(topicId, request);
            }

            // Success
            return true;
        }

        /// <summary>
        /// Callback when request has completed tracking
        /// </summary>
        protected virtual void CompleteRequestTracking(IWitWebSocketRequest request)
        {
            // Stop tracking the request
            UntrackRequest(request);

            // Update subscription state if
            if (request is WitWebSocketSubscriptionRequest subscriptionRequest)
            {
                FinalizeSubscription(subscriptionRequest);
            }
        }

        /// <summary>
        /// Safely removes a request from the current request list
        /// </summary>
        public bool UntrackRequest(IWitWebSocketRequest request)
        {
            if (request == null)
            {
                return false;
            }
            return UntrackRequest(request.RequestId);
        }

        /// <summary>
        /// Safely removes a request from the current request list by request id
        /// </summary>
        public bool UntrackRequest(string requestId)
        {
            // Ensure already tracked
            if (string.IsNullOrEmpty(requestId) || !_requests.ContainsKey(requestId))
            {
                VLog.E(GetType().Name, $"Untrack Request - Failed\nReason: Not tracking this request\nRequest Id: {requestId}");
                return false;
            }

            // Remove request from tracking
            var request = _requests[requestId];
            request.OnComplete -= CompleteRequestTracking;
            _requests.Remove(requestId);
            _untrackedRequests.Add(requestId);
            if (!request.IsComplete)
            {
                request.Cancel();
            }
            _log.Info($"Untrack Request\n{request}");
            return true;
        }

        /// <summary>
        /// Attempts to generate a request to handle a specific json response
        /// </summary>
        /// <param name="requestId">The request id that should be handling the response.</param>
        /// <param name="jsonData">The json response that is currently unhandled.</param>
        private IWitWebSocketRequest GenerateRequest(string requestId, WitResponseNode jsonData)
        {
            // Ignore no longer tracked requests
            if (_untrackedRequests.Contains(requestId))
            {
                _log.Info($"Generate Request - Ignored\nReason: Request has been cancelled\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Get topic id if possible
            var topicId = jsonData[WitConstants.WIT_SOCKET_PUBSUB_TOPIC_KEY].Value;
            if (string.IsNullOrEmpty(topicId))
            {
                VLog.W(GetType().Name, $"Generate Request - Failed\nReason: No topic id provided in response\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Check if topic is subscribed
            var subState = GetTopicSubscriptionState(topicId);
            if (subState != PubSubSubscriptionState.Subscribed
                && subState != PubSubSubscriptionState.Subscribing)
            {
                VLog.W(GetType().Name, $"Generate Request - Failed\nReason: Topic id is not currently subscribed to\nTopic Id: {topicId}\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Generate message request if topic is found
            _log.Info($"Generate Request - Success\nTopic Id: {topicId}\nRequest Id: {requestId}");
            var request = new WitWebSocketMessageRequest(jsonData, requestId);
            request.TopicId = topicId;
            TrackRequest(request);
            return request;
        }
        #endregion REQUESTS

        #region SUBSCRIPTION
        /// <summary>
        /// A private struct used to track pubsub topic subscription and reference count
        /// </summary>
        private class PubSubSubscription
        {
            /// <summary>
            /// The current subscription state
            /// </summary>
            public PubSubSubscriptionState state;
            /// <summary>
            /// The current reference count
            /// </summary>
            public int referenceCount;
        }
        /// <summary>
        /// Dictionary with keys for topic id and values containing subscription state and reference count
        /// </summary>
        private Dictionary<string, PubSubSubscription> _subscriptions = new Dictionary<string, PubSubSubscription>();

        /// <summary>
        /// Callback when subscription state changes for a specific topic id
        /// </summary>
        public event PubSubTopicSubscriptionDelegate OnTopicSubscriptionStateChange;

        /// <summary>
        /// Callback when a tracked topic generates a request
        /// </summary>
        public event Action<string, IWitWebSocketRequest> OnTopicRequestTracked;

        /// <summary>
        /// Obtains the current subscription state for a specific topic
        /// </summary>
        public PubSubSubscriptionState GetTopicSubscriptionState(string topicId)
        {
            if (!string.IsNullOrEmpty(topicId)
                && _subscriptions.TryGetValue(topicId, out var subscription))
            {
                return subscription.state;
            }
            return PubSubSubscriptionState.NotSubscribed;
        }

        /// <summary>
        /// Method to subscribe to a specific topic id
        /// </summary>
        public void Subscribe(string topicId) => Subscribe(topicId, false);

        /// <summary>
        /// Method to subscribe to a specific topic id with a parameter to ignore
        /// ref count for local subscribing following reconnect/error.
        /// </summary>
        private void Subscribe(string topicId, bool ignoreRefCount)
        {
            // Ignore if null
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }
            // Add to subscription list if not added
            if (!_subscriptions.TryGetValue(topicId, out var subscription))
            {
                subscription = new PubSubSubscription();
            }
            // Increment reference count if not ignored
            if (!ignoreRefCount)
            {
                subscription.referenceCount++;
            }
            // If not connecting or connected, set as subscribe error and retry when connected
            if (ConnectionState != WitWebSocketConnectionState.Connected
                && ConnectionState != WitWebSocketConnectionState.Connecting)
            {
                SetTopicSubscriptionState(subscription, topicId, PubSubSubscriptionState.SubscribeError, "Not connected.  Will retry once connected.");
                return;
            }
            // Ignore if already subscribing or subscribed
            if (subscription.state == PubSubSubscriptionState.Subscribing
                || subscription.state == PubSubSubscriptionState.Subscribed)
            {
                return;
            }

            // Subscribing
            SetTopicSubscriptionState(subscription, topicId, PubSubSubscriptionState.Subscribing);

            // Generate and send subscribe request
            var subscribeRequest = new WitWebSocketSubscriptionRequest(topicId, WitWebSocketSubscriptionType.Subscribe);
            SendRequest(subscribeRequest);
        }

        /// <summary>
        /// Method to unsubscribe to a specific topic id
        /// </summary>
        public void Unsubscribe(string topicId) => Unsubscribe(topicId, false);

        /// <summary>
        /// Method to subscribe to a specific topic id with a parameter to ignore
        /// ref count for local unsubscribing following disconnect/error.
        /// </summary>
        public void Unsubscribe(string topicId, bool ignoreRefCount)
        {
            // Ignore if null
            if (string.IsNullOrEmpty(topicId))
            {
                return;
            }
            // Ignore if not subscribed
            if (!_subscriptions.TryGetValue(topicId, out var subscription))
            {
                return;
            }
            // Decrement reference count if not ignored
            if (!ignoreRefCount)
            {
                subscription.referenceCount = Mathf.Max(0, subscription.referenceCount - 1);
            }
            // Ignore if still referenced
            if (subscription.referenceCount > 0)
            {
                return;
            }
            // If not connected, unsubscribe immediately
            if (ConnectionState != WitWebSocketConnectionState.Connected)
            {
                SetTopicSubscriptionState(subscription, topicId, PubSubSubscriptionState.Unsubscribing);
                SetTopicSubscriptionState(subscription, topicId, PubSubSubscriptionState.NotSubscribed);
                return;
            }
            // Ignore if already unsubscribing
            if (subscription.state == PubSubSubscriptionState.Unsubscribing
                || subscription.state == PubSubSubscriptionState.NotSubscribed)
            {
                return;
            }

            // Begin unsubscribing
            SetTopicSubscriptionState(subscription, topicId, PubSubSubscriptionState.Unsubscribing);

            // Get and send unsubscribe request
            var unsubscribeRequest = new WitWebSocketSubscriptionRequest(topicId, WitWebSocketSubscriptionType.Unsubscribe);
            SendRequest(unsubscribeRequest);
        }

        /// <summary>
        /// Called once subscribe or unsubscribe completes.
        /// Sets the resultant subscription state & retries if applicable.
        /// </summary>
        private void FinalizeSubscription(WitWebSocketSubscriptionRequest request)
        {
            // Ignore not subscribed topic ids
            var topicId = request.TopicId;
            if (!_subscriptions.TryGetValue(topicId, out var subscription))
            {
                return;
            }

            // Check for error
            bool subscribing = request.SubscriptionType == WitWebSocketSubscriptionType.Subscribe;
            if (!string.IsNullOrEmpty(request.Error))
            {
                // Set subscribe or unsubscribe error
                var errorType = subscribing
                    ? PubSubSubscriptionState.SubscribeError
                    : PubSubSubscriptionState.UnsubscribeError;
                SetTopicSubscriptionState(subscription, topicId, errorType, request.Error);

                // Retry
                if (subscribing)
                {
                    Subscribe(topicId, true);
                }
                else
                {
                    Unsubscribe(topicId, true);
                }
                return;
            }

            // Set subscribed or not subscribed
            var successType = subscribing
                ? PubSubSubscriptionState.Subscribed
                : PubSubSubscriptionState.NotSubscribed;
            SetTopicSubscriptionState(subscription, topicId, successType);
        }

        /// <summary>
        /// Sets the current subscription state using the subscription asset and the topic id
        /// </summary>
        private void SetTopicSubscriptionState(PubSubSubscription subscription, string topicId, PubSubSubscriptionState state, string error = null)
        {
            // Ignore if same state
            if (subscription.state == state)
            {
                return;
            }

            // Apply state
            subscription.state = state;

            // Remove reference
            if (state == PubSubSubscriptionState.NotSubscribed)
            {
                _subscriptions.Remove(topicId);
            }
            // Set reference
            else
            {
                _subscriptions[topicId] = subscription;
            }

            // Log
            if (!string.IsNullOrEmpty(error))
            {
                VLog.W(GetType().Name, $"{state}\nError: {error}\nTopic Id: {topicId}");
            }
            else
            {
                _log.Info($"{state}\nTopic Id: {topicId}");
            }

            // Call delegate
            OnTopicSubscriptionStateChange?.Invoke(topicId, state);
        }
        #endregion SUBSCRIPTION
    }
}
