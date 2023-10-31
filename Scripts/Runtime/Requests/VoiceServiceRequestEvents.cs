/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice;
using Meta.WitAi.Json;
using UnityEngine.Events;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A set of events associated with a Voice Service activation.
    /// </summary>
    [Serializable]
    public class VoiceServiceRequestEvents
        : NLPRequestEvents<VoiceServiceRequestEvent, WitResponseNode>
    {
        /// <summary>
        /// Adds all listeners for another VoiceServiceRequestEvents
        /// </summary>
        public void AddListeners(VoiceServiceRequestEvents events) =>
            SetListeners(events, true);

        /// <summary>
        /// Removes all listeners from another VoiceServiceRequestEvents
        /// </summary>
        public void RemoveListeners(VoiceServiceRequestEvents events) =>
            SetListeners(events, false);

        /// <summary>
        /// Adds or removes all listeners for the provided VoiceServiceRequestEvents
        /// event object based on the add boolean parameter
        /// </summary>
        public void SetListeners(VoiceServiceRequestEvents events, bool add)
        {
            OnInit.SetListener(events.OnInit.Invoke, add);
            OnStateChange.SetListener(events.OnStateChange.Invoke, add);
            OnAudioInputStateChange.SetListener(events.OnAudioInputStateChange.Invoke, add);
            OnStartListening.SetListener(events.OnStartListening.Invoke, add);
            OnStopListening.SetListener(events.OnStopListening.Invoke, add);
            OnAudioActivation.SetListener(events.OnAudioActivation.Invoke, add);
            OnAudioDeactivation.SetListener(events.OnAudioDeactivation.Invoke, add);
            OnSend.SetListener(events.OnSend.Invoke, add);
            OnRawResponse.SetListener(events.OnRawResponse.Invoke, add);
            OnPartialTranscription.SetListener(events.OnPartialTranscription.Invoke, add);
            OnFullTranscription.SetListener(events.OnFullTranscription.Invoke, add);
            OnPartialResponse.SetListener(events.OnPartialResponse.Invoke, add);
            OnFullResponse.SetListener(events.OnFullResponse.Invoke, add);
            OnDownloadProgressChange.SetListener(events.OnDownloadProgressChange.Invoke, add);
            OnUploadProgressChange.SetListener(events.OnUploadProgressChange.Invoke, add);
            OnCancel.SetListener(events.OnCancel.Invoke, add);
            OnFailed.SetListener(events.OnFailed.Invoke, add);
            OnSuccess.SetListener(events.OnSuccess.Invoke, add);
            OnComplete.SetListener(events.OnComplete.Invoke, add);
        }
    }

    /// <summary>
    /// A UnityEvent with a parameter of VoiceServiceRequest
    /// </summary>
    [Serializable]
    public class VoiceServiceRequestEvent : UnityEvent<VoiceServiceRequest> {}


    /// <summary>
    /// A base class to provide quick overridable methods that map to events from VoiceServiceRequestEvents.
    /// </summary>
    public class VoiceServiceRequestEventsWrapper
    {
        /// <summary>
        /// Adds all listeners for VoiceServiceRequestEvents to overridable methods
        /// </summary>
        public void Wrap(VoiceServiceRequestEvents events) =>
            SetListeners(events, true);

        /// <summary>
        /// Removes all listeners for the provided VoiceServiceRequestEvents event object.
        /// </summary>
        public void Unwrap(VoiceServiceRequestEvents events) =>
            SetListeners(events, false);

        /// <summary>
        /// Adds or removes all listeners for the provided VoiceServiceRequestEvents
        /// event object based on the add boolean parameter
        /// </summary>
        private void SetListeners(VoiceServiceRequestEvents events, bool add)
        {
            events.OnInit.SetListener(OnInit, add);
            events.OnStateChange.SetListener(OnStateChange, add);
            events.OnAudioInputStateChange.SetListener(OnAudioInputStateChange, add);
            events.OnStartListening.SetListener(OnStartListening, add);
            events.OnStopListening.SetListener(OnStopListening, add);
            events.OnAudioActivation.SetListener(OnAudioActivation, add);
            events.OnAudioDeactivation.SetListener(OnAudioDeactivation, add);
            events.OnSend.SetListener(OnSend, add);
            events.OnRawResponse.SetListener(OnRawResponse, add);
            events.OnPartialTranscription.SetListener(OnPartialTranscription, add);
            events.OnFullTranscription.SetListener(OnFullTranscription, add);
            events.OnPartialResponse.SetListener(OnPartialResponse, add);
            events.OnFullResponse.SetListener(OnFullResponse, add);
            events.OnDownloadProgressChange.SetListener(OnDownloadProgressChange, add);
            events.OnUploadProgressChange.SetListener(OnUploadProgressChange, add);
            events.OnCancel.SetListener(OnCancel, add);
            events.OnFailed.SetListener(OnFailed, add);
            events.OnSuccess.SetListener(OnSuccess, add);
            events.OnComplete.SetListener(OnComplete, add);
        }

        protected virtual void OnAudioInputStateChange(VoiceServiceRequest request)
        {
        }

        protected virtual void OnUploadProgressChange(VoiceServiceRequest request)
        {
        }

        protected virtual void OnDownloadProgressChange(VoiceServiceRequest request)
        {
        }

        protected virtual void OnStateChange(VoiceServiceRequest request)
        {
        }

        protected virtual void OnStopListening(VoiceServiceRequest request)
        {
        }

        protected virtual void OnStartListening(VoiceServiceRequest request)
        {
        }

        protected virtual void OnFullTranscription(string transcription)
        {
        }

        protected virtual void OnPartialTranscription(string transcription)
        {
        }

        protected virtual void OnRawResponse(string rawResponse)
        {
        }

        protected virtual void OnPartialResponse(WitResponseNode request)
        {
        }

        protected virtual void OnFullResponse(WitResponseNode request)
        {
        }

        protected virtual void OnAudioDeactivation(VoiceServiceRequest request)
        {
        }

        protected virtual void OnAudioActivation(VoiceServiceRequest request)
        {
        }

        protected virtual void OnSuccess(VoiceServiceRequest request)
        {
        }

        protected virtual void OnSend(VoiceServiceRequest request)
        {
        }

        protected virtual void OnInit(VoiceServiceRequest request)
        {
        }

        protected virtual void OnFailed(VoiceServiceRequest request)
        {
        }

        protected virtual void OnComplete(VoiceServiceRequest request)
        {

        }

        protected virtual void OnCancel(VoiceServiceRequest request)
        {

        }
    }
}
