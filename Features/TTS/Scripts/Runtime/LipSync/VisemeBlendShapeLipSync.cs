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
    /// A class for adjusting mouth position during an audio animation based on the current viseme
    /// </summary>
    [RequireComponent(typeof(VisemeLipSyncAnimator))]
    public class VisemeBlendShapeLipSync : BaseVisemeBlendShapeLipSync
    {
        /// <summary>
        /// The skinned mesh renderer for the face
        /// </summary>
        public SkinnedMeshRenderer meshRenderer;

        private VisemeLipSyncAnimator _lipsyncAnimator;

        public override SkinnedMeshRenderer SkinnedMeshRenderer => meshRenderer;

        protected override void Awake()
        {
            _lipsyncAnimator = GetComponent<VisemeLipSyncAnimator>();
            base.Awake();
        }

        protected virtual void OnEnable()
        {
            _lipsyncAnimator.OnVisemeLerp.AddListener(OnVisemeLerp);
        }

        protected void OnDisable()
        {
            _lipsyncAnimator.OnVisemeLerp.RemoveListener(OnVisemeLerp);
        }
    }
}
