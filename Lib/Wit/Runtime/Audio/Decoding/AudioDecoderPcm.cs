/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder for raw PCM audio data
    /// </summary>
    [Preserve]
    public class AudioDecoderPcm : IAudioDecoder
    {
        #region INSTANCE
        // PCM16 uses 2 bytes
        private const int _byteCount = 2;

        // Storage of overflow bytes
        private int _overflowOffset = 0;
        private readonly byte[] _overflow = new byte[_byteCount];
        private readonly List<float> _samples = new List<float>();

        /// <summary>
        /// Default constructor for PCM16
        /// </summary>
        [Preserve]
        public AudioDecoderPcm() {}

        /// <summary>
        /// Once setup this should display the number of channels expected to be decoded
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Once setup this should display the number of samples per second expected
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Mp3 must be decoded sequentially in since frame data could be
        /// carried over to the next chunk
        /// </summary>
        public bool RequireSequentialDecode => false;

        /// <summary>
        /// Initial setup of the decoder
        /// </summary>
        /// <param name="channels">Total channels of audio data</param>
        /// <param name="sampleRate">The rate of audio data received</param>
        public void Setup(int channels, int sampleRate)
        {
            Channels = channels;
            SampleRate = sampleRate;
        }

        /// <summary>
        /// A method for decoded bytes and returning audio data in the form of a float[]
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <returns>Returns a float[] of audio data to be used for audio playback</returns>
        public float[] Decode(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Clear last sample list
            _samples.Clear();

            // Append previous overflow
            if (_overflowOffset > 0)
            {
                // Finish overflow
                var overflowLength = Mathf.Min(_byteCount - _overflowOffset, bufferLength);
                Array.Copy(buffer, bufferOffset, _overflow, _overflowOffset, overflowLength);

                // Decode and add overflow sample
                var sample = DecodePCM16Sample(_overflow, 0);
                _samples.Add(sample);

                // Increment buffer offset/decrement length
                bufferOffset += overflowLength;
                bufferLength -= overflowLength;
                _overflowOffset = 0;
            }

            // Decode and append while there are enough for a sample
            while (bufferLength >= _byteCount)
            {
                var sample = DecodePCM16Sample(buffer, bufferOffset);
                bufferOffset += _byteCount;
                bufferLength -= _byteCount;
                _samples.Add(sample);
            }

            // Store remaining buffer into overflow
            if (bufferLength > 0)
            {
                Array.Copy(buffer, bufferOffset, _overflow, _overflowOffset, bufferLength);
                _overflowOffset += bufferLength;
            }

            // Return final samples
            return _samples.ToArray();
        }
        #endregion

        #region STATIC
        /// <summary>
        /// Gets pcm sample count from byte content length (1 sample = 2 bytes)
        /// </summary>
        /// <param name="contentLength">The provided number of bytes</param>
        public static long GetTotalSamplesPCM(long contentLength)
            => contentLength / 2;

        /// <summary>
        /// Decodes an array of pcm data
        /// </summary>
        /// <param name="rawData">Raw array of pcm bytes</param>
        public static float[] DecodePCM(byte[] rawData)
        {
            int totalSamples = (int)GetTotalSamplesPCM(rawData.Length);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = DecodePCM16Sample(rawData, i * 2);
            }
            return samples;
        }

        /// <summary>
        /// Decodes a sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodePCM16Sample(byte[] rawData, int index)
        {
            return (float)BitConverter.ToInt16(rawData, index) / Int16.MaxValue;
        }
        #endregion
    }
}
