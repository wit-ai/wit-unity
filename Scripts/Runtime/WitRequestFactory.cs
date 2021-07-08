/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using com.facebook.witai.data;

namespace com.facebook.witai
{
    public static class WitRequestFactory
    {
        private static WitRequest.QueryParam QueryParam(string key, string value)
        {
            return new WitRequest.QueryParam() { key = key, value = value };
        }

        /// <summary>
        /// Creates a message request that will process a query string with NLU
        /// </summary>
        /// <param name="config"></param>
        /// <param name="query">Text string to process with the NLU</param>
        /// <returns></returns>
        public static WitRequest MessageRequest(this WitConfiguration config, string query)
        {
            return new WitRequest(config, "message", QueryParam("q", query));
        }

        /// <summary>
        /// Creates a request for nlu processing that includes a data stream for mic data
        /// </summary>
        /// <param name="config"></param>
        /// <param name="maxBestIntents"></param>
        /// <returns></returns>
        public static WitRequest SpeechRequest(this WitConfiguration config, int maxBestIntents = 1)
        {
            return new WitRequest(config, "speech", QueryParam("n", maxBestIntents.ToString()));
        }

        /// <summary>
        /// Requests a list of intents available under this configuration
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static WitRequest ListIntentsRequest(this WitConfiguration config)
        {
            return new WitRequest(config, "intents");
        }

        /// <summary>
        /// Requests details on a specific intent
        /// </summary>
        /// <param name="config"></param>
        /// <param name="intentName">The name of the defined intent</param>
        /// <returns></returns>
        public static WitRequest GetIntentRequest(this WitConfiguration config, string intentName)
        {
            return new WitRequest(config, $"intents/{intentName}");
        }

        /// <summary>
        /// Requests a list of utterances
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static WitRequest ListUtterancesRequest(this WitConfiguration config)
        {
            return new WitRequest(config, "utterances");
        }

        #region IDE Only Requests
        #if UNITY_EDITOR

        /// <summary>
        /// Requests a list of available entites
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static WitRequest ListEntitiesRequest(this WitConfiguration config)
        {
            return new WitRequest(config, "entities", true);
        }

        /// <summary>
        /// Requests details of a specific entity
        /// </summary>
        /// <param name="config"></param>
        /// <param name="entityName">The name of the entity as it is defined in wit.ai</param>
        /// <returns></returns>
        public static WitRequest GetEntityRequest(this WitConfiguration config, string entityName)
        {
            return new WitRequest(config, $"entities/{entityName}", true);
        }

        /// <summary>
        /// Requests a list of apps available to the account defined in the WitConfiguration
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static WitRequest ListAppsRequest(this WitConfiguration config, int limit, int offset = 0)
        {
            return new WitRequest(config, "apps", true,
                QueryParam("limit", limit.ToString()),
                QueryParam("offset", offset.ToString()));
        }

        /// <summary>
        /// Requests details for a specific application
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appId">The id of the app as it is defined in wit.ai</param>
        /// <returns></returns>
        public static WitRequest GetAppRequest(this WitConfiguration config, string appId)
        {
            return new WitRequest(config, $"apps/{appId}", true);
        }

        /// <summary>
        /// Requests a client token for an application
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appId">The id of the app as it is defined in wit.ai</param>
        /// <param name="refresh">Should the token be refreshed</param>
        /// <returns></returns>
        public static WitRequest GetClientToken(this WitConfiguration config, string appId, bool refresh = false)
        {
            var postString = "{\"refresh\":" + refresh.ToString().ToLower() + "}";
            var postData = Encoding.UTF8.GetBytes(postString);
            var request = new WitRequest(config, $"apps/{appId}/client_tokens", true)
            {
                postContentType = "application/json",
                postData = postData
            };

            return request;
        }
        #endif
        #endregion
    }
}
