/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.WitAi
{
    public static class UnityObjectExtensions
    {
        /// <summary>
        /// Properly determines whether to use DestroyImmediate or Destroy
        /// dependent on the current state of the Editor.
        /// </summary>
        public static void DestroySafely(this Object unityObject)
        {
            // Ignore null/already destroyed
            if (!unityObject)
            {
                return;
            }
            #if UNITY_EDITOR
            // Editor only destroy
            if (!Application.isPlaying)
            {
                MonoBehaviour.DestroyImmediate(unityObject);
                return;
            }
            #endif
            // Destroy object
            MonoBehaviour.Destroy(unityObject);
        }

        /// <summary>
        /// Attempts to obtain a component and adds it if it does not already exist
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject unityObject) where T : Component
        {
            if (!unityObject) return null;
            T comp = unityObject.GetComponent<T>();
            if (comp) return comp;
            return unityObject.AddComponent<T>();
        }
    }
}
