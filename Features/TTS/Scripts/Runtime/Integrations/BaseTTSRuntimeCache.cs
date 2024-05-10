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
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.TTS.Events;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// A cache that holds onto audio files only while they are being played
    /// </summary>
    public class BaseTTSRuntimeCache : MonoBehaviour, ITTSRuntimeCacheHandler
    {
        /// <summary>
        /// On clip added callback
        /// </summary>
        public TTSClipEvent OnClipAdded { get; set; } = new TTSClipEvent();
        /// <summary>
        /// On clip removed callback
        /// </summary>
        public TTSClipEvent OnClipRemoved { get; set; } = new TTSClipEvent();

        /// <summary>
        /// Simple getter for all clips
        /// </summary>
        public TTSClipData[] GetClips() => _clips.Values.ToArray();
        // Clips contained in the class by unique id
        protected Dictionary<string, TTSClipData> _clips = new Dictionary<string, TTSClipData>();

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
            if (clipData == null)
            {
                return false;
            }
            // If clip is already set, return success
            if (_clips.ContainsKey(clipData.clipID) && _clips[clipData.clipID] == clipData)
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
            // Ignore if not found
            if (!_clips.Remove(clipID, out var clipData))
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
            clipData.clipStream = null;
            // Remove delegate callback
            OnClipRemoved?.Invoke(clipData);
        }
    }
}
