/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An interface for handling audio decoding
    /// </summary>
    public interface IAudioDecoder
    {
        /// <summary>
        /// Once setup this should display the number of channels expected to be decoded
        /// </summary>
        int Channels { get; }

        /// <summary>
        /// Once setup this should display the number of samples per second expected
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// A property for whether the decoder can decode chunks on separate threads
        /// simultaneously or if they need to be performed sequentially.
        /// </summary>
        bool RequireSequentialDecode { get; }

        /// <summary>
        /// Initial setup of the decoder
        /// </summary>
        /// <param name="channels">Total channels of audio data</param>
        /// <param name="sampleRate">The total samples per second expected</param>
        void Setup(int channels, int sampleRate);

        /// <summary>
        /// A method for returning decoded bytes into audio data
        /// </summary>
        /// <param name="chunkData">A chunk of bytes to be decoded into audio data</param>
        /// <param name="chunkStart">The array start index into account when decoding</param>
        /// <param name="chunkLength">The total number of bytes to be used within chunkData</param>
        /// <returns>Returns an array of audio data from 0-1</returns>
        float[] Decode(byte[] chunkData, int chunkStart, int chunkLength);
    }
}
