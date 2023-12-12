/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Requests;
using UnityEngine;

namespace Meta.WitAi
{
    /*
     * We call UpdateInfo, directly on the configuration and then have a series of async functions which
     * are performed together via the UpdateInfoAsync method.
     *
     * UpdateInfo, calls UpdateInfoAsync
     * UpdateInfoAsync calls
     * 1. UpdateAppId if server token is present to get the latest app id for the server token
     * 1. UpdateClientToken if server token is present & app id was found in order to update the client token
     * 3. UpdateEntities to perform a single request for all entity ids and then update each entity
     * 4. UpdateIntents to perform a single request for all intent ids and then update each intent
     * 5. UpdateTraits to perform a single request for all trait ids and then update each intent
     * 6. UpdateVoices to update all tts voice info
     * 7. UpdateVersionTags to update the version tag list
     * 7. UpdateExportInfo to receive and update export data info
     * then it returns any warnings that occured
     */
    internal static class WitAppInfoUtility
    {
        #region SETUP
        // Setup with server token and return on complete method
        internal static void GetAppInfo(string serverToken, Action<string, WitAppInfo, string> onComplete) =>
            #pragma warning disable CS4014
            WaitForGetAppInfo(serverToken, onComplete);

        // Wait for update & return app info if possible
        private static async Task WaitForGetAppInfo(string serverToken, Action<string, WitAppInfo, string> onComplete)
        {
            WitServerRequestConfiguration tempConfig = new WitServerRequestConfiguration(serverToken);
            string warnings = await UpdateAsync(tempConfig);
            onComplete?.Invoke(tempConfig.GetClientAccessToken(), tempConfig.GetApplicationInfo(), warnings);
        }

        // Attempts to obtain application id with server token
        internal static VRequest CheckServerToken(string serverToken, Action<bool> onComplete)
        {
            var tempConfig = new WitServerRequestConfiguration(serverToken);
            var request = new WitInfoVRequest(tempConfig, true);
            #pragma warning disable CS4014
            WaitForCheckServerToken(request, onComplete);
            return request;
        }
        // Perform id lookup
        private static async Task WaitForCheckServerToken(WitInfoVRequest request, Action<bool> onComplete)
        {
            var result = await request.RequestAppIdAsync();
            bool success = string.IsNullOrEmpty(result.Error) && !string.IsNullOrEmpty(result.Value);
            onComplete?.Invoke(success);
        }
        #endregion

        #region UPDATE
        // List of ids being tracked for currently updating configurations
        private static List<string> _updatingIds = new List<string>();

        // Setter for configuration update state
        private static void SetUpdatingInfo(string id, bool toRefreshing)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            int index = _updatingIds.IndexOf(id);
            bool wasRefreshing = index != -1;
            if (toRefreshing && !wasRefreshing)
            {
                _updatingIds.Add(id);
            }
            else if (!toRefreshing && wasRefreshing)
            {
                _updatingIds.RemoveAt(index);
            }
        }

        /// <summary>
        /// Extension method to determine if the current configuration's info is being updated
        /// </summary>
        /// <param name="configuration">the configuration to perform all update requests</param>
        /// <returns>True if already updating</returns>
        internal static bool IsUpdatingData(this IWitRequestConfiguration configuration)
        {
            string clientToken = configuration.GetClientAccessToken();
            string serverToken = configuration.GetServerAccessToken();
            return (!string.IsNullOrEmpty(clientToken) && _updatingIds.Contains(clientToken)) ||
                   (!string.IsNullOrEmpty(serverToken) && _updatingIds.Contains(serverToken));
        }

        /// <summary>
        /// Performs an update on a specified configuration with app info
        /// </summary>
        /// <param name="configuration">the configuration to perform all update requests</param>
        /// <param name="onComplete">The callback delegate which returns a string of all issues encountered.</param>
        internal static void Update(this IWitRequestConfiguration configuration,
            Action<string> onComplete = null) =>
            WaitForUpdate(configuration, onComplete);

