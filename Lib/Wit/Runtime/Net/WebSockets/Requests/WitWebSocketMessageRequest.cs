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
    /// Performs a request that transmits a single json chunk and receives one or more responses
    /// </summary>
    public class WitWebSocketMessageRequest : WitWebSocketJsonRequest
    {
        /// <summary>
        /// The endpoint being used
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Whether or not this request should consider an end of transcription as the end of the request
        /// </summary>
        public bool EndWithFullTranscription { get; }

        /// <summary>
        /// Callback one or more times when a chunk of json is decoded
        /// </summary>
        public event Action<WitResponseNode> OnDecodedResponse;

        /// <summary>
        /// Constructor for request that generates a WitResponseClass to be posted
        /// </summary>
        /// <param name="externalPostData">The data used exernally for the request</param>
        /// <param name="requestId">A unique id to be used for the request</param>
        /// <param name="clientUserId">A unique id that can be used to determine the interacting client user</param>
        /// <param name="endWithFullTranscription">Whether or not this request should consider an end of transcription as the end of the request</param>
        public WitWebSocketMessageRequest(WitResponseNode externalPostData,
            string requestId, string clientUserId, bool endWithFullTranscription = false)
            : base(externalPostData, requestId, clientUserId)
        {
            Endpoint = WitConstants.WIT_SOCKET_EXTERNAL_ENDPOINT_KEY;
            EndWithFullTranscription = endWithFullTranscription;
            SetResponseData(externalPostData);
            _ = WaitForTimeout();
        }

        /// <summary>
        /// Constructor for request that generates a WitResponseClass to be posted
        /// </summary>
        /// <param name="endpoint">The endpoint to be used for the request</param>
        /// <param name="parameters">All additional data required for the request</param>
        /// <param name="requestId">A unique id to be used for the request</param>
        /// <param name="clientUserId">A unique id that can be used to determine the interacting client user</param>
        /// <param name="endWithFullTranscription">Whether or not this request should consider an end of transcription as the end of the request</param>
        public WitWebSocketMessageRequest(string endpoint, Dictionary<string, string> parameters,
            string requestId = null, string clientUserId = null, bool endWithFullTranscription = false)
            : base(GetPostData(endpoint, parameters), requestId, clientUserId)
        {
            Endpoint = endpoint;
            EndWithFullTranscription = endWithFullTranscription;
        }

        /// <summary>
        /// Logs the currently used endpoint
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}\nEndpoint: {Endpoint}";
        }

        /// <summary>
        /// Generates a json node with specified endpoint & parameters
        /// </summary>
        /// <param name="endpoint">Endpoint to be used</param>
        /// <param name="parameters">Parameters for the endpoint</param>
        /// <returns>Json class for upload</returns>
        public static WitResponseClass GetPostData(string endpoint, Dictionary<string, string> parameters)
        {
            var rootNode = new WitResponseClass();
            var dataNode = new WitResponseClass();
            var parameterNode = new WitResponseClass();
            if (parameters != null)
            {
                foreach (var key in parameters.Keys)
                {
                    if (string.Equals(key, WitConstants.HEADER_TAG_ID))
                    {
                        continue;
                    }
                    parameterNode[key] = parameters[key];
                }
            }
            dataNode[endpoint] = parameterNode;
            rootNode[WitConstants.WIT_SOCKET_DATA_KEY] = dataNode;
            return rootNode;
        }

        /// <summary>
        /// Called multiple times as partial responses are received.
        /// </summary>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
        public override void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData)
        {
            // Ignore once complete
            if (IsComplete)
            {
                return;
            }

            // Call begin download methods
            if (!IsDownloading)
            {
                HandleDownloadBegin();
            }

            // Callback for raw response
            ReturnRawResponse(jsonString);
            // Set current response data
            SetResponseData(jsonData);

            // Skip for error
            if (!string.IsNullOrEmpty(Error))
            {
                HandleComplete();
                return;
            }

            // Check for end of stream
            if (IsEndOfStream(ResponseData))
            {
                HandleComplete();
            }
        }

        /// <summary>
        /// Returns true if 'is_final' is within the response
        /// </summary>
        protected virtual bool IsEndOfStream(WitResponseNode responseData)
        {
            // End with final response
            if (responseData?[WitConstants.KEY_RESPONSE_IS_FINAL].AsBool ?? false)
            {
                return true;
            }
            // If end with full transcription, do so now
            if (EndWithFullTranscription)
            {
                return responseData?[WitConstants.WIT_SOCKET_TRANSCRIBE_IS_FINAL].AsBool ?? false;
            }
            return false;
        }

        /// <summary>
        /// Perform callback following response data setting
        /// </summary>
        protected override void SetResponseData(WitResponseNode newResponseData)
        {
            base.SetResponseData(newResponseData);
            OnDecodedResponse?.Invoke(ResponseData);
        }
    }
}
