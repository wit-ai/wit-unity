/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A request to be used with VoiceService.WrapRequest method for custom application of response data
    /// </summary>
    public class VoiceServiceMockRequest : VoiceServiceRequest
    {
        // Wrapper for all setup data
        public VoiceServiceMockRequest(NLPRequestInputType newInputType, WitRequestOptions newOptions, VoiceServiceRequestEvents newEvents)
            : base(newInputType, newOptions, newEvents) {}

        // Decode raw responses if sent
        protected override bool DecodeRawResponses => true;

        // Whether audio has been sent by this mock request (Assume no)
        protected override bool HasSentAudio() => false;

        // Immediately consider enabled & send
        protected override void HandleAudioActivation() =>
            SetAudioInputState(VoiceAudioInputState.On);
        // Immediately consider disabled
        protected override void HandleAudioDeactivation() =>
            SetAudioInputState(VoiceAudioInputState.Off);
        // Nothing to be done for sending
        protected override void HandleSend() {}
        // Nothing to be done for cancellation
        protected override void HandleCancel() {}

        /// <summary>
        /// Preferred method for setting raw response data and decoding it locally
        /// </summary>
        /// <param name="jsonText">The raw json text to be decoded</param>
        /// <param name="final">Whether this is the final text chunk or not</param>
        public void SetRawResponse(string jsonText, bool final = false)
        {
            // Ignore if not active
            if (State != VoiceRequestState.Transmitting)
            {
                VLog.W("Cannot apply a raw response unless transmitting");
                return;
            }

            // Perform raw response handling
            HandleRawResponse(jsonText, final);
        }

        /// <summary>
        /// If additional transcription callbacks are required, handle them with this method
        /// </summary>
        /// <param name="newTranscription">The new transcription to be used</param>
        /// <param name="full">Whether this is a full transcription or not</param>
        public void SetTranscription(string newTranscription, bool full = false)
        {
            // Ignore if not active
            if (State != VoiceRequestState.Transmitting)
            {
                VLog.W("Cannot set transcription unless transmitting");
                return;
            }

            // Perform application of transcription
            ApplyTranscription(newTranscription, full);
        }

        /// <summary>
        /// If additional decoded partial responses are required this method will handle
        /// their application.
        /// </summary>
        /// <param name="responseData">The decoded response data</param>
        /// <param name="final">Whether this is the final response or not</param>
        public void SetResponseData(WitResponseNode responseData, bool final = false)
        {
            // Ignore if not active
            if (State != VoiceRequestState.Transmitting)
            {
                VLog.W("Cannot set decoded response data unless transmitting");
                return;
            }

            // Perform application of response data
            ApplyResponseData(responseData, final);
        }

        /// <summary>
        /// Force error handling on a request with status code defaulted to default
        /// </summary>
        /// <param name="error">The error to be returned</param>
        public void Fail(string error) =>
            Fail(WitConstants.ERROR_CODE_GENERAL, error);

        /// <summary>
        /// Force error handling on a request
        /// </summary>
        /// <param name="statusCode">The specified status code</param>
        /// <param name="error">The error to be returned</param>
        public void Fail(int statusCode, string error)
        {
            // Ignore if not active
            if (State != VoiceRequestState.Transmitting)
            {
                VLog.W("Cannot make a request fail unless transmitting");
                return;
            }

            // Perform application of response data
            HandleFailure(statusCode, error);
        }
    }
}
