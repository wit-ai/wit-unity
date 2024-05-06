/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Voice.Net.WebSockets.Requests
{
    /// <summary>
    /// Performs a request that transmits raw audio samples to a web service,
    /// downloads and encodes responses.
    /// </summary>
    public class WitWebSocketSpeechRequest : WitWebSocketMessageRequest
    {
        /// <summary>
        /// Callback when the server is ready to upload audio
        /// </summary>
        public bool IsReadyForInput { get; private set; }

        /// <summary>
        /// Callback action when ready for input is toggled on
        /// </summary>
        public event Action OnReadyForInput;

        /// <summary>
        /// Constructor for request that posts binary audio data
        /// </summary>
        /// <param name="endpoint">The endpoint to be used for the request</param>
        /// <param name="parameters">All additional data required for the request</param>
        /// <param name="requestId">A unique id to be used for the request</param>
        public WitWebSocketSpeechRequest(string endpoint, Dictionary<string, string> parameters, string requestId = null) : base(endpoint, parameters, requestId)
        {
        }


        /// <summary>
        /// Called multiple times as partial responses are received. Determines if ready for input and if so,
        /// performs the appropriate callback following the application of data.
        /// </summary>
        /// <param name="jsonString">Raw json string.</param>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which should be null or empty.</param>
        public override void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData)
        {
            bool callback = false;
            if (!IsComplete && !IsReadyForInput)
            {
                var type = jsonData[WitConstants.RESPONSE_TYPE_KEY].Value;
                IsReadyForInput = string.Equals(type, WitConstants.RESPONSE_TYPE_READY_FOR_AUDIO);
                callback = IsReadyForInput;
            }
            base.HandleDownload(jsonString, jsonData, binaryData);
            if (callback)
            {
                OnReadyForInput?.Invoke();
            }
        }

        /// <summary>
        /// Public method for sending binary audio data
        /// </summary>
        /// <param name="buffer">The buffer used for uploading data</param>
        /// <param name="offset">The starting offset of the buffer selection</param>
        /// <param name="length">The length of the buffer to be used</param>
        public void SendAudioData(byte[] buffer, int offset, int length)
        {
            // Ignore without upload handler
            if (_uploader == null || !IsReadyForInput)
            {
                return;
            }
            // Obtain safe chunk
            var chunk = buffer;
            if (offset != 0 || length != buffer.Length)
            {
                chunk = new byte[length];
                Array.Copy(buffer, offset, chunk, 0, length);
            }
            // Perform upload
            _uploader.Invoke(RequestId, GetAdditionalPostJson(), chunk);
        }

        /// <summary>
        /// Stop sending audio data
        /// </summary>
        public virtual void CloseAudioStream()
        {
            // Ignore without upload handler
            if (_uploader == null || !IsReadyForInput)
            {
                return;
            }
            // Send final post data
            IsReadyForInput = false;
            var finalPostData = GetAdditionalPostJson().AsObject;
            var data = new WitResponseClass();
            data[WitConstants.WIT_SOCKET_END_KEY] = new WitResponseClass();
            finalPostData[WitConstants.WIT_SOCKET_DATA_KEY] = data;
            _uploader.Invoke(RequestId, finalPostData, null);
        }

        /// <summary>
        /// Obtains additional json chunk data if required
        /// </summary>
        private WitResponseNode GetAdditionalPostJson() => new WitResponseClass();
    }
}
