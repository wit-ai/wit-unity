/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Net;
using Meta.Voice;
using Meta.WitAi.Json;
using UnityEngine.Scripting;

namespace Meta.WitAi.Requests
{
    public class VoiceServiceRequestResults : INLPRequestResults<WitResponseNode>
    {
        /// <summary>
        /// Request status code if applicable
        /// </summary>
        public int StatusCode { get; private set; } = (int)HttpStatusCode.OK;

        /// <summary>
        /// Request cancelation/error message
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Response transcription
        /// </summary>
        public string Transcription { get; private set; }
        /// <summary>
        /// Response transcription
        /// </summary>
        public string[] FinalTranscriptions { get; private set; }

        /// <summary>
        /// Parsed json response data
        /// </summary>
        public WitResponseNode ResponseData { get; internal set; }

        /// <summary>
        /// Constructor to be used for generation
        /// </summary>
        [Preserve]
        public VoiceServiceRequestResults() {}

        /// <summary>
        /// Sets results to cancellation status code with a specified reason
        /// </summary>
        public void SetCancel(string reason)
        {
            StatusCode = WitConstants.ERROR_CODE_ABORTED;
            Message = reason;
        }

        /// <summary>
        /// Sets results error message & error status
        /// </summary>
        public void SetError(int errorStatusCode, string error)
        {
            StatusCode = errorStatusCode;
            Message = error;
        }

        /// <summary>
        /// Sets current transcription & update final transcription array
        /// </summary>
        /// <param name="transcription">The newest transcription</param>
        /// <param name="full">Whether the transcription is partial or full</param>
        public void SetTranscription(string transcription, bool full)
        {
            Transcription = transcription;
            if (full)
            {
                List<string> transcriptions = new List<string>();
                if (FinalTranscriptions != null)
                {
                    transcriptions.AddRange(FinalTranscriptions);
                }
                transcriptions.Add(Transcription);
                FinalTranscriptions = transcriptions.ToArray();
            }
        }

        /// <summary>
        /// Applies response data
        /// </summary>
        public void SetResponseData(WitResponseNode responseData)
        {
            ResponseData = responseData;
        }
    }
}
