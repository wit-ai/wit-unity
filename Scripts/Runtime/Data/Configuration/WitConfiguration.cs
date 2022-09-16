/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Entities;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Data.Traits;
using Meta.WitAi;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Facebook.WitAi.Data.Configuration
{
    public class WitConfiguration : ScriptableObject, IWitRequestConfiguration
    {
        [HideInInspector]
        [SerializeField] public WitApplication application;

        /// <summary>
        /// Configuration id
        /// </summary>
        [FormerlySerializedAs("configId")]
        [HideInInspector] [SerializeField] private string _configurationId;

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
        [SerializeField] public string manifestLocalPath;

        public string WitApplicationId
        {
            get
            {
                if (String.IsNullOrEmpty(application?.id))
                {
                    // NOTE: If a dev only provides a client token we may not have the application id.
                    if (!string.IsNullOrEmpty(clientAccessToken))
                    {
                        return INVALID_APP_ID_WITH_CLIENT_TOKEN;
                    }

                    return INVALID_APP_ID_NO_CLIENT_TOKEN;
                }

                return application.id;
            }
        }


        #if UNITY_EDITOR
        // Manifest editor path
        public string ManifestEditorPath
        {
            get
            {
                if (string.IsNullOrEmpty(_manifestFullPath) || !File.Exists(_manifestFullPath))
                {
                    string lookup = Path.GetFileNameWithoutExtension(manifestLocalPath);
                    string[] guids = UnityEditor.AssetDatabase.FindAssets(lookup);
                    if (guids != null && guids.Length > 0)
                    {
                        _manifestFullPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    }
                }
                return _manifestFullPath;
            }
        }
        private string _manifestFullPath;
        #endif

        /// <summary>
        /// When true, will open Conduit manifests when they are manually generated.
        /// </summary>
        [SerializeField] public bool openManifestOnGeneration = false;

        public const string INVALID_APP_ID_NO_CLIENT_TOKEN = "App Info Not Set - No Client Token";

        public const string INVALID_APP_ID_WITH_CLIENT_TOKEN =
            "App Info Not Set - Has Client Token";

        public WitApplication Application => application;

        private void OnEnable()
        {
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(manifestLocalPath))
            {
                manifestLocalPath = $"ConduitManifest-{Guid.NewGuid()}.json";
                EditorUtility.SetDirty(this);
            }
            #endif
        }

        public void ResetData()
        {
            application = null;
            clientAccessToken = null;
            entities = null;
            intents = null;
            traits = null;
        }

        #region IWitRequestConfiguration
        /// <summary>
        /// Returns unique configuration guid
        /// </summary>
        public string GetConfigurationId()
        {
            #if UNITY_EDITOR
            // Ensure configuration id is generated
            if (string.IsNullOrEmpty(_configurationId))
            {
                _configurationId = Guid.NewGuid().ToString();
                EditorUtility.SetDirty(this);
                #if UNITY_2021_3_OR_NEWER
                AssetDatabase.SaveAssetIfDirty(this);
                #else
                AssetDatabase.SaveAssets();
                #endif
            }
            #endif
            // Return configuration id
            return _configurationId;
        }
        /// <summary>
        /// Return endpoint override
        /// </summary>
        public WitRequestEndpointOverride GetEndpointOverrides()
        {
            WitRequestEndpointOverride endpoint = new WitRequestEndpointOverride();
            if (endpointConfiguration != null)
            {
                endpoint.uriScheme = endpointConfiguration.uriScheme;
                endpoint.authority = endpointConfiguration.authority;
                endpoint.port = endpointConfiguration.port;
                endpoint.witApiVersion = endpointConfiguration.witApiVersion;
            }
            return endpoint;
        }
        /// <summary>
        /// Returns client access token
        /// </summary>
        public string GetClientAccessToken()
        {
            return clientAccessToken;
        }
        #if UNITY_EDITOR
        /// <summary>
        /// Returns server access token (Editor Only)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public string GetServerAccessToken()
        {
            return WitAuthUtility.GetAppServerToken(application?.id);
        }
        #endif
        #endregion
    }
}
