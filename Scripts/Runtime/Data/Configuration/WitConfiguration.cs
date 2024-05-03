/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.Voice.Net.WebSockets;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Info;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using System.Reflection;
using Meta.WitAi.Attributes;
using Meta.WitAi.Utilities;
using UnityEditor;
#endif

namespace Meta.WitAi.Data.Configuration
{
    public class WitConfiguration : ScriptableObject, IWitRequestConfiguration, IWitWebSocketClientProvider
    {
        /// <summary>
        /// Access token used in builds to make requests for data from Wit.ai
        /// </summary>
        [Tooltip("Access token used in builds to make requests for data from Wit.ai")]
        [FormerlySerializedAs("clientAccessToken")]
        [SerializeField] private string _clientAccessToken;

        /// <summary>
        /// Application info
        /// </summary>
        [FormerlySerializedAs("application")]
        [SerializeField] private WitAppInfo _appInfo;   //to be replaced by _configData

        /// <summary>
        /// Configuration data about the app.
        /// </summary>
        [SerializeField] private WitConfigurationAssetData[] _configData;

        /// <summary>
        /// Configuration id
        /// </summary>
        [FormerlySerializedAs("configId")]
        [HideInInspector] [SerializeField] private string _configurationId;

        [Tooltip("The request connection type to be used by all requests made with this configuration.")]
        [SerializeField] private WitRequestType _requestType = WitConstants.DEFAULT_REQUEST_TYPE;
        /// <summary>
        /// The request connection type to be used by all requests made with this configuration.
        /// </summary>
        public WitRequestType RequestType
        {
            get => _requestType;
            set => _requestType = value;
        }

        [Tooltip("The number of milliseconds to wait before requests to Wit.ai will timeout")]
        [FormerlySerializedAs("timeoutMS")]
        [SerializeField] private int _requestTimeoutMs = WitConstants.DEFAULT_REQUEST_TIMEOUT;
        /// <summary>
        /// The request timeout in ms to be used by all requests made with this configuration.
        /// </summary>
        public int RequestTimeoutMs
        {
            get => _requestTimeoutMs;
            set => _requestTimeoutMs = value;
        }
        [Obsolete("Deprecated in favor of 'RequestTimeoutMs'. Access will be removed in the future.")]
        public int timeoutMS
        {
            get => RequestTimeoutMs;
            set => RequestTimeoutMs = value;
        }

        /// <summary>
        /// Configuration parameters to set up a custom endpoint for testing purposes and request forwarding. The default values here will work for most.
        /// </summary>
        [Tooltip("Configuration parameters to set up a custom endpoint for testing purposes and request forwarding. The default values here will work for most.")]
        [SerializeField] public WitEndpointConfig endpointConfiguration = new WitEndpointConfig();

        /// <summary>
        /// True if this configuration should not show up in the demo list
        /// </summary>
        [SerializeField] public bool isDemoOnly;

        /// <summary>
        /// When set to true, will use Conduit to dispatch voice commands.
        /// </summary>
        [Tooltip("Conduit enables manifest-based dispatching to invoke callbacks with native types directly without requiring manual parsing.")]
        [SerializeField] public bool useConduit = true;

        /// <summary>
        /// The path to the Conduit manifest.
        /// </summary>
        [SerializeField] private string _manifestLocalPath;

        /// <summary>
        /// The assemblies that we want to exclude from Conduit.
        /// </summary>
        [SerializeField] public List<string> excludedAssemblies = new List<string>
        {
            "Oculus.Voice.Demo, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            "Meta.WitAi.Samples, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
        };

        [Tooltip("When true, Conduit will attempt to match incoming requests by type when no exact matches are found. This increases tolerance but reduces runtime performance.")]
        [SerializeField] public bool relaxedResolution;

