/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi
{
    /// <summary>
    /// Audio types supported by tts
    /// </summary>
    public enum TTSWitAudioType
    {
        /// <summary>
        /// Raw pcm 16 data
        /// </summary>
        PCM = 0,

        /// <summary>
        /// MP3 data format
        /// </summary>
        MPEG = 1,

        /// <summary>
        /// Wave data format
        /// </summary>
        WAV = 2,

        /// <summary>
        /// Opus data format
        /// </summary>
        OPUS = 3
    }
}
