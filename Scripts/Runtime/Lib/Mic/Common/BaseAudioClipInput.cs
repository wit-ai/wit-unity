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
    public abstract class BaseAudioClipInput : MonoBehaviour, IAudioInputSource
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
        /// Generates an audio encoding object using the existing settings
        /// </summary>
        public AudioEncoding AudioEncoding => new AudioEncoding()
        {
            numChannels = AudioChannels,
            samplerate = AudioSampleRate,
            encoding = AudioEncoding.ENCODING_SIGNED
        };

        /// <summary>
        /// Callback when successfully started recording
        /// </summary>
        public event Action OnStartRecording;

        /// <summary>
        /// Callback if recording request failed
        /// </summary>
        public event Action OnStartRecordingFailed;

        /// <summary>
        /// Callback for audio sample read from Audio Clip
        /// </summary>
        public event Action<int, float[], float> OnSampleReady;

        /// <summary>
        /// Callback when successfully stopped recording
        /// </summary>
        public event Action OnStopRecording;

        /// <summary>
        /// Whether the audio has begun recording or not
        /// </summary>
        public virtual bool IsRecording => AudioState == VoiceAudioInputState.On || AudioState == VoiceAudioInputState.Activating;
        /// <summary>
        /// Whether recording & sending audio data back to
        /// </summary>
        public VoiceAudioInputState AudioState { get; private set; }
        /// <summary>
        /// Callback when successfully stopped recording
        /// </summary>
        public event Action<VoiceAudioInputState> OnAudioStateChange;

        // Record coroutine
        private Coroutine _coroutine;

        /// <summary>
        /// Begins reading with a specified number of ms per sample
        /// </summary>
        public virtual void StartRecording(int sampleDurationMS)
        {
            // Already recording, throw a failure
            if (AudioState != VoiceAudioInputState.Off)
            {
                VLog.W(GetType().Name, "Cannot start recording when audio is {AudioState}");
                OnStartRecordingFailed?.Invoke();
                return;
            }
            // Cannot activate
            if (!CanActivateAudio)
            {
                VLog.W(GetType().Name,"Cannot currently activate audio");
                OnStartRecordingFailed?.Invoke();
                return;
            }

            // Begin activation
            AudioSampleLength = sampleDurationMS;
            SetAudioState(VoiceAudioInputState.Activating);

            // Start recording
            _coroutine = StartCoroutine(ReadRawAudio());
        }

        /// <summary>
        /// Setter for audio state changes
        /// </summary>
        protected void SetAudioState(VoiceAudioInputState newAudioState)
        {
            AudioState = newAudioState;
            OnAudioStateChange?.Invoke(AudioState);
        }

        /// <summary>
        /// Perform audio activation
        /// </summary>
        protected abstract IEnumerator ActivateAudio();

        /// <summary>
        /// Read raw audio as long as possible
        /// </summary>
        private IEnumerator ReadRawAudio()
        {
            // Performs audio activation
            yield return ActivateAudio();

            // Get audio
            AudioClip micClip = Clip;
            if (micClip == null)
            {
                VLog.W(GetType().Name, "No AudioClip found following activation");
                SetAudioState(VoiceAudioInputState.Off);
                OnStartRecordingFailed?.Invoke();
                yield break;
            }

            // Officially on
            SetAudioState(VoiceAudioInputState.On);
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
            while (micClip != null && AudioState == VoiceAudioInputState.On)
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
            if (AudioState == VoiceAudioInputState.On)
            {
                StopRecording();
            }
        }

        /// <summary>
        /// Stop recording audio from the provided mic source
        /// </summary>
        public virtual void StopRecording()
        {
            // Already recording, throw a failure
            if (AudioState != VoiceAudioInputState.On)
            {
                VLog.E(GetType().Name, $"Cannot stop recording when audio is {AudioState}");
                return;
            }

            // Begin deactivation
            SetAudioState(VoiceAudioInputState.Deactivating);

            // Stop previous coroutine
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
                _coroutine = null;
            }

            // Stop recording
            _coroutine = StartCoroutine(DeactivateAudio());
        }

        /// <summary>
        /// Deactivate current audio
        /// </summary>
        protected virtual IEnumerator DeactivateAudio()
        {
            SetAudioState(VoiceAudioInputState.Off);
            OnStopRecording?.Invoke();
            yield break;
        }
    }
}
