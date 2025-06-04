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
using Meta.WitAi.Data;
using UnityEngine;

namespace Meta.Voice.Audio
{
    public class RingBufferRawAudioClipStream : BaseAudioClipStream
    {
        private readonly RingBuffer<float> _buffer;
        private RingBuffer<float>.Marker _marker;
        public Action OnCompletedBufferPlayback;
        public int BufferLength => _buffer.Capacity;

        public RingBufferRawAudioClipStream(int newChannels, int newSampleRate,
            float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : base(newChannels, newSampleRate, newReadyLength, newMaxLength)
        {
            _buffer = new RingBuffer<float>(Mathf.CeilToInt(newChannels * newSampleRate * newMaxLength));
            _marker = _buffer.CreateMarker();
        }

        /// <inheritdoc/>
        public override void AddSamples(float[] buffer, int bufferOffset, int bufferLength)
        {
            // Ensure length added does not surpass buffer
            var writeAvailable = (int)(_buffer.Capacity - _marker.AvailableByteCount);
            var sampleLength = Mathf.Min(bufferLength, writeAvailable);
            if (sampleLength <= 0)
            {
                return;
            }
            _buffer.Push(buffer, bufferOffset, sampleLength);
            AddedSamples += sampleLength;
            OnAddSamples?.Invoke(buffer, bufferOffset, sampleLength);
            UpdateState();
        }

        /// <summary>
        /// Reads as many samples as possible into the destination with the specified offset
        /// </summary>
        /// <param name="destinationSamples">A buffer that samples will be copied to</param>
        public int ReadSamples(float[] destinationSamples)
            => ReadSamples(0, destinationSamples, 0);

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, float[] destinationSamples, int destinationOffset = 0)
        {
            var length = !_marker.IsValid ? 0 : _marker.Read(destinationSamples, destinationOffset, destinationSamples.Length - destinationOffset);
            int end = destinationOffset + length;
            if (end < destinationSamples.Length)
            {
                int remainder = destinationSamples.Length - end;
                Array.Clear(destinationSamples, end, remainder);
            }
            if (length > 0 && _marker.AvailableByteCount == 0) OnCompletedBufferPlayback?.Invoke();
            return length;
        }

        /// <inheritdoc/>
        public override int ReadSamples(int readOffset, AudioClipStreamSampleDelegate onRead)
        {
            // TODO: Fix in D75885934
            return 0;
        }

        /// <inheritdoc/>
        public override void Unload()
        {
            base.Unload();
            _buffer.Clear();
            _marker = _buffer.CreateMarker();
        }
    }
}
