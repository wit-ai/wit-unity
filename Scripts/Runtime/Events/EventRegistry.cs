/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEngine;

namespace Meta.WitAi.Events
{
    /// <summary>
    /// This class tracks which callbacks are being used.
    /// </summary>
    public class EventRegistry
    {
        [SerializeField]
        private readonly HashSet<string> _overriddenCallbacks = new HashSet<string>();

        public HashSet<string> OverriddenCallbacks
        {
            get
            {
                return _overriddenCallbacks;
            }
        }

        public void RegisterOverriddenCallback(string callback)
        {
            _overriddenCallbacks.Add(callback);
        }

        public void RemoveOverriddenCallback(string callback)
        {
            if (_overriddenCallbacks.Contains(callback))
            {
                _overriddenCallbacks.Remove(callback);
            }
        }

        public bool IsCallbackOverridden(string callback)
        {
            return OverriddenCallbacks.Contains(callback);
        }
    }
}
