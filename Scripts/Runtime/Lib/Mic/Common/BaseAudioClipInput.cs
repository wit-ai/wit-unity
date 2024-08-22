/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using Meta.Voice;
using Meta.WitAi.Data;
using Meta.WitAi.Interfaces;
using UnityEngine;

namespace Meta.WitAi.Lib
{
    /// <summary>
    /// An abstract class for IAudioInputSources that use audio clips
    /// </summary>
    public abstract class BaseAudioClipInput : MonoBehaviour, IAudioInputSource, IAudioLevelRangeProvider
    {
        /// <summary>
        /// The audio clip generated
        /// </summary>
        public abstract AudioClip Clip { get; }
        /// <summary>
        /// The current total samples written to the audio clip
        /// </summary>
        public abstract int ClipPosition { get; }
        /// <summary>
        /// Whether audio can be activated
        /// </summary>
        public abstract bool CanActivateAudio { get; }
        /// <summary>
        /// Whether audio should be activated as soon as enabled
        /// </summary>
        public virtual bool ActivateOnEnable => false;

        /// <summary>
        /// The audio channels expected for this
        /// </summary>
        public virtual int AudioChannels => 1;
        /// <summary>
        /// The audio sample rate that should be captured at
        /// </summary>
        public virtual int AudioSampleRate => WitConstants.ENDPOINT_SPEECH_SAMPLE_RATE;
        /// <summary>
        /// The total length in ms for the samples
        /// </summary>
        public virtual int AudioSampleLength { get; private set; }

        /// <summary>
        /// Ignore audio input below 0.5f magnitude
        /// </summary>
        public virtual float MinAudioLevel => 0.5f;
        /// <summary>
        /// Allow max audio input magnitude
        /// </summary>
        public virtual float MaxAudioLevel => 1f;

        /// <summary>
        /// Generates an audio encoding object using the existing settings
        /// </summary>
        public AudioEncoding AudioEncoding
        {
            get
            {
                if (_audioEncoding == null)
                {
                    _audioEncoding = new AudioEncoding();
                }
                _audioEncoding.numChannels = AudioChannels;
                _audioEncoding.samplerate = AudioSampleRate;
                _audioEncoding.encoding = AudioEncoding.ENCODING_SIGNED;
                return _audioEncoding;
            }
        }

        private AudioEncoding _audioEncoding;

        /// <summary>
        /// Whether audio is activated or not, independent of record state
        /// </summary>
        public VoiceAudioInputState ActivationState { get; private set; } = VoiceAudioInputState.Off;
        /// <summary>
        /// Callbacks for audio activation, independent of record state
        /// </summary>
        public event Action<VoiceAudioInputState> OnActivationStateChange;

        /// <summary>
        /// Whether the audio has begun recording or not
        /// </summary>
        public virtual bool IsRecording { get; private set; }
        /// <summary>
        /// Callback when successfully started recording
        /// </summary>
        public event Action OnStartRecording;
        /// <summary>
        /// Callback if recording request failed
        /// </summary>
        public event Action OnStartRecordingFailed;
        /// <summary>
        /// Callback when successfully stopped recording
        /// </summary>
        public event Action OnStopRecording;

        /// <summary>
        /// Callback for audio sample read from Audio Clip
        /// </summary>
        public event Action<int, float[], float> OnSampleReady;

        /// <summary>
        /// Setter for activation state changes
        /// </summary>
        protected void SetActivationState(VoiceAudioInputState newActivationState)
        {
            ActivationState = newActivationState;
            OnActivationStateChange?.Invoke(ActivationState);
        }

        #region Muting

        /// <inheritdoc />
        public virtual bool IsMuted { get; private set; } = false;

        /// <inheritdoc />
        public event Action OnMicMuted;

        /// <inheritdoc />
        public event Action OnMicUnmuted;

