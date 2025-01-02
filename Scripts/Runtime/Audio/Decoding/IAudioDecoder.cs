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
    /// A delegate to be called when audio samples are decoded from a web stream
    /// </summary>
    public delegate void AudioSampleDecodeDelegate(float[] samples, int offset, int length);

    /// <summary>
    /// An interface for handling audio decoding
    /// </summary>
    public interface IAudioDecoder
    {
        /// <summary>
        /// Whether or not this decoder should run all decoding on a background thread.
        /// If true, performant but requires multiple buffers.
        /// If false, buffers are not required but less performant.
        /// </summary>
        bool WillDecodeInBackground { get; }

        /// <summary>
        /// A method for decoded bytes and calling onSamplesDecoded as float[] are decoded
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback with sample buffer</param>
        void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded);
    }
}
