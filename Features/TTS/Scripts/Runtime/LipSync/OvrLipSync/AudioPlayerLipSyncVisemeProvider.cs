/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.LipSync;
using UnityEngine;

namespace Meta.WitAi.TTS.LipSync.OvrLipSyncIntegration
{
    public class AudioPlayerLipSyncVisemeProvider : MonoBehaviour, IVisemeAnimatorProvider
    {
        public Viseme LastViseme { get; private set; } = Viseme.sil;
        public VisemeChangedEvent OnVisemeStarted { get; } = new VisemeChangedEvent();
        public VisemeChangedEvent OnVisemeFinished { get; } = new VisemeChangedEvent();
        public VisemeLerpEvent OnVisemeLerp { get; } = new VisemeLerpEvent();
        public VisemeUpdateEvent OnVisemeUpdate { get; } = new VisemeUpdateEvent();

        [SerializeField] private Viseme[] ovrVisemeMap = new[]
        {
            Viseme.sil,
            Viseme.PP,
            Viseme.FF,
            Viseme.TH,
            Viseme.DD,
            Viseme.kk,
            Viseme.CH,
            Viseme.SS,
            Viseme.nn,
            Viseme.RR,
            Viseme.aa,
            Viseme.E,
            Viseme.ih,
            Viseme.oh,
            Viseme.ou
        };

        private float[] lastVisemes;

        // Look for a lip-sync Context (should be set at the same level as this component)
        [SerializeField] private LipSyncContextBase lipsyncContext = null;


        /// <summary>
        /// Start this instance.
        /// </summary>
        void Start()
        {
            // make sure there is a phoneme context assigned to this object
            if (!lipsyncContext) lipsyncContext = GetComponent<LipSyncContextBase>();
            if (lipsyncContext == null)
            {
                Debug.LogError("LipSyncContextMorphTarget.Start Error: " +
                               $"No OVRLipSyncContext component on {name}!");
            }
        }

        /// <summary>
        /// Update this instance.
        /// </summary>
        void Update()
        {
            if (lipsyncContext != null)
            {
                if (!lipsyncContext.IsPlaying && lastVisemes != null)
                {
                    for (int i = 0; i < lastVisemes.Length; i++)
                    {
                        OnVisemeUpdate?.Invoke(ovrVisemeMap[i], 0);
                    }
                    lastVisemes = null;
                }
                // get the current viseme frame
                OvrLipSyncEngine.Frame frame = lipsyncContext.GetCurrentPhonemeFrame();
                if (frame != null)
                {
                    SetVisemeToMorphTarget(frame);
                }
                else if(null != lastVisemes)
                {
                    for (int i = 0; i < lastVisemes.Length; i++)
                    {
                        if (lastVisemes[i] > .001)
                        {
                            lastVisemes[i] = 0;
                            OnVisemeLerp?.Invoke(Viseme.sil, ovrVisemeMap[i], 0);
                            OnVisemeFinished?.Invoke(ovrVisemeMap[i]);
                        }
                    }

                    lastVisemes = null;
                }
            }
        }


        /// <summary>
        /// Sets the viseme to morph target.
        /// </summary>
        void SetVisemeToMorphTarget(OvrLipSyncEngine.Frame frame)
        {

            for (int i = 0; i < frame.Visemes.Length; i++)
            {
                OnVisemeUpdate?.Invoke(ovrVisemeMap[i], frame.Visemes[i]);
            }
        }
    }
}
