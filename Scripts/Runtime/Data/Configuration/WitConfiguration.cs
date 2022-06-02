/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Entities;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Data.Traits;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Facebook.WitAi.Data.Configuration
{
    public class WitConfiguration : ScriptableObject
    {
        [HideInInspector]
        [SerializeField] public WitApplication application;
        [HideInInspector] [SerializeField] public string configId;

        /// <summary>
        /// Access token used in builds to make requests for data from Wit.ai
        /// </summary>
        [Tooltip("Access token used in builds to make requests for data from Wit.ai")]
        [SerializeField] public string clientAccessToken;

        [Tooltip("The number of milliseconds to wait before requests to Wit.ai will timeout")]
        [SerializeField] public int timeoutMS = 10000;

        /// <summary>
        /// Configuration parameters to set up a custom endpoint for testing purposes and request forwarding. The default values here will work for most.
        /// </summary>
        [Tooltip("Configuration parameters to set up a custom endpoint for testing purposes and request forwarding. The default values here will work for most.")]
        [SerializeField] public WitEndpointConfig endpointConfiguration = new WitEndpointConfig();

        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;
        [SerializeField] public WitTrait[] traits;

        [SerializeField] public bool isDemoOnly;

        /// <summary>
        /// When set to true, will use Conduit to dispatch voice commands.
        /// </summary>
        [Tooltip("Conduit enables manifest-based dispatching to invoke callbacks with native types directly without requiring manual parsing.")]
        [SerializeField] public bool useConduit;

        /// <summary>
        /// The path to the Conduit manifest.
        /// </summary>
        [SerializeField] public string manifestPath;

        /// <summary>
        /// When true, Conduit will automatically generate manifests each time code changes.
        /// </summary>
        [SerializeField] public bool autoGenerateManifest = false;

        /// <summary>
        /// When true, will open Conduit manifests when they are manually generated.
        /// </summary>
        [SerializeField] public bool openManifestOnGeneration = false;

        public WitApplication Application => application;

        private void OnEnable()
        {
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(configId))
            {
                configId = GUID.Generate().ToString();
                EditorUtility.SetDirty(this);
            }

            if (string.IsNullOrEmpty(manifestPath))
            {
                manifestPath =
                    $"{UnityEngine.Application.dataPath}/Oculus/Voice/Resources/ConduitManifest-{Guid.NewGuid()}.json";
                EditorUtility.SetDirty(this);
            }

            #endif
        }
    }
}
