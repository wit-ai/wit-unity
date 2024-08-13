/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meta.Voice;
using Meta.Voice.Logging;
using Meta.Voice.TelemetryUtilities;
using Meta.WitAi.Configuration;
using Meta.WitAi.Events;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.WitAi
{
    /// <summary>
    /// A simple base class for wrapping VoiceServiceRequest event callbacks
    /// </summary>
    [LogCategory(LogCategory.SpeechService)]
    public abstract class BaseSpeechService : MonoBehaviour
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.SpeechService);

        /// <summary>
        /// Whether this script should wrap all request event setups
        /// </summary>
        public bool ShouldWrap = true;

        /// <summary>
        /// Whether this script should log
        /// </summary>
        public bool ShouldLog = true;

        /// <summary>
        /// All currently running requests
        /// </summary>
        public HashSet<VoiceServiceRequest> Requests { get; } = new HashSet<VoiceServiceRequest>();

        /// <summary>
        /// Dictionary that holds generated actions that pass a request along with additional event data
        /// </summary>
        private ConcurrentDictionary<int, object> _customRequestEvents = new ConcurrentDictionary<int, object>();

        /// <summary>
        /// Returns true if this voice service is currently active, listening with the mic or performing a networked request
        /// </summary>
        public virtual bool Active => Requests != null && Requests.Count > 0;

        /// <summary>
        /// If applicable, get all speech events
        /// </summary>
        protected virtual SpeechEvents GetSpeechEvents() => null;

        /// <summary>
        /// Returns true if this voice service is currently active, listening with the mic or performing a networked request
        /// </summary>
        public virtual bool IsAudioInputActive
        {
            get
            {
                var audioRequest = GetAudioRequest();
                return audioRequest != null && audioRequest.IsAudioInputActivated;
            }
        }

        /// <summary>
        /// Get the first running audio request
        /// </summary>
        protected virtual VoiceServiceRequest GetAudioRequest() =>
            Requests?.FirstOrDefault((request) => request.InputType == NLPRequestInputType.Audio);

        /// <summary>
        /// Check for error that will occur if attempting to activate audio
        /// </summary>
        /// <returns>Returns an error audio activation should not be allowed.</returns>
        public virtual string GetActivateAudioError()
        {
            // Ensure audio is not already active
            if (IsAudioInputActive)
            {
                return "Audio input is already being performed for this service.";
            }
            // No error
            return string.Empty;
        }

        /// <summary>
        /// Whether an audio request can be started or not
        /// </summary>
        public virtual bool CanActivateAudio() => string.IsNullOrEmpty(GetActivateAudioError());

        /// <summary>
        /// Check for error that will occur if attempting to send data
        /// </summary>
        /// <returns>Returns an error if send will not be allowed.</returns>
        public virtual string GetSendError()
        {
            // No error
            return string.Empty;
        }

        /// <summary>
        /// Whether a voice service request can be sent or not
        /// </summary>
        public virtual bool CanSend() => string.IsNullOrEmpty(GetSendError());

        /// <summary>
        /// On enable, begin watching for request initialized callbacks
        /// </summary>
        protected virtual void OnEnable()
        {
            // Cannot send if internet is not reachable (Only works on Mobile)
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Logger.Error("Unable to reach the internet. Check your connection.");
            }
            GetSpeechEvents()?.OnRequestInitialized.AddListener(OnRequestInit);
        }
        /// <summary>
        /// On enable, stop watching for request initialized callbacks
        /// </summary>
        protected virtual void OnDisable()
        {
            GetSpeechEvents()?.OnRequestInitialized.RemoveListener(OnRequestInit);
        }

        /// <summary>
        /// Deactivate all requests
        /// </summary>
        public virtual void Deactivate()
        {
            foreach (var request in Requests.ToArray())
            {
                Deactivate(request);
            }
        }

        /// <summary>
        /// Deactivate a specific request
        /// </summary>
        public virtual void Deactivate(VoiceServiceRequest request)
        {
            if (request == null || !request.IsLocalRequest)
            {
                return;
            }
            request.DeactivateAudio();
        }

        /// <summary>
        /// Deactivate and abort all locally originated requests
        /// </summary>
        public virtual void DeactivateAndAbortRequest()
        {
            foreach (var request in Requests.ToArray())
            {
                DeactivateAndAbortRequest(request);
            }
        }

        /// <summary>
        /// Deactivate and abort a specific requests
        /// </summary>
        public virtual void DeactivateAndAbortRequest(VoiceServiceRequest request)
        {
            if (request == null || !request.IsLocalRequest)
            {
                return;
            }
            request.Cancel();
        }

        /// <summary>
        /// Method to setup request events with provided base events
        /// </summary>
        /// <param name="events">Generate request events if empty</param>
        public virtual void SetupRequestParameters(ref WitRequestOptions options, ref VoiceServiceRequestEvents events)
        {
            // Ensure options & events exist
            if (options == null)
            {
                options = new WitRequestOptions();
            }
            if (events == null)
            {
                events = new VoiceServiceRequestEvents();
            }

            // Call option setup if desired
            if (ShouldWrap)
            {
                GetSpeechEvents().OnRequestOptionSetup?.Invoke(options);
            }
        }

        /// <summary>
        /// Accepts a generated voice service request, wraps all request events & returns local methods
        /// for each
        /// </summary>
        /// <param name="request">The provided VoiceServiceRequest to be tracked</param>
        /// <returns>Returns false if wrap fails</returns>
        public virtual bool WrapRequest(VoiceServiceRequest request)
        {
            // Cannot track
            if (request == null)
            {
                Log(null, "Cannot wrap a null VoiceServiceRequest", true);
                return false;
            }

            // Already complete, return
            if (request.State == VoiceRequestState.Canceled)
            {
                RuntimeTelemetry.Instance.LogEventTermination((OperationID)request.Options.RequestId, TerminationReason.Canceled);
                OnRequestCancel(request);
                OnRequestComplete(request);
                return true;
            }
            if (request.State == VoiceRequestState.Failed)
            {
                RuntimeTelemetry.Instance.LogEventTermination((OperationID)request.Options.RequestId, TerminationReason.Failed);
                OnRequestFailed(request);
                OnRequestComplete(request);
                return true;
            }
            if (request.State == VoiceRequestState.Successful)
            {
                RuntimeTelemetry.Instance.LogEventTermination((OperationID)request.Options.RequestId, TerminationReason.Successful);
                OnRequestPartialResponse(request, request?.ResponseData);
                OnRequestSuccess(request);
                OnRequestComplete(request);
                return true;
            }

            // Call init & add delegates
            if (ShouldWrap)
            {
                // Call request initialized method
                RuntimeTelemetry.Instance.StartEvent((OperationID)request.Options.RequestId, RuntimeTelemetryEventType.VoiceServiceRequest);
                GetSpeechEvents()?.OnRequestInitialized?.Invoke(request);

                // Send if desired
                if (request.State == VoiceRequestState.Transmitting)
                {
                    OnRequestSend(request);
                }
            }

            // Success
            return true;
        }

        // The desired log method for this script.  Ensures request id is included in every call
        protected virtual void Log(VoiceServiceRequest request, string log, bool warn = false)
        {
            if (!ShouldLog)
            {
                return;
            }
            if (warn)
            {
                Logger.Error("{0}\nRequest Id: {1}", log, request?.Options?.RequestId);
            }
            else
            {
                Logger.Info(log);
            }
        }

        // Called via VoiceServiceRequest constructor
        protected virtual void OnRequestInit(VoiceServiceRequest request)
        {
            // Ignore if already set up
            if (Requests.Contains(request))
            {
                return;
            }

            // Add main completion event callbacks
            SetEventListeners(request, true);

            // Add to request list
            Requests.Add(request);
            Log(request, "Request Initialized");

            // Now initialized
#pragma warning disable CS0618
            GetSpeechEvents()?.OnRequestCreated?.Invoke(request is WitRequest witRequest ? witRequest : null);
#pragma warning restore CS0618
        }

        // Called when VoiceServiceRequest OnStartListening is returned
        protected virtual void OnRequestStartListening(VoiceServiceRequest request)
        {
            Log(request, "Request Start Listening");
            GetSpeechEvents()?.OnStartListening?.Invoke();
        }

        // Called when VoiceServiceRequest OnStopListening is returned
        protected virtual void OnRequestStopListening(VoiceServiceRequest request)
        {
            Log(request, "Request Stop Listening");
            GetSpeechEvents()?.OnStoppedListening?.Invoke();
        }

        // Called when VoiceServiceRequest OnPartialResponse is returned & tries to end early if possible
        protected virtual void OnRequestSend(VoiceServiceRequest request)
        {
            Log(request, "Request Send");
            GetSpeechEvents()?.OnSend?.Invoke(request);
        }

        // Called when VoiceServiceRequest OnPartialTranscription is returned with early ASR
        protected virtual void OnRequestRawResponse(VoiceServiceRequest request, string rawResponse)
        {
            GetSpeechEvents()?.OnRawResponse?.Invoke(rawResponse);
        }

        // Called when VoiceServiceRequest OnPartialTranscription is returned with early ASR
        protected virtual void OnRequestPartialTranscription(VoiceServiceRequest request, string transcription)
        {
            RuntimeTelemetry.Instance.LogPoint((OperationID)request.Options.RequestId, RuntimeTelemetryPoint.PartialTranscriptionReceived);
            GetSpeechEvents()?.OnPartialTranscription?.Invoke(transcription);
            GetSpeechEvents()?.OnUserPartialTranscription?.Invoke(request.Options.ClientUserId, transcription);
        }

        // Called when VoiceServiceRequest OnFullTranscription is returned from request with final ASR
        protected virtual void OnRequestFullTranscription(VoiceServiceRequest request, string transcription)
        {
            RuntimeTelemetry.Instance.LogPoint((OperationID)request.Options.RequestId, RuntimeTelemetryPoint.FullTranscriptionReceived);
            Log(request, $"Request Full Transcription\nText: {transcription}");
            GetSpeechEvents()?.OnFullTranscription?.Invoke(transcription);
            GetSpeechEvents()?.OnUserFullTranscription?.Invoke(request.Options.ClientUserId, transcription);
        }

        // Called when VoiceServiceRequest OnPartialResponse is returned & tries to end early if possible
        protected virtual void OnRequestPartialResponse(VoiceServiceRequest request, WitResponseNode responseData)
        {
            if (responseData != null)
            {
                var requestId = request?.Options.RequestId;
                RuntimeTelemetry.Instance.LogPoint((OperationID)requestId, RuntimeTelemetryPoint.PartialResponseReceived);
                GetSpeechEvents()?.OnPartialResponse?.Invoke(responseData);
            }
        }

        // Called when VoiceServiceRequest OnCancel is returned
        protected virtual void OnRequestCancel(VoiceServiceRequest request)
        {
            string message = request?.Results?.Message;
            Log(request, $"Request Canceled\nReason: {message}");
            GetSpeechEvents()?.OnCanceled?.Invoke(message);
            if (!string.Equals(message, WitConstants.CANCEL_MESSAGE_PRE_SEND))
            {
                GetSpeechEvents()?.OnAborted?.Invoke();
            }
        }

        // Called when VoiceServiceRequest OnFailed is returned
        protected virtual void OnRequestFailed(VoiceServiceRequest request)
        {
            string code = $"HTTP Error {request.Results.StatusCode}";
            string message = request?.Results?.Message;
            Log(request, $"Request Failed\n{code}: {message}", true);
            GetSpeechEvents()?.OnError?.Invoke(code, message);
            GetSpeechEvents()?.OnRequestCompleted?.Invoke();
        }

        // Called when VoiceServiceRequest OnSuccess is returned
        protected virtual void OnRequestSuccess(VoiceServiceRequest request)
        {
            Log(request, $"Request Success");
            GetSpeechEvents()?.OnResponse?.Invoke(request?.ResponseData);
            GetSpeechEvents()?.OnRequestCompleted?.Invoke();
        }

        // Called when VoiceServiceRequest returns successfully, with an error or is cancelled
        protected virtual void OnRequestComplete(VoiceServiceRequest request)
        {
            // Remove from set & unwrap if found
            if (Requests.Contains(request))
            {
                SetEventListeners(request, false);
                Requests.Remove(request);
            }

            // Perform log & event callbacks
            Log(request, $"Request Complete\nRemaining: {Requests.Count}");
            GetSpeechEvents()?.OnComplete?.Invoke(request);
        }

        /// <summary>
        /// Adds or removes event listeners for every request event callback
        /// </summary>
        /// <param name="request">The request to begin or stop listening to</param>
        /// <param name="addListeners">If true, adds listeners and if false, removes listeners.</param>
        protected virtual void SetEventListeners(VoiceServiceRequest request, bool addListeners)
        {
            // Get request events
            var events = request.Events;

            // Add/Remove 1 : 1 listeners
            events.OnStartListening.SetListener(OnRequestStartListening, addListeners);
            events.OnStopListening.SetListener(OnRequestStopListening, addListeners);
            events.OnSend.SetListener(OnRequestSend, addListeners);
            events.OnSuccess.SetListener(OnRequestSuccess, addListeners);
            events.OnFailed.SetListener(OnRequestFailed, addListeners);
            events.OnCancel.SetListener(OnRequestCancel, addListeners);
            events.OnComplete.SetListener(OnRequestComplete, addListeners);

            // Add/Remove custom actions
            SetRequestEventListener(events.OnRawResponse, request, OnRequestRawResponse, addListeners);
            SetRequestEventListener(events.OnPartialTranscription, request, OnRequestPartialTranscription, addListeners);
            SetRequestEventListener(events.OnFullTranscription, request, OnRequestFullTranscription, addListeners);
            SetRequestEventListener(events.OnPartialResponse, request, OnRequestPartialResponse, addListeners);
        }

        /// <summary>
        /// Adds or removes listener to a base event and passes base event's parameter along with the request to the added action.
        /// </summary>
        /// <param name="baseEvent">The base event to begin or stop listening to.</param>
        /// <param name="request">The request to be passed to the callback along with the baseEvent parameter.</param>
        /// <param name="callbackWithRequest">The callback action that should occur whenever the baseEvent is called.</param>
        /// <param name="addListener">If true, adds listener to baseEvent.  If false, removes listener</param>
        private void SetRequestEventListener<TParam>(UnityEvent<TParam> baseEvent,
            VoiceServiceRequest request,
            UnityAction<VoiceServiceRequest, TParam> callbackWithRequest,
            bool addListener)
        {
            var id = baseEvent.GetHashCode();
            if (addListener)
            {
                UnityAction<TParam> intermediaryEvent = (param) => callbackWithRequest?.Invoke(request, param);
                _customRequestEvents[id] = intermediaryEvent;
                baseEvent.AddListener(intermediaryEvent);
            }
            else if (_customRequestEvents.TryRemove(id, out var adapter)
                     && adapter is UnityAction<TParam> intermediaryEvent)
            {
                baseEvent.RemoveListener(intermediaryEvent);
            }
        }
    }
}
