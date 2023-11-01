/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using UnityEngine;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// A class for adjusting mouth position during an audio animation based on the current viseme
    /// </summary>
    public class VisemeBlendShapeLipSync : TTSEventAnimator<TTSVisemeEvent, Viseme>
    {
        /// <summary>
        /// The skinned mesh renderer for the face
        /// </summary>
        public SkinnedMeshRenderer meshRenderer;

        /// <summary>
        /// The weight scale to be multiplied to all scales
        /// </summary>
        public float blendShapeWeightScale = 1f;

        /// <summary>
        /// The textures to be used for viseme swapping
        /// </summary>
        public VisemeBlendShapeData[] VisemeBlendShapes;
        // A dictionary for quick lookup of which viseme uses which blend shape
        private Dictionary<Viseme, int> _visemeLookup = new Dictionary<Viseme, int>();
        // A dictionary for quick lookup of which blend shape corresponds to each index
        private Dictionary<string, int> _blendShapeLookup = new Dictionary<string, int>();
        // A list of all blend shape names
        private List<string> _blendShapeNames = new List<string>();

        // Stored blend shape data
        [Serializable]
        public struct VisemeBlendShapeData
        {
            public Viseme viseme;
            public VisemeBlendShapeWeight[] weights;
        }
        [Serializable]
        public struct VisemeBlendShapeWeight
        {
            [DropDown("GetBlendShapeNames", true)]
            public string blendShapeId;
            public float weight;
        }

        // Setup one for each viseme
        protected virtual void Reset()
        {
            if (VisemeBlendShapes != null && VisemeBlendShapes.Length > 0)
            {
                return;
            }
            List<VisemeBlendShapeData> list = new List<VisemeBlendShapeData>();
            foreach (Viseme v in Enum.GetValues(typeof(Viseme)))
            {
                list.Add(new VisemeBlendShapeData()
                {
                    viseme = v
                });
            }
            VisemeBlendShapes = list.ToArray();
        }

        // Refresh texture lookup on awake
        protected override void Awake()
        {
            base.Awake();
            RefreshBlendShapeLookup();
            if (meshRenderer == null)
            {
                VLog.E(GetType().Name, "Skinned Mesh Renderer unassigned");
            }
        }

        /// <summary>
        /// Refreshes blend shape lookup
        /// </summary>
        public void RefreshBlendShapeLookup()
        {
            // Ignore without textures
            if (VisemeBlendShapes == null)
            {
                return;
            }

            // Sets up viseme lookup
            StringBuilder log = new StringBuilder();
            _visemeLookup.Clear();
            _blendShapeLookup.Clear();
            for (int i = 0; i < VisemeBlendShapes.Length; i++)
            {
                Viseme v = VisemeBlendShapes[i].viseme;
                if (_visemeLookup.ContainsKey(v))
                {
                    log.AppendLine($"{v} Viseme already set (VisemeBlendShapes[{i}] ignored)");
                    continue;
                }
                _visemeLookup[v] = i;
                foreach (var weight in VisemeBlendShapes[i].weights)
                {
                    if (!string.IsNullOrEmpty(weight.blendShapeId) && !_blendShapeLookup.ContainsKey(weight.blendShapeId))
                    {
                        _blendShapeLookup[weight.blendShapeId] = -1;
                    }
                }
            }

            // Ensure each is represented
            foreach (Viseme v in Enum.GetValues(typeof(Viseme)))
            {
                if (!_visemeLookup.ContainsKey(v))
                {
                    log.AppendLine($"{v} Viseme missing texture");
                }
            }

            // Update blend shape name list
            GetBlendShapeNames();

            // Update lookup dictionary if applicable
            for (int i = 0; i < _blendShapeNames.Count; i++)
            {
                if (_blendShapeLookup.ContainsKey(_blendShapeNames[i]))
                {
                    _blendShapeLookup[_blendShapeNames[i]] = i;
                }
            }

            // Log warnings
            if (log.Length > 0)
            {
                VLog.E(GetType().Name, $"Setup Warnings:\n{log}");
            }
        }

        // Simply sets to the previous unless equal to the next
        protected override void LerpEvent(TTSVisemeEvent fromEvent, TTSVisemeEvent toEvent, float percentage)
        {
            if (_blendShapeLookup == null)
            {
                return;
            }

            // Apply all blend shapes
            foreach (var blendShapeName in _blendShapeLookup.Keys)
            {
                // Get blend shape index if possible
                int blendShapeIndex = _blendShapeLookup[blendShapeName];
                if (blendShapeIndex == -1)
                {
                    continue;
                }

                // Weight to be used
                float weight;
                // Set to final viseme weight
                if (percentage >= 1f)
                {
                    weight = GetBlendShapeWeight(toEvent.Data, blendShapeName);
                }
                // Set to previous viseme weight
                else if (percentage <= 0f)
                {
                    weight = GetBlendShapeWeight(fromEvent.Data, blendShapeName);
                }
                // Lerp/Ease visemes
                else
                {
                    float from = GetBlendShapeWeight(fromEvent.Data, blendShapeName);
                    float to = GetBlendShapeWeight(toEvent.Data, blendShapeName);
                    weight = Mathf.Lerp(from, to, percentage);
                }

                // Set blend shape weight
                meshRenderer.SetBlendShapeWeight(blendShapeIndex, weight * blendShapeWeightScale);
            }
        }

        // Attempts to grab viseme index
        public float GetBlendShapeWeight(Viseme viseme, string blendShapeName)
        {
            if (_visemeLookup.TryGetValue(viseme, out var index))
            {
                if (index >= 0 && index < VisemeBlendShapes.Length)
                {
                    var visemeData = VisemeBlendShapes[index];
                    var blendShapeWeight = visemeData.weights.FirstOrDefault((weight) => string.Equals(weight.blendShapeId, blendShapeName));
                    return blendShapeWeight.weight;
                }
            }
            return 0f;
        }

        // Returns all blend shape ids in order
        public string[] GetBlendShapeNames()
        {
            if (_blendShapeNames == null)
            {
                _blendShapeNames = new List<string>();
            }
            if (meshRenderer != null && meshRenderer.sharedMesh != null && _blendShapeNames.Count != meshRenderer.sharedMesh.blendShapeCount)
            {
                _blendShapeNames.Clear();
                for (int b = 0; b < meshRenderer.sharedMesh.blendShapeCount; b++)
                {
                    _blendShapeNames.Add(meshRenderer.sharedMesh.GetBlendShapeName(b));
                }
            }
            return _blendShapeNames.ToArray();
        }
    }
}
