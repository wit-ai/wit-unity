/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    [Serializable]
    public abstract class VoiceServiceRequest
        : NLPRequest<VoiceServiceRequestEvent, WitRequestOptions, VoiceServiceRequestEvents, VoiceServiceRequestResults, WitResponseNode>
    {
        /// <summary>
        /// Constructor for Voice Service requests
        /// </summary>
        /// <param name="newInputType">The request input type (text/audio) to be used</param>
        /// <param name="newOptions">The request parameters to be used</param>
        /// <param name="newEvents">The request events to be called throughout it's lifecycle</param>
        protected VoiceServiceRequest(NLPRequestInputType newInputType, WitRequestOptions newOptions, VoiceServiceRequestEvents newEvents) : base(newInputType, newOptions, newEvents) {}

        /// <summary>
        /// The status code returned from the last request
        /// </summary>
        public int StatusCode => Results.StatusCode;

        // Use a wit response decoder to obtain WitResponseNode from text
        protected override INLPRequestResponseDecoder<WitResponseNode> ResponseDecoder => _responseDecoder;
        private static WitResponseDecoder _responseDecoder = new WitResponseDecoder();

        public TaskCompletionSource<bool> PreparationTask => new TaskCompletionSource<bool>();

        /// <summary>
        /// Check for ignored error status codes & messages.
        /// </summary>
        /// <param name="errorStatusCode">The error status code if applicable</param>
        /// <param name="errorMessage">The error to be returned</param>
        protected override bool ShouldIgnoreError(int errorStatusCode, string errorMessage)
        {
            // Ignore if base allows
            if (base.ShouldIgnoreError(errorStatusCode, errorMessage))
            {
                return true;
            }
            // Ignore no transcription responses
            if (string.Equals(errorMessage, WitConstants.ERROR_NO_TRANSCRIPTION))
            {
                return true;
            }
            // Don't ignore
            return false;
        }

        #region Simulation
        protected override bool OnSimulateResponse()
        {
            if (null == simulatedResponse) return false;

            SimulateResponse();
            return true;
        }

        private async void SimulateResponse()
        {
            var stackTrace = new StackTrace();
            var statusDescription = simulatedResponse.responseDescription;
            for (int i = 0; i < simulatedResponse.messages.Count - 1; i++)
            {
                var message = simulatedResponse.messages[i];
                await System.Threading.Tasks.Task.Delay((int)(message.delay * 1000));
                var partialResponse = WitResponseNode.Parse(message.responseBody);
                partialResponse["code"] = new WitResponseData(simulatedResponse.code);
                ApplyResponseData(partialResponse, false);
            }

            var lastMessage = simulatedResponse.messages.Last();
            await System.Threading.Tasks.Task.Delay((int)(lastMessage.delay * 1000));
            var lastResponseData = WitResponseNode.Parse(lastMessage.responseBody);
            lastResponseData["code"] = new WitResponseData(simulatedResponse.code);
            ApplyResponseData(lastResponseData, true);
        }
        #endregion

        // Sets request id
        protected override void ApplyResponseData(WitResponseNode responseData, bool isFinal)
        {
            if (responseData != null)
            {
                responseData[WitConstants.HEADER_REQUEST_ID] = Options?.RequestId;
            }
            base.ApplyResponseData(responseData, isFinal);
        }

        /// <summary>
        /// Subscribes or unsubscribes all provided VoiceServiceRequestEvents from this request's
        /// VoiceServiceRequestEvents callbacks.
        /// </summary>
        /// <param name="newEvents">The events to subscribe or unsubscribe to the request.Events</param>
        /// <param name="add">Whether to add listeners or remove listeners</param>
        protected override void SetEventListeners(VoiceServiceRequestEvents newEvents, bool add) =>
            Events.SetListeners(newEvents, add);

        /// <summary>
        /// Performs an event callback with this request as the parameter
        /// </summary>
        /// <param name="eventCallback">The voice service request event to be called</param>
        protected override void RaiseEvent(VoiceServiceRequestEvent eventCallback) => ThreadUtility.CallOnMainThread(() =>
            eventCallback?.Invoke(this));
    }
}
