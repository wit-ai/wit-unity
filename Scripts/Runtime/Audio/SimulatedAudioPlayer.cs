/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice.Logging;
using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// Audio player that simulates playback without playing audio.  Useful for server based playback simulation.
    /// </summary>
    [LogCategory(LogCategory.Audio | LogCategory.Output)]
    public class SimulatedAudioPlayer : BaseAudioPlayer
    {
        /// <summary>
        /// Whether the player is currently playing back audio
        /// </summary>
        public override bool IsPlaying => _playing;

        /// <summary>
        /// Elapsed samples can be used for animation progress
        /// </summary>
        public override bool CanSetElapsedSamples => true;

        /// <summary>
        /// The currently elapsed sample count
        /// </summary>
        public override int ElapsedSamples => ClipStream == null ? 0 : GetSamplesFromSeconds(_elapsedTime);

        /// <summary>
        /// The current elapsed time in seconds
        /// </summary>
        private float _elapsedTime = 0;

        /// <summary>
        /// Whether or not currently paused
        /// </summary>
        private bool _playing = false;

        /// <summary>
        /// Handles error logging
        /// </summary>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Audio);

        /// <summary>
        /// No initialization required
        /// </summary>
        public override void Init() {}

        /// <summary>
        /// No playback errors possible
        /// </summary>
        public override string GetPlaybackErrors() => string.Empty;

        /// <summary>
        /// Begins fake playback at a specified offset.
        /// </summary>
        /// <param name="offsetSamples">The starting offset of the clip in samples</param>
        protected override void Play(int offsetSamples = 0)
        {
            // Null clip
            if (ClipStream == null)
            {
                Logger.Error("{0} cannot play null Audio clip stream", GetType().Name);
                return;
            }

            // Set offset & play
            _elapsedTime = GetSecondsFromSamples(offsetSamples);
            _playing = true;
        }

        /// <summary>
        /// Performs a pause if the current clip is playing
        /// </summary>
        public override void Pause()
        {
            if (IsPlaying)
            {
                _playing = false;
            }
        }

        /// <summary>
        /// Performs a resume if the current clip is paused
        /// </summary>
        public override void Resume()
        {
            if (!IsPlaying)
            {
                _playing = true;
            }
        }

        /// <summary>
        /// Stops playback & removes the current clip reference
        /// </summary>
        public override void Stop()
        {
            if (IsPlaying)
            {
                _playing = false;
            }
            base.Stop();
        }

        /// <summary>
        /// Track playback
        /// </summary>
        private void Update()
        {
            // Skip if not playing
            if (!IsPlaying || ClipStream == null)
            {
                return;
            }

            // Iterate elapsed samples
            _elapsedTime += Time.deltaTime;

            // Complete
            if (ClipStream.IsComplete && _elapsedTime >= ClipStream.Length)
            {
                _playing = false;
            }
        }

        /// <summary>
        /// Returns samples from elapsed time
        /// </summary>
        private int GetSamplesFromSeconds(float elapsedSeconds)
            => Mathf.FloorToInt(elapsedSeconds * ClipStream.Channels * ClipStream.SampleRate);

        /// <summary>
        /// Returns samples from elapsed time
        /// </summary>
        private float GetSecondsFromSamples(int samples)
            => (float)samples / (ClipStream.Channels * ClipStream.SampleRate);
    }
}
