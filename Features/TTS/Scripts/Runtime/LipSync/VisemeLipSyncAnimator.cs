/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using UnityEngine;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A class for adjusting mouth position during an audio animation based on the current viseme
    /// </summary>
    public class VisemeLipSyncAnimator : TTSEventAnimator<TTSVisemeEvent, Viseme>, IVisemeAnimatorProvider
    {
        public Viseme CurrentViseme { get; private set; }

        [Header("Viseme Events")]
        [TooltipBox("Fired when transitioning from one viseme to the next")]
        [SerializeField]
        private VisemeLerpEvent onVisemeLerp = new VisemeLerpEvent();
        [TooltipBox("Fired when a transition is completed and the next viseme should be fully shown")]
        [SerializeField]
        private VisemeChangedEvent onVisemeChanged = new VisemeChangedEvent();

        public VisemeLerpEvent OnVisemeLerp => onVisemeLerp;
        public VisemeChangedEvent OnVisemeChanged => onVisemeChanged;

        // Simply sets to the previous unless equal to the next
        protected override void LerpEvent(TTSVisemeEvent fromEvent, TTSVisemeEvent toEvent, float percentage)
        {
            var viseme = fromEvent.Data;
            if (percentage >= 1f)
            {
                onVisemeLerp?.Invoke(fromEvent.Data, toEvent.Data, 1);
                viseme = toEvent.Data;
            }
            onVisemeLerp?.Invoke(fromEvent.Data, toEvent.Data, Mathf.Clamp01(percentage));
            SetViseme(viseme);
        }

        /// <summary>
        /// Sets the current viseme and performs callback on change
        /// </summary>
        private void SetViseme(Viseme newViseme)
        {
            if (CurrentViseme == newViseme)
            {
                return;
            }
            CurrentViseme = newViseme;
            OnVisemeChanged?.Invoke(CurrentViseme);
        }
    }
}
