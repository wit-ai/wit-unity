/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.WitAi.Data
{
    [Serializable]
    public class AudioEncoding
    {
        public enum Endian
        {
            Big,
            Little
        }

        /// <summary>
        /// The number of recording channels
        /// </summary>
        /// <returns></returns>
        public int numChannels = 1;

        /// <summary>
        /// The amount of samples used per second
        /// </summary>
        public int samplerate = WitConstants.ENDPOINT_SPEECH_SAMPLE_RATE;

        /// <summary>
        /// The expected encoding of the mic pcm data
        /// </summary>
        public string encoding = ENCODING_SIGNED;
        public const string ENCODING_SIGNED = "signed-integer";
        public const string ENCODING_UNSIGNED = "unsigned-integer";

        /// <summary>
        /// The number of bits per sample
        /// </summary>
        public int bits = BITS_SHORT;
        public const int BITS_BYTE = 8;
        public const int BITS_SHORT = 16;
        public const int BITS_INT = 32;
        public const int BITS_LONG = 64;

        /// <summary>
        /// The endianess of the data
        /// </summary>
        public Endian endian = Endian.Little;

        /// <summary>
        /// Convert encoding into string for transmission
        /// </summary>
        public override string ToString()
        {
            return $"audio/raw;bits={bits};rate={samplerate / 1000}k;encoding={encoding};endian={endian.ToString().ToLower()}";
        }
    }
}
