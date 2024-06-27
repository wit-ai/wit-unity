/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using System.Threading.Tasks;
using Meta.Voice.Logging;
using Meta.Voice.TelemetryUtilities;
using Meta.WitAi;
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
    [LogCategory(LogCategory.Network)]
    public abstract class NLPRequest<TUnityEvent, TOptions, TEvents, TResults, TResponseData>
        : TranscriptionRequest<TUnityEvent, TOptions, TEvents, TResults>
        where TUnityEvent : UnityEventBase
        where TOptions : INLPRequestOptions
        where TEvents : NLPRequestEvents<TUnityEvent, TResponseData>
        where TResults : INLPRequestResults<TResponseData>
    {
        public override IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Network);

        /// <summary>
        /// Getter for request input type
        /// </summary>
        public NLPRequestInputType InputType => Options == null ? NLPRequestInputType.Audio : Options.InputType;

        /// <summary>
        /// Getter for decoded response data
        /// </summary>
        public TResponseData ResponseData => Results == null ? default(TResponseData) : Results.ResponseData;

        // Ensure initialized only once
        private bool _initialized = false;
        // Ensure final is not called multiple times
        private bool _finalized = false;
        // Async delay
        private const int DECODE_DELAY_MS = 5;

        /// <summary>
        /// Constructor for NLP requests
        /// </summary>
        /// <param name="newInputType">The input type for nlp request transmission</param>
        /// <param name="newOptions">The request parameters sent to the backend service</param>
        /// <param name="newEvents">The request events to be called throughout it's lifecycle</param>
        protected NLPRequest(NLPRequestInputType inputType, TOptions options, TEvents newEvents) : base(options, newEvents)
        {
            // Set option input type & bools
            Options.InputType = inputType;
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
        protected override void AppendLogData(StringBuilder log, VLoggerVerbosity logLevel)
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

        #region DECODING
        /// <summary>
        /// The response decoder used to decode response json & provide vars for response data
        /// </summary>
        protected virtual INLPRequestResponseDecoder<TResponseData> ResponseDecoder => null;

        /// <summary>
        /// Whether or not raw responses should be decoded within this script.  Defaults to false
        /// </summary>
        protected virtual bool DecodeRawResponses => false;

        /// <summary>
        /// Whether currently decoding a raw response
        /// </summary>
        public virtual bool IsDecoding => _rawQueued > _rawDecoded;

        // Last raw response received
        private string _rawResponseLast;
        // Apply
        private int _rawQueued = 0;
        private int _rawDecoded = 0;
        // Whether the currently decoding raw response should be considered final
        private bool _rawResponseFinal;

        /// <summary>
        /// Performs callbacks for raw response &
        /// </summary>
        /// <param name="rawResponse"></param>
        /// <param name="final"></param>
        protected virtual void HandleRawResponse(string rawResponse, bool final)
        {
            // Ignore if not active
            if (!IsActive)
            {
                return;
            }
            // Ignore null partials, handle failure if should decode final
            if (string.IsNullOrEmpty(rawResponse))
            {
                if (final && DecodeRawResponses)
                {
                    HandleFailure("Final response is empty");
                }
                return;
            }
            // Ignore same partials, finalize if should decode final
            if (string.Equals(_rawResponseLast, rawResponse))
            {
                return;
            }

            // Apply last raw response
            _rawResponseFinal |= final;
            _rawResponseLast = rawResponse;

            // Perform callback
            OnRawResponse(rawResponse);

            // Enqueue decode
            if (DecodeRawResponses && ResponseDecoder != null)
            {
                EnqueueDecode(rawResponse, final);
            }
        }

        /// <summary>
        /// Called when raw response data has been received
        /// </summary>
        protected virtual void OnRawResponse(string rawResponse) => ThreadUtility.CallOnMainThread(() =>
            Events?.OnRawResponse?.Invoke(rawResponse));

        private Task _lastDecode;
        private TResponseData _lastResponse;

        private void EnqueueDecode(string rawResponse, bool final)
        {
            _rawQueued++;
            var blockingTask = _lastDecode;
            _lastDecode = ThreadUtility.BackgroundAsync(Logger,  async () =>
            {
                if (null != blockingTask) await blockingTask;
                DecodeRawResponse(rawResponse, final);
            });
        }
        private void DecodeRawResponse(string rawResponse, bool final)
        {
            // Decode data
            TResponseData responseData = ResponseDecoder.Decode(rawResponse);
            _rawDecoded++;

            // Ignore if no longer active
            if (!IsActive)
            {
                return;
            }

            // If passed as final or set to final while decoding and the last decoding chunk
            final |= _rawResponseFinal && !IsDecoding;
            _lastResponse = responseData;
            ApplyResponseData(responseData, final);
        }
        #endregion DECODING

        /// <summary>
        /// Sets response data to the current results object
        /// </summary>
        /// <param name="responseData">Parsed json data returned from request</param>
        /// <param name="final">Whether or not this response should be considered final</param>
        protected virtual void ApplyResponseData(TResponseData responseData, bool final)
        {
            // Ignore if not active
            if (!IsActive)
            {
                return;
            }
            // Only perform final once
            if (final)
            {
                if (_finalized)
                {
                    return;
                }
                _finalized = true;
            }
            // Handle null response if final
            if (responseData == null)
            {
                if (final)
                {
                    HandleFailure($"Failed to decode partial raw response");
                }
                return;
            }
            // Handle error immediately
            string error = ResponseDecoder?.GetResponseError(responseData);
            if (!string.IsNullOrEmpty(error))
            {
                int errorStatusCode = ResponseDecoder == null
                    ? WitConstants.ERROR_CODE_GENERAL
                    : ResponseDecoder.GetResponseStatusCode(responseData);
                HandleFailure(errorStatusCode, error);
                return;
            }

            // Store whether data is changing
            bool hasChanged = !responseData.Equals(Results.ResponseData);

            // Apply new response data
            Results.SetResponseData(responseData);

            // Apply partial transcription if changed & exists
            string transcription = ResponseDecoder?.GetResponseTranscription(responseData);
            bool hasTranscription = ResponseDecoder != null && ResponseDecoder.GetResponseHasTranscription(responseData);
            bool isTranscriptionFull = ResponseDecoder != null && ResponseDecoder.GetResponseIsTranscriptionFull(responseData);
            if (hasChanged && hasTranscription)
            {
                ApplyTranscription(transcription, isTranscriptionFull);
            }

            // Call partial response if changed & exists
            bool hasPartial = ResponseDecoder != null && ResponseDecoder.GetResponseHasPartial(responseData);
            if (hasChanged && hasPartial)
            {
                OnPartialResponse(responseData);
            }

            // Final was called, handle success
            if (final)
            {
                // Call partial response if not previously called
                if (!hasPartial)
                {
                    OnPartialResponse(responseData);
                }

                // Additional event validation on final
                var validationErrors = new StringBuilder();
                Events.OnValidateResponse?.Invoke(responseData, validationErrors);
                if (validationErrors.Length > 0)
                {
                    HandleFailure($"Response validation failed due to {validationErrors}");
                    return;
                }

                // Call final response
                OnFullResponse(responseData);
                // Handle success
                HandleSuccess();
            }
        }

        /// <summary>
        /// Called when response data has been updated
        /// </summary>
        protected virtual void OnPartialResponse(TResponseData responseData)
        {
            RuntimeTelemetry.Instance.LogPoint((OperationID)Options.RequestId, RuntimeTelemetryPoint.PartialResponseReceived);
            ThreadUtility.CallOnMainThread(() =>
            Events?.OnPartialResponse?.Invoke(responseData));
        }

        /// <summary>
        /// Called when full response has completed
        /// </summary>
        protected virtual void OnFullResponse(TResponseData responseData) => ThreadUtility.CallOnMainThread(() =>
            Events?.OnFullResponse?.Invoke(responseData));

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
                MakeLastResponseFinal();
            }
        }

        // Make current response final if possible
        protected virtual void MakeLastResponseFinal()
        {
            // Ignore if not active
            if (!IsActive)
            {
                return;
            }
            // Still decoding, enable flag to be handled on completion
            if (IsDecoding)
            {
                _rawResponseFinal = true;
                return;
            }
            // Apply previous data as final
            ApplyResponseData(_lastResponse, true);
        }
    }
}
