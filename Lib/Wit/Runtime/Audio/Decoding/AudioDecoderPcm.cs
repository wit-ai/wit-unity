/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
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
        // Storage of overflow bytes
        private bool _hasOverflow = false;
        private byte[] _overflow = new byte[2];

        /// <summary>
        /// Once setup this should display the number of channels expected to be decoded
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Once setup this should display the number of samples per second expected
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// PCM can be decoded in any order prior to merging back in
        /// </summary>
        public bool RequireSequentialDecode => false;

        /// <summary>
        /// Initial setup of the decoder
        /// </summary>
        /// <param name="channels">Total channels of audio data</param>
        /// <param name="sampleRate">The rate of audio data received</param>
        public void Setup(int channels, int sampleRate)
        {
            _hasOverflow = false;
            Channels = channels;
            SampleRate = sampleRate;
        }

        /// <summary>
        /// Gets pcm sample count from byte content length (1 sample = 2 bytes)
        /// </summary>
        /// <param name="contentLength">The provided number of bytes</param>
        public int GetTotalSamples(ulong contentLength)
            => GetTotalSamplesPCM16(contentLength);

        /// <summary>
        /// A method for returning decoded bytes into audio data
        /// </summary>
        /// <param name="chunkData">A chunk of bytes to be decoded into audio data</param>
        /// <param name="chunkStart">The array start index into account when decoding</param>
        /// <param name="chunkLength">The total number of bytes to be used within chunkData</param>
        /// <returns>Returns an array of audio data from 0-1</returns>
        public float[] Decode(byte[] chunkData, int chunkStart, int chunkLength)
        {
            // Determine if previous chunk had a leftover or if newest chunk contains one
            bool prevLeftover = _hasOverflow;
            bool nextLeftover = (chunkLength - (prevLeftover ? 1 : 0)) % 2 != 0;
            _hasOverflow = nextLeftover;

            // Generate sample array
            int startOffset = prevLeftover ? 1 : 0;
            int endOffset = nextLeftover ? 1 : 0;
            int newSampleCount = GetTotalSamplesPCM16(chunkLength + startOffset - endOffset);
            float[] newSamples = new float[newSampleCount];

            // Append first byte to previous array
            if (prevLeftover)
            {
                // Append first byte to leftover array
                _overflow[1] = chunkData[chunkStart];
                // Decode first sample
                newSamples[0] = DecodeSamplePCM16(_overflow, 0);
            }

            // Store last byte
            if (nextLeftover)
            {
                _overflow[0] = chunkData[chunkStart + chunkLength - 1];
            }

            // Decode remaining samples
            for (int i = 0; i < newSamples.Length - startOffset; i++)
            {
                newSamples[startOffset + i] = DecodeSamplePCM16(chunkData, chunkStart + startOffset + i * 2);
            }

            // Return samples
            return newSamples;
        }
        #endregion

        #region STATIC
        /// <summary>
        /// Gets pcm sample count from byte content length (1 sample = 2 bytes)
        /// </summary>
        /// <param name="contentLength">The provided number of bytes</param>
        public static int GetTotalSamplesPCM16(ulong contentLength) =>
            Mathf.FloorToInt(contentLength / 2f);

        /// <summary>
        /// Gets pcm sample count from byte content length
        /// </summary>
        /// <param name="contentLength">The provided number of bytes</param>
        public static int GetTotalSamplesPCM16(int contentLength) =>
            GetTotalSamplesPCM16((ulong)contentLength);

        // Decode an entire array
        public static float[] DecodePCM16(byte[] rawData)
        {
            int totalSamples = GetTotalSamplesPCM16(rawData.Length);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = DecodeSamplePCM16(rawData, i * 2);
            }
            return samples;
        }

        // Decode a single sample
        public static float DecodeSamplePCM16(byte[] rawData, int index)
        {
            return (float)BitConverter.ToInt16(rawData, index) / (float)Int16.MaxValue;
        }
        #endregion
    }
}
