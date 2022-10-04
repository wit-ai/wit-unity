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
using UnityEngine;
using Meta.WitAi.Json;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi.Lib.Editor
{
    public static class WitEditorRequestUtility
    {
        #region APP INFO
        // Gets all app data
        public static RequestPerformer GetAppsRequest(IWitRequestConfiguration configuration, int limit, int offset,
            Action<float> onProgress, Action<WitAppInfo[], string> onComplete)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = Mathf.Max(limit, 1).ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = Mathf.Max(offset, 0).ToString();
            return WitRequestUtility.GetRequest<WitAppInfo[]>(WitEditorConstants.ENDPOINT_APPS, uriParameters, configuration, true, onProgress, onComplete);
        }

        // Get all apps & return the current app info
        public static RequestPerformer GetAppIdRequest(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<string, string> onComplete)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = 10000.ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = 0.ToString();
            return WitRequestUtility.GetRequest<WitResponseNode>(WitEditorConstants.ENDPOINT_APPS, uriParameters, configuration, true, onProgress, (root, error) =>
            {
                if (string.IsNullOrEmpty(error) && root != null)
                {
                    WitResponseArray nodes = root.AsArray;
                    if (nodes != null)
                    {
                        foreach (WitResponseNode node in nodes)
                        {
                            WitResponseClass child = node.AsObject;
                            if (child.HasChild(WitEditorConstants.ENDPOINT_APP_FOR_TOKEN) && child[WitEditorConstants.ENDPOINT_APP_FOR_TOKEN].AsBool && child.HasChild(WitEditorConstants.ENDPOINT_APP_ID))
                            {
                                onComplete?.Invoke(child[WitEditorConstants.ENDPOINT_APP_ID], null);
                                return;
                            }
                        }
                    }
                    error = "No app id found for token";
                }
                onComplete?.Invoke(null, error);
            });
        }

        // Get app info request
        public static RequestPerformer GetAppInfoRequest(IWitRequestConfiguration configuration, string applicationId,
            Action<float> onProgress, Action<WitAppInfo, string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitAppInfo>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}", null,
                configuration, true, onProgress, onComplete);
        }

        // Obtain client app token
        public static RequestPerformer GetClientAppToken(IWitRequestConfiguration configuration, string applicationId,
            Action<float> onProgress, Action<string, string> onComplete)
        {
            return WitRequestUtility.PostTextRequest<WitResponseNode>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_CLIENTTOKENS}", null, "{\"refresh\":false}",
                configuration, true, onProgress, (node, error) =>
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        WitResponseClass child = node.AsObject;
                        if (child.HasChild(WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL))
                        {
                            onComplete?.Invoke(child[WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL].Value, error);
                            return;
                        }

                        error = $"No client app token found for app\nApp: {applicationId}";
                    }

                    onComplete?.Invoke(null, error);
                });
        }

        // Obtain wit app intents
        public static RequestPerformer GetIntentList(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<WitIntentInfo[], string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitIntentInfo[]>(WitEditorConstants.ENDPOINT_INTENTS, null,
                configuration, true, onProgress, onComplete);
        }

        // Get specific intent info
        public static RequestPerformer GetIntentInfo(IWitRequestConfiguration configuration, string intentId,
            Action<float> onProgress, Action<WitIntentInfo, string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitIntentInfo>($"{WitEditorConstants.ENDPOINT_INTENTS}/{intentId}", null,
                configuration, true, onProgress, onComplete);
        }

        // Obtain wit app entities
        public static RequestPerformer GetEntityList(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<WitEntityInfo[], string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitEntityInfo[]>(WitEditorConstants.ENDPOINT_ENTITIES, null,
                configuration, true, onProgress, onComplete);
        }

        // Get specific entity info
        public static RequestPerformer GetEntityInfo(IWitRequestConfiguration configuration, string entityId,
            Action<float> onProgress, Action<WitEntityInfo, string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitEntityInfo>($"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}", null,
                configuration, true, onProgress, onComplete);
        }

        // Obtain wit app traits
        public static RequestPerformer GetTraitList(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<WitTraitInfo[], string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitTraitInfo[]>(WitEditorConstants.ENDPOINT_TRAITS, null,
                configuration, true, onProgress, onComplete);
        }

        // Get specific trait info
        public static RequestPerformer GetTraitInfo(IWitRequestConfiguration configuration, string traitId,
            Action<float> onProgress, Action<WitTraitInfo, string> onComplete)
        {
            return WitRequestUtility.GetRequest<WitTraitInfo>($"{WitEditorConstants.ENDPOINT_TRAITS}/{traitId}", null,
                configuration, true, onProgress, onComplete);
        }

        // Request TTS voices
        public static RequestPerformer GetVoiceList(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<Dictionary<string, WitVoiceInfo[]>, string> onComplete)
        {
            return WitRequestUtility.GetRequest(WitEditorConstants.ENDPOINT_TTS_VOICES, null,
                configuration, true, onProgress, onComplete);
        }
        #endregion

        #region ENTITY SYNC
        // Add a new entity to wit
        public static RequestPerformer AddEntity(IWitRequestConfiguration configuration, WitEntityInfo newEntity,
            Action<float> onProgress, Action<WitEntityInfo, string> onComplete)
        {
            // Ensure entity exist
            if (string.IsNullOrEmpty(newEntity.name))
            {
                onComplete?.Invoke(new WitEntityInfo(), "No entity provided");
                return null;
            }

            // Get data
            string payload = JsonConvert.SerializeObject(newEntity);

            // Post text
            return WitRequestUtility.PostTextRequest<WitEntityInfo>(WitEditorConstants.ENDPOINT_ENTITIES, null, payload, configuration, true, onProgress, (entity, error) => onComplete(entity, error));
        }

        // Add a new keyword to an entity
        public static RequestPerformer AddEntityKeyword(IWitRequestConfiguration configuration, string entityId, string keyword, string[] synonyms,
            Action<float> onProgress, Action<string> onComplete)
        {
            // Ensure entity & keywords exist
            if (string.IsNullOrEmpty(entityId))
            {
                onComplete?.Invoke("No entity id provided");
                return null;
            }
            if (string.IsNullOrEmpty(keyword))
            {
                onComplete?.Invoke("No keyword provided");
                return null;
            }

            // Get data
            string endpoint = $"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}/{WitEditorConstants.ENDPOINT_ENTITY_KEYWORDS}";
            StringBuilder synonymBuilder = new StringBuilder();
            if (synonyms != null && synonyms.Length > 0)
            {
                foreach (var synonym in synonyms)
                {
                    if (synonymBuilder.Length > 0)
                    {
                        synonymBuilder.Append(',');
                    }
                    synonymBuilder.Append($"\"{synonym}\"");
                }
            }
            string payload = "{\"keyword\":\"" + keyword + "\",\"synonyms\":[" + synonymBuilder + "]}";

            // Post text
            return WitRequestUtility.PostTextRequest<WitResponseNode>(endpoint, null, payload, configuration, true, onProgress, (response, error) => onComplete(error));
        }
        #endregion
    }
}
