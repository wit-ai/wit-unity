/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
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
        /// The total number of playbacks requested per clip
        /// </summary>
        private Dictionary<string, int> _playbacks = new Dictionary<string, int>();

        /// <summary>
        /// On setup, add delegates for playback
        /// </summary>
        protected override void SetupClip(TTSClipData clipData)
        {
            _playbacks[clipData.clipID] = 0;
            clipData.onPlaybackQueued += OnPlaybackBegin;
            clipData.onPlaybackComplete += OnPlaybackComplete;
            base.SetupClip(clipData);
        }

        private int IncrementPlayback(string clipId, bool up)
        {
            if (!_playbacks.ContainsKey(clipId))
            {
                return 1;
            }
            _playbacks[clipId] += up ? 1 : -1;
            return _playbacks[clipId];
        }

        /// <summary>
        /// On playback begin, increment
        /// </summary>
        private void OnPlaybackBegin(TTSClipData clipData)
        {
            IncrementPlayback(clipData.clipID, true);
        }

        /// <summary>
        /// On playback complete, decrement and unload if applicable
        /// </summary>
        private void OnPlaybackComplete(TTSClipData clipData)
        {
            var playbacks = IncrementPlayback(clipData.clipID, false);
            if (playbacks <= 0)
            {
                RemoveClip(clipData.clipID);
            }
        }

        /// <summary>
        /// On breakdown, remove delegates for playback
        /// </summary>
        protected override void BreakdownClip(TTSClipData clipData)
        {
            clipData.onPlaybackQueued -= OnPlaybackBegin;
            clipData.onPlaybackComplete -= OnPlaybackComplete;
            _playbacks.Remove(clipData.clipID);
            base.BreakdownClip(clipData);
        }
    }
}
