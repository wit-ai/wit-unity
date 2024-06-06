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
    internal class WitInfoVRequest : WitVRequest, IWitInfoVRequest
    {
        /// <summary>
        /// Constructor for wit based info VRequests
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        /// <param name="useServerToken">Editor only option to use server token instead of client token</param>
        public WitInfoVRequest(IWitRequestConfiguration configuration, bool useServerToken = true)
            : base(configuration, null, useServerToken) {}

        /// <summary>
        /// A request to obtain the current app id by grabbing multiple apps & using the app id
        /// </summary>
        /// <returns>Returns app id string if possible</returns>
        public async Task<VRequestResponse<string>> RequestAppId()
        {
            // Encode parameters
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = 10000.ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = 0.ToString();

            // Send
            var results = await RequestWitGet<WitResponseNode>(WitEditorConstants.ENDPOINT_APPS, uriParameters);

            // Failed
            if (!string.IsNullOrEmpty(results.Error))
            {
                return new VRequestResponse<string>(WitConstants.ERROR_CODE_GENERAL, results.Error);
            }
            var appId = GetAppId(results.Value, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                return new VRequestResponse<string>(WitConstants.ERROR_CODE_GENERAL, error);
            }
            // Success
            return new VRequestResponse<string>(appId);
        }

        // Get app id from response node including multiple applications that has app for token tag
        private string GetAppId(WitResponseNode responseNode, out string error)
        {
            if (responseNode == null)
            {
                error = "Token not valid";
                return null;
            }
            WitResponseArray nodes = responseNode.AsArray;
            if (nodes == null)
            {
                error = "No app id found for token";
                return null;
            }
            for (int n = 0; n < nodes.Count; n++)
            {
                WitResponseClass child = nodes[n].AsObject;
                if (child.HasChild(WitEditorConstants.ENDPOINT_APP_FOR_TOKEN) &&
                    child[WitEditorConstants.ENDPOINT_APP_FOR_TOKEN].AsBool &&
                    child.HasChild(WitEditorConstants.ENDPOINT_APP_ID))
                {
                    string id = child[WitEditorConstants.ENDPOINT_APP_ID];
                    if (!string.IsNullOrEmpty(id))
                    {
                        error = null;
                        return id;
                    }
                }
            }
            error = "No app id found for token";
            return null;
        }

        /// <summary>
        /// Get all wit app data asynchronously
        /// </summary>
        /// <param name="limit">The maximum amount of apps to be returned</param>
        /// <param name="offset">The index offset for apps to be returned</param>
        /// <returns>Returns all WitAppInfo[] data found for the specified parameters</returns>
        public async Task<VRequestResponse<WitAppInfo[]>> RequestApps(int limit, int offset)
        {
            Dictionary<string, string> uriParameters = new Dictionary<string, string>();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_LIMIT] = Mathf.Max(limit, 1).ToString();
            uriParameters[WitEditorConstants.ENDPOINT_APPS_OFFSET] = Mathf.Max(offset, 0).ToString();
            return await RequestWitGet<WitAppInfo[]>(WitEditorConstants.ENDPOINT_APPS, uriParameters);
        }

        /// <summary>
        /// Get data for a specific application id async
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns wit application info</returns>
        public async Task<VRequestResponse<WitAppInfo>> RequestAppInfo(string applicationId) =>
            await RequestWitGet<WitAppInfo>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}");

        /// <summary>
        /// Get all export data for a wit application asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns WitExportInfo if possible</returns>
        public async Task<VRequestResponse<WitExportInfo>> RequestAppExportInfo(string applicationId) =>
            await RequestWitGet<WitExportInfo>(WitEditorConstants.ENDPOINT_EXPORT);

        /// <summary>
        /// Retrieve the version tags for the app asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns WitVersionTagInfo[][] if possible</returns>
        public async Task<VRequestResponse<WitVersionTagInfo[][]>> RequestAppVersionTags(string applicationId) =>
            await RequestWitGet<WitVersionTagInfo[][]>($"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_TAGS}");

        /// <summary>
        /// Obtain client token asynchronously
        /// </summary>
        /// <param name="applicationId">The application's unique identifier</param>
        /// <returns>Returns client token string if possible</returns>
        public async Task<VRequestResponse<string>> RequestClientToken(string applicationId)
        {
            // Perform a post
            var postContents = new WitResponseClass()
            {
                { "refresh", "false" }
            };
            var postResult = await RequestWitPost<WitResponseNode>(
                $"{WitEditorConstants.ENDPOINT_APPS}/{applicationId}/{WitEditorConstants.ENDPOINT_CLIENTTOKENS}",
                null, postContents.ToString());

            // Load failed
            if (!string.IsNullOrEmpty(postResult.Error))
            {
                return new VRequestResponse<string>(WitConstants.ERROR_CODE_GENERAL, $"Client token load failed\n{postResult.Error}");
            }

            // Decode failed
            WitResponseClass child = postResult.Value?.AsObject;
            if (child == null || !child.HasChild(WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL))
            {
                return new VRequestResponse<string>(WitConstants.ERROR_CODE_GENERAL, $"Client token decode failed\nNo '{WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL}' found.");
            }

            // Success
            return new VRequestResponse<string>(child[WitEditorConstants.ENDPOINT_CLIENTTOKENS_VAL].Value);
        }

        /// <summary>
        /// Obtain a list of wit intents for the configuration
        /// </summary>
        /// <returns>Returns decoded intent info structs</returns>
        public async Task<VRequestResponse<WitIntentInfo[]>> RequestIntentList() =>
            await RequestWitGet<WitIntentInfo[]>(WitEditorConstants.ENDPOINT_INTENTS);

        /// <summary>
        /// Obtain specific wit info for the configuration
        /// </summary>
        /// <returns>Returns of WitIntentInfo if possible</returns>
        public async Task<VRequestResponse<WitIntentInfo>> RequestIntentInfo(string intentId) =>
            await RequestWitGet<WitIntentInfo>($"{WitEditorConstants.ENDPOINT_INTENTS}/{intentId}");

        /// <summary>
        /// Obtain a list of wit entities for the configuration
        /// </summary>
        /// <returns>Returns decoded entity info structs</returns>
        public async Task<VRequestResponse<WitEntityInfo[]>> RequestEntityList() =>
            await RequestWitGet<WitEntityInfo[]>(WitEditorConstants.ENDPOINT_ENTITIES);

        /// <summary>
        /// Obtain all info on a specific wit entity for the configuration
        /// </summary>
        /// <returns>Returns a decoded entity info struct</returns>
        public async Task<VRequestResponse<WitEntityInfo>> RequestEntityInfo(string entityId)
            => await RequestWitGet<WitEntityInfo>($"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}");

        /// <summary>
        /// Obtain a list of wit traits for the configuration
        /// </summary>
        /// <returns>Returns decoded trait info structs</returns>
        public async Task<VRequestResponse<WitTraitInfo[]>> RequestTraitList() =>
            await RequestWitGet<WitTraitInfo[]>(WitEditorConstants.ENDPOINT_TRAITS);

        /// <summary>
        /// Obtain all info on a specific wit trait for the configuration
        /// </summary>
        /// <returns>Returns a decoded trait info struct</returns>
        public async Task<VRequestResponse<WitTraitInfo>> RequestTraitInfo(string traitId) =>
            await RequestWitGet<WitTraitInfo>($"{WitEditorConstants.ENDPOINT_TRAITS}/{traitId}");

        /// <summary>
        /// Obtain all info on wit voices for the configuration
        /// </summary>
        /// <returns>Returns a decoded dictionary of WitVoiceInfo per language</returns>
        public async Task<VRequestResponse<Dictionary<string, WitVoiceInfo[]>>> RequestVoiceList() =>
            await RequestWitGet<Dictionary<string, WitVoiceInfo[]>>(WitEditorConstants.ENDPOINT_TTS_VOICES);
    }
}
