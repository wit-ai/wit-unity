/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.TTS.Events;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// An LRU cache that unloads based on least recently used access
    /// </summary>
    public class TTSRuntimeLRUCache : BaseTTSRuntimeCache
    {
        /// <summary>
        /// Whether or not to unload clip data after the clip capacity is hit
        /// </summary>
        [Header("Runtime Cache Settings")]
        [Tooltip("Whether or not to unload clip data after the clip capacity is hit")]
        [FormerlySerializedAs("_clipLimit")]
        public bool ClipLimit = false;

        /// <summary>
        /// The maximum clips allowed in the runtime cache
        /// </summary>
        [Tooltip("The maximum clips allowed in the runtime cache")]
        [FormerlySerializedAs("_clipCapacity")]
        [Min(1)] public int ClipCapacity = 5;

        /// <summary>
        /// Whether or not to unload clip data after the ram capacity is hit
        /// </summary>
        [Tooltip("Whether or not to unload clip data after the ram capacity is hit")]
        [FormerlySerializedAs("_ramLimit")]
        public bool RamLimit = true;

        /// <summary>
        /// The maximum amount of RAM allowed in the runtime cache.  In KBs
        /// </summary>
        [Tooltip("The maximum amount of RAM allowed in the runtime cache in KBs.  For example, 24k samples per second * 2bits per sample * 10 minutes (600 seconds) = 3600KBs")]
        [FormerlySerializedAs("_ramCapacity")]
        [Min(1)] public int RamCapacity = 3600;

        // Clips & their ids
        private List<string> _clipOrder = new List<string>();

        // Remove all
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _clipOrder.Clear();
        }

        /// <summary>
        /// Refresh clip id's least recently used order
        /// </summary>
        public bool RefreshClipLRU(string clipId)
        {
            int clipIndex = _clipOrder.IndexOf(clipId);
            if (clipIndex != -1)
            {
                _clipOrder.RemoveAt(clipIndex);
                _clipOrder.Add(clipId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Getter for a clip that also moves clip to the back of the queue
        /// </summary>
        public override TTSClipData GetClip(string clipId)
        {
            RefreshClipLRU(clipId);
            return base.GetClip(clipId);
        }

        /// <summary>
        /// Add clip id to lru order
        /// </summary>
        protected override void SetupClip(TTSClipData clipData)
        {
            _clipOrder.Add(clipData.clipID);
            base.SetupClip(clipData);
        }

        /// <summary>
        /// Add clip to cache and ensure it is most recently referenced
        /// </summary>
        public override bool AddClip(TTSClipData clipData)
        {
            // Ignore if invalid
            if (!base.AddClip(clipData))
            {
                return false;
            }

            // Refresh lru order
            RefreshClipLRU(clipData.clipID);

            // If not full, evict least recently used clips
            while (IsCacheFull() && _clipOrder.Count > 0)
            {
                RemoveClip(_clipOrder[0]);
            }

            // True if successfully added
            return _clipOrder.Count > 0;
        }

        /// <summary>
        /// Remove clip id from lru order if possible
        /// </summary>
        protected override void BreakdownClip(TTSClipData clipData)
        {
            int clipIndex = _clipOrder.IndexOf(clipData.clipID);
            if (clipIndex != -1)
            {
                _clipOrder.RemoveAt(clipIndex);
            }
            base.BreakdownClip(clipData);
        }

        /// <summary>
        /// Check if cache is full
        /// </summary>
        private bool IsCacheFull()
        {
            // Capacity full
            if (ClipLimit && _clipOrder.Count > ClipCapacity)
            {
                return true;
            }
            // Ram full
            if (RamLimit && GetCacheDiskSize() > RamCapacity)
            {
                return true;
            }
            // Free
            return false;
        }
        /// <summary>
        /// Get RAM size of cache in KBs
        /// </summary>
        /// <returns>Returns size in KBs rounded up</returns>
        public int GetCacheDiskSize()
        {
            long total = 0;
            foreach (var key in _clips.Keys)
            {
                if (_clips[key].clipStream != null)
                {
                    total += GetClipBytes(_clips[key].clipStream.Channels, _clips[key].clipStream.TotalSamples);
                }
            }
            return (int)(total / (long)1024) + 1;
        }
        // Return bytes occupied by clip
        public static long GetClipBytes(AudioClip clip)
        {
            if (clip != null)
            {
                return GetClipBytes(clip.channels, clip.samples);
            }
            return 0;
        }
        // Return bytes occupied by clip
        public static long GetClipBytes(int channels, int samples)
        {
            return channels * samples * 2;
        }
    }
}
