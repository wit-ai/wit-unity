/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

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
        private readonly AudioDecoderMp3Frame _frame = new AudioDecoderMp3Frame();

        /// <summary>
        /// All mpeg decoding should occur in background due to slow decode speed
        /// </summary>
        public bool WillDecodeInBackground => true;

        /// <summary>
        /// A method for decoded bytes and calling an AddSample delegate for each
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback following a sample decode</param>
        public void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded)
        {
            // Iterate until chunk is complete
            while (bufferLength > 0)
            {
                // Decode a single frame and append samples
                var decodeLength = _frame.Decode(buffer, bufferOffset, bufferLength, onSamplesDecoded);

                // Increment buffer values
                bufferOffset += decodeLength;
                bufferLength -= decodeLength;
            }
        }
    }
}
