/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.Voice
{
    [Serializable]
    public class TranscriptionRequestEvents<TUnityEvent>
        : VoiceRequestEvents<TUnityEvent>,
            ITranscriptionRequestEvents<TUnityEvent>
        where TUnityEvent : UnityEventBase
    {
        /// <summary>
        /// Called when audio input state changes
        /// </summary>
        public TUnityEvent OnAudioInputStateChange { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called when audio is activated for this audio transcription request
        /// </summary>
        public TUnityEvent OnAudioActivation { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called when audio is being listened to for this request
        /// </summary>
        public TUnityEvent OnStartListening { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called when audio is deactivated for this audio transcription request
        /// </summary>
        public TUnityEvent OnAudioDeactivation { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called when audio is no longer being listened to for this request
        /// </summary>
        public TUnityEvent OnStopListening { get; private set; } = Activator.CreateInstance<TUnityEvent>();

        /// <summary>
        /// Called on request transcription while audio is still being analyzed
        /// </summary>
        public TUnityEvent OnEarlyTranscription { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called on request transcription when audio has been completely transferred
        /// </summary>
        public TUnityEvent OnFinalTranscription { get; private set; } = Activator.CreateInstance<TUnityEvent>();
    }
}
