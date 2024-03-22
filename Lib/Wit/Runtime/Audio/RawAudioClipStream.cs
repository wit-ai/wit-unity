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
    public class RawAudioClipStream : AudioClipStream
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
            int bufferLengthSeconds = (int)WitConstants.ENDPOINT_TTS_DEFAULT_BUFFER_LENGTH)
            : base(newChannels, newSampleRate, newStreamReadyLength)
        {
            SampleBuffer = new float[newChannels * newSampleRate * bufferLengthSeconds];
        }

        /// <summary>
        /// Adds an array of samples to the current stream
        /// </summary>
        /// <param name="newSamples">A list of decoded floats from 0f to 1f</param>
        public override void AddSamples(float[] newSamples)
        {
            // Add samples
            int start = AddedSamples;
            int max = SampleBuffer.Length - start;
            int length = Mathf.Min(newSamples.Length, max);
            if (length > 0)
            {
                // Copy samples
                Array.Copy(newSamples, 0, SampleBuffer, start, length);
                AddedSamples += length;
            }

            // Update state
            UpdateState();
        }
    }
}
