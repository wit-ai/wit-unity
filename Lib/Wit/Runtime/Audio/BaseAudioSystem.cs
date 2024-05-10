/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEngine;
using Meta.WitAi;
using UnityEngine.Serialization;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// An abstract audio system class that defaults to use RawAudioClipStream and
    /// </summary>
    public abstract class BaseAudioSystem<TAudioClipStream, TAudioPlayer>
        : MonoBehaviour, IAudioSystem
        where TAudioClipStream : IAudioClipStream
        where TAudioPlayer : MonoBehaviour, IAudioPlayer
    {
        /// <summary>
        /// Total seconds of audio required before OnReady callbacks occur.
        /// </summary>
        [Tooltip("Total seconds of audio required before OnReady callbacks occur.")]
        [FormerlySerializedAs("AudioClipReadyLength")]
        public float readyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH;

        /// <summary>
        /// Maximum length of audio clip stream in seconds.
        /// </summary>
        [Tooltip("Maximum length of audio clip stream in seconds.")]
        [FormerlySerializedAs("ChunkBufferLength")]
        public float maxLength = WitConstants.ENDPOINT_TTS_DEFAULT_BUFFER_LENGTH;

        /// <summary>
        /// Number of audio clip streams to pool immediately on awake.
        /// </summary>
        [Tooltip("Number of audio clip streams to pool immediately on awake.")]
        [FormerlySerializedAs("AudioClipPreloadCount")]
        public int clipPreloadCount = WitConstants.ENDPOINT_TTS_DEFAULT_PRELOAD;

        // Clip containers
        private Queue<TAudioClipStream> _unusedClips = new Queue<TAudioClipStream>();
        private HashSet<TAudioClipStream> _usedClips = new HashSet<TAudioClipStream>();

        /// <summary>
        /// Preload clips into audio clip cache
        /// </summary>
        protected virtual void Awake()
        {
            PreloadClipCache(clipPreloadCount);
        }

        /// <summary>
        /// Destroy all audio clips in the cache
        /// </summary>
        protected virtual void OnDestroy()
        {
            DestroyClipCache();
        }

        /// <summary>
        /// Returns a new audio player for managing audio clip stream playback states
        /// </summary>
        /// <param name="root">The gameobject to add the player to if applicable</param>
        public IAudioPlayer GetAudioPlayer(GameObject root) => root.AddComponent<TAudioPlayer>();

        /// <summary>
        /// Returns a new audio clip stream for audio stream handling
        /// </summary>
        /// <param name="channels">Number of channels within audio</param>
        /// <param name="sampleRate">Desired rate of playback</param>
        public IAudioClipStream GetAudioClipStream(int channels, int sampleRate) => DequeueClip();

        #region CLIP CACHE
        /// <summary>
        /// Ensures the specified amount of TAudioClipStreams are generated and ready for use
        /// </summary>
        public void PreloadClipCache(int count)
        {
            // Ignore if none are needed
            var needed = count - _unusedClips.Count;
            if (needed <= 0)
            {
                return;
            }
            // Generate and enqueue needed clips
            for (int i = 0; i < needed; i++)
            {
                _unusedClips.Enqueue(GenerateClip());
            }
        }

        /// <summary>
        /// Destroys all generated TAudioClipStreams
        /// </summary>
        public void DestroyClipCache()
        {
            var usedClips = _usedClips;
            _usedClips = new HashSet<TAudioClipStream>();
            var unusedClips = _unusedClips;
            _unusedClips = new Queue<TAudioClipStream>();
            foreach (var clip in usedClips)
            {
                clip.Unload();
            }
            foreach (var clip in unusedClips)
            {
                clip.Unload();
            }
        }

        /// <summary>
        /// Dequeues a clip if possible, otherwise destroys it
        /// </summary>
        private TAudioClipStream DequeueClip()
        {
            // Attempt to dequeue an existing clip
            if (!_unusedClips.TryDequeue(out TAudioClipStream clip))
            {
                // Generate if no unused clip is found
                clip = GenerateClip();
            }
            // Add to used set and return
            _usedClips.Add(clip);
            // Enqueues clip following stream completion
            clip.OnStreamUnloaded += UnloadClip;
            return clip;
        }

        /// <summary>
        /// Abstract method for generating new audio clip streams
        /// </summary>
        protected abstract TAudioClipStream GenerateClip();

        /// <summary>
        /// Unload method for clip
        /// </summary>
        private void UnloadClip(IAudioClipStream clipStream)
        {
            if (clipStream is TAudioClipStream localClipStream)
            {
                EnqueueClip(localClipStream);
            }
        }

        /// <summary>
        /// Unloads clip stream
        /// </summary>
        private void EnqueueClip(TAudioClipStream audioClipStream)
        {
            // Remove callback
            audioClipStream.OnStreamUnloaded -= UnloadClip;
            // Remove from used clips
            if (_usedClips.Contains(audioClipStream))
            {
                _usedClips.Remove(audioClipStream);
            }
            // Add to unused queue
            if (!_unusedClips.Contains(audioClipStream))
            {
                _unusedClips.Enqueue(audioClipStream);
            }
        }
        #endregion CLIP POOL
    }
}
