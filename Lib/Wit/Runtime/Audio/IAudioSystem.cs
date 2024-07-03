/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// Clip settings required for setup
    /// </summary>
    public struct AudioClipSettings
    {
        /// <summary>
        /// The total number of channels expected for the audio clip
        /// </summary>
        public int Channels;
        /// <summary>
        /// The expected sample rate of the audio clip
        /// </summary>
        public int SampleRate;
        /// <summary>
        /// The total number of seconds to be buffered in order to consider ready
        /// </summary>
        public float ReadyDuration;
        /// <summary>
        /// Maximum length of audio clip stream in seconds.
        /// </summary>
        public float MaxDuration;
    }

    /// <summary>
    /// An interface for an audio system that can be used to return custom audio
    /// clip streams and audio players on specific gameObjects
    /// </summary>
    public interface IAudioSystem
    {
        /// <summary>
        /// The clip generation settings to be used for all clip generations
        /// </summary>
        public AudioClipSettings ClipSettings { get; set; }

        /// <summary>
        /// A method for preloading clip streams into a cache
        /// </summary>
        /// <param name="total">Total clip streams to be preloaded</param>
        void PreloadClipStreams(int total);

        /// <summary>
        /// Returns a new audio clip stream for audio stream handling
        /// </summary>
        IAudioClipStream GetAudioClipStream();

        /// <summary>
        /// Returns a new audio player for managing audio clip stream playback states
        /// </summary>
        /// <param name="root">The gameobject to add the player to if applicable</param>
        IAudioPlayer GetAudioPlayer(GameObject root);
    }
}
