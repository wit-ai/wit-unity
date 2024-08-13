/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text;
using Meta.Voice.Logging;
using Meta.WitAi.TTS.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A base class for swapping out mouth textures during an audio animation based on the current viseme
    /// </summary>
    public abstract class BaseTextureFlipLipSync : MonoBehaviour, ILipsyncAnimator
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger(LogCategory.TextToSpeech);

        /// <summary>
        /// The material to be used for the texture flip
        /// </summary>
        public abstract Renderer Renderer { get; }

        /// <summary>
        /// The textures to be used for viseme swapping
        /// </summary>
        [Header("Texture Settings")]
        public VisemeTextureData[] VisemeTextures;
        // A dictionary per viseme lookup
        private Dictionary<Viseme, int> _textureLookup = new Dictionary<Viseme, int>();

        // Stored texture data
        [Serializable]
        public struct VisemeTextureData
        {
            public Viseme viseme;
            public Texture2D[] textures;
        }

        // Setup texture list
        protected virtual void Reset()
        {
            if (VisemeTextures != null && VisemeTextures.Length > 0)
            {
                return;
            }
            List<VisemeTextureData> newTextures = new List<VisemeTextureData>();
            foreach (Viseme v in Enum.GetValues(typeof(Viseme)))
            {
                newTextures.Add(new VisemeTextureData()
                {
                    viseme = v
                });
            }
            VisemeTextures = newTextures.ToArray();
        }

        // Refresh texture lookup on awake
        protected virtual void Awake()
        {
            RefreshTextureLookup();
        }

        private void Start()
        {
            if (!Renderer)
            {
                _log.Warning("Texture Flip material unassigned on {0}", name);
            }
            SetViseme(Viseme.sil);
        }

        /// <summary>
        /// Refreshes texture lookup
        /// </summary>
        public void RefreshTextureLookup()
        {
            // Ignore without textures
            if (VisemeTextures == null)
            {
                return;
            }

            // Adds textures to dictionary lookup
            StringBuilder log = new StringBuilder();
            _textureLookup.Clear();
            for (int i = 0; i < VisemeTextures.Length; i++)
            {
                Viseme v = VisemeTextures[i].viseme;
                if (VisemeTextures[i].textures == null || VisemeTextures[i].textures.Length == 0)
                {
                    log.AppendLine($"VisemeTextures[{i}] Warning: No textures are set.");
                    continue;
                }
                if (_textureLookup.ContainsKey(v))
                {
                    log.AppendLine($"VisemeTextures[{i}] Warning: Viseme '{v}' already used by VisemeTextures[{_textureLookup[v]}].");
                    continue;
                }
                _textureLookup[v] = i;
            }

            // Logs missing viseme textures
            CheckForMissingVisemes(log);

            // Log warnings
            if (log.Length > 0)
            {
                VLog.E(GetType().Name, $"Setup Warnings:\n{log}");
            }
        }

        // Check for missing visemes & add them to the log
        private void CheckForMissingVisemes(StringBuilder log)
        {
            foreach (Viseme v in Enum.GetValues(typeof(Viseme)))
            {
                if (!_textureLookup.ContainsKey(v))
                {
                    log.AppendLine($"{v} Viseme missing texture");
                }
            }
        }

        // Apply the specified viseme
        private void SetViseme(Viseme v)
        {
            // If viseme is missing, try to use sil
            if (!_textureLookup.ContainsKey(v))
            {
                if (v != Viseme.sil)
                {
                    SetViseme(Viseme.sil);
                }
                return;
            }

            // Get texture index
            int visemeIndex = _textureLookup[v];
            int textureIndex = 0;
            var vt = VisemeTextures[visemeIndex];
            if (vt.textures.Length > 1)
            {
                textureIndex = Random.Range(0, vt.textures.Length);
            }

            // Apply texture
            SetTexture(vt.textures[textureIndex]);
        }

        /// <summary>
        /// Apply texture to material
        /// </summary>
        protected virtual void SetTexture(Texture2D texture)
        {
            if (!Renderer) return;

            Renderer.material.SetTexture("_MainTex", texture);
        }

        public void OnVisemeStarted(Viseme viseme)
        {
            SetViseme(viseme);
        }

        public void OnVisemeFinished(Viseme viseme){}
        public void OnVisemeLerp(Viseme oldVieseme, Viseme newViseme, float percentage){}
    }
}