        protected virtual void SetMuted(bool muted)
        {
            if (IsMuted != muted)
            {
                IsMuted = muted;
                if(IsMuted) OnMicMuted?.Invoke();
                else OnMicUnmuted?.Invoke();
            }
        }
        #endregion

        #region ACTIVATION
        // Audio activation coroutine
        private Coroutine _activateCoroutine;

        /// <summary>
        /// If activate on enable, begin activation immediately
        /// </summary>
        protected virtual void OnEnable()
        {
            if (ActivateOnEnable
                && ActivationState != VoiceAudioInputState.Activating
                && ActivationState != VoiceAudioInputState.On)
            {
                ActivateAudio();
            }
        }

        /// <summary>
        /// Performs the audio activation
        /// </summary>
        private void ActivateAudio()
        {
            // Already active, throw a failure
            if (ActivationState == VoiceAudioInputState.On
                || ActivationState == VoiceAudioInputState.Activating)
            {
                VLog.W(GetType().Name, $"Cannot activate when audio is already {ActivationState}");
                return;
            }
            // Cannot activate while deactivated
            if (!gameObject.activeInHierarchy)
            {
                VLog.W(GetType().Name,"Audio activation is disabled while GameObject is inactive");
                return;
            }
            // Cannot activate
            if (!CanActivateAudio)
            {
                VLog.W(GetType().Name,"Audio activation is currently restricted");
                return;
            }

            // Stop previous activation
            if (_activateCoroutine != null)
            {
                StopCoroutine(_activateCoroutine);
                _activateCoroutine = null;
            }

            // Now activating
            _activateCoroutine = StartCoroutine(PerformActivation());
        }

        // Performs an activation via the abstract method
        private IEnumerator PerformActivation()
        {
            // Now activating
            SetActivationState(VoiceAudioInputState.Activating);

            // Perform audio activation
            yield return HandleActivation();

            // Activation successful
            if (ActivationState == VoiceAudioInputState.Activating)
            {
                SetActivationState(VoiceAudioInputState.On);
            }

            // Complete
            _activateCoroutine = null;
        }

        /// <summary>
        /// Perform audio activation in child class
        /// </summary>
        protected abstract IEnumerator HandleActivation();
        #endregion ACTIVATION

        #region DEACTIVATION
        /// <summary>
        /// Stop recording if disabled
        /// </summary>
        protected virtual void OnDisable()
        {
            if (IsRecording)
            {
                StopRecording();
            }
            if (ActivateOnEnable
                     && ActivationState != VoiceAudioInputState.Deactivating
                     && ActivationState != VoiceAudioInputState.Off)
            {
                DeactivateAudio();
            }
        }

        /// <summary>
        /// Performs an audio deactivation
        /// </summary>
        private void DeactivateAudio()
        {
            // Already deactivated/ing, throw a failure
            if (ActivationState == VoiceAudioInputState.Off
                || ActivationState == VoiceAudioInputState.Deactivating)
            {
                VLog.W(GetType().Name, $"Cannot deactivate when audio is already {ActivationState}");
                return;
            }

            // Stop activation if applicable
            if (_activateCoroutine != null)
            {
                StopCoroutine(_activateCoroutine);
                _activateCoroutine = null;
            }

            // Now deactivating
            SetActivationState(VoiceAudioInputState.Deactivating);

            // Perform audio deactivating
            HandleDeactivation();

            // Deactivating successful
            if (ActivationState == VoiceAudioInputState.Deactivating)
            {
                SetActivationState(VoiceAudioInputState.Off);
            }
        }

        /// <summary>
        /// Deactivate current audio immediately
        /// </summary>
        protected abstract void HandleDeactivation();
        #endregion PERSISTENCE

        #region RECORDING
        // Record coroutine
        private Coroutine _recordCoroutine;

