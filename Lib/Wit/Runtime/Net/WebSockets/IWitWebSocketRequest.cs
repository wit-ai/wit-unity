/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Json;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// Called one or more times to  thread when a chunk has returned
    /// </summary>
    /// <param name="requestId">Request id used for chunk lookup and prioritization.</param>
    /// <param name="jsonData">Decoded json data object.</param>
    /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
    public delegate void UploadChunkDelegate(string requestId, WitResponseNode jsonData, byte[] binaryData);

    /// <summary>
    /// An interface used to handle web socket upload
    /// and download communication.
    /// </summary>
    public interface IWitWebSocketRequest
    {
        /// <summary>
        /// The priority of data submission
        /// </summary>
        string RequestId { get; }

        /// <summary>
        /// The specific topic id that is being published to or received via subscription, if applicable.
        /// </summary>
        string TopicId { get; set; }

        /// <summary>
        /// The request timeout in milliseconds
        /// </summary>
        int TimeoutMs { get; set; }

        /// <summary>
        /// Whether currently uploading data
        /// </summary>
        bool IsUploading { get; }

        /// <summary>
        /// Called once from the main thread to begin the upload process.
        /// </summary>
        /// <param name="uploadChunk">The method to be called as each json and/or binary chunk is ready to be uploaded</param>
        void HandleUpload(UploadChunkDelegate uploadChunk);

        /// <summary>
        /// Whether currently downloading data
        /// </summary>
        bool IsDownloading { get; }

        /// <summary>
        /// Called one or more times from the background thread when a chunk has returned
        /// </summary>
        /// <param name="jsonString">Raw json string.</param>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
        void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData);

        /// <summary>
        /// Whether request is currently complete
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        ///  The response code if applicable
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Error that occured during upload and/or download
        /// </summary>
        string Error { get; }

        /// <summary>
        /// Method that can be used to perform a cancellation on a request
        /// </summary>
        void Cancel();

        /// <summary>
        /// Callback method when raw json response is received
        /// </summary>
        Action<string> OnRawResponse { get; set; }

        /// <summary>
        /// Callback method the request should perform on first download
        /// </summary>
        Action<IWitWebSocketRequest> OnFirstResponse { get; set; }

        /// <summary>
        /// Callback method the request should perform once completed
        /// </summary>
        Action<IWitWebSocketRequest> OnComplete { get; set; }
    }
}
