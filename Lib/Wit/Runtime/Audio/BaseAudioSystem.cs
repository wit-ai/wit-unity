/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using Meta.WitAi;
using Meta.Voice.Logging;
using Lib.Wit.Runtime.Utilities.Logging;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// An abstract audio system class that defaults to use RawAudioClipStream and
    /// </summary>
    [LogCategory(LogCategory.Audio)]
    public abstract class BaseAudioSystem<TAudioClipStream, TAudioPlayer>
        : MonoBehaviour, IAudioSystem, ILogSource
        where TAudioClipStream : IAudioClipStream
        where TAudioPlayer : MonoBehaviour, IAudioPlayer
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Audio);

        /// <summary>
        /// The clip settings accessor
        /// </summary>
        public AudioClipSettings ClipSettings
        {
            get => _clipSettings;
            set
            {
                if (_clipSettings.Equals(value))
                {
                    return;
                }
                _clipSettings = value;
                if (_pool != null)
                {
                    Logger.Warning("Due to a settings change, the pool is being cleared.");
                    _pool.Dispose();
                    _pool = null;
                }
            }
        }
        private AudioClipSettings _clipSettings = new AudioClipSettings()
        {
            Channels = WitConstants.ENDPOINT_TTS_CHANNELS,
            SampleRate = WitConstants.ENDPOINT_TTS_SAMPLE_RATE,
            ReadyDuration = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            MaxDuration = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH
        };

        // Clip stream pool
        private ObjectPool<TAudioClipStream> _pool;

        /// <summary>
        /// Generate pool if missing
        /// </summary>
        protected virtual void GeneratePool()
        {
            if (_pool != null)
            {
                return;
            }
            _pool = new ObjectPool<TAudioClipStream>(GenerateClip);
        }

        /// <summary>
        /// Attempts to generate the specified clip stream if possible
        /// </summary>
        protected virtual TAudioClipStream GenerateClip()
        {
            if (typeof(TAudioClipStream) == typeof(RawAudioClipStream))
            {
                object streamRef = new RawAudioClipStream(ClipSettings.Channels, ClipSettings.SampleRate,
                    ClipSettings.ReadyDuration, ClipSettings.MaxDuration);
                return (TAudioClipStream)streamRef;
            }
            Logger.Warning("{0}.GenerateClip() is missing clip instantiation for {1}",
                GetType().Name, typeof(TAudioClipStream).Name);
            return default(TAudioClipStream);
        }

        /// <summary>
        /// Destroy all audio clips in the cache
        /// </summary>
        protected virtual void OnDestroy()
        {
            _pool.Dispose();
            _pool = null;
        }

        /// <summary>
        /// A method for preloading clip streams into a cache
        /// </summary>
        /// <param name="total">Total clip streams to be preloaded</param>
        public virtual void PreloadClipStreams(int total)
        {
            GeneratePool();
            _pool.Preload(total);
        }

        /// <summary>
        /// Returns a new audio clip stream for audio stream handling
        /// </summary>
        public virtual IAudioClipStream GetAudioClipStream()
        {
            GeneratePool();
            var clipStream = _pool.Get();
            clipStream.OnStreamUnloaded += UnloadAudioClipStream;
            return clipStream;
        }

        /// <summary>
        /// Unload method to add back to the pool
        /// </summary>
        protected virtual void UnloadAudioClipStream(IAudioClipStream clipStream)
        {
            if (clipStream is TAudioClipStream tClipStream)
            {
                tClipStream.OnStreamUnloaded -= UnloadAudioClipStream;
                _pool.Return(tClipStream);
            }
        }

        /// <summary>
        /// Returns a new audio player for managing audio clip stream playback states
        /// </summary>
        /// <param name="root">The gameobject to add the player to if applicable</param>
        public virtual IAudioPlayer GetAudioPlayer(GameObject root) => root.AddComponent<TAudioPlayer>();
    }
}
