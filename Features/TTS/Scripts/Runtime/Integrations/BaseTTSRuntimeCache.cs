/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// A cache that holds onto audio files only while they are being played
    /// </summary>
    public class BaseTTSRuntimeCache : MonoBehaviour, ITTSRuntimeCacheHandler
    {
        /// <summary>
        /// Callback for clips being added to the runtime cache
        /// </summary>
        public event TTSClipCallback OnClipAdded;

        /// <summary>
        /// Callback for clips being removed from the runtime cache
        /// </summary>
        public event TTSClipCallback OnClipRemoved;

        /// <summary>
        /// Simple getter for all clips
        /// </summary>
        public virtual TTSClipData[] GetClips() => _clips.Values.ToArray();
        // Clips contained in the class by unique id
        protected ConcurrentDictionary<string, TTSClipData> _clips = new ConcurrentDictionary<string, TTSClipData>();

        // Remove all clips on destroy
        protected virtual void OnDestroy()
        {
            _clips.Clear();
        }

        /// <summary>
        /// Grabs clip from dictionary if possible
        /// </summary>
        public virtual TTSClipData GetClip(string clipId)
        {
            _clips.TryGetValue(clipId, out var clip);
            return clip;
        }

        /// <summary>
        /// Add clip to dictionary and begins watching playback
        /// </summary>
        /// <param name="clipData"></param>
        public virtual bool AddClip(TTSClipData clipData)
        {
            // Do not add null
            if (clipData == null || string.IsNullOrEmpty(clipData.clipID))
            {
                return false;
            }
            // If clip is already set, return success
            if (_clips.TryGetValue(clipData.clipID, out var checkClipData)
                && checkClipData != null
                && checkClipData.Equals(clipData))
            {
                return true;
            }

            // Apply clip and setup
            _clips[clipData.clipID] = clipData;
            SetupClip(clipData);
            return true;
        }

        /// <summary>
        /// Performs setup and callback when clip is added
        /// </summary>
        protected virtual void SetupClip(TTSClipData clipData)
        {
            OnClipAdded?.Invoke(clipData);
        }

        /// <summary>
        /// Remove clip from cache immediately
        /// </summary>
        /// <param name="clipID"></param>
        public virtual void RemoveClip(string clipID)
        {
            // Keep empty clips
            if (_clips.TryGetValue(clipID, out var clipData)
                && string.IsNullOrEmpty(clipData.textToSpeak))
            {
                return;
            }
            // Ignore if not found
            if (!_clips.TryRemove(clipID, out clipData))
            {
                return;
            }

            // Call remove delegate
            BreakdownClip(clipData);
        }

        /// <summary>
        /// Performs breakdown and callback when clip is removed
        /// </summary>
        protected virtual void BreakdownClip(TTSClipData clipData)
        {
            // Unloads clip stream
            clipData.clipStream?.Unload();
            clipData.clipStream = null;
            // Remove delegate callback
            OnClipRemoved?.Invoke(clipData);
        }
    }
}
