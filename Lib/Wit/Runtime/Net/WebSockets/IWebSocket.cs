/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Threading.Tasks;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An interface for a WebSocket wrapper class
    /// </summary>
    public interface IWebSocket
    {
        /// <summary>
        /// The current state of the web socket client
        /// </summary>
        WitWebSocketConnectionState State { get; }

        /// <summary>
        /// Callback when web socket is opened
        /// </summary>
        event Action OnOpen;

        /// <summary>
        /// Callback when message is received from web socket server
        /// </summary>
        event Action<byte[]> OnMessage;

        /// <summary>
        /// Callback when an error is received from web socket server
        /// </summary>
        event Action<string> OnError;

        /// <summary>
        /// Callback when message is received from web socket server
        /// </summary>
        event Action<WebSocketCloseCode> OnClose;

        /// <summary>
        /// Method to begin web socket communication
        /// </summary>
        Task Connect();

        /// <summary>
        /// Method to send byte data to a web socket
        /// </summary>
        Task Send(byte[] data);

        /// <summary>
        /// Method to close web socket communication with normal close code
        /// </summary>
        Task Close();
    }
}
