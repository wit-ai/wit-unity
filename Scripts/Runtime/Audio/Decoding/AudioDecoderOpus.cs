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
using UnityEngine.Scripting;
using Meta.Voice.UnityOpus;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder for OPUS audio data
    /// </summary>
    [Preserve]
    public class AudioDecoderOpus : IAudioDecoder
    {
        // Decoder data
        private readonly Decoder _decoder;
        private readonly byte[] _frameBuffer;
        private readonly float[] _opusBuffer;

        // Store header in case of split between
        private const int _headerLength = 8;
        // Maximum allowed frame size
        private const int _frameMax = 240; // 32000 is technically max but largest we see is 240

        // Header specific data
        private int _frameLength;
        private bool _validHeader;

        // Current offset of the frame buffer
        private int _frameOffset;

        /// <summary>
        /// Constructor with channels and sample rate
        /// </summary>
        public AudioDecoderOpus(int channels, int samplerate)
        {
            _decoder = new Decoder((SamplingFrequency)samplerate, (NumChannels)channels);
            _frameBuffer = new byte[_frameMax];
            _opusBuffer = new float[Decoder.maximumPacketDuration * channels];
        }

        /// <summary>
        /// Defaults to background but allows for foreground decode if desired.
        /// </summary>
        public bool WillDecodeInBackground { get; set; } = true;

        /// <summary>
        /// A method for decoded bytes and calling onSamplesDecoded as float[] are decoded
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback with sample buffer</param>
        public void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded)
        {
            while (bufferLength > 0)
            {
                // Decode header
                if (!_validHeader)
                {
                    var decoded = DecodeFrameHeader(buffer, bufferOffset, bufferLength);
                    bufferOffset += decoded;
                    bufferLength -= decoded;
                    continue;
                }

                // Determine length
                var length = Mathf.Min(_frameLength - _frameOffset, bufferLength);
                if (length == 0)
                {
                    _validHeader = false;
                    _frameOffset = 0;
                    continue;
                }

                // Copy to local buffer
                Array.Copy(buffer, bufferOffset, _frameBuffer, _frameOffset, length);
                _frameOffset += length;
                bufferOffset += length;
                bufferLength -= length;

                // Buffered full frame
                if (_frameOffset == _frameLength)
                {
                    int sampleCount = _decoder.Decode(_frameBuffer, _frameLength, _opusBuffer);
                    onSamplesDecoded?.Invoke(_opusBuffer, 0, sampleCount);
                    _validHeader = false;
                    _frameOffset = 0;
                }
            }
        }

        /// <summary>
        /// Decodes the header size using the frame buffer
        /// </summary>
        private int DecodeFrameHeader(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Decode as much header as possible
            var length = Mathf.Min(_headerLength - _frameOffset, bufferLength);
            Array.Copy(buffer, bufferOffset, _frameBuffer, _frameOffset, length);
            _frameOffset += length;
            if (_frameOffset < _headerLength)
            {
                return length;
            }

            // Reset values
            _frameOffset = 0;

            // Get frame size & range
            Array.Reverse(_frameBuffer, 0, 4); // Account for Big-Endian
            _frameLength = BitConverter.ToInt32(_frameBuffer, 0);

            // Check for length errors
            if (_frameLength == 0)
                throw new Exception("Invalid zero-length opus frame");
            if (_frameLength > _frameMax)
                throw new Exception($"Frame size ({_frameLength}) exceeded max frame size ({_frameMax})");

            // Mark valid
            _validHeader = true;
            return length;
        }
    }
}
