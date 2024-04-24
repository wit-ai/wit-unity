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
    public class WitWebSocketTranscribeRequest : WitWebSocketSpeechRequest
    {
        /// <summary>
        /// Whether or not this transcribe request will occur multiple times
        /// </summary>
        public bool MultipleSegments { get; }

        /// <summary>
        /// Constructor for transcribe request
        /// </summary>
        public WitWebSocketTranscribeRequest(string endpoint, Dictionary<string, string> parameters, bool multipleSegments, string requestId = null) : base(endpoint, parameters, requestId)
        {
            // Store multiple segments value
            MultipleSegments = multipleSegments;
            // Update post data
            if (MultipleSegments)
            {
                PostData[WitConstants.WIT_SOCKET_DATA_KEY][endpoint][WitConstants.WIT_SOCKET_TRANSCRIBE_MULTIPLE_KEY] =
                    true.ToString();
            }
        }

        /// <summary>
        /// Returns true if 'is_final' is within the response
        /// </summary>
        protected override bool IsEndOfStream(WitResponseNode responseData)
        {
            if (!MultipleSegments)
            {
                return base.IsEndOfStream(responseData);
            }
            return false;
        }

        /// <summary>
        /// If multiple segments, close when audio stops uploading
        /// </summary>
        public override void CloseAudioStream()
        {
            // Perform close if possible
            base.CloseAudioStream();
            // Ignore if not downloading or complete
            if (!IsDownloading || IsComplete || !MultipleSegments)
            {
                return;
            }
            // Close now
            HandleComplete();
        }
    }
}
