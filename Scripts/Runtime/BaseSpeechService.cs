/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Events;
using Meta.WitAi.Requests;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.WitAi
{
    /// <summary>
    /// A simple base class for wrapping VoiceServiceRequest event callbacks
    /// </summary>
    public abstract class BaseSpeechService : MonoBehaviour
    {
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
        /// Returns true if this voice service is currently active, listening with the mic or performing a networked request
        /// </summary>
        public virtual bool Active => Requests != null && Requests.Count > 0;

        /// <summary>
        /// If applicable, get all speech events
        /// </summary>
        protected virtual SpeechEvents GetSpeechEvents() => null;

        /// <summary>
        /// Check for error that will occur if attempting to send data
        /// </summary>
        /// <returns>Returns an error if send will not be allowed.</returns>
        public virtual string GetSendError()
        {
            // Cannot send if internet is not reachable (Only works on Mobile)
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return "Unable to reach the internet.  Check your connection.";
            }
            // No error
            return string.Empty;
        }

        /// <summary>
        /// Whether a voice service request can be sent or not
        /// </summary>
        public virtual bool CanSend() => string.IsNullOrEmpty(GetSendError());

        /// <summary>
        /// Deactivate all requests
        /// </summary>
        public virtual void Deactivate()
        {
            foreach (var request in Requests.ToArray())
            {
                request.DeactivateAudio();
            }
        }

        /// <summary>
        /// Deactivate a specific request
        /// </summary>
        public virtual void Deactivate(VoiceServiceRequest request)
        {
            request?.DeactivateAudio();
        }

        /// <summary>
        /// Deactivate and abort all requests
        /// </summary>
        public virtual void DeactivateAndAbortRequest()
        {
            foreach (var request in Requests.ToArray())
            {
                request.Cancel();
            }
        }

        /// <summary>
        /// Deactivate and abort a specific requests
        /// </summary>
        public virtual void DeactivateAndAbortRequest(VoiceServiceRequest request)
        {
            request?.Cancel();
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

            // Wrap if desired
            if (ShouldWrap)
            {
                // Call option setup
                GetSpeechEvents().OnRequestOptionSetup?.Invoke(options);

                // Wait for init
                WrapRequestEvent(events?.OnInit, OnRequestInit, true);
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
                OnRequestCancel(request);
                OnRequestComplete(request);
                return true;
            }
            if (request.State == VoiceRequestState.Failed)
            {
                OnRequestFailed(request);
                OnRequestComplete(request);
                return true;
            }
            if (request.State == VoiceRequestState.Successful)
            {
                OnRequestPartialResponse(request);
                OnRequestSuccess(request);
                OnRequestComplete(request);
                return true;
            }

            // Call init & add delegates
            if (ShouldWrap)
            {
                OnRequestInit(request);
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
            var category = GetType().Name;
            var result = new StringBuilder();
            result.AppendLine(log);
            result.AppendLine($"Request Id: {request?.Options?.RequestId}");
            if (warn)
            {
                VLog.W(category, result);
            }
            else
            {
                VLog.I(category, result);
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
            WrapRequestEvents(request, true);

            // Add to request list
            Requests.Add(request);

            // Now initialized
            Log(request, "Request Initialized");
            GetSpeechEvents()?.OnRequestInitialized?.Invoke(request);
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
        protected virtual void OnRequestPartialTranscription(VoiceServiceRequest request)
        {
            GetSpeechEvents()?.OnPartialTranscription?.Invoke(request?.Transcription);
        }

        // Called when VoiceServiceRequest OnFullTranscription is returned from request with final ASR
        protected virtual void OnRequestFullTranscription(VoiceServiceRequest request)
        {
            Log(request, $"Request Final Transcription\nText: {request?.Transcription}");
            GetSpeechEvents()?.OnFullTranscription?.Invoke(request?.Transcription);
        }

        // Called when VoiceServiceRequest OnPartialResponse is returned & tries to end early if possible
        protected virtual void OnRequestPartialResponse(VoiceServiceRequest request)
        {
            var responseData = request?.ResponseData;
            if (responseData != null)
            {
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
            Log(request, $"Request Success\nResponse:\n{request?.ResponseData}");
            GetSpeechEvents()?.OnResponse?.Invoke(request?.ResponseData);
            GetSpeechEvents()?.OnRequestCompleted?.Invoke();
        }

        // Called when VoiceServiceRequest returns successfully, with an error or is cancelled
        protected virtual void OnRequestComplete(VoiceServiceRequest request)
        {
            // Remove from set & unwrap if found
            if (Requests.Contains(request))
            {
                Requests.Remove(request);
                WrapRequestEvents(request, false);
            }

            // Perform log & event callbacks
            Log(request, $"Request Complete\nRemaining: {Requests.Count}");
            GetSpeechEvents()?.OnComplete?.Invoke(request);
        }

        // Remove or add all events
        protected virtual void WrapRequestEvents(VoiceServiceRequest request, bool add)
        {
            var events = request.Events;
            WrapRequestEvent(events.OnInit, OnRequestInit, add);
            WrapRequestEvent(events.OnStartListening, OnRequestStartListening, add);
            WrapRequestEvent(events.OnStopListening, OnRequestStopListening, add);
            WrapRequestEvent(events.OnSend, OnRequestSend, add);
            WrapRequestEvent(events.OnPartialTranscription, (text) => OnRequestPartialTranscription(request), add);
            WrapRequestEvent(events.OnFullTranscription, (text) => OnRequestFullTranscription(request), add);
            WrapRequestEvent(events.OnPartialResponse, (results) => OnRequestPartialResponse(request), add);
            WrapRequestEvent(events.OnSuccess, OnRequestSuccess, add);
            WrapRequestEvent(events.OnFailed, OnRequestFailed, add);
            WrapRequestEvent(events.OnCancel, OnRequestCancel, add);
            WrapRequestEvent(events.OnComplete, OnRequestComplete, add);
        }

        // Set event action if possible
        private void WrapRequestEvent<T>(UnityEvent<T> unityEvent, UnityAction<T> action, bool add)
        {
            if (add)
            {
                unityEvent.AddListener(action);
            }
            else
            {
                unityEvent.RemoveAllListeners();
            }
        }
    }
}
