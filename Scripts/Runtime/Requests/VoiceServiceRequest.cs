/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.WitAi.Requests
{
    [Serializable]
    public abstract class VoiceServiceRequest
        : NLPRequest<VoiceServiceRequestEvent, WitRequestOptions, VoiceServiceRequestEvents, VoiceServiceRequestResults>
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

        // Getter for response decoding
        protected override int GetResponseStatusCode(WitResponseNode responseData) =>
            responseData == null ? 0 : responseData.GetStatusCode();
        protected override string GetResponseError(WitResponseNode responseData) =>
            responseData?.GetError();
        protected override bool GetResponseHasPartial(WitResponseNode responseData) =>
            responseData != null && responseData.HasResponse();

        #region Simulation
        protected override bool OnSimulateResponse()
        {
            if (null == simulatedResponse) return false;


            // Begin calling on main thread if needed
            WatchMainThreadCallbacks();

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
            MainThreadCallback(() =>
            {
                ApplyResponseData(lastResponseData, true);
            });
        }
        #endregion

        #region Thread Safety
        // Check performing
        private CoroutineUtility.CoroutinePerformer _performer = null;
        // All actions
        private ConcurrentQueue<Action> _mainThreadCallbacks = new ConcurrentQueue<Action>();

        // While active, perform any sent callbacks
        protected void WatchMainThreadCallbacks()
        {
            // Ignore if already performing
            if (_performer != null)
            {
                return;
            }

            // Check callbacks every frame (editor or runtime)
            _performer = CoroutineUtility.StartCoroutine(PerformMainThreadCallbacks());
        }
        // Every frame check for callbacks & perform any found
        private System.Collections.IEnumerator PerformMainThreadCallbacks()
        {
            // While checking, continue
            while (HasMainThreadCallbacks())
            {
                // Wait for frame
                if (Application.isPlaying && !Application.isBatchMode)
                {
                    yield return new WaitForEndOfFrame();
                }
                // Wait for a tick
                else
                {
                    yield return null;
                }

                // Perform if possible
                while (_mainThreadCallbacks.Count > 0 && _mainThreadCallbacks.TryDequeue(out var result))
                {
                    result();
                }
            }
            _performer = null;
        }
        // If active or performing callbacks
        private bool HasMainThreadCallbacks()
        {
            return IsActive || _mainThreadCallbacks.Count > 0;
        }
        // Called from background thread
        protected void MainThreadCallback(Action action)
        {
            if (action == null)
            {
                return;
            }
            _mainThreadCallbacks.Enqueue(action);
        }
        #endregion

        // Add request id as response data if possible
        protected override void ApplyResponseData(WitResponseNode responseData, bool final)
        {
            if (responseData != null)
            {
                responseData[WitConstants.HEADER_REQUEST_ID] = Options?.RequestId;
            }
            base.ApplyResponseData(responseData, final);
        }

        /// <summary>
        /// Performs an event callback with this request as the parameter
        /// </summary>
        /// <param name="eventCallback">The voice service request event to be called</param>
        protected override void RaiseEvent(VoiceServiceRequestEvent eventCallback)
        {
            eventCallback?.Invoke(this);
        }
    }
}
