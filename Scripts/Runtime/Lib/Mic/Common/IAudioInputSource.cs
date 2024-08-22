/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Data;

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// This interface defines the methods and properties required for an audio input source.
    /// It is used to start and stop recording, check if recording is in progress, and handle events related to recording.
    /// </summary>
    public interface IAudioInputSource
    {
        /// <summary>
        /// Invoked when the instance starts Recording.
        /// </summary>
        event Action OnStartRecording;

        /// <summary>
        /// Invoked when an AudioClip couldn't be created to start recording.
        /// </summary>
        event Action OnStartRecordingFailed;

        /// <summary>
        /// Invoked everytime an audio frame is collected. Includes the frame.
        /// </summary>
        event Action<int, float[], float> OnSampleReady;

        /// <summary>
        /// Invoked when the instance stop Recording.
        /// </summary>
        event Action OnStopRecording;

        /// <summary>
        /// Starts recording audio with the specified sample length.
        /// </summary>
        /// <param name="sampleLen">The length of the audio sample to record.</param>
        void StartRecording(int sampleLen);

        /// <summary>
        /// Stops recording audio.
        /// </summary>
        void StopRecording();

        /// <summary>
        /// Returns true if the audio input source is currently recording.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Settings determining how audio is encoded by the source.
        ///
        /// NOTE: Default values for AudioEncoding are server optimized to reduce latency.
        /// </summary>
        AudioEncoding AudioEncoding { get; }

        #region Muting

        /// <summary>
        /// Returns true if the audio source is currently muted
        /// </summary>
        bool IsMuted { get; }

        /// <summary>
        /// Invoked when the user mutes their input to this audio source
        /// </summary>
        event Action OnMicMuted;

        /// <summary>
        /// Invoked when the user unmutes their input to this audio source
        /// </summary>
        event Action OnMicUnmuted;

        #endregion
    }
}
