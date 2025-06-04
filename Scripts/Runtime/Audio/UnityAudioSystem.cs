/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Audio
{
    /// <summary>
    /// An audio system that provides RawAudioClipStream & UnityAudioPlayers for playback using Unity's built-in audio system
    /// </summary>
    public class UnityAudioSystem : BaseAudioSystem<RawAudioClipStream, UnityAudioPlayer>
    {
    }
}
