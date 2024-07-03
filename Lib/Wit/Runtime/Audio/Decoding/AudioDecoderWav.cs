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

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder that reads wav sub chunks and sends pcm data
    /// directly to base class for decoding.
    /// </summary>
    [Preserve]
    public class AudioDecoderWav : AudioDecoderPcm
    {
        /// <summary>
        /// Used to track sub chunk header and data offset
        /// </summary>
        private int _subChunkOffset = 0;

        /// <summary>
        /// Array for decoding sub chunk headers with descriptor and length
        /// </summary>
        private readonly byte[] _subChunkHeader = new byte[8];
        /// <summary>
        /// Whether the current sub chunk can decode raw pcm data
        /// </summary>
        private bool _subChunkIsData = false;
        /// <summary>
        /// The current sub chunk length.  Starts at 12 to immediately dump RIFF header
        /// </summary>
        private int _subChunkLength = 12;

        /// <summary>
        /// Constant byte[] for the data descriptor sub chunk
        /// </summary>
        private static readonly byte[] DataDescriptor = new byte[] { 0x64, 0x61, 0x74, 0x61 };

        /// <summary>
        /// Constructor that sets to PCM int 16
        /// </summary>
        [Preserve]
        public AudioDecoderWav(int sampleBufferLength = WitConstants.ENDPOINT_TTS_DEFAULT_SAMPLE_LENGTH)
        : base(AudioDecoderPcmType.Int16, sampleBufferLength) {}

        /// <summary>
        /// A method for decoded bytes and calling an onSamplesDecoded to return float[] as they are decoded
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback following a sample decode</param>
        public override void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded)
        {
            // Iterate until buffer is empty
            while (bufferLength > 0)
            {
                // Decode the next sub chunk header if needed
                if (_subChunkLength == 0)
                {
                    var headerLength = DecodeSubChunkHeader(buffer, bufferOffset, bufferLength);
                    bufferOffset += headerLength;
                    bufferLength -= headerLength;
                    continue;
                }

                // Get as much as possible
                var dataLength = Mathf.Min((int)(_subChunkLength - _subChunkOffset), bufferLength);

                // Decode pcm
                if (_subChunkIsData)
                {
                    base.Decode(buffer, bufferOffset, dataLength, onSamplesDecoded);
                }

                // Increment counters
                _subChunkOffset += dataLength;
                bufferOffset += dataLength;
                bufferLength -= dataLength;

                // End of subchunk, decode next header
                if (_subChunkOffset >= _subChunkLength)
                {
                    _subChunkOffset = 0;
                    _subChunkLength = 0;
                }
            }
        }

        /// <summary>
        /// Decodes the current sub chunk header
        /// </summary>
        private int DecodeSubChunkHeader(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Get the amount that can be read
            var length = Mathf.Min(_subChunkHeader.Length - _subChunkOffset, bufferLength);
            Array.Copy(buffer, bufferOffset, _subChunkHeader, _subChunkOffset, length);
            _subChunkOffset += length;

            // Successfully decoded
            if (_subChunkOffset >= _subChunkHeader.Length)
            {
                // Reset offset
                _subChunkOffset = 0;
                // Determine if data
                _subChunkIsData = SubArrayEquals(_subChunkHeader, 0, DataDescriptor, 0, DataDescriptor.Length);
                // Determine length
                _subChunkLength = (int)BitConverter.ToUInt32(_subChunkHeader, 4);
            }

            // Return the total
            return length;
        }

        /// <summary>
        /// Compare sub arrays
        /// </summary>
        private static bool SubArrayEquals<T>(T[] array1, int offset1, T[] array2, int offset2, int length)
        {
            if (array1 == null || array2 == null || array1.Length < offset1 + length || array2.Length < offset2 + length)
            {
                return false;
            }
            for (int i = 0; i < length; i++)
            {
                if (!array1[offset1 + i].Equals(array2[offset2 + i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