        // Awaits an async method and then returns with a completion delegate
        private static async void WaitForUpdate(IWitRequestConfiguration configuration,
            Action<string> onComplete)
        {
            string result = await UpdateAsync(configuration);
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// Performs an update on a specified configuration with app info
        /// </summary>
        /// <param name="configuration">the configuration to perform all update requests</param>
        /// <returns>A string that details all issues encountered.</returns>
        internal static async Task<string> UpdateAsync(this IWitRequestConfiguration configuration)
        {
            // Already updating data
            if (configuration == null)
            {
                string error = "Cannot update a null configuration";
                VLog.E($"Update Info - Failed\n{error}");
                return error;
            }

            // If no client token & no app id or client token fail now
            string serverToken = configuration.GetServerAccessToken();
            bool hasServerToken = !string.IsNullOrEmpty(serverToken);
            string clientToken = configuration.GetClientAccessToken();
            bool hasClientToken = !string.IsNullOrEmpty(clientToken);
            string appId = configuration.GetApplicationId();
            if (!hasServerToken && !hasClientToken)
            {
                string error = "Cannot update configuration without a server access token or a client access token";
                VLog.E($"Update Info - Failed\n{error}");
                return error;
            }

            // Update begin
            StringBuilder warnings = new StringBuilder();

            // Update app id & client tokens
            if (hasServerToken && Application.isEditor)
            {
                // Begin updating using server token
                SetUpdatingInfo(serverToken, true);

                // Update app id & set if possible
                appId = await UpdateAppId(configuration, warnings);

                // Update additional app info if possible
                if (!string.IsNullOrEmpty(appId))
                {
                    await UpdateAppInfo(configuration, warnings);
                }

                // Update client token & set if possible
                clientToken = await UpdateClientToken(configuration, appId, warnings);

                // Update all editor only data
                await UpdateVersionTags(configuration, warnings);

                // Done updating using server token
                SetUpdatingInfo(serverToken, false);
            }

            // Fail without client token
            if (string.IsNullOrEmpty(clientToken))
            {
                warnings.AppendLine("Cannot update configuration info without client access token.");
                VLog.E($"Update Info - Failed\n{warnings}");
                return warnings.ToString();
            }

            // Begin update
            SetUpdatingInfo(clientToken, true);

            // Update all info for the configuration
            await UpdateEntities(configuration, warnings);
            await UpdateIntents(configuration, warnings);
            await UpdateTraits(configuration, warnings);
            await UpdateVoices(configuration, warnings);

            // Log success
            if (warnings.Length == 0)
            {
                VLog.D($"Update Info - Complete\nApp: {configuration.GetApplicationInfo().name}");
            }
            // Log warnings
            else
            {
                VLog.W($"Update Info - Complete with Warnings\nApp: {configuration.GetApplicationInfo().name}\n\n{warnings}\n");
            }

            // Update complete, return warnings
            SetUpdatingInfo(clientToken, false);
            return warnings.ToString();
        }

        // Generates a wit request as specified
        private static WitInfoVRequest GetRequest(IWitRequestConfiguration configuration, bool useServerToken) =>
            new WitInfoVRequest(configuration, useServerToken);

        // Handles results
        private static TValue HandleResults<TValue>(VRequest.RequestCompleteResponse<TValue> result, string errorInfo, StringBuilder warnings)
        {
            // Failure: Appends error & returns default value
            if (!string.IsNullOrEmpty(result.Error))
            {
                warnings.AppendLine(errorInfo);
                warnings.Append($"\t{result.Error.Replace("\n", "\n\t")}");
                return default(TValue);
            }
            // Failure: received empty string
            if (typeof(TValue) == typeof(string) && string.IsNullOrEmpty(result.Value as string))
            {
                result.Error = "Resultant string is empty";
                warnings.AppendLine(errorInfo);
                warnings.Append($"\t{result.Error.Replace("\n", "\n\t")}");
                return default(TValue);
            }
            // Failure: received null result value
            if (result.Value == null)
            {
                result.Error = "Results are null";
                warnings.AppendLine(errorInfo);
                warnings.Append($"\t{result.Error.Replace("\n", "\n\t")}");
                return default(TValue);
            }
            // Success: Returns value directly
            return result.Value;
        }

        // Handle array update
        private static async Task<TData[]> UpdateArray<TData>(IWitRequestConfiguration configuration, WitAppInfo appInfo, StringBuilder warnings,
            Func<TData, string> arrayItemIdGetter,
            Func<WitInfoVRequest, Task<VRequest.RequestCompleteResponse<TData[]>>> arrayRequestHandler,
            Func<WitInfoVRequest, TData, Task<VRequest.RequestCompleteResponse<TData>>> arrayItemRequestHandler)
        {
            // Get request for all items in the array
            var request = GetRequest(configuration, false);

            // Perform request & return all items
            var result = await arrayRequestHandler.Invoke(request);

            // Get items & add warning if needed
            string name = GetTypeName<TData>();
            TData[] newItems = HandleResults(result, $"{name}[] update failed", warnings);

            // Update each item
            int total = newItems == null ? 0 : newItems.Length;
            for (int e = 0; e < total; e++)
            {
                newItems[e] = await UpdateArrayItem(configuration, newItems[e], warnings, arrayItemIdGetter, arrayItemRequestHandler);
            }

            // Return new items
            VLog.I($"Update Info - {name} update success (Total: {total})");
            return newItems;
        }

        // Handle array item update
        private static async Task<TData> UpdateArrayItem<TData>(IWitRequestConfiguration configuration, TData oldInfo, StringBuilder warnings,
            Func<TData, string> arrayItemIdGetter,
            Func<WitInfoVRequest, TData, Task<VRequest.RequestCompleteResponse<TData>>> arrayItemRequestHandler)
        {
            // Get request for additional info on a single item in the array
            var request = GetRequest(configuration, false);

            // Perform request & return item data
            var result = await arrayItemRequestHandler.Invoke(request, oldInfo);

            // Get new item info & add warning if needed
            string oldId = arrayItemIdGetter.Invoke(oldInfo);
            string name = $"{GetTypeName<TData>()}[{oldId}]";
            TData newInfo = HandleResults(result, $"{name} update failed", warnings);

            // Use old info if inconsistent
            string newId = arrayItemIdGetter.Invoke(newInfo);
            if (!string.Equals(oldId, newId))
            {
                // Log if not null
                if (!string.IsNullOrEmpty(newId))
                {
                    warnings.AppendLine($"{name} request ignored due to inconsistent id: {newId}");
                }
                return oldInfo;
            }

            // Update success
            return newInfo;
        }
        // Simple type name determinator
        private static string GetTypeName<TData>() => typeof(TData).Name;
        #endregion

        #region EDITOR REQUESTS
        // Performs an editor request for app id, appends any warnings and returns the app id if possible
        private static async Task<string> UpdateAppId(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            // Get old application id
            string oldAppId = configuration.GetApplicationId();

            // Perform app id request
            var result = await GetRequest(configuration, true).RequestAppIdAsync();

            // Get new app id if possible
            string newAppId = HandleResults(result, "App id update failed", warnings);
            if (string.IsNullOrEmpty(newAppId))
            {
                VLog.I("Update Info - App id update failed");
                return oldAppId;
            }

            // Apply new app id
            var appInfo = configuration.GetApplicationInfo();
            appInfo.id = newAppId;
            configuration.SetApplicationInfo(appInfo);

            // Return new app id
            VLog.I($"Update Info - App id update success\nApp Id: {newAppId}");
            return newAppId;
        }

        // Perform an update to app info
        private static async Task<WitAppInfo> UpdateAppInfo(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            // Get old application info
            WitAppInfo oldInfo = configuration.GetApplicationInfo();

            // Perform request for app info
            var result = await GetRequest(configuration, true).RequestAppInfoAsync(oldInfo.id);

            // Get results & add warning if needed
            WitAppInfo newInfo = HandleResults(result, "App info update failed", warnings);
            if (string.IsNullOrEmpty(newInfo.id))
            {
                warnings.AppendLine($"App info update failed.\nOld Id: '{oldInfo.id}'");
                VLog.I($"Update Info - App info update failed\nOld Id: {oldInfo.id}");
                return oldInfo;
            }

            // Set & return new info
            configuration.SetApplicationInfo(newInfo);
            VLog.I($"Update Info - App info update success\nApp: {newInfo.name}\nId: {newInfo.id}");
            return newInfo;
        }

        // Performs an editor request for client token, appends any warnings and returns the client token if possible.
        private static async Task<string> UpdateClientToken(IWitRequestConfiguration configuration, string appId, StringBuilder warnings)
        {
            // Get old client id
            string oldClientToken = configuration.GetClientAccessToken();

            // Cannot request client token without an app id
            if (string.IsNullOrEmpty(appId))
            {
                return oldClientToken;
            }

            // Perform a client token request
            var result = await GetRequest(configuration, true).RequestClientTokenAsync(appId);
            string newClientToken = HandleResults(result, "Client token request failed", warnings);
            if (string.IsNullOrEmpty(newClientToken))
            {
                VLog.I($"Update Info - Client token update failed");
                return oldClientToken;
            }

            // Apply new token
            configuration.SetClientAccessToken(newClientToken);

            // Success
            VLog.I($"Update Info - Client token update success");
            return newClientToken;
        }

        // Updates all version tags
        private static async Task UpdateVersionTags(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            // Perform request for version tags
            var result = await GetRequest(configuration, true).RequestAppVersionTagsAsync(configuration.GetApplicationId());

            // Get results & add warning if needed
            WitVersionTagInfo[][] versionTagsBySnapshot = HandleResults(result, "Version tags update failed", warnings);

            // Get single array
            int totalSnapshots = versionTagsBySnapshot != null ? versionTagsBySnapshot.Length : 0;
            int totalTagCount = versionTagsBySnapshot != null ? versionTagsBySnapshot.Sum(snap =>snap.Length) : 0;
            WitVersionTagInfo[] versionTags = new WitVersionTagInfo[totalTagCount];
            for (int snapshot = 0, currentTag = 0; snapshot < totalSnapshots; snapshot++)
            {
                for (var tag = 0; tag < versionTagsBySnapshot[snapshot].Length; tag++, currentTag++)
                {
                    versionTags[currentTag] = versionTagsBySnapshot[snapshot][tag];
                }
            }

            // Set new info
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            appInfo.versionTags = versionTags;
            configuration.SetApplicationInfo(appInfo);
            VLog.I($"Update Info -  Version tags update success (Total: {versionTags.Length})");
        }
        #endregion

        #region RUNTIME REQUESTS
        // Updates all entity info items
        private static async Task UpdateEntities(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            appInfo.entities = await UpdateArray(configuration,
                appInfo, warnings,
                (info) => info.id,
                (request) => request.RequestEntityListAsync(),
                (request, info) => request.RequestEntityInfoAsync(info.id));
            configuration.SetApplicationInfo(appInfo);
        }

        // Updates all intent info items
        private static async Task UpdateIntents(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            appInfo.intents = await UpdateArray(configuration,
                appInfo, warnings,
                (info) => info.id,
                (request) => request.RequestIntentListAsync(),
                (request, info) => request.RequestIntentInfoAsync(info.id));
            configuration.SetApplicationInfo(appInfo);
        }

        // Updates all trait info items
        private static async Task UpdateTraits(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            appInfo.traits = await UpdateArray(configuration,
                appInfo, warnings,
                (info) => info.id,
                (request) => request.RequestTraitListAsync(),
                (request, info) => request.RequestTraitInfoAsync(info.id));
            configuration.SetApplicationInfo(appInfo);
        }

        // Updates all tts voices within the configuration
        private static async Task UpdateVoices(IWitRequestConfiguration configuration, StringBuilder warnings)
        {
            // Perform request for app info
            var result = await GetRequest(configuration, false).RequestVoiceListAsync();

            // Get results & add warning if needed
            Dictionary<string, WitVoiceInfo[]> voicesByLocale = HandleResults(result, "TTS Voices update failed", warnings);

            // Add all voices by locale
            List<WitVoiceInfo> voiceList = new List<WitVoiceInfo>();
            if (voicesByLocale != null)
            {
                foreach (var voices in voicesByLocale.Values)
                {
                    voiceList.AddRange(voices);
                }
                voiceList.Sort((voice1, voice2) => voice1.name.CompareTo(voice2.name));
            }

            // Set new info
            WitAppInfo appInfo = configuration.GetApplicationInfo();
            appInfo.voices = voiceList.ToArray();
            configuration.SetApplicationInfo(appInfo);
            VLog.I($"Update Info -  TTS Voices update success (Total: {voiceList.Count})");
        }
        #endregion
    }
}
