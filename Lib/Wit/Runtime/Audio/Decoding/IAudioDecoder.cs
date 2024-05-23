/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An interface for handling audio decoding
    /// </summary>
    public interface IAudioDecoder
    {
        /// <summary>
        /// A method for decoded bytes and calling an AddSample delegate for each
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="decodedSamples">List to add all decoded samples to</param>
        void Decode(byte[] buffer, int bufferOffset, int bufferLength, List<float> decodedSamples);
    }
}
