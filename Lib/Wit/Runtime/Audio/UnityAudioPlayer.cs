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
    public class UnityAudioPlayer : AudioPlayer, IAudioSourceProvider
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
        /// Elapsed samples can be used for animation progress
        /// </summary>
        public override bool CanSetElapsedSamples => true;

        /// <summary>
        /// The currently elapsed sample count
        /// </summary>
        public override int ElapsedSamples => AudioSource != null ? AudioSource.timeSamples : 0;

        // Local clip adjustments
        private bool _local = false;
        private int _offset = 0;

        /// <summary>
        /// Performs all player initialization
        /// </summary>
        public override void Init()
        {
            // Find base audio source if possible
            if (AudioSource == null)
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
            else if (ClipStream is RawAudioClipStream rawAudioClipStream)
            {
                newClip = AudioClip.Create("CustomClip", rawAudioClipStream.SampleBuffer.Length,
                    rawAudioClipStream.Channels, rawAudioClipStream.SampleRate, true,
                    OnReadRawSamples, OnSetRawPosition);
                _local = true;
            }

            // Null clip
            if (newClip == null)
            {
                VLog.E($"{GetType()} cannot play null AudioClip");
                return;
            }

            // Play audio clip
            AudioSource.loop = false;
            AudioSource.clip = newClip;
            AudioSource.timeSamples = offsetSamples;
            AudioSource.Play();
        }

        // Set offset position
        private void OnSetRawPosition(int offset)
        {
            _offset = offset;
        }

        // Read raw sample
        private void OnReadRawSamples(float[] samples)
        {
            // Length of copied samples
            var length = 0;

            // Copy as many samples as possible from the raw sample buffer
            if (ClipStream is RawAudioClipStream rawAudioClipStream)
            {
                var start = _offset;
                var available = Mathf.Max(0, rawAudioClipStream.AddedSamples - start);
                length = Mathf.Min(samples.Length, available);
                if (length > 0)
                {
                    Array.Copy(rawAudioClipStream.SampleBuffer, start, samples, 0, length);
                    _offset += length;
                }
            }

            // Clear unavailable samples
            if (length < samples.Length)
            {
                int dif = samples.Length - length;
                Array.Clear(samples, length, dif);
                _offset += dif;
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
            if (_local)
            {
                if (AudioSource.clip != null)
                {
                    Destroy(AudioSource.clip);
                }
                _local = false;
            }
            AudioSource.clip = null;
            base.Stop();
        }
    }
}
