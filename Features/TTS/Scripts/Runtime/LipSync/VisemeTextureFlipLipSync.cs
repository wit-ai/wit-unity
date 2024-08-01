/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using UnityEngine.Serialization;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A class for swapping out mouth textures during an audio animation based on the current viseme
    /// </summary>
    public class VisemeTextureFlipLipSync : BaseTextureFlipLipSync
    {
        [FormerlySerializedAs("renderer")] [SerializeField] private Renderer visemeRenderer;

        [SerializeField] private VisemeLipSyncAnimator _lipSyncAnimator;

        public override Renderer Renderer => visemeRenderer;

        protected override void Awake()
        {
            base.Awake();
            if (!_lipSyncAnimator)
            {
                _lipSyncAnimator = GetComponent<VisemeLipSyncAnimator>();
            }
            if (!visemeRenderer)
            {
                visemeRenderer = GetComponent<Renderer>();
            }
        }

        protected virtual void OnEnable()
        {
            if (!visemeRenderer)
            {
                VLog.E($"No renderer has been set on {name}. Viseme texture flipping will not be visible.");
                enabled = false;
                return;
            }
            _lipSyncAnimator.OnVisemeStarted?.AddListener(OnVisemeStarted);
        }

        protected virtual void OnDisable()
        {
            _lipSyncAnimator.OnVisemeStarted?.RemoveListener(OnVisemeStarted);
        }
    }
}
