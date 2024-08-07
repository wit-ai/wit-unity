/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A class for adjusting mouth position during an audio animation based on the current viseme
    /// </summary>
    public class VisemeLipSyncAnimator : TTSEventAnimator<TTSVisemeEvent, Viseme>, IVisemeAnimatorProvider
    {
        [Header("Viseme Events")]
        [TooltipBox("Fired when entering or passing a sample with this specified viseme")]
        [SerializeField]
        private VisemeChangedEvent _onVisemeStarted = new VisemeChangedEvent();

        [TooltipBox("Fired when entering or passing a new sample with a different specified viseme")]
        [SerializeField]
        private VisemeChangedEvent _onVisemeFinished = new VisemeChangedEvent();

        [TooltipBox("Fired once per frame with the previous viseme and next viseme as well as a percentage of the current frame in between each viseme.")]
        [SerializeField] [FormerlySerializedAs("onVisemeLerp")]
        private VisemeLerpEvent _onVisemeLerp = new VisemeLerpEvent();

        public Viseme LastViseme { get; private set; }
        public VisemeChangedEvent OnVisemeStarted => _onVisemeStarted;
        public VisemeChangedEvent OnVisemeFinished => _onVisemeFinished;
        public VisemeLerpEvent OnVisemeLerp => _onVisemeLerp;
        [Obsolete("Use OnVisemeStarted, OnVisemeLerp or OnVisemeFinished instead.")]
        public VisemeChangedEvent OnVisemeChanged => OnVisemeStarted;

        // Simply sets to the previous unless equal to the next
        protected override void LerpEvent(TTSVisemeEvent fromEvent, TTSVisemeEvent toEvent, float percentage)
        {
            // Set viseme if changed
            SetViseme(percentage >= 1f ? toEvent.Data : fromEvent.Data);

            // Callback viseme lerp
            percentage = Mathf.Clamp01(percentage);
            OnVisemeLerp?.Invoke(fromEvent.Data, toEvent.Data, percentage);
        }

        /// <summary>
        /// Sets the current viseme and performs callback on change
        /// </summary>
        private void SetViseme(Viseme newViseme)
        {
            if (LastViseme == newViseme)
            {
                return;
            }
            var oldViseme = LastViseme;
            LastViseme = newViseme;
            OnVisemeFinished?.Invoke(oldViseme);
            OnVisemeStarted?.Invoke(LastViseme);
        }
    }
}
