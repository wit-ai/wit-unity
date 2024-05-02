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
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.WitAi.Requests
{
    public class WitVRequest : VRequest
    {
        /// <summary>
        /// Uri customization delegate
        /// </summary>
        [Obsolete("Use WitRequestSettings.OnProvideCustomUri instead.")]
        public static Func<UriBuilder, UriBuilder> OnProvideCustomUri
            => WitRequestSettings.OnProvideCustomUri;
        /// <summary>
        /// Header customization delegate
        /// </summary>
        [Obsolete("Use WitRequestSettings.OnProvideCustomHeaders instead.")]
        public static Action<Dictionary<string, string>> OnProvideCustomHeaders
            => WitRequestSettings.OnProvideCustomHeaders;
        /// <summary>
        /// User agent customization delegate
        /// </summary>
        [Obsolete("Use WitRequestSettings.OnProvideCustomUserAgent instead.")]
        public static Action<StringBuilder> OnProvideCustomUserAgent
            => WitRequestSettings.OnProvideCustomUserAgent;

        /// <summary>
        /// The unique identifier used by Wit to track requests
        /// </summary>
        public string RequestId { get; private set; }

        /// <summary>
        /// The configuration used for voice requests
        /// </summary>
        public IWitRequestConfiguration Configuration { get; private set; }

        // Whether or not the configuration's server token should be used
        private bool _useServerToken;

        /// <summary>
        /// Constructor that takes in a configuration interface
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        /// <param name="requestId">A unique identifier that can be used to track the request</param>
        /// <param name="useServerToken">Editor only option to use server token instead of client token</param>
        /// <param name="onDownloadProgress">The callback for progress related to downloading</param>
        /// <param name="onFirstResponse">The callback for the first response of data from a request</param>
        public WitVRequest(IWitRequestConfiguration configuration, string requestId, bool useServerToken = false,
            RequestProgressDelegate onDownloadProgress = null,
            RequestFirstResponseDelegate onFirstResponse = null) : base(onDownloadProgress, onFirstResponse)
        {
            Configuration = configuration;
            RequestId = requestId;
            Timeout = Mathf.RoundToInt(configuration.RequestTimeoutMs / 1000f);
            if (string.IsNullOrEmpty(RequestId))
            {
                RequestId = WitConstants.GetUniqueId();
            }
            _useServerToken = useServerToken;
        }

        // Return uri
        public Uri GetUri(string path, Dictionary<string, string> queryParams = null)
            => WitRequestSettings.GetUri(Configuration, path, queryParams);

        // Gets wit headers using static header generation
        protected override Dictionary<string, string> GetHeaders()
            => WitRequestSettings.GetHeaders(Configuration, RequestId, _useServerToken);

        #region REQUESTS
        /// <summary>
        /// Perform a unity request with coroutines
        /// </summary>
        /// <param name="unityRequest">The request to be managed</param>
        /// <param name="onComplete">The callback delegate on request completion</param>
        /// <returns>False if the request cannot be performed</returns>
        public override bool Request(UnityWebRequest unityRequest,
            RequestCompleteDelegate<UnityWebRequest> onComplete)
        {
            // Ensure configuration is set
            if (Configuration == null)
            {
                onComplete?.Invoke(unityRequest, "Cannot perform a request without a Wit configuration");
                return false;
            }

            // Perform base
            return base.Request(unityRequest, onComplete);
        }

        /// <summary>
        /// Initialize with a request & return an error if applicable
        /// </summary>
        /// <param name="unityRequest">The unity request to be performed</param>
        /// <returns>Any errors encountered during the request</returns>
        public override async Task<RequestCompleteResponse<TData>> RequestAsync<TData>(UnityWebRequest unityRequest,
            Func<UnityWebRequest, TData> onDecode)
        {
            // Ensure configuration is set
            if (Configuration == null)
            {
                return new RequestCompleteResponse<TData>("Cannot perform a request without a Wit configuration");
            }

            // Perform base
            return await base.RequestAsync(unityRequest, onDecode);
        }

        /// <summary>
        /// Get request to a wit endpoint
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="onComplete">The callback delegate on request completion</param>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestWitGet<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = string.Empty;
            return RequestJsonGet(uri, onComplete, onPartial);
        }

        /// <summary>
        /// Get request to a wit endpoint asynchronously
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <returns>Returns the request complete data including a parsed result if possible</returns>
        public async Task<RequestCompleteResponse<TData>> RequestWitGetAsync<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams = null,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = string.Empty;
            return await RequestJsonGetAsync<TData>(uri, onPartial);
        }

        /// <summary>
        /// Post text request to a wit endpoint
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="postText">Text to be sent to endpoint</param>
        /// <param name="onComplete">The callback delegate on request completion</param>
        /// <param name="onPartial">The callback delegate when a partial response is received</param>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestWitPost<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams, string postText,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = postText;
            return RequestJsonPost(uri, postText, onComplete, onPartial);
        }

        /// <summary>
        /// Post request to a wit endpoint asynchronously
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="postText">Text to be sent to endpoint</param>
        /// <returns>Returns the request complete data including a parsed result if possible</returns>
        public async Task<RequestCompleteResponse<TData>> RequestWitPostAsync<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams, string postText,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = postText;
            return await RequestJsonPostAsync<TData>(uri, postText, onPartial);
        }

        /// <summary>
        /// Put text request to a wit endpoint
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="putText">Text to be sent to endpoint</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <param name="onPartial">The callback delegate when a partial response is received</param>
        /// <param name="onProgress">The upload progress</param>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestWitPut<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams, string putText,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = putText;
            return RequestJsonPut(uri, putText, onComplete, onPartial);
        }

        /// <summary>
        /// Put text request to a wit endpoint asynchronously
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="putText">Text to be sent to endpoint</param>
        /// <returns>Returns the request complete data including a parsed result if possible</returns>
        public async Task<RequestCompleteResponse<TData>> RequestWitPutAsync<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams, string putText,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            uri = GetUri(uriEndpoint, uriParams);
            payload = putText;
            return await RequestJsonPutAsync<TData>(uri, putText, onPartial);
        }

        #endregion
    }
}
