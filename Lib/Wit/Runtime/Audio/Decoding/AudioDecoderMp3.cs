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
        /// The ordered collection of samples being used for audio decoding
        /// </summary>
        private List<float> _decodedSamples = new List<float>();

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
        /// A method for decoded bytes and returning audio data in the form of a float[]
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <returns>Returns a float[] of audio data to be used for audio playback</returns>
        public float[] Decode(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Resultant float array
            _decodedSamples.Clear();

            // Iterate until chunk is complete
            while (bufferLength > 0)
            {
                // Decode a single frame and append samples
                var decodeLength = _frame.Decode(buffer, bufferOffset, bufferLength, _decodedSamples);

                // Increment buffer values
                bufferOffset += decodeLength;
                bufferLength -= decodeLength;
            }

            // Return results
            return _decodedSamples.ToArray();
        }
    }
}
