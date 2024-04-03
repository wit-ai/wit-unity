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

        // Method used to upload chunks to a web socket client
        private UploadChunkDelegate _performUpload;

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
        /// Called once from the main thread to begin the upload process. Sends a single post chunk.
        /// </summary>
        /// <param name="uploadChunk">The method to be called as each json and/or binary chunk is ready to be uploaded</param>
        public override void HandleUpload(UploadChunkDelegate uploadChunk)
        {
            // Ignore if uploading
            if (IsUploading)
            {
                return;
            }
            // Perform post with provided post data
            base.HandleUpload(uploadChunk);
            // Begin upload process
            _performUpload = uploadChunk;
        }


        /// <summary>
        /// Called multiple times as partial responses are received. Determines if ready for input and if so,
        /// performs the appropriate callback following the application of data.
        /// </summary>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which should be null or empty.</param>
        public override void HandleDownload(WitResponseNode jsonData, byte[] binaryData)
        {
            bool callback = false;
            if (!IsComplete && !IsReadyForInput)
            {
                var type = jsonData[WitConstants.RESPONSE_TYPE_KEY];
                IsReadyForInput = string.Equals(type, WitConstants.RESPONSE_TYPE_READY_FOR_AUDIO);
                callback = IsReadyForInput;
            }
            base.HandleDownload(jsonData, binaryData);
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
            if (_performUpload == null || !IsReadyForInput)
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
            _performUpload.Invoke(RequestId, GetAdditionalPostJson(), chunk);
        }

        /// <summary>
        /// Stop sending audio data
        /// </summary>
        public void CloseAudioStream()
        {
            // Ignore without upload handler
            if (_performUpload == null)
            {
                return;
            }
            // Send final post data
            var finalPostData = GetAdditionalPostJson().AsObject;
            finalPostData[WitConstants.WIT_SOCKET_END_KEY] = new WitResponseData(true);
            _performUpload.Invoke(RequestId, finalPostData, null);
            _performUpload = null;
        }

        /// <summary>
        /// Obtains additional json chunk data if required
        /// </summary>
        private WitResponseNode GetAdditionalPostJson() => new WitResponseClass();

        /// <summary>
        /// Remove upload handler on completion
        /// </summary>
        protected override void HandleComplete()
        {
            _performUpload = null;
            base.HandleComplete();
        }
    }
}
