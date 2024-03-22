/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.Net.NativeWebSocket;
using Meta.Voice.Net.Encoding.Wit;

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
        /// The utc time of the last response from the server
        /// </summary>
        public DateTime LastResponseTime { get; private set; }

        /// <summary>
        /// Callback on connection state change.
        /// </summary>
        public event Action<WitWebSocketConnectionState> OnConnectionStateChanged;

        /// <summary>
        /// Callback when wit chunk has downloaded successfully
        /// </summary>
        public event Action<WitChunk> OnDownloadedChunk;

        // Stores last request id to handle binary data without headers
        private string _lastRequestId;
        // Total number of chunks currently being uploaded
        private int _uploadCount;
        // Total number of responses currently being decoded
        private int _downloadCount;

        // The web socket client handling all communication
        private WebSocket _socket;

        // Script used for decoding server responses
        private WitChunkConverter _decoder = new WitChunkConverter();

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

            VLog.I(GetType().Name, $"State: {newConnectionState}");
            ConnectionState = newConnectionState;
            OnConnectionStateChanged?.Invoke(ConnectionState);
        }

        #region CONNECT
        /// <summary>
        /// Connects to specified socket
        /// </summary>
        public async Task<string> Connect()
        {
            // Ignore if already active
            if (ConnectionState == WitWebSocketConnectionState.Connecting
                || ConnectionState == WitWebSocketConnectionState.Connected)
            {
                return $"Cannot connect while already {ConnectionState}";
            }

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
                VLog.I(GetType().Name, $"Connection Start\nId: {requestId}");
                await _socket.Connect();
            }
            // Timeout handling
            catch (OperationCanceledException)
            {
                string error = "Connection timed out";
                VLog.E(GetType().Name, error);
                SetConnectionState(WitWebSocketConnectionState.Disconnected);
                return error;
            }
            // Additional exception handling
            catch (Exception e)
            {
                string error = $"Connection connect error caught\n{e}";
                VLog.E(GetType().Name, error);
                SetConnectionState(WitWebSocketConnectionState.Disconnected);
                return error;
            }
            // Connection completed
            return string.Empty;
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

            // Perform final setup
            _ = Setup();
        }

        /// <summary>
        /// Option to perform additional setup for the socket, including authentication
        /// </summary>
        private async Task Setup()
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
            bool complete = false;
            string error = null;
            Action<WitChunk> onDownload = (chunk) =>
            {
                error = chunk.jsonData[WitConstants.ENDPOINT_ERROR_PARAM];
                if (string.IsNullOrEmpty(error))
                {
                    var authText = chunk.jsonData[WitConstants.WIT_SOCKET_AUTH_RESPONSE_KEY];
                    if (!string.Equals(authText, WitConstants.WIT_SOCKET_AUTH_RESPONSE_VAL))
                    {
                        error = WitConstants.WIT_SOCKET_AUTH_RESPONSE_ERROR;
                    }
                }
                complete = true;
            };
            OnDownloadedChunk += onDownload;
            WitResponseClass authNode = new WitResponseClass();
            authNode[WitConstants.WIT_SOCKET_AUTH_TOKEN] = clientAccessToken;
            SendChunk(WitConstants.GetUniqueId(), authNode, null);
            await TaskUtility.WaitWhile(() => !complete); // TODO: Remove in T181285302
            OnDownloadedChunk -= onDownload;

            // Auth error
            IsAuthenticated = string.IsNullOrEmpty(error);
            if (!IsAuthenticated)
            {
                HandleSetupFailed(error);
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
                _ = Disconnect();
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
                _ = Disconnect();
            }
        }

        /// <summary>
        /// Disconnects socket after checking state
        /// </summary>
        public async Task<string> Disconnect()
        {
            // Ignore if already disconnecting/disconnected
            if (ConnectionState == WitWebSocketConnectionState.Disconnecting
                || ConnectionState == WitWebSocketConnectionState.Disconnected)
            {
                return $"Cannot disconnect while already {ConnectionState}";
            }

            // Perform breakdown if possible
            SetConnectionState(WitWebSocketConnectionState.Disconnecting);
            await Breakdown();

            // Fail if changed during breakdown
            if (ConnectionState != WitWebSocketConnectionState.Disconnecting)
            {
                return $"State changed to {ConnectionState} during breakdown.";
            }

            // Disconnected successfully
            SetConnectionState(WitWebSocketConnectionState.Disconnected);
            return null;
        }

        /// <summary>
        /// Disconnects without checking state
        /// </summary>
        private async Task Breakdown()
        {
            // No longer authenticated
            IsAuthenticated = false;

            // Destroy dispatcher
            if (_dispatcher != null)
            {
                _dispatcher.DestroySafely();
                _dispatcher = null;
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
                _ = Disconnect();
            }
        }
        #endif
        #endregion DISCONNECT

        #region UPLOAD
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
                yield return new UnityEngine.WaitForEndOfFrame();
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
            OnDownloadedChunk?.Invoke(chunk);
        }
        #endregion DOWNLOAD
    }
}
