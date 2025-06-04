/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi;
using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A data container that stores all data within a float buffer
    /// </summary>
    public class BufferPoolAudioClipStream : BaseAudioClipStream
    {
        #region STATIC POOL
        // Pool to be used
        private static ArrayPool<float> _bufferPool;
        // Total number of pool references
        private static int _bufferPoolCount;
        // 1 second of audio
        private const int INDIVIDUAL_BUFFER_LENGTH = WitConstants.ENDPOINT_TTS_CHANNELS * WitConstants.ENDPOINT_TTS_SAMPLE_RATE;

        /// <summary>
        /// Increment pool when Audio System using it OnEnable
        /// </summary>
        public static void IncrementPool()
        {
            _bufferPoolCount++;
            if (_bufferPool == null)
            {
                _bufferPool = new ArrayPool<float>(INDIVIDUAL_BUFFER_LENGTH);
            }
        }

        /// <summary>
        /// Decrement pool when Audio System using it OnEnable
        /// </summary>
        public static void DecrementPool()
        {
            _bufferPoolCount--;
            if (_bufferPoolCount <= 0 && _bufferPool != null)
            {
                _bufferPool.Dispose();
                _bufferPool = null;
            }
        }
        #endregion STATIC POOL

        // All data that has been written to this stream
        private List<float[]> _sampleBuffers = new();

        /// <summary>
        /// Constructor for buffer pool that ensures multiple buffers are preloaded and ready
        /// </summary>
        public BufferPoolAudioClipStream(int newChannels, int newSampleRate,
            float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : base(newChannels, newSampleRate, newReadyLength, newMaxLength)
        {
            int clips = Mathf.CeilToInt((Channels * SampleRate * StreamMaxLength) / (float)INDIVIDUAL_BUFFER_LENGTH);
            _bufferPool.Preload(clips);
        }

        /// <summary>
        /// Unload the current samples
        /// </summary>
        public override void Unload()
        {
            base.Unload();
            for (int bufferIndex = 0; bufferIndex < _sampleBuffers.Count; bufferIndex++)
            {
                _bufferPool.Return(_sampleBuffers[bufferIndex]);
            }
            _sampleBuffers.Clear();
        }

        /// <inheritdoc />
        public override void AddSamples(float[] buffer, int bufferOffset, int bufferLength)
        {
            // Continue to fill buffers until all samples are written
            var bufferIndex = Mathf.FloorToInt(AddedSamples / (float)INDIVIDUAL_BUFFER_LENGTH);
            while (bufferLength > 0)
            {
                // Get buffer index
                while (_sampleBuffers.Count <= bufferIndex)
                {
                    _sampleBuffers.Add(_bufferPool.Get());
                }

                // Copy into top array
                var currentBuffer = _sampleBuffers[bufferIndex];
                var currentOffset = AddedSamples - (bufferIndex * INDIVIDUAL_BUFFER_LENGTH);
                var currentLength = Mathf.Min(bufferLength, buffer.Length - bufferOffset, currentBuffer.Length - currentOffset);
                Array.Copy(buffer, bufferOffset, currentBuffer, currentOffset, currentLength);

                // Increment
                AddedSamples += currentLength;
                bufferOffset += currentLength;
                bufferLength -= currentLength;
                bufferIndex++;

                // Perform callback
                OnAddSamples?.Invoke(currentBuffer, currentOffset, currentLength);
            }

            // Update state
            UpdateState();
        }

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, AudioClipStreamSampleDelegate onReadSamples)
            => ReadSamples(readOffset, onReadSamples, null, 0);

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, float[] destinationSamples, int destinationOffset = 0)
            => ReadSamples(readOffset, null, destinationSamples, destinationOffset);

        // Read with either the sample callback or destination array
        private int ReadSamples(int readOffset, AudioClipStreamSampleDelegate onReadSamples, float[] destinationSamples, int destinationOffset)
        {
            // Ignore if no samples are available
            var available = AddedSamples - readOffset;
            if (available <= 0)
            {
                return 0;
            }

            // Copy from buffers
            var length = 0;
            var bufferIndex = Mathf.FloorToInt(readOffset / (float)INDIVIDUAL_BUFFER_LENGTH);
            while (bufferIndex < _sampleBuffers.Count)
            {
                // Copy to destination
                var currentBuffer = _sampleBuffers[bufferIndex];
                var currentOffset = readOffset - (bufferIndex * INDIVIDUAL_BUFFER_LENGTH);
                var currentLength = Mathf.Min(available, currentBuffer.Length - currentOffset);

                // Send to on read delegate if available
                if (onReadSamples != null)
                {
                    onReadSamples.Invoke(currentBuffer, currentOffset, currentLength);
                }
                // Copy to destination array if available
                if (destinationSamples != null)
                {
                    currentLength = Mathf.Min(currentLength, destinationSamples.Length - destinationOffset);
                    Array.Copy(currentBuffer, currentOffset, destinationSamples, destinationOffset, currentLength);
                    destinationOffset += currentLength;

                    // Exit after increment if destination is full
                    if (destinationSamples.Length - destinationOffset == 0)
                    {
                        available = currentLength;
                    }
                }

                // Increment
                readOffset += currentLength;
                available -= currentLength;
                length += currentLength;
                bufferIndex++;

                // Exit if no more to be written
                if (available == 0)
                {
                    break;
                }
            }

            // Return length
            return length;
        }
    }
}
