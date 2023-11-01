/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Meta.WitAi.Json;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi.Requests
{
    public class WitInfoVRequest : WitVRequest, IWitInfoVRequest
    {
        /// <summary>
        /// Constructor for wit based info VRequests
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        /// <param name="useServerToken">Editor only option to use server token instead of client token</param>
        /// <param name="onDownloadProgress">The callback for progress related to downloading</param>
        /// <param name="onFirstResponse">The callback for the first response of data from a request</param>
        public WitInfoVRequest(IWitRequestConfiguration configuration, bool useServerToken = true,
            RequestProgressDelegate onDownloadProgress = null,
            RequestFirstResponseDelegate onFirstResponse = null)
            : base(configuration, null, useServerToken, onDownloadProgress, onFirstResponse) {}

        /// <summary>
        /// A request to obtain the current app id by grabbing multiple apps & using the app id
        /// </summary>
        /// <param name="onComplete">Returns string with the specified app id if possible.</param>
        /// <returns>Returns false if request cannot be made</returns>
        public bool RequestAppId(RequestCompleteDelegate<string> onComplete)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = 10000.ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = 0.ToString();
            return RequestWitGet<WitResponseNode>(WitEditorConstants.ENDPOINT_APPS, uriParameters, (responseNode, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                    return;
                }
                var results = GetAppId(responseNode);
                onComplete?.Invoke(results.Value, results.Error);
            });
        }

        /// <summary>
        /// A request to obtain all apps available & return the current app id
        /// </summary>
        /// <returns>Returns the specified app id</returns>
        public async Task<RequestCompleteResponse<string>> RequestAppIdAsync()
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = 10000.ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = 0.ToString();
            var responseNodeResult = await RequestWitGetAsync<WitResponseNode>(WitEditorConstants.ENDPOINT_APPS, uriParameters);
            if (!string.IsNullOrEmpty(responseNodeResult.Error))
            {
                return new RequestCompleteResponse<string>(null, responseNodeResult.Error);
            }
            return GetAppId(responseNodeResult.Value);
        }

        // Get app id from response node including multiple applications that has app for token tag
        private RequestCompleteResponse<string> GetAppId(WitResponseNode responseNode)
        {
            if (responseNode == null)
            {
                return new RequestCompleteResponse<string>(null, "Token not valid");
            }
            WitResponseArray nodes = responseNode.AsArray;
            if (nodes == null)
            {
                return new RequestCompleteResponse<string>(null, "No app id found for token");
            }
            foreach (WitResponseNode node in nodes)
            {
                WitResponseClass child = node.AsObject;
                if (child.HasChild(WitEditorConstants.ENDPOINT_APP_FOR_TOKEN) &&
                    child[WitEditorConstants.ENDPOINT_APP_FOR_TOKEN].AsBool &&
                    child.HasChild(WitEditorConstants.ENDPOINT_APP_ID))
                {
                    string id = child[WitEditorConstants.ENDPOINT_APP_ID];
                    return new RequestCompleteResponse<string>(id, string.Empty);
                }
            }
            return new RequestCompleteResponse<string>(null, "No app id found for token");
        }

        /// <summary>
        /// Get all wit app data
        /// </summary>
        /// <param name="limit">The maximum amount of apps to be returned</param>
        /// <param name="offset">The index offset for apps to be returned</param>
        /// <param name="onComplete">Callback on completion</param>
        /// <returns>Returns false if request cannot be made</returns>
        public bool RequestApps(int limit, int offset,
            RequestCompleteDelegate<WitAppInfo[]> onComplete)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = Mathf.Max(limit, 1).ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = Mathf.Max(offset, 0).ToString();
            return RequestWitGet<WitAppInfo[]>(WitEditorConstants.ENDPOINT_APPS, uriParameters, onComplete);
        }

        /// <summary>
        /// Get all wit app data asynchronously
        /// </summary>
        /// <param name="limit">The maximum amount of apps to be returned</param>
        /// <param name="offset">The index offset for apps to be returned</param>
        /// <returns>Returns all WitAppInfo[] data found for the specified parameters</returns>
        public async Task<RequestCompleteResponse<WitAppInfo[]>> RequestAppsAsync(int limit, int offset)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = Mathf.Max(limit, 1).ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = Mathf.Max(offset, 0).ToString();
            return await RequestWitGetAsync<WitAppInfo[]>(WitEditorConstants.ENDPOINT_APPS, uriParameters);
        }

        /// <summary>
        /// Get data for a specific application id
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <param name="onComplete">Callback on completion that returns app info if possible</param>
        /// <returns>Returns false if request cannot be made</returns>
        public bool RequestAppInfo(string applicationId,
            RequestCompleteDelegate<WitAppInfo> onComplete) =>
            RequestWitGet<WitAppInfo>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}",
                null, onComplete);

        /// <summary>
        /// Get data for a specific application id async
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns wit application info</returns>
        public async Task<RequestCompleteResponse<WitAppInfo>> RequestAppInfoAsync(string applicationId) =>
            await RequestWitGetAsync<WitAppInfo>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}");

        /// <summary>
        /// Get all export data for a wit application
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns false if request cannot be made</returns>
        public bool RequestAppExportInfo(RequestCompleteDelegate<WitExportInfo> onComplete) =>
            RequestWitGet(WitEditorConstants.ENDPOINT_EXPORT, null, onComplete);

        /// <summary>
        /// Get all export data for a wit application asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns WitExportInfo if possible</returns>
        public async Task<RequestCompleteResponse<WitExportInfo>> RequestAppExportInfoAsync(string applicationId) =>
            await RequestWitGetAsync<WitExportInfo>(WitEditorConstants.ENDPOINT_EXPORT);

        //
        public bool RequestAppVersionTags(string applicationId,
            RequestCompleteDelegate<WitVersionTagInfo[][]> onComplete)
        {
            return RequestWitGet<WitVersionTagInfo[][]>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_TAGS}", null, onComplete);
        }

        /// <summary>
        /// Retrieve the version tags for the app asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns WitVersionTagInfo[][] if possible</returns>
        public async Task<RequestCompleteResponse<WitVersionTagInfo[][]>> RequestAppVersionTagsAsync(string applicationId) =>
            await RequestWitGetAsync<WitVersionTagInfo[][]>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_TAGS}");

        // Obtain client app token
        public bool RequestClientAppToken(string applicationId,
            RequestCompleteDelegate<string> onComplete)
        {
            var jsonNode = new WitResponseClass()
            {
                { "refresh", "false" }
            };
            return RequestWitPost<WitResponseNode>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_CLIENTTOKENS}",
                null, jsonNode.ToString(),
                (results, error) =>
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        WitResponseClass child = results.AsObject;
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

        /// <summary>
        /// Obtain client token asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns client token string if possible</returns>
        public async Task<RequestCompleteResponse<string>> RequestClientTokenAsync(string applicationId)
        {
            // Perform a post
            var postContents = new WitResponseClass()
            {
                { "refresh", "false" }
            };
            var postResult = await RequestWitPostAsync<WitResponseNode>(
                $"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_CLIENTTOKENS}",
                null, postContents.ToString());

            // Load failed
            if (!string.IsNullOrEmpty(postResult.Error))
            {
                return new RequestCompleteResponse<string>(null, $"Client token load failed\n{postResult.Error}");
            }

            // Decode failed
            WitResponseClass child = postResult.Value.AsObject;
            if (!child.HasChild(WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL))
            {
                return new RequestCompleteResponse<string>(null, $"Client token decode failed\nNo '{WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL}' found.");
            }

            // Success
            return new RequestCompleteResponse<string>(child[WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL].Value, null);
        }

        // Obtain wit app intents
        public bool RequestIntentList(RequestCompleteDelegate<WitIntentInfo[]> onComplete)
        {
            return RequestWitGet<WitIntentInfo[]>(WitEditorConstants.ENDPOINT_INTENTS, null, onComplete);
        }

        /// <summary>
        /// Obtain a list of wit intents for the configuration
        /// </summary>
        /// <returns>Returns decoded intent info structs</returns>
        public async Task<RequestCompleteResponse<WitIntentInfo[]>> RequestIntentListAsync() =>
            await RequestWitGetAsync<WitIntentInfo[]>(WitEditorConstants.ENDPOINT_INTENTS);

        // Get specific intent info
        public bool RequestIntentInfo(string intentId, RequestCompleteDelegate<WitIntentInfo> onComplete)
        {
            return RequestWitGet<WitIntentInfo>($"{WitEditorConstants.ENDPOINT_INTENTS}/{intentId}",
                null, onComplete);
        }

        /// <summary>
        /// Obtain specific wit info for the configuration
        /// </summary>
        /// <returns>Returns of WitIntentInfo if possible</returns>
        public async Task<RequestCompleteResponse<WitIntentInfo>> RequestIntentInfoAsync(string intentId) =>
            await RequestWitGetAsync<WitIntentInfo>($"{WitEditorConstants.ENDPOINT_INTENTS}/{intentId}");

        // Obtain wit app entities
        public bool RequestEntityList(RequestCompleteDelegate<WitEntityInfo[]> onComplete)
        {
            return RequestWitGet<WitEntityInfo[]>(WitEditorConstants.ENDPOINT_ENTITIES,
                null, onComplete);
        }

        /// <summary>
        /// Obtain a list of wit entities for the configuration
        /// </summary>
        /// <returns>Returns decoded entity info structs</returns>
        public async Task<RequestCompleteResponse<WitEntityInfo[]>> RequestEntityListAsync() =>
            await RequestWitGetAsync<WitEntityInfo[]>(WitEditorConstants.ENDPOINT_ENTITIES);

        // Get specific entity info
        public bool RequestEntityInfo(string entityId,
            RequestCompleteDelegate<WitEntityInfo> onComplete)
        {
            return RequestWitGet<WitEntityInfo>($"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}",
                null, onComplete);
        }

        /// <summary>
        /// Obtain all info on a specific wit entity for the configuration
        /// </summary>
        /// <returns>Returns a decoded entity info struct</returns>
        public async Task<RequestCompleteResponse<WitEntityInfo>> RequestEntityInfoAsync(string entityId)
            => await RequestWitGetAsync<WitEntityInfo>($"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}");

        // Obtain wit app traits
        public bool RequestTraitList(RequestCompleteDelegate<WitTraitInfo[]> onComplete)
        {
            return RequestWitGet<WitTraitInfo[]>(WitEditorConstants.ENDPOINT_TRAITS,
                null, onComplete);
        }

        /// <summary>
        /// Obtain a list of wit traits for the configuration
        /// </summary>
        /// <returns>Returns decoded trait info structs</returns>
        public async Task<RequestCompleteResponse<WitTraitInfo[]>> RequestTraitListAsync() =>
            await RequestWitGetAsync<WitTraitInfo[]>(WitEditorConstants.ENDPOINT_TRAITS);

        // Get specific trait info
        public bool RequestTraitInfo(string traitId,
            RequestCompleteDelegate<WitTraitInfo> onComplete)
        {
            return RequestWitGet<WitTraitInfo>($"{WitEditorConstants.ENDPOINT_TRAITS}/{traitId}",
                null, onComplete);
        }

        /// <summary>
        /// Obtain all info on a specific wit trait for the configuration
        /// </summary>
        /// <returns>Returns a decoded trait info struct</returns>
        public async Task<RequestCompleteResponse<WitTraitInfo>> RequestTraitInfoAsync(string traitId) =>
            await RequestWitGetAsync<WitTraitInfo>($"{WitEditorConstants.ENDPOINT_TRAITS}/{traitId}");

        // Obtain wit app voices in a dictionary format
        public bool RequestVoiceList(RequestCompleteDelegate<Dictionary<string, WitVoiceInfo[]>> onComplete)
        {
            return RequestWitGet<Dictionary<string, WitVoiceInfo[]>>(WitEditorConstants.ENDPOINT_TTS_VOICES, null, onComplete);
        }

        /// <summary>
        /// Obtain all info on wit voices for the configuration
        /// </summary>
        /// <returns>Returns a decoded dictionary of WitVoiceInfo per language</returns>
        public async Task<RequestCompleteResponse<Dictionary<string, WitVoiceInfo[]>>> RequestVoiceListAsync() =>
            await RequestWitGetAsync<Dictionary<string, WitVoiceInfo[]>>(WitEditorConstants.ENDPOINT_TTS_VOICES);
    }
}
