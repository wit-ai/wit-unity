/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Facebook.WitAi.Data.Entities;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Data.Traits;
using Facebook.WitAi.Lib;
using Facebook.WitAi.Utilities;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.Data.Configuration
{
    public static class WitConfigurationUtility
    {
        #region ACCESS
        // Wit configuration assets
        private static WitConfiguration[] witConfigs = null;
        public static WitConfiguration[] WitConfigs => witConfigs;

        // Wit configuration asset names
        private static string[] witConfigNames = Array.Empty<string>();
        public static string[] WitConfigNames => witConfigNames;

        // Has configuration
        public static bool HasValidConfig()
        {
            // Refresh list
            RefreshConfigurationList();
            // Check configs
            for (int i = 0; i < witConfigs.Length; i++)
            {
                if (!string.IsNullOrEmpty(witConfigs[i].clientAccessToken))
                {
                    return true;
                }
            }
            // None found
            return false;
        }
        // Refresh configuration asset list
        public static void RefreshConfigurationList()
        {
            // Find all Wit Configurations
            string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");

            // Store wit configuration data
            witConfigs = new WitConfiguration[guids.Length];
            witConfigNames = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                witConfigs[i] = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
                witConfigNames[i] = witConfigs[i].name;
                //witConfigs[i].RefreshConfiguration();
            }
        }
        // Get configuration index
        public static int GetConfigurationIndex(WitConfiguration configuration)
        {
            // Init if needed
            if (witConfigs == null)
            {
                RefreshConfigurationList();
            }
            // Check for config
            if (witConfigs != null)
            {
                for (int c = 0; c < witConfigs.Length; c++)
                {
                    if (witConfigs[c] == configuration)
                    {
                        return c;
                    }
                }
            }
            // Not found
            return -1;
        }
        // Get configuration index
        public static int GetConfigurationIndex(string configurationName)
        {
            // Init if needed
            if (witConfigs == null)
            {
                RefreshConfigurationList();
            }
            // Check for config
            if (witConfigs != null)
            {
                for (int c = 0; c < witConfigs.Length; c++)
                {
                    if (configurationName.Equals(witConfigs[c].name))
                    {
                        return c;
                    }
                }
            }
            // Not found
            return -1;
        }
        #endregion

        #region MANAGEMENT
        // Create configuration for token
        public static int CreateConfiguration(string serverToken)
        {
            // Create
            string path = EditorUtility.SaveFilePanel("Create Wit Configuration", Application.dataPath, "WitConfiguration", "asset");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                // Create
                WitConfiguration asset = ScriptableObject.CreateInstance<WitConfiguration>();
                asset.clientAccessToken = string.Empty;
                path = path.Substring(Application.dataPath.Length - 6);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();

                // Refresh configurations
                RefreshConfigurationList();

                // Get new index following reload
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                int index = GetConfigurationIndex(name);
                return index;
            }

            // Return new index
            return -1;
        }
        // TODO: Delete
        public static bool DeleteConfiguration(WitConfiguration configuration)
        {
            return false;
        }
        #endregion

        #region TOKENS
        // Check if server token is valid
        public static bool IsConfigurationServerTokenValid(string serverToken)
        {
            return !string.IsNullOrEmpty(serverToken) && WitAuthUtility.IsServerTokenValid(serverToken);
        }
        // Check if client token is valid
        public static bool IsConfigurationClientTokenValid(string clientToken)
        {
            return !string.IsNullOrEmpty(clientToken) && clientToken.Length == 32;
        }
        // Update token data
        public static void UpdateTokenData(string serverToken, Action updateComplete = null)
        {
            if (!WitAuthUtility.IsServerTokenValid(serverToken)) return;

            var listRequest = WitRequestFactory.ListAppsRequest(serverToken, 10000);
            listRequest.onResponse = (r) =>
            {
                if (r.StatusCode == 200)
                {
                    var applications = r.ResponseData.AsArray;
                    for (int i = 0; i < applications.Count; i++)
                    {
                        if (applications[i]["is_app_for_token"].AsBool)
                        {
                            var application = WitApplication.FromJson(applications[i]);
                            WitAuthUtility.SetAppServerToken(application.id, serverToken);
                            updateComplete?.Invoke();
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError(r.StatusDescription);
                }
            };
            listRequest.Request();
        }
        public static void ApplyConfigurationToken(WitConfiguration configuration, string token)
        {
            string appID = configuration?.application?.id;
            if (string.IsNullOrEmpty(appID))
            {
                appID = WitAuthUtility.GetAppId(token);
            }
            if (!string.IsNullOrEmpty(token) && WitAuthUtility.IsServerTokenValid(token))
            {
                RefreshConfigurationAppData(configuration, appID, token);
            }
        }
        public static void RefreshConfigurationAppData(WitConfiguration configuration, string appId, string token)
        {
            var refreshToken = WitAuthUtility.GetAppServerToken(appId, token);
            if (string.IsNullOrEmpty(refreshToken) &&
                string.IsNullOrEmpty(appId) &&
                !string.IsNullOrEmpty(configuration?.application?.id))
            {
                refreshToken = WitAuthUtility.GetAppServerToken(configuration.application.id,
                    WitAuthUtility.ServerToken);
                appId = WitAuthUtility.GetAppId(refreshToken);
                if (string.IsNullOrEmpty(appId))
                {
                    UpdateTokenData(refreshToken, () =>
                    {
                        appId = WitAuthUtility.GetAppId(refreshToken);
                        if (appId == configuration.application.id)
                        {
                            configuration.FetchAppConfigFromServerToken(refreshToken, () =>
                            {
                                WitAuthUtility.SetAppServerToken(configuration.application.id, refreshToken);
                                EditorUtility.SetDirty(configuration);
                            });
                        }
                    });
                    return;
                }

                if (appId == configuration.application.id)
                {
                    refreshToken = WitAuthUtility.ServerToken;
                }
            }

            // Apply token
            WitAuthUtility.SetAppServerToken(configuration?.application?.id, refreshToken);

            // Apply token
            configuration.FetchAppConfigFromServerToken(refreshToken, () =>
            {
                WitAuthUtility.SetAppServerToken(configuration?.application?.id, refreshToken);
            });
        }
        #endregion

        #region UPDATE
        public static void UpdateData(this WitConfiguration configuration, Action onUpdateComplete = null)
        {
            DoUpdateData(configuration, onUpdateComplete);
        }

        private static void DoUpdateData(WitConfiguration configuration, Action onUpdateComplete)
        {
            if (!string.IsNullOrEmpty(
                WitAuthUtility.GetAppServerToken(configuration.application.id)))
            {
                var intentsRequest = configuration.ListIntentsRequest();
                intentsRequest.onResponse =
                    (r) => ListEntities(r, configuration, onUpdateComplete);

                configuration.application?.UpdateData(intentsRequest.Request);
            }
        }

        private static void ListEntities(WitRequest r, WitConfiguration configuration, Action onUpdateComplete)
        {
            var entitiesRequest = configuration.ListEntitiesRequest();
            entitiesRequest.onResponse = (er) => ListTraits(er, configuration, onUpdateComplete);
            OnUpdateData(r, (response) => UpdateIntentList(configuration, response),
                entitiesRequest.Request);
        }

         private static void ListTraits(WitRequest er, WitConfiguration configuration, Action onUpdateComplete)
        {
            var traitsRequest = configuration.ListTraitsRequest();
            traitsRequest.onResponse =
                (tr) =>
                {
                    OnUpdateData(tr,
                                (dataResponse) => UpdateTraitList(configuration, dataResponse),
                                onUpdateComplete);
                };
            OnUpdateData(er,
                (entityResponse) => UpdateEntityList(configuration, entityResponse),
                traitsRequest.Request);
        }

        private static void OnUpdateData(WitRequest request,
            Action<WitResponseNode> updateComponent, Action onUpdateComplete)
        {
            if (request.StatusCode == 200)
            {
                updateComponent(request.ResponseData);
            }
            else
            {
                Debug.LogError($"Request for {request} failed: {request.StatusDescription}");
            }
            if (onUpdateComplete != null)
            {
                onUpdateComplete();
            }
        }

        private static void UpdateIntentList(this WitConfiguration configuration,
            WitResponseNode intentListWitResponse)
        {
            var intentList = intentListWitResponse.AsArray;
            var n = intentList.Count;
            configuration.intents = new WitIntent[n];
            for (int i = 0; i < n; i++)
            {
                var intent = WitIntent.FromJson(intentList[i]);
                intent.witConfiguration = configuration;
                configuration.intents[i] = intent;
                intent.UpdateData();
            }
        }

        private static void UpdateEntityList(this WitConfiguration configuration,
            WitResponseNode entityListWitResponse)
        {
            var entityList = entityListWitResponse.AsArray;
            var n = entityList.Count;
            configuration.entities = new WitEntity[n];
            for (int i = 0; i < n; i++)
            {
                var entity = WitEntity.FromJson(entityList[i]);
                entity.witConfiguration = configuration;
                configuration.entities[i] = entity;
                entity.UpdateData();
            }
        }

        public static void UpdateTraitList(this WitConfiguration configuration,
            WitResponseNode traitListWitResponse)
        {
            var traitList = traitListWitResponse.AsArray;
            var n = traitList.Count;
            configuration.traits = new WitTrait[n];
            for (int i = 0; i < n; i++)
            {
                var trait = WitTrait.FromJson(traitList[i]);
                trait.witConfiguration = configuration;
                configuration.traits[i] = trait;
                trait.UpdateData();
            }
        }

        /// <summary>
        /// Gets the app info and client id that is associated with the server token being used
        /// </summary>
        /// <param name="serverToken">The server token to use to get the app config</param>
        /// <param name="action"></param>
        public static void FetchAppConfigFromServerToken(this WitConfiguration configuration,
            string serverToken, Action action)
        {
            if (WitAuthUtility.IsServerTokenValid(serverToken))
            {
                FetchApplicationFromServerToken(configuration, serverToken,
                    () =>
                    {
                        FetchClientToken(configuration,
                            () => { configuration.UpdateData(action); });
                    });
            }
            else
            {
                Debug.LogError($"No server token set for {configuration.name}.");
            }
        }

        private static void FetchApplicationFromServerToken(WitConfiguration configuration,
            string serverToken, Action response)
        {
            var listRequest = WitRequestFactory.ListAppsRequest(serverToken, 10000);
            listRequest.onResponse = (r) =>
            {
                if (r.StatusCode == 200)
                {
                    var applications = r.ResponseData.AsArray;
                    for (int i = 0; i < applications.Count; i++)
                    {
                        if (applications[i]["is_app_for_token"].AsBool)
                        {
                            if (null != configuration.application)
                            {
                                configuration.application.UpdateData(applications[i]);
                            }
                            else
                            {
                                configuration.application =
                                    WitApplication.FromJson(applications[i]);
                            }

                            WitAuthUtility.SetAppServerToken(configuration.application.id, serverToken);
                            response?.Invoke();
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError(r.StatusDescription);
                }
            };
            listRequest.Request();
        }

        private static void FetchClientToken(WitConfiguration configuration, Action action)
        {
            if (!string.IsNullOrEmpty(configuration.application?.id))
            {
                var tokenRequest = configuration.GetClientToken(configuration.application.id);
                tokenRequest.onResponse = (r) =>
                {
                    if (r.StatusCode == 200)
                    {
                        var token = r.ResponseData["client_token"];
                        SerializedObject so = new SerializedObject(configuration);
                        so.FindProperty("clientAccessToken").stringValue =
                            r.ResponseData["client_token"];
                        so.ApplyModifiedProperties();

                        configuration.clientAccessToken = token;
                        EditorUtility.SetDirty(configuration);
                        action?.Invoke();
                    }
                    else
                    {
                        Debug.LogError(r.StatusDescription);
                    }
                };
                tokenRequest.Request();
            }
        }
        #endregion
    }
}