        /// <summary>
        /// Begins reading with a specified number of ms per sample
        /// </summary>
        public virtual void StartRecording(int sampleDurationMS)
        {
            // Already recording, throw a failure
            if (IsRecording)
            {
                VLog.W(GetType().Name, $"Cannot start recording when already recording");
                OnStartRecordingFailed?.Invoke();
                return;
            }

            // Now recording
            IsRecording = true;

            // Store record sample length
            AudioSampleLength = sampleDurationMS;

            // Stop previous coroutine
            if (_recordCoroutine != null)
            {
                StopCoroutine(_recordCoroutine);
                _recordCoroutine = null;
            }

            // Start recording
            _recordCoroutine = StartCoroutine(ReadRawAudio());
        }

        /// <summary>
        /// Read raw audio as long as possible
        /// </summary>
        private IEnumerator ReadRawAudio()
        {
            // Performs activation if needed
            if (ActivationState != VoiceAudioInputState.On)
            {
                // Activate
                if (ActivationState != VoiceAudioInputState.Activating)
                {
                    ActivateAudio();
                }
                // Wait for activation completion
                while (ActivationState == VoiceAudioInputState.Activating)
                {
                    yield return null;
                }
                // Failed
                if (ActivationState != VoiceAudioInputState.On)
                {
                    IsRecording = false;
                    OnStartRecordingFailed?.Invoke();
                    yield break;
                }
            }

            // Get audio
            AudioClip micClip = Clip;
            if (micClip == null)
            {
                VLog.W(GetType().Name, "No AudioClip found following activation");
                IsRecording = false;
                OnStartRecordingFailed?.Invoke();
                yield break;
            }

            // Officially on
            OnStartRecording?.Invoke();

            // Get mic data & determine total samples per sample duration
            float micSampleLength = AudioSampleLength / 1000f;
            int micSampleChannels = AudioChannels;
            int micSampleRate = AudioSampleRate;
            int totalSamples = Mathf.CeilToInt(micSampleChannels * micSampleRate * micSampleLength);
            float[] samples = new float[totalSamples];

            // Last mic position from start of buffer
            int prevMicPosition = ClipPosition;
            // Last mic position from start of recording
            int readAbsPosition = prevMicPosition;
            // Total loops of AudioClip buffer reads
            int loops = 0;

            // Continue as long as clip exists & is recording
            while (micClip != null && IsRecording)
            {
                // Wait a moment
                yield return null;

                // Continue as long as clip exists & has new data
                bool isNewDataAvailable = true;
                while (micClip != null && isNewDataAvailable)
                {
                    // Get current mic position
                    var curMicPosition = ClipPosition;

                    // If before previous, clip looped
                    if (curMicPosition < prevMicPosition)
                        loops++;

                    // Store previous position
                    prevMicPosition = curMicPosition;

                    // Get mic samples since recording began
                    var micAbsPosition = loops * micClip.samples + curMicPosition;

                    // Check if there are enough samples available for another read
                    var desReadAbsPosition = readAbsPosition + samples.Length;
                    isNewDataAvailable = desReadAbsPosition < micAbsPosition;
                    if (isNewDataAvailable && micClip.GetData(samples, readAbsPosition % micClip.samples))
                    {
                        // Return newly decoded sample
                        OnSampleReady?.Invoke(0, samples, 0f); // TODO: Adjust delegate
                        // Increase total read samples to previously desired destination
                        readAbsPosition = desReadAbsPosition;
                    }
                }
            }

            // Stop recording
            if (IsRecording)
            {
                StopRecording();
            }
        }

        /// <summary>
        /// Stop recording audio from the provided mic source
        /// </summary>
        public virtual void StopRecording()
        {
            // Not recording, throw a failure
            if (!IsRecording)
            {
                VLog.E(GetType().Name, $"Cannot stop recording when not recording");
                return;
            }

            // Deactivate audio if desired
            if (!ActivateOnEnable
                || !gameObject.activeInHierarchy)
            {
                DeactivateAudio();
            }

            // No longer recording
            IsRecording = false;

            // Recording complete
            OnStopRecording?.Invoke();
        }
        #endregion RECORDING
    }
}
