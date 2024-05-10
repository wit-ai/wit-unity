/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi;
using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A data container that stores all data within a float buffer
    /// </summary>
    public class RawAudioClipStream : BaseAudioClipStream
    {
        /// <summary>
        /// Sample buffer containing all raw float data
        /// </summary>
        public float[] SampleBuffer { get; }

        /// <summary>
        /// Generates sample buffer on construct
        /// </summary>
        public RawAudioClipStream(int newChannels, int newSampleRate,
            float newStreamReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float bufferLengthSeconds = WitConstants.ENDPOINT_TTS_DEFAULT_BUFFER_LENGTH)
            : base(newChannels, newSampleRate, newStreamReadyLength)
        {
            SampleBuffer = new float[newChannels * newSampleRate * Mathf.CeilToInt(bufferLengthSeconds)];
        }

        /// <summary>
        /// Adds an array of samples to the current stream
        /// </summary>
        /// <param name="samples">A list of decoded floats from 0f to 1f</param>
        /// <param name="offset">The index of samples to begin adding from</param>
        /// <param name="length">The total number of samples that should be appended</param>
        public override void AddSamples(float[] samples, int offset, int length)
        {
            // Ensure length added does not surpass buffer
            var localOffset = AddedSamples;
            var localMax = SampleBuffer.Length - localOffset;
            var localLength = Mathf.Min(length, localMax);
            if (localLength <= 0)
            {
                return;
            }

            // Copy samples
            Array.Copy(samples, offset, SampleBuffer, localOffset, localLength);

            // Update count & callbacks
            base.AddSamples(samples, offset, localLength);
        }
    }
}
