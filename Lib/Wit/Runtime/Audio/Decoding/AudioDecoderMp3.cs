/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder for raw MPEG audio data
    /// </summary>
    [Preserve]
    public class AudioDecoderMp3 : IAudioDecoder
    {
        /// <summary>
        /// Decoder on a frame by frame basis
        /// </summary>
        private AudioDecoderMp3Frame _frame = new AudioDecoderMp3Frame();

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
        public bool RequireSequentialDecode => true;

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
        /// A method for returning decoded bytes into audio data
        /// </summary>
        /// <param name="chunkData">A chunk of bytes to be decoded into audio data</param>
        /// <param name="chunkStart">The array start index into account when decoding</param>
        /// <param name="chunkLength">The total number of bytes to be used within chunkData</param>
        /// <returns>Returns an array of audio data from 0-1</returns>
        public float[] Decode(byte[] chunkData, int chunkStart, int chunkLength)
        {
            // Resultant float array
            int start = chunkStart;
            List<float> results = new List<float>();

            // Iterate until chunk is complete
            while (start < chunkLength)
            {
                // Decode a single frame, return samples if possible & update start position
                int length = chunkLength - start;
                float[] samples = _frame.Decode(chunkData, ref start, length);

                // Add all newly decoded samples
                if (samples != null)
                {
                    results.AddRange(samples);
                }
            }

            // Return results
            return results.ToArray();
        }
    }
}
