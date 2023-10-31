/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine.Events;

namespace Meta.Voice
{
    /// <summary>
    /// Abstract class for NLP text & audio requests
    /// </summary>
    /// <typeparam name="TUnityEvent">The type of event callback performed by TEvents for all event callbacks</typeparam>
    /// <typeparam name="TOptions">The type containing all specific options to be passed to the end service.</typeparam>
    /// <typeparam name="TEvents">The type containing all events of TSession to be called throughout the lifecycle of the request.</typeparam>
    /// <typeparam name="TResults">The type containing all data that can be returned from the end service.</typeparam>
    public abstract class NLPRequest<TUnityEvent, TOptions, TEvents, TResults>
        : TranscriptionRequest<TUnityEvent, TOptions, TEvents, TResults>
        where TUnityEvent : UnityEventBase
        where TOptions : INLPRequestOptions
        where TEvents : NLPRequestEvents<TUnityEvent>
        where TResults : INLPRequestResults
    {
        /// <summary>
        /// Getter for request input type
        /// </summary>
        public NLPRequestInputType InputType => Options == null ? NLPRequestInputType.Audio : Options.InputType;

        /// <summary>
        /// Getter for response data
        /// </summary>
        public WitResponseNode ResponseData => Results?.ResponseData;

        // Ensure initialized only once
        private bool _initialized = false;
        // Ensure final is not called multiple times
        private bool _finalized = false;

        /// <summary>
        /// Constructor for NLP requests
        /// </summary>
        /// <param name="newInputType">The input type for nlp request transmission</param>
        /// <param name="newOptions">The request parameters sent to the backend service</param>
        /// <param name="newEvents">The request events to be called throughout it's lifecycle</param>
        protected NLPRequest(NLPRequestInputType inputType, TOptions options, TEvents newEvents) : base(options, newEvents)
        {
            // Set option input type & bools
            var opt = Options;
            opt.InputType = inputType;
            Options = opt;
            _initialized = true;
            _finalized = false;

            // Finalize
            SetState(VoiceRequestState.Initialized);
        }

        /// <summary>
        /// Sets the NLPRequest object to the given state, but only after being initialized
        /// </summary>
        protected override void SetState(VoiceRequestState newState)
        {
            if (_initialized)
            {
                base.SetState(newState);
            }
        }

        /// <summary>
        /// Append NLP request specific data to log
        /// </summary>
        /// <param name="log">Building log</param>
        /// <param name="warning">True if this is a warning log</param>
        protected override void AppendLogData(StringBuilder log, VLogLevel logLevel)
        {
            base.AppendLogData(log, logLevel);
            log.AppendLine($"Input Type: {InputType}");
        }

        /// <summary>
        /// Throw error on text request
        /// </summary>
        protected override string GetActivateAudioError()
        {
            if (InputType == NLPRequestInputType.Text)
            {
                return "Cannot activate audio on a text request";
            }
            return string.Empty;
        }

        /// <summary>
        /// Throw error on text request
        /// </summary>
        protected override string GetSendError()
        {
            if (InputType == NLPRequestInputType.Audio && !IsAudioInputActivated)
            {
                return "Cannot send audio without activation";
            }
            return base.GetSendError();
        }

        /// <summary>
        /// Method to be called when an NLP request had completed
        /// </summary>
        /// <param name="responseData">Parsed json data returned from request</param>
        /// <param name="error">Error returned from a request</param>
        protected virtual void HandlePartialResponse(WitResponseNode responseData, string error)
        {
            // Ignore if not in correct state
            if (!IsActive)
            {
                return;
            }
            // Ignore if failed
            if (!string.IsNullOrEmpty(error))
            {
                return;
            }

            // Apply response data
            ApplyResultResponseData(responseData);

            // Partial response called
            OnPartialResponse();
        }

        /// <summary>
        /// Sets response data to the current results object
        /// </summary>
        protected abstract void ApplyResultResponseData(WitResponseNode newData);

        /// <summary>
        /// Called when response data has been updated
        /// </summary>
        protected virtual void OnPartialResponse() =>
            Events?.OnPartialResponse?.Invoke(ResponseData);

        /// <summary>
        /// Method to be called when an NLP request had completed
        /// </summary>
        /// <param name="responseData">Parsed json data returned from request</param>
        /// <param name="error">Error returned from a request</param>
        protected virtual void HandleFinalResponse(WitResponseNode responseData, string error)
        {
            // Ignore if not in correct state
            if (!IsActive || _finalized)
            {
                return;
            }
            _finalized = true;

            // Send partial data if not previously sent
            if (responseData != null && responseData != ResponseData)
            {
                HandlePartialResponse(responseData, error);
            }

            // Error returned
            if (!string.IsNullOrEmpty(error))
            {
                HandleFailure(error);
            }
            // No response
            else if (responseData == null)
            {
                HandleFailure("No response returned");
            }
            // Success
            else
            {
                // Callback for final response
                OnFullResponse();

                // Handle success
                HandleSuccess(Results);
            }
        }

        /// <summary>
        /// Called when full response has completed
        /// </summary>
        protected virtual void OnFullResponse() =>
            Events?.OnFullResponse?.Invoke(ResponseData);

        /// <summary>
        /// Cancels the current request but handles success immediately if possible
        /// </summary>
        public virtual void CompleteEarly()
        {
            // Ignore if not in correct state
            if (!IsActive || _finalized)
            {
                return;
            }

            // Cancel instead
            if (ResponseData == null)
            {
                Cancel("Cannot complete early without response data");
            }
            // Handle success
            else
            {
                HandleFinalResponse(ResponseData, string.Empty);
            }
        }
    }
}
