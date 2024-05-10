/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using Meta.WitAi;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// An audio system that provides RawAudioClipStream & UnityAudioPlayers for playback using Unity's built-in audio system
    /// </summary>
    public class UnityAudioSystem : BaseAudioSystem<RawAudioClipStream, UnityAudioPlayer>
    {
        /// <summary>
        /// Generates a raw audio clip stream
        /// </summary>
        protected override RawAudioClipStream GenerateClip()
        {
            return new RawAudioClipStream(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE, readyLength, maxLength);
        }
    }
}
