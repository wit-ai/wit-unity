/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.ComponentModel;
using com.facebook.witai.lib;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.facebook.witai.data
{
    #if !WIT_DISABLE_UI
    [CreateAssetMenu(fileName = "WitConfiguration", menuName = "Wit/Configuration", order = 1)]
    #endif
    public class WitConfiguration : ScriptableObject
    {
        [HideInInspector]
        [SerializeField] public WitApplication application;

        [ReadOnly(true)] [SerializeField] public string clientAccessToken;

        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;
        [SerializeField] public WitTrait[] traits;

        public WitApplication Application => application;
    }
}
