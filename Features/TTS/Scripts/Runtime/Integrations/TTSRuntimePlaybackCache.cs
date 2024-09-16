/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Concurrent;
using Meta.WitAi.TTS.Data;
using UnityEngine;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// A cache that unloads when all playback requests have completed
    /// </summary>
    public class TTSRuntimePlaybackCache : BaseTTSRuntimeCache
    {
        /// <summary>
        /// The total number of clips requests loading and playing
        /// </summary>
        private ConcurrentDictionary<string, int> _requests = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// On setup, add delegates for playback
        /// </summary>
        protected override void SetupClip(TTSClipData clipData)
        {
            _requests[clipData.clipID] = 0;
            clipData.onRequestBegin += OnRequestBegin;
            clipData.onRequestComplete += OnRequestComplete;
            base.SetupClip(clipData);
        }

        /// <summary>
        /// On request begin: increment count
        /// </summary>
        private void OnRequestBegin(TTSClipData clipData)
        {
            var clipId = clipData.clipID;
            if (!_requests.TryGetValue(clipId, out int count))
            {
                count = 0;
            }
            _requests[clipId] = count + 1;
        }

        /// <summary>
        /// On request complete due to load failure, cancellation or playback completion: decrement count
        /// </summary>
        private void OnRequestComplete(TTSClipData clipData)
        {
            var clipId = clipData.clipID;
            if (!_requests.TryGetValue(clipId, out int count))
            {
                return;
            }
            count = Mathf.Max(0, count - 1);
            _requests[clipId] = count;
            if (count == 0)
            {
                RemoveClip(clipData.clipID);
            }
        }

        /// <summary>
        /// On breakdown, remove delegates for playback
        /// </summary>
        protected override void BreakdownClip(TTSClipData clipData)
        {
            clipData.onRequestBegin -= OnRequestBegin;
            clipData.onRequestComplete -= OnRequestComplete;
            _requests.TryRemove(clipData.clipID, out var discard);
            base.BreakdownClip(clipData);
        }
    }
}
