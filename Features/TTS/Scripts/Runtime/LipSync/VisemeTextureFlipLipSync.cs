/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A class for swapping out mouth textures during an audio animation based on the current viseme
    /// </summary>
    [RequireComponent(typeof(VisemeLipSyncAnimator))]
    public class VisemeTextureFlipLipSync : BaseTextureFlipLipSync
    {
        [SerializeField] private Renderer renderer;
        
        private VisemeLipSyncAnimator _lipSyncAnimator;

        public override Renderer Renderer => renderer;

        protected override void Awake()
        {
            base.Awake();
            _lipSyncAnimator = GetComponent<VisemeLipSyncAnimator>();
        }

        protected virtual void OnEnable()
        {
            _lipSyncAnimator.OnVisemeLerp?.AddListener(OnVisemeLerp);
        }

        protected virtual void OnDisable()
        {
            _lipSyncAnimator.OnVisemeLerp?.RemoveListener(OnVisemeLerp);
        }
    }
}
