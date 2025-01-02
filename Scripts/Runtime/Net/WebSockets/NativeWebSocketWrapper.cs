/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.Net.NativeWebSocket;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An wrapper for NativeWebSocket to ensure it implements
    /// </summary>
    public class NativeWebSocketWrapper : IWebSocket
    {
        /// <summary>
        /// The websocket being wrapped
        /// </summary>
        private readonly WebSocket _webSocket;

        /// <summary>
        /// Constructor that takes in url and headers and adds all callbacks.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        public NativeWebSocketWrapper(string url, Dictionary<string, string> headers)
        {
            _webSocket = new WebSocket(url, headers);
            _webSocket.OnOpen += RaiseOpen;
            _webSocket.OnMessage += RaiseMessage;
            _webSocket.OnError += RaiseError;
            _webSocket.OnClose += RaiseClose;
        }

        /// <summary>
        /// Destructor that removes callbacks and clears reference
        /// </summary>
        ~NativeWebSocketWrapper()
        {
            _webSocket.OnOpen -= RaiseOpen;
            _webSocket.OnMessage -= RaiseMessage;
            _webSocket.OnError -= RaiseError;
            _webSocket.OnClose -= RaiseClose;
        }

        /// <summary>
        /// The current state of the web socket client
        /// </summary>
        public WitWebSocketConnectionState State
        {
            get
            {
                switch (_webSocket.State)
                {
                    case WebSocketState.Connecting:
                        return WitWebSocketConnectionState.Connecting;
                    case WebSocketState.Open:
                        return WitWebSocketConnectionState.Connected;
                    case WebSocketState.Closing:
                        return WitWebSocketConnectionState.Disconnecting;
                    case WebSocketState.Closed:
                        return WitWebSocketConnectionState.Disconnected;
                }
                return WitWebSocketConnectionState.Disconnected;
            }
        }

        /// <summary>
        /// Method to begin web socket communication
        /// </summary>
        public async Task Connect() => await _webSocket.Connect();

        /// <summary>
        /// Callback when web socket is opened
        /// </summary>
        public event Action OnOpen;
        private void RaiseOpen() => OnOpen?.Invoke();

        /// <summary>
        /// Method to send byte data to a web socket
        /// </summary>
        public async Task Send(byte[] data) => await _webSocket.Send(data);

        /// <summary>
        /// Callback when message is received from web socket server
        /// </summary>
        public event Action<byte[], int, int> OnMessage;
        private void RaiseMessage(byte[] data, int offset, int length) => OnMessage?.Invoke(data, offset, length);

        /// <summary>
        /// Callback when an error is received from web socket server
        /// </summary>
        public event Action<string> OnError;
        private void RaiseError(string error) => OnError?.Invoke(error);

        /// <summary>
        /// Method to close web socket communication with normal close code
        /// </summary>
        public async Task Close() => await _webSocket.Close();

        /// <summary>
        /// Callback when message is received from web socket server
        /// </summary>
        public event Action<WebSocketCloseCode> OnClose;
        private void RaiseClose(Meta.Net.NativeWebSocket.WebSocketCloseCode closeCode) => OnClose?.Invoke((WebSocketCloseCode)closeCode);
    }
}
