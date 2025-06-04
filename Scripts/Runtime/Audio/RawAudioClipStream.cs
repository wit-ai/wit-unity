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
        public RawAudioClipStream(float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : this(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE, newReadyLength,
                newMaxLength) {}

        /// <summary>
        /// Generates sample buffer on construct
        /// </summary>
        public RawAudioClipStream(int newChannels, int newSampleRate,
            float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : base(newChannels, newSampleRate, newReadyLength, newMaxLength)
        {
            SampleBuffer = new float[Mathf.CeilToInt(newChannels * newSampleRate * newMaxLength)];
        }

        /// <summary>
        /// Adds a sample buffer to the current stream in its entirety.
        /// </summary>
        /// <param name="buffer">A buffer of decoded floats that were decoded</param>
        /// <param name="bufferOffset">The offset of the buffer to be used</param>
        /// <param name="bufferLength">The total samples to be used</param>
        public override void AddSamples(float[] buffer, int bufferOffset, int bufferLength)
        {
            // Ensure length added does not surpass buffer
            var sampleOffset = AddedSamples;
            var sampleLength = Mathf.Min(bufferLength, SampleBuffer.Length - sampleOffset);
            if (sampleLength <= 0)
            {
                return;
            }

            // Copy samples
            Array.Copy(buffer, bufferOffset, SampleBuffer, sampleOffset, sampleLength);

            // Update count & callback
            AddedSamples += sampleLength;
            OnAddSamples?.Invoke(SampleBuffer, sampleOffset, sampleLength);

            // Update state
            UpdateState();
        }

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, float[] destinationSamples, int destinationOffset = 0)
        {
            var sampleOffset = readOffset;
            var destinationLength = destinationSamples.Length - destinationOffset;
            var sampleLength = Mathf.Min(AddedSamples - sampleOffset, destinationLength);
            if (sampleLength > 0)
            {
                Array.Copy(SampleBuffer, sampleOffset, destinationSamples, destinationOffset, sampleLength);
                if (sampleLength < destinationLength)
                {
                    Array.Clear(destinationSamples, sampleLength, destinationLength - sampleLength);
                }
            }
            return sampleLength;
        }

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, AudioClipStreamSampleDelegate onReadSamples)
        {
            var sampleOffset = readOffset;
            var sampleLength = AddedSamples - sampleOffset;
            if (sampleLength > 0)
            {
                onReadSamples?.Invoke(SampleBuffer, sampleOffset, sampleLength);
            }
            return sampleLength;
        }
    }
}