        /// <summary>
        /// Safe access of local path
        /// </summary>
        public string ManifestLocalPath
        {
            get
            {
                #if UNITY_EDITOR
                if (string.IsNullOrEmpty(_manifestLocalPath))
                {
                    _manifestLocalPath = $"ConduitManifest-{Guid.NewGuid()}.json";
                    SaveConfiguration();
                }
                #endif
                return _manifestLocalPath;
            }
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Returns manifest full editor path
        /// </summary>
        public string GetManifestEditorPath()
        {
            if (string.IsNullOrEmpty(_manifestLocalPath)) return string.Empty;

            string lookup = Path.GetFileNameWithoutExtension(_manifestLocalPath);
            string[] guids = AssetDatabase.FindAssets(lookup);
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return string.Empty;
        }

        #region Refresh
        private string RefreshKey
        {
            get
            {
                string assetPath = AssetDatabase.GetAssetPath(this);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                return $"WitConfig::Refresh::{guid}";
            }
        }

        /// <summary>
        /// The last time this configuration was refreshed in reference to the UnixEpoch
        /// </summary>
        public DateTime LastRefresh => DateTime.UnixEpoch.AddSeconds(LastRefreshSeconds);

        /// <summary>
        /// The last time this configuration was refreshed in reference to the UnixEpoch
        /// </summary>
        private double _lastRefreshSeconds;

        /// <summary>
        /// The serialized seconds since UnixEpoch
        /// </summary>
        private double LastRefreshSeconds
        {
            get => SessionState.GetFloat(RefreshKey, 0);
            set => SessionState.SetFloat(RefreshKey, (float) value);
        }


        /// <summary>
        /// Refreshes the last update seconds
        /// </summary>
        private void RefreshLastUpdate()
        {
            LastRefreshSeconds = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            EditorUtility.SetDirty(this);
        }
        #endregion
        #endif

        /// <summary>
        /// Reset all data
        /// </summary>
        public void ResetData()
        {
            _configurationId = null;
            _appInfo = new WitAppInfo();
            endpointConfiguration = new WitEndpointConfig();
        }

        /// <summary>
        /// Refreshes the individual data components of the configuration.
        /// </summary>
        public void UpdateDataAssets()
        {
            #if UNITY_EDITOR
            // Update plugins
            RefreshPlugins();
            #endif
        }

        // Logger invalid warnings
        private const string INVALID_APP_ID_NO_CLIENT_TOKEN = "App Info Not Set - No Client Token";
        private const string INVALID_APP_ID_WITH_CLIENT_TOKEN =
            "App Info Not Set - Has Client Token";
        public string GetLoggerAppId()
        {
            // Get application id
            string applicationId = GetApplicationId();
            if (String.IsNullOrEmpty(applicationId))
            {
                // NOTE: If a dev only provides a client token we may not have the application id.
                string clientAccessToken = GetClientAccessToken();
                if (!string.IsNullOrEmpty(clientAccessToken))
                {
                    return INVALID_APP_ID_WITH_CLIENT_TOKEN;
                }
                return INVALID_APP_ID_NO_CLIENT_TOKEN;
            }
            return applicationId;
        }

        #region Web Sockets
        // The web socket client to be used
        public WitWebSocketClient WebSocketClient
        {
            get
            {
                if (_client == null)
                {
                    _client = new WitWebSocketClient(this);
                }
                return _client;
            }
        }
        private WitWebSocketClient _client;
        #endregion

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
            }
            #endif
            // Return configuration id
            return _configurationId;
        }
        /// <summary>
        /// Returns unique application id
        /// </summary>
        public string GetApplicationId() => _appInfo.id;
        /// <summary>
        /// Returns application info
        /// </summary>
        public WitAppInfo GetApplicationInfo() => _appInfo;

        /// <summary>
        /// Returns all the configuration data for this app.
        /// </summary>
        public WitConfigurationAssetData[] GetConfigData()
        {
            if (_configData == null)
            {
                _configData = Array.Empty<WitConfigurationAssetData>();
            }
            return _configData;
        }

        /// <summary>
        /// Get a specific configuration data for this configuration
        /// </summary>
        public TConfigData GetConfigData<TConfigData>()
            where TConfigData : WitConfigurationAssetData
        {
            if (_configData == null)
            {
                return null;
            }
            return _configData.FirstOrDefault((data) => data.GetType() == typeof(TConfigData)) as TConfigData;
        }

