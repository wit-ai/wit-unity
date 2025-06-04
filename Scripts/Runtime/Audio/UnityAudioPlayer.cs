/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using Meta.WitAi;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// Unity specific audio player that will play any IAudioClipStream that includes an IAudioClipProvider
    /// or RawAudioClipStreams via AudioClip streaming.
    /// </summary>
    [Serializable]
    public class UnityAudioPlayer : BaseAudioPlayer, IAudioSourceProvider
    {
        /// <summary>
        /// Audio source to be used for text-to-speech playback
        /// </summary>
        [Header("Playback Settings")]
        [Tooltip("Audio source to be used for text-to-speech playback")]
        [SerializeField] private AudioSource _audioSource;
        public AudioSource AudioSource => _audioSource;

        /// <summary>
        /// Duplicates audio source reference on awake instead of using it directly.
        /// </summary>
        [Tooltip("Duplicates audio source reference on awake instead of using it directly.")]
        [SerializeField] private bool _cloneAudioSource = false;
        public bool CloneAudioSource => _cloneAudioSource;

        /// <summary>
        /// Whether the player is currently playing back audio
        /// </summary>
        public override bool IsPlaying => AudioSource != null && AudioSource.isPlaying;

        /// <summary>
        /// Elapsed samples can be used if not using a clip buffer
        /// </summary>
        public override bool CanSetElapsedSamples => AudioSource != null && _clipBuffer == null;

        /// <summary>
        /// Use time samples directly if possible, otherwise leave it to TTS to determine elapsed samples
        /// </summary>
        public override int ElapsedSamples => CanSetElapsedSamples ? AudioSource.timeSamples : 0;

        // Small locally generated audio clip that is used to buffer audio to the Unity AudioSource
        private AudioClip _clipBuffer;
        private int _clipBufferOffset;
        private int _clipBufferLoops;
        private int _clipBufferMaxLength;
        private int ReadAbsoluteOffset => _clipBufferOffset + _clipBufferLoops * _clipBufferMaxLength;

        private void Awake()
        {
            // Find base audio source if possible
            if (!AudioSource)
            {
                _audioSource = gameObject.GetComponentInChildren<AudioSource>();
            }
        }

        /// <summary>
        /// Performs all player initialization
        /// </summary>
        public override void Init()
        {
            // Find base audio source if possible, checking the audio source again in Init just in case the audio source
            // was delayed in spawning.
            if (!AudioSource)
            {
                _audioSource = gameObject.GetComponentInChildren<AudioSource>();
            }

            // Duplicate audio source
            if (CloneAudioSource)
            {
                // Create new audio source
                AudioSource instance = new GameObject($"{gameObject.name}_AudioOneShot").AddComponent<AudioSource>();
                instance.PreloadCopyData();

                // Move into this transform & default to 3D audio
                if (AudioSource == null)
                {
                    instance.transform.SetParent(transform, false);
                    instance.spread = 1f;
                }

                // Move into audio source & copy source values
                else
                {
                    instance.transform.SetParent(AudioSource.transform, false);
                    instance.Copy(AudioSource);
                }

                // Reset instance's transform
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Apply
                _audioSource = instance;
            }

            // Setup audio source settings
            AudioSource.playOnAwake = false;
        }

        /// <summary>
        /// A string returned to describe any reasons playback
        /// is currently unavailable
        /// </summary>
        public override string GetPlaybackErrors()
        {
            if (AudioSource == null)
            {
                return "Audio source is missing";
            }
            return string.Empty;
        }

        /// <summary>
        /// Get local audio clip
        /// </summary>
        protected virtual AudioClip CreateStreamedClip(int offset, int channels, int sampleRate)
        {
            // Get buffer offset to be used prior to audio clip creation
            _clipBufferMaxLength = channels * sampleRate; // 1 second
            _clipBufferLoops = Mathf.FloorToInt(offset / (float)_clipBufferMaxLength);
            _clipBufferOffset = offset - (_clipBufferLoops * _clipBufferMaxLength);

            // Only generate new clip buffer if the previous has changed
            if (_clipBuffer == null
                || channels != _clipBuffer.channels
                || sampleRate != _clipBuffer.samples)
            {
                // Destroy previous if applicable
                if (_clipBuffer != null)
                {
                    Destroy(_clipBuffer);
                    _clipBuffer = null;
                }

                // Create new buffer
                _clipBuffer = AudioClip.Create("StreamedAudioClip", _clipBufferMaxLength, channels, sampleRate,
                    true, OnReadRawSamples, OnSetRawPosition);
            }

            // Return clip buffer
            return _clipBuffer;
        }

        /// <summary>
        /// Sets audio clip via an IAudioClipProvider or generates one if using a
        /// RawAudioClipStream.  Then begins playback at a specified offset.
        /// </summary>
        /// <param name="offsetSamples">The starting offset of the clip in samples</param>
        protected override void Play(int offsetSamples = 0)
        {
            // Get new clip
            AudioClip newClip = null;
            if (ClipStream is IAudioClipProvider uacs)
            {
                newClip = uacs.Clip;
            }
            else if (ClipStream is BaseAudioClipStream)
            {
                newClip = CreateStreamedClip(offsetSamples, ClipStream.Channels, ClipStream.SampleRate);
            }

            // Null clip
            if (newClip == null)
            {
                VLog.E($"{GetType()} cannot play null AudioClip");
                return;
            }

            // Play audio clip
            AudioSource.clip = newClip;
            AudioSource.loop = _clipBuffer != null;
            AudioSource.timeSamples = _clipBuffer != null ? 0 : offsetSamples;
            AudioSource.Play();
        }

        // Ignore set offset position
        private void OnSetRawPosition(int offset) {}

        // Read raw samples
        private void OnReadRawSamples(float[] samples)
        {
            _clipBufferOffset += ClipStream.ReadSamples(ReadAbsoluteOffset, samples);
            if (_clipBufferOffset >= _clipBufferMaxLength)
            {
                _clipBufferOffset -= _clipBufferMaxLength;
                _clipBufferLoops++;
            }
        }

        /// <summary>
        /// Performs a pause if the current clip is playing
        /// </summary>
        public override void Pause()
        {
            if (IsPlaying)
            {
                AudioSource.Pause();
            }
        }

        /// <summary>
        /// Performs a resume if the current clip is paused
        /// </summary>
        public override void Resume()
        {
            if (!IsPlaying)
            {
                AudioSource.UnPause();
            }
        }

        /// <summary>
        /// Stops playback & removes the current clip reference
        /// </summary>
        public override void Stop()
        {
            if (IsPlaying)
            {
                AudioSource.Stop();
            }
            AudioSource.clip = null;
            base.Stop();
        }

        /// <summary>
        /// Destroy clip if applicable
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_clipBuffer != null)
            {
                Destroy(_clipBuffer);
                _clipBuffer = null;
            }
        }
    }
}
