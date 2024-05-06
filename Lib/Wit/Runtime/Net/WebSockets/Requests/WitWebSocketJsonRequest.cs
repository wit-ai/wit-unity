/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Threading.Tasks;
using Meta.WitAi;
using Meta.WitAi.Json;

namespace Meta.Voice.Net.WebSockets.Requests
{
    /// <summary>
    /// Web socket request that encodes a single json request
    /// </summary>
    public class WitWebSocketJsonRequest : IWitWebSocketRequest
    {
        /// <summary>
        /// Unique request id generated on init
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// The specific topic id that is being published to or received via subscription, if applicable.
        /// </summary>
        public string TopicId { get; set; }

        /// <summary>
        /// The timeout in milliseconds from the initial upload to the response from the server.
        /// If no response in time, the request will fail.
        /// </summary>
        public int TimeoutMs { get; set; } = WitConstants.DEFAULT_REQUEST_TIMEOUT;

        /// <summary>
        /// Whether or not uploading has begun
        /// </summary>
        public bool IsUploading { get; protected set; }

        /// <summary>
        /// Whether or not downloading has begun
        /// </summary>
        public bool IsDownloading { get; private set; }

        /// <summary>
        /// Whether or not complete
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// A response code if applicable
        /// </summary>
        public string Code { get; protected set; }

        /// <summary>
        /// An error if applicable
        /// </summary>
        public string Error { get; protected set; }

        /// <summary>
        /// Initial json data to be uploaded
        /// </summary>
        public WitResponseNode PostData { get; }

        /// <summary>
        /// First decoded json response received
        /// </summary>
        public WitResponseNode ResponseData { get; protected set; }

        /// <summary>
        /// Callback method when raw json response is received
        /// </summary>
        public Action<string> OnRawResponse { get; set; }

        /// <summary>
        /// Callback when request receives the first chunk of data from the server
        /// </summary>
        public Action<IWitWebSocketRequest> OnFirstResponse { get; set; }

        /// <summary>
        /// Callback when the request has completed due to a cancellation, error or success
        /// </summary>
        public Action<IWitWebSocketRequest> OnComplete { get; set; }

        /// <summary>
        /// The method used to upload chunks
        /// </summary>
        protected UploadChunkDelegate _uploader;

        /// <summary>
        /// Start of the timeout
        /// </summary>
        protected DateTime _timeoutStart;

        /// <summary>
        /// Constructor which accepts a WitResponseNode as post data and applies request id
        /// </summary>
        public WitWebSocketJsonRequest(WitResponseNode postData, string requestId = null)
        {
            PostData = postData;
            RequestId = string.IsNullOrEmpty(requestId) ? WitConstants.GetUniqueId() : requestId;
        }

        /// <summary>
        /// Called once from the main thread to begin the upload process. Sends a single post chunk.
        /// </summary>
        /// <param name="uploadChunk">The method to be called as each json and/or binary chunk is ready to be uploaded</param>
        public virtual void HandleUpload(UploadChunkDelegate uploadChunk)
        {
            if (IsUploading)
            {
                return;
            }
            IsUploading = true;
            _uploader = uploadChunk;

            // Append topic id if applicable
            if (!string.IsNullOrEmpty(TopicId) && PostData != null)
            {
                var publish = new WitResponseClass();
                publish[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_TRANSCRIPTION_KEY] = TopicId;
                publish[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_COMPOSER_KEY] = TopicId;
                PostData[WitConstants.WIT_SOCKET_PUBSUB_PUBLISH_KEY] = publish;
            }

            // Upload chunk
            _uploader?.Invoke(RequestId, PostData, null);

            // Generate task to handle timeout error
            BeginTimeout();
        }

        /// <summary>
        /// Generates a task to watch for timeout
        /// </summary>
        protected void BeginTimeout()
        {
            _timeoutStart = DateTime.UtcNow;
            _ = CheckForTimeout();
        }

        /// <summary>
        /// Timeout if needed
        /// </summary>
        private async Task CheckForTimeout()
        {
            // Wait while not complete and not timed out
            await TaskUtility.WaitWhile(() => !IsComplete && (DateTime.UtcNow - _timeoutStart).TotalMilliseconds < TimeoutMs);

            // Timed out
            if (!IsComplete)
            {
                SendAbort(WitConstants.ERROR_RESPONSE_TIMEOUT);
                Code = WitConstants.ERROR_CODE_TIMEOUT.ToString();
                Error = WitConstants.ERROR_RESPONSE_TIMEOUT;
                HandleComplete();
            }
        }

