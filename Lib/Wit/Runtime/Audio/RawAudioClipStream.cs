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
        /// Callback for new samples
        /// </summary>
        public Action<float[], int> OnAddSamples;

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
        /// <param name="samples">A list of decoded floats from 0f to 1f</param>
        public override void AddSamples(float[] newSamples)
        {
            // Add samples
            int start = AddedSamples;
            int length = Mathf.Min(newSamples.Length, SampleBuffer.Length - AddedSamples);
            if (length > 0)
            {
                // Copy samples
                Array.Copy(newSamples, 0, SampleBuffer, start, length);
                AddedSamples += length;

                // On added samples
                if (OnAddSamples != null)
                {
                    OnAddSamples?.Invoke(newSamples, length);
                }
            }

            // Update state
            UpdateState();
        }
    }
}
