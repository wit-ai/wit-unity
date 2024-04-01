/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Net;
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
        /// Callback when request receives the first chunk of data from the server
        /// </summary>
        public Action<IWitWebSocketRequest> OnFirstResponse { get; set; }

        /// <summary>
        /// Callback when the request has completed due to a cancellation, error or success
        /// </summary>
        public Action<IWitWebSocketRequest> OnComplete { get; set; }

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
            uploadChunk?.Invoke(RequestId, PostData, null);
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
            Code = WitConstants.ERROR_CODE_ABORTED.ToString();
            Error = WitConstants.CANCEL_ERROR;
            HandleComplete();
        }

        /// <summary>
        /// Called one or more times from the background thread when a chunk has returned.  Stores the json data from the first response.
        /// </summary>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
        public virtual void HandleDownload(WitResponseNode jsonData, byte[] binaryData)
        {
            // Download first only
            if (IsDownloading || IsComplete)
            {
                return;
            }

            // Store downloaded json data
            SetResponseData(jsonData);

            // Download begin
            HandleDownloadBegin();
            // Download complete
            HandleComplete();
        }

        /// <summary>
        /// Apply response data
        /// </summary>
        /// <param name="newResponseData">New response data received</param>
        protected virtual void SetResponseData(WitResponseNode newResponseData)
        {
            ResponseData = newResponseData;
            Code = ResponseData[WitConstants.KEY_RESPONSE_CODE];
            Error = ResponseData[WitConstants.KEY_RESPONSE_ERROR];
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
            var error = string.IsNullOrEmpty(Error) ? "" : $"\nError: {Error}";
            return $"Type: {GetType().Name}\nId: {RequestId}{error}";
        }
    }
}