        /// <summary>
        /// Cancel current request
        /// </summary>
        public virtual void Cancel()
        {
            if (IsComplete)
            {
                return;
            }

            // Send abort method if possible
            SendAbort(WitConstants.CANCEL_ERROR);

            // Handle completion
            Code = WitConstants.ERROR_CODE_ABORTED.ToString();
            Error = WitConstants.CANCEL_ERROR;
            HandleComplete();
        }

        /// <summary>
        /// Method to perform an abort call
        /// </summary>
        protected void SendAbort(string reason)
        {
            // Ignore if not uploading
            if (!IsUploading || _uploader == null)
            {
                return;
            }

            // Upload abort data
            var abortData = new WitResponseClass();
            var data = new WitResponseClass();
            data[WitConstants.WIT_SOCKET_ABORT_KEY] = new WitResponseClass();
            data[WitConstants.WIT_SOCKET_ABORT_KEY]["reason"] = reason;
            abortData[WitConstants.WIT_SOCKET_DATA_KEY] = data;
            _uploader?.Invoke(RequestId, abortData, null);
        }

        /// <summary>
        /// Called one or more times from the background thread when a chunk has returned.  Stores the json data from the first response.
        /// </summary>
        /// <param name="jsonString">Raw json string.</param>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
        public virtual void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData)
        {
            // Download first only
            if (IsDownloading || IsComplete)
            {
                return;
            }

            // Download begin
            HandleDownloadBegin();

            // Callback for raw response
            ReturnRawResponse(jsonString);
            // Store downloaded json data
            SetResponseData(jsonData);

            // Download complete
            HandleComplete();
        }

        /// <summary>
        /// If raw response callback is implemented, calls on main thread
        /// </summary>
        protected virtual void ReturnRawResponse(string jsonString)
        {
            if (OnRawResponse == null)
            {
                return;
            }
            ThreadUtility.CallOnMainThread(() => OnRawResponse.Invoke(jsonString));
        }

        /// <summary>
        /// Apply response data
        /// </summary>
        /// <param name="newResponseData">New response data received</param>
        protected virtual void SetResponseData(WitResponseNode newResponseData)
        {
            _timeoutStart = DateTime.UtcNow;
            ResponseData = newResponseData;
            Code = ResponseData[WitConstants.KEY_RESPONSE_CODE];
            Error = ResponseData[WitConstants.KEY_RESPONSE_ERROR];
            var topicId = ResponseData[WitConstants.WIT_SOCKET_PUBSUB_TOPIC_KEY]?.Value;
            if (!string.IsNullOrEmpty(topicId))
            {
                TopicId = topicId;
            }
        }

        /// <summary>
        /// Method called for first response handling to mark download begin and
        /// perform first response callback on main thread.
        /// </summary>
        protected virtual void HandleDownloadBegin()
        {
            if (IsDownloading)
            {
                return;
            }
            IsDownloading = true;
            ThreadUtility.CallOnMainThread(RaiseFirstResponse);
        }

        /// <summary>
        /// Method called on main thread when first response callback should occur
        /// </summary>
        protected virtual void RaiseFirstResponse()
        {
            OnFirstResponse?.Invoke(this);
        }

        /// <summary>
        /// Resets the state of the request and marks request as complete.
        /// </summary>
        protected virtual void HandleComplete()
        {
            if (IsComplete)
            {
                return;
            }
            IsUploading = false;
            _uploader = null;
            IsDownloading = false;
            IsComplete = true;
            ThreadUtility.CallOnMainThread(RaiseComplete);
        }

        /// <summary>
        /// Method called on main thread when first response callback should occur
        /// </summary>
        protected virtual void RaiseComplete()
        {
            OnComplete?.Invoke(this);
        }

        /// <summary>
        /// Override ToString for more request specific info
        /// </summary>
        public override string ToString()
        {
            var result = $"Type: {GetType().Name}";
            result += $"\nId: {RequestId}";
            if (!string.IsNullOrEmpty(TopicId))
            {
                result += $"\nTopic Id: {TopicId}";
            }
            if (!string.IsNullOrEmpty(Error))
            {
                result += $"\nError: {Error}";
            }
            return result;
        }
    }
}
