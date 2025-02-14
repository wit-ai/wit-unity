/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using UnityEngine.EventSystems;

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
// New input system backends are enabled.
using UnityEngine.InputSystem.UI;
#endif

namespace Meta.WitAi.Utilities
{
    public class EventSystemInstantiator : MonoBehaviour
    {
        public void Awake()
        {
            // Get or add event system
            gameObject.GetOrAddComponent<EventSystem>();

            #if ENABLE_LEGACY_INPUT_MANAGER
            // Get or Add Old EventSystem
            gameObject.GetOrAddComponent<StandaloneInputModule>();

            #elif ENABLE_INPUT_SYSTEM
            // Get or Add New EventSystem
            gameObject.GetOrAddComponent<InputSystemUIInputModule>();
            #endif
        }
    }
}
