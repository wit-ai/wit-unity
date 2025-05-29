/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.Voice.Audio;

namespace Meta.WitAi.TTS.Interfaces
{
    /// <summary>
    /// An interface for various audio sources to provide access to an IAudioPlayer. The IAudioPlayer is frequently
    /// a runtime generated audio source. Typically a UnityAudioPlayer, but it can be customized to provide output to
    /// other audio systems.
    /// </summary>
    public interface IAudioPlayerProvider
    {
        /// <summary>
        /// The script used to perform audio playback of IAudioClipStreams.
        /// </summary>
        IAudioPlayer AudioPlayer { get; }
    }
}
