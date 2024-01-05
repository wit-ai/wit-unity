/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice;
using Meta.WitAi.Data;
using UnityEngine;

namespace Meta.WitAi.Events
{
    /// <summary>
    /// A collection of events related to audio being processed by the <see cref="AudioBuffer"/>
    /// </summary>
    [Serializable]
    public class AudioBufferEvents
    {
        /// <summary>
        /// Callbacks as audio state changes
        /// </summary>
        public Action<VoiceAudioInputState> OnAudioStateChange;

        /// <summary>
        /// Fired when a sample is ready to be read
        /// <param name="marker">The marker in the AudioBuffer's ringbuffer where the sample starts</param>
        /// <param name="levelMax">The maximum volume (0-1) seen in this sample</param>
        /// </summary>
        public delegate void OnSampleReadyEvent(RingBuffer<byte>.Marker marker, float levelMax);

        /// <summary>
        /// Fired when a sample is ready to be read
        /// <param name="marker">The marker in the AudioBuffer's ringbuffer where the sample starts</param>
        /// <param name="levelMax">The maximum volume (0-1) seen in this sample</param>
        /// </summary>
        public OnSampleReadyEvent OnSampleReady;

        /// <summary>
        /// Fired when a sample is received from an audio input source
        /// <param name="samples">The raw float sample buffer</param>
        /// <param name="sampleCount">The number of samples in the buffer</param>
        /// <param name="maxLevel">The max volume in this sample</param>
        /// </summary>
        [Tooltip("Fired when a sample is received from an audio input source")]
        public WitSampleEvent OnSampleReceived = new WitSampleEvent();

        /// <summary>
        /// Fired when the volume level of an input source changes
        /// <param name="level">The level of the volume on that input (0-1)</param>
        /// </summary>
        [Tooltip("Called when the volume level of the mic input has changed")]
        public WitMicLevelChangedEvent OnMicLevelChanged = new WitMicLevelChangedEvent();

        /// <summary>
        /// Fired when data is ready to be sent to various voice services
        /// <param name="buffer">The byte buffer about to be sent</param>
        /// <param name="offset">The offset into the buffer that should be read</param>
        /// <param name="length">The length of the data to be sent</param>
        /// </summary>
        [Header("Data")]
        public WitByteDataEvent OnByteDataReady = new WitByteDataEvent();

        /// <summary>
        /// Fired when byte data is sent to various voice services
        /// <param name="buffer">The byte buffer about to be sent</param>
        /// <param name="offset">The offset into the buffer that should be read</param>
        /// <param name="length">The length of the data to be sent</param>
        /// </summary>
        public WitByteDataEvent OnByteDataSent = new WitByteDataEvent();
    }
}