        /// <summary>
        /// Return endpoint override
        /// </summary>
        public IWitRequestEndpointInfo GetEndpointInfo() => endpointConfiguration;
        /// <summary>
        /// Returns client access token
        /// </summary>
        public string GetClientAccessToken()
        {
            return _clientAccessToken;
        }
        #if UNITY_EDITOR
        /// <summary>
        /// Returns server access token (Editor Only)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public string GetServerAccessToken()
        {
            return WitAuthUtility.GetAppServerToken(GetApplicationId());
        }
        /// <summary>
        /// Set application info
        /// </summary>
        public void SetApplicationInfo(WitAppInfo newInfo)
        {
            _appInfo = newInfo;
            RefreshLastUpdate();
            SaveConfiguration();
        }

        /// <summary>
        /// Saves the plugin-specific data for this WitConfiguration
        /// </summary>
        public void SetConfigData(WitConfigurationAssetData[] configData)
        {
            _configData = configData;
        }
        // Save this configuration asset
        private void SaveConfiguration()
        {
            EditorUtility.SetDirty(this);
            #if UNITY_2021_3_OR_NEWER
            AssetDatabase.SaveAssetIfDirty(this);
            #else
            AssetDatabase.SaveAssets();
            #endif
        }

        private void RefreshPlugins()
        {
            // Find all derived data types
            List<Type> dataPlugins =  typeof(WitConfigurationAssetData).GetSubclassTypes();
            Dictionary<Type, MethodInfo> refreshMethods = GetRefreshMethods(dataPlugins);

            // Create instances of the types and register them
            List<WitConfigurationAssetData> newConfigs = new List<WitConfigurationAssetData>();
            var configurationAssetPath = AssetDatabase.GetAssetPath(this);
            foreach (Type dataType in dataPlugins)
            {
                // Grab existing if present
                var plugin = (WitConfigurationAssetData)AssetDatabase.LoadAssetAtPath(configurationAssetPath, dataType);
                // Generate instance & add to asset
                if (plugin == null)
                {
                    plugin = (WitConfigurationAssetData)CreateInstance(dataType);
                    plugin.name = dataType.Name;
                    AssetDatabase.AddObjectToAsset(plugin, configurationAssetPath);
                    AssetDatabase.SaveAssetIfDirty(plugin);
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(plugin));
                    plugin = (WitConfigurationAssetData)AssetDatabase.LoadAssetAtPath(configurationAssetPath, dataType);
                }
                // Invoke plugin refresh
                if (!refreshMethods.ContainsKey(dataType))
                {
                    VLog.E(GetType().Name, $"No refresh method found for {dataType}");
                }
                else
                {
                    refreshMethods[dataType].Invoke(null, new object[] { this, plugin });
                }
                // Add plugin to list
                newConfigs.Add(plugin);
            }

            // Apply data & save
            SetConfigData(newConfigs.ToArray());
            SaveConfiguration();
        }

        /// <summary>
        /// Finds all static refresh methods that implement DataAssetRefresh Attribute
        /// </summary>
        private Dictionary<Type, MethodInfo> GetRefreshMethods(List<Type> dataTypes)
        {
            // Get all methods tagged with AssetDataRefresh
            var methods = ReflectionUtils.GetMethodsWithAttribute<WitConfigurationAssetRefreshAttribute>();
            var methodLookups = new Dictionary<Type, MethodInfo>();
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == GetType() && dataTypes.Contains(parameters[1].ParameterType))
                {
                    methodLookups[parameters[1].ParameterType] = method;
                }
                else
                {
                    VLog.E(GetType().Name, $"Found AssetDataRefreshAttribute with invalid parameters\nMethod: {method.DeclaringType}.{method.Name}\nTotal: {parameters.Length}");
                }
            }
            return methodLookups;
        }
        #endif

        /// <summary>
        /// Editor only setter
        /// </summary>
        public void SetClientAccessToken(string newToken)
        {
            _clientAccessToken = newToken;
#if UNITY_EDITOR
            SaveConfiguration();
#endif
        }

        #endregion
    }
}
