/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.Net.NativeWebSocket;
using Meta.Voice.Net.Encoding.Wit;
using UnityEngine;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An class for performing multi-threaded web socket communication to a Wit endpoint
    /// WitWebSocketClient is a wrapper for web socket communication that facilitates the safe upload and
    /// download of data between the Voice SDK and Wit.ai.  The class handles connection, transmission &amp; response handling
    /// for all socket communication.  Three background threads are spawned by each WitWebSocketClient in order to handle
    /// encoding/uploading, downloading/decoding and response handling respectively.  WitWebSocketClients contain a list of
    /// IWitWebSocketRequests which are used to facilitate upload and download of data.  When sending a WitChunk the IWitWebSocketRequest’s
    /// request id will also be sent within the json data.  When receiving a WitChunk, request id will be checked within the json data and
    /// the appropriate IWitWebSocketRequest will handle the response.  If no matching request is found, the WitChunk’s topic id will be
    /// used to find the appropriate IWitWebSocketSubscriber which then will generate a request to handle the response.
    /// </summary>
    public class WitWebSocketClient
    {
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
        private WebSocket _socket;

        // Script used for decoding server responses
        private WitChunkConverter _decoder = new WitChunkConverter();

        // Request providers grouped by topic
        private Dictionary<string, IWitWebSocketRequestProvider> _requestProviders = new Dictionary<string, IWitWebSocketRequestProvider>();

        // Coroutine utility dispatcher
        private CoroutineUtility.CoroutinePerformer _dispatcher;

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
            if (ConnectionState != WitWebSocketConnectionState.Disconnecting)
            {
                VLog.I(GetType().Name, ConnectionState);
            }
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

                // Generate socket wrapper & assign message callback method
                _socket = new WebSocket(Settings.ServerUrl, headers);
                _socket.OnOpen += HandleSocketConnected;
                _socket.OnMessage += HandleSocketResponse;
                _socket.OnError += HandleSocketError;
                _socket.OnClose += HandleSocketDisconnect;

                // Connect and wait until connection completes
                await _socket.Connect();
            }
            // Timeout handling
            catch (OperationCanceledException)
            {
                HandleSetupFailed("Connection timed out");
            }
            // Additional exception handling
            catch (Exception e)
            {
                HandleSetupFailed($"Connection connect error caught\n{e}");
            }
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
            if (_socket.State != WebSocketState.Open)
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

            // Begin dispatching responses
            _dispatcher = CoroutineUtility.StartCoroutine(DispatchResponses());

            // Make authentication request & return any encountered error
            var authRequest = new Requests.WitWebSocketAuthRequest(clientAccessToken);
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
        /// Breaks down all
        /// </summary>
        private async Task BreakdownAsync()
        {
            // No longer authenticated
            IsAuthenticated = false;

            // Destroy dispatcher
            if (_dispatcher != null)
            {
                _dispatcher.DestroySafely();
                _dispatcher = null;
            }

            // Unload all requests, uploads & downloads
            if (!IsReferenced)
            {
                // Untrack all running requests
                var requestIds = new Dictionary<string, IWitWebSocketRequest>(_requests).Keys.ToArray();
                foreach (var requestId in requestIds)
                {
                    UntrackRequest(requestId);
                }
                // Clear untracked list
                _untrackedRequests.Clear();
            }

            // Close socket connection
            if (_socket != null)
            {
                _socket.OnOpen -= HandleSocketConnected;
                _socket.OnMessage -= HandleSocketResponse;
                _socket.OnError -= HandleSocketError;
                _socket.OnClose -= HandleSocketDisconnect;
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.Close();
                }
                _socket = null;
            }

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
            // Wait & reconnect
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
            VLog.I(GetType().Name, $"Reconnect Attempt {FailedConnectionAttempts}");
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

            // Begin upload & send method for chunks when ready to be sent
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

            // If not authorization chunk, wait
            bool isAuth = _requests.TryGetValue(requestId, out var request) && request is Requests.WitWebSocketAuthRequest;
            if (!isAuth)
            {
                // Wait while connecting or reconnecting
                await TaskUtility.WaitWhile(() => ConnectionState == WitWebSocketConnectionState.Connecting || IsReconnecting);
                if (ConnectionState != WitWebSocketConnectionState.Connected && !IsReconnecting)
                {
                    return;
                }
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
        /// Dispatches responses every frame while connected
        /// </summary>
        private IEnumerator DispatchResponses()
        {
            // Dispatch queue until no longer open
            while (_socket != null && _socket.State == WebSocketState.Open)
            {
                yield return new WaitForEndOfFrame();
                _socket.DispatchMessageQueue();
            }
            // Destroy & remove reference
            if (_dispatcher != null)
            {
                _dispatcher.DestroySafely();
                _dispatcher = null;
            }
        }

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
                request.HandleDownload(chunk.jsonData, chunk.binaryData);
            }
            // Catch exceptions or else they will be ignored
            catch (Exception e)
            {
                VLog.E(GetType().Name, $"Request HandleDownload method exception caught\n{request}\n\n{e}\n");
                UntrackRequest(request);
                return;
            }

            // Request is complete, remove from tracking list
            if (request.IsComplete)
            {
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
            _requests[request.RequestId] = request;
            VLog.I(GetType().Name, $"Track Request\n{request}");
            return true;
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
            _requests.Remove(requestId);
            _untrackedRequests.Add(requestId);
            if (!request.IsComplete)
            {
                request.Cancel();
            }
            VLog.I(GetType().Name, $"Untrack Request\n{request}");
            return true;
        }

        /// <summary>
        /// Adds a request provider for the specified topic
        /// </summary>
        /// <param name="topicId">Unique topic identifier</param>
        /// <param name="requestProvider">An interface that provides</param>
        public void AddRequestProvider(string topicId, IWitWebSocketRequestProvider requestProvider)
        {
            if (string.IsNullOrEmpty(topicId) || requestProvider == null)
            {
                return;
            }
            _requestProviders[topicId] = requestProvider;
        }

        /// <summary>
        /// Removes the request provider for the specified topic
        /// </summary>
        /// <param name="topicId">Unique topic identifier</param>
        public void RemoveRequestProvider(string topicId)
        {
            if (string.IsNullOrEmpty(topicId) || !_requestProviders.ContainsKey(topicId))
            {
                return;
            }
            _requestProviders.Remove(topicId);
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
                VLog.I(GetType().Name, $"Generate Request - Ignored\nReason: Request has been cancelled\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Get topic id if possible
            var topicId = jsonData[WitConstants.WIT_SOCKET_PUBSUB_TOPIC_KEY];
            if (string.IsNullOrEmpty(topicId))
            {
                VLog.W(GetType().Name, $"Generate Request - Failed\nReason: No topic id provided in response\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Get provider if possible
            if (!_requestProviders.TryGetValue(topicId, out var provider))
            {
                VLog.W(GetType().Name, $"Generate Request - Failed\nReason: No request provider for specified topic\nTopic Id: {topicId}\nRequest Id: {requestId}\nJson:\n{(jsonData?.ToString() ?? "Null")}");
                return null;
            }
            // Generate request for specified response data
            VLog.I(GetType().Name, $"Generate Request - Success\nTopic Id: {topicId}\nRequest Id: {requestId}");
            var request = provider.GenerateWebSocketRequest(requestId, jsonData);
            TrackRequest(request);
            return request;
        }
        #endregion REQUESTS
    }
}
