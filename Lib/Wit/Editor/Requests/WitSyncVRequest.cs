/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    internal class WitSyncVRequest : WitVRequest, IWitSyncVRequest
    {
        /// <summary>
        /// Constructor for wit based editor data sync VRequests
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        public WitSyncVRequest(IWitRequestConfiguration configuration)
            : base(configuration, null, true) {}

        /// <summary>
        /// Submits an intent to be added to the current wit app
        /// </summary>
        /// <param name="intentInfo">The intent data to be submitted</param>
        /// <returns>Returns an intent with unique id if successful</returns>
        public async Task<VRequestResponse<WitIntentInfo>> RequestAddIntent(WitIntentInfo intentInfo)
        {
            string json = JsonConvert.SerializeObject(intentInfo);
            return await RequestWitPost<WitIntentInfo>(WitEditorConstants.ENDPOINT_ADD_INTENT, null, json);
        }

        /// <summary>
        /// Submits an entity to be added to the current wit app
        /// </summary>
        /// <param name="entityInfo">The entity info to be submitted</param>
        /// <returns>Returns an entity with unique id if successful</returns>
        public async Task<VRequestResponse<WitEntityInfo>> RequestAddEntity(WitEntityInfo entityInfo)
        {
            string json = JsonConvert.SerializeObject(entityInfo);
            return await RequestWitPost<WitEntityInfo>(WitEditorConstants.ENDPOINT_ADD_ENTITY, null, json);
        }

        /// <summary>
        /// Submits a keyword to be added to an entity on the current wit app
        /// </summary>
        /// <param name="entityId">The entity this keyword should be added to</param>
        /// <param name="keywordInfo">The keyword and synonyms submitted</param>
        /// <returns>Returns updated entity if successful</returns>
        public async Task<VRequestResponse<WitEntityInfo>> RequestAddEntityKeyword(string entityId,
            WitEntityKeywordInfo keywordInfo)
        {
            string json = JsonConvert.SerializeObject(keywordInfo);
            return await RequestWitPost<WitEntityInfo>($"{WitEditorConstants.ENDPOINT_ADD_ENTITY}/{entityId}/{WitEditorConstants.ENDPOINT_ADD_ENTITY_KEYWORD}",
                null, json);
        }

        /// <summary>
        /// Submits a synonym to be added to a keyword on the specified entity on the current wit app
        /// </summary>
        /// <param name="entityId">The entity that holds the keyword</param>
        /// <param name="keyword">The keyword we're adding the synonym to</param>
        /// <param name="synonym">The synonym we're adding</param>
        /// <returns>Returns updated entity if successful</returns>
        public async Task<VRequestResponse<WitEntityInfo>> RequestAddEntitySynonym(string entityId, string keyword, string synonym)
        {
            var node = new WitResponseClass()
            {
                { "synonym", synonym }
            };
            string json = JsonConvert.SerializeObject(node);
            return await RequestWitPost<WitEntityInfo>(
                $"{WitEditorConstants.ENDPOINT_ENTITIES}/{entityId}/{WitEditorConstants.ENDPOINT_ADD_ENTITY_KEYWORD}/{keyword}/{WitEditorConstants.ENDPOINT_ADD_ENTITY_KEYWORD_SYNONYMS}",
                null, json);
        }

        /// <summary>
        /// Submits a trait to be added to the current wit app
        /// </summary>
        /// <param name="traitInfo">The trait data to be submitted</param>
        /// <returns>Returns a trait with unique id if successful</returns>
        public async Task<VRequestResponse<WitTraitInfo>> RequestAddTrait(WitTraitInfo traitInfo)
        {
            List<JsonConverter> converters = new List<JsonConverter>(JsonConvert.DefaultConverters);
            converters.Add(new WitTraitValueInfoAddConverter());
            string json = JsonConvert.SerializeObject(traitInfo, converters.ToArray());
            return await RequestWitPost<WitTraitInfo>(WitEditorConstants.ENDPOINT_ADD_TRAIT, null, json);
        }
        // Simple trait value converter since post requires string array
        private class WitTraitValueInfoAddConverter : JsonConverter
        {
            public override bool CanWrite => true;
            public override bool CanConvert(Type objectType)
            {
                return typeof(WitTraitValueInfo) == objectType;
            }
            public override WitResponseNode WriteJson(object existingValue)
            {
                return new WitResponseData(((WitTraitValueInfo)existingValue).value);
            }
        }
        /// <summary>
        /// Submits a trait value to be added to the current wit app
        /// </summary>
        /// <param name="traitId">The trait id to be submitted</param>
        /// <param name="traitValue">The trait value to be submitted</param>
        /// <returns>Returns updated trait if successful</returns>
        public async Task<VRequestResponse<WitTraitInfo>> RequestAddTraitValue(string traitId,
            string traitValue)
        {
            WitTraitValueInfo traitValInfo = new WitTraitValueInfo()
            {
                value = traitValue
            };
            string json = JsonConvert.SerializeObject(traitValInfo);
            return await RequestWitPost<WitTraitInfo>($"{WitEditorConstants.ENDPOINT_ADD_TRAIT}/{traitId}/{WitEditorConstants.ENDPOINT_ADD_TRAIT_VALUE}",
                null, json);
        }

        /// <summary>
        /// Import app data from generated manifest JSON
        /// </summary>
        /// <param name="manifestData">The serialized manifest to import from</param>
        /// <returns>Built request object</returns>
        public async Task<VRequestResponse<WitResponseNode>> RequestImportData(string manifestData)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();
            queryParams["name"] = Configuration.GetApplicationId();
            queryParams["private"] = "true";
            queryParams["action_graph"] = "true";
            var jsonNode = new WitResponseClass()
            {
                { "text", manifestData ?? string.Empty },
                { "config_type", "1" },
                { "config_value", "" }
            };
            string json = JsonConvert.SerializeObject(jsonNode);
            return await RequestWitPost<WitResponseNode>(WitEditorConstants.ENDPOINT_IMPORT, queryParams, json);
        }
    }
}
