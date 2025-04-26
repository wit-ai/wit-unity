// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Meta.Voice.Net.Encoding.Wit;
using Meta.Voice.Net.WebSockets;
using Meta.WitAi.Json;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// Mock web socket handler
    /// </summary>
    public class MockWebSocket : IWebSocket
    {
        // State to be viewed
        public WitWebSocketConnectionState State { get; set; }

        // Constructor with option to auto open
        public MockWebSocket(bool autoOpen = false)
        {
            if (autoOpen)
            {
                HandleConnect = SimulateOpen;
            }
        }

        // Simulate opening of web socket connection
        public event Action OnOpen;
        public void SimulateOpen()
        {
            State = WitWebSocketConnectionState.Connected;
            OnOpen?.Invoke();
        }

        // Simulate service message callback
        public event Action<byte[], int, int> OnMessage;
        public void SimulateResponse(byte[] bytes, int offset, int length) => OnMessage?.Invoke(bytes, offset, length);
        public void SimulateResponse(WitResponseNode jsonData, byte[] binaryData = null)
        {
            var bytes = WitChunkConverter.Encode(jsonData, binaryData);
            SimulateResponse(bytes, 0, bytes.Length);
        }

        // Simulate error
        public event Action<string> OnError;
        public void SimulateError(string error)
        {
            OnError?.Invoke(error);
            SimulateClose(WebSocketCloseCode.Abnormal);
        }

        // Simulate close
        public event Action<WebSocketCloseCode> OnClose;
        private void SimulateClose(WebSocketCloseCode closeCode)
        {
            State = WitWebSocketConnectionState.Disconnected;
            OnClose?.Invoke(closeCode);
        }

        // Handle connection
        public event Action HandleConnect;
        public async Task Connect()
        {
            await Task.Delay(1);
            State = WitWebSocketConnectionState.Connecting;
            HandleConnect?.Invoke();
        }

        // Handle sending data
        public event Action<byte[]> HandleSend;
        public async Task Send(byte[] data)
        {
            await Task.Delay(1);
            HandleSend?.Invoke(data);
        }

        public event Action HandleClose;
        public Task Close()
        {
            State = WitWebSocketConnectionState.Disconnecting;
            HandleClose?.Invoke();
            SimulateClose(WebSocketCloseCode.Normal);
            return Task.CompletedTask;
        }
    }
}
