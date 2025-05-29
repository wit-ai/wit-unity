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
        private RingBuffer<float> _buffer;
        public Action OnCompletedBufferPlayback;
        private readonly RingBuffer<float>.Marker _marker;

        public RingBufferRawAudioClipStream(float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : this(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE, newReadyLength,
                newMaxLength) {}

        public RingBufferRawAudioClipStream(int newChannels, int newSampleRate,
            float newReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            float newMaxLength = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH)
            : base(newChannels, newSampleRate, newReadyLength, newMaxLength)
        {
            _buffer = new RingBuffer<float>(Mathf.CeilToInt(newChannels * newSampleRate * newMaxLength));
            _marker = _buffer.CreateMarker();
        }

        public int BufferLength => _buffer.Capacity;

        public override void AddSamples(float[] buffer, int bufferOffset, int bufferLength)
        {
            _buffer.Push(buffer, bufferOffset, bufferLength);
            AddedSamples += bufferLength;
            OnAddSamples?.Invoke(buffer, bufferOffset, bufferLength);
            UpdateState();
        }

        public int ReadSamples(float[] samples)
        {
            var length = _marker.Read(samples, 0, samples.Length);
            if (length < samples.Length)
            {
                int dif = samples.Length - length;
                Array.Clear(samples, length, dif);
            }
            if(length > 0 && _marker.AvailableByteCount == 0) OnCompletedBufferPlayback?.Invoke();
            return length;
        }
    }
}
