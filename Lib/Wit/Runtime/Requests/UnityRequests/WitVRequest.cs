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
        public static event Func<UriBuilder, UriBuilder> OnProvideCustomUri;
        /// <summary>
        /// Header customization delegate
        /// </summary>
        public static event Action<Dictionary<string, string>> OnProvideCustomHeaders;
        /// <summary>
        /// User agent customization delegate
        /// </summary>
        public static event Action<StringBuilder> OnProvideCustomUserAgent;

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
            if (string.IsNullOrEmpty(RequestId))
            {
                RequestId = Guid.NewGuid().ToString();
            }
            _useServerToken = useServerToken;
        }

        // Return uri
        public Uri GetUri(string path, Dictionary<string, string> queryParams = null)
        {
            return GetWitUri(Configuration, path, queryParams);
        }

        // Gets wit headers using static header generation
        protected override Dictionary<string, string> GetHeaders()
        {
            return GetWitHeaders(Configuration, RequestId, _useServerToken);
        }

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
            RequestCompleteDelegate<TData> onPartial = null) =>
            RequestJsonGet(GetUri(uriEndpoint, uriParams), onComplete, onPartial);

        /// <summary>
        /// Get request to a wit endpoint asynchronously
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <returns>Returns the request complete data including a parsed result if possible</returns>
        public async Task<RequestCompleteResponse<TData>> RequestWitGetAsync<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams = null,
            RequestCompleteDelegate<TData> onPartial = null) =>
            await RequestJsonGetAsync<TData>(GetUri(uriEndpoint, uriParams), onPartial);

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
            RequestCompleteDelegate<TData> onPartial = null) =>
            RequestJsonPost(GetUri(uriEndpoint, uriParams), postText, onComplete, onPartial);

        /// <summary>
        /// Post request to a wit endpoint asynchronously
        /// </summary>
        /// <param name="uriEndpoint">Endpoint name</param>
        /// <param name="uriParams">Endpoint url parameters</param>
        /// <param name="postText">Text to be sent to endpoint</param>
        /// <returns>Returns the request complete data including a parsed result if possible</returns>
        public async Task<RequestCompleteResponse<TData>> RequestWitPostAsync<TData>(string uriEndpoint,
            Dictionary<string, string> uriParams, string postText,
            RequestCompleteDelegate<TData> onPartial = null) =>
            await RequestJsonPostAsync<TData>(GetUri(uriEndpoint, uriParams), postText, onPartial);

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
            RequestCompleteDelegate<TData> onPartial = null) =>
            RequestJsonPut(GetUri(uriEndpoint, uriParams), putText, onComplete, onPartial);

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
            => await RequestJsonPutAsync<TData>(GetUri(uriEndpoint, uriParams), putText, onPartial);
        #endregion

        #region STATIC
        /// <summary>
        /// Get custom wit uri using a specific path & query parameters
        /// </summary>
        public static Uri GetWitUri(IWitRequestConfiguration configuration, string path, Dictionary<string, string> queryParams = null)
        {
            // Uri builder
            UriBuilder uriBuilder = new UriBuilder();

            // Append endpoint data
            IWitRequestEndpointInfo endpoint = configuration.GetEndpointInfo();
            uriBuilder.Scheme = endpoint.UriScheme;
            uriBuilder.Host = endpoint.Authority;
            uriBuilder.Port = endpoint.Port;

            // Set path
            uriBuilder.Path = path;

            // Build query
            string apiVersion = endpoint.WitApiVersion;
            uriBuilder.Query = $"v={apiVersion}";
            if (queryParams != null)
            {
                foreach (string key in queryParams.Keys)
                {
                    var value = UnityWebRequest.EscapeURL(queryParams[key]).Replace("+", "%20");
                    uriBuilder.Query += $"&{key}={value}";
                }
            }

            // Return custom uri
            if (OnProvideCustomUri != null)
            {
                foreach (Func<UriBuilder, UriBuilder> del in OnProvideCustomUri.GetInvocationList())
                {
                    uriBuilder = del(uriBuilder);
                }
            }

            // Return uri
            return uriBuilder.Uri;
        }
        /// <summary>
        /// Obtain headers to be used with every wit service
        /// </summary>
        public static Dictionary<string, string> GetWitHeaders(IWitRequestConfiguration configuration, string requestId, bool useServerToken)
        {
            // Get headers
            Dictionary<string, string> headers = new Dictionary<string, string>();

            // Set authorization
            headers[WitConstants.HEADER_AUTH] = GetAuthorizationHeader(configuration, useServerToken);

            #if UNITY_EDITOR || !UNITY_WEBGL
            // Set request id
            headers[WitConstants.HEADER_REQUEST_ID] = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString() : requestId;
            // Set User-Agent
            headers[WitConstants.HEADER_USERAGENT] = GetUserAgentHeader(configuration);
            #endif

            // Allow overrides
            if (OnProvideCustomHeaders != null)
            {
                // Allow overrides
                foreach (Action<Dictionary<string, string>> del in OnProvideCustomHeaders.GetInvocationList())
                {
                    del(headers);
                }
            }

            // Return results
            return headers;
        }
        /// <summary>
        /// Obtain authorization header using provided access token
        /// </summary>
        private static string GetAuthorizationHeader(IWitRequestConfiguration configuration, bool useServerToken)
        {
            // Default to client access token
            string token = configuration.GetClientAccessToken();
            // Use server token
            if (useServerToken)
            {
                #if UNITY_EDITOR
                token = configuration.GetServerAccessToken();
                #else
                token = string.Empty;
                #endif
            }
            // Trim token
            if (!string.IsNullOrEmpty(token))
            {
                token = token.Trim();
            }
            // Use invalid token
            else
            {
                token = "XXX";
            }
            // Return with bearer
            return $"Bearer {token}";
        }
        // Build and return user agent header
        private static string _operatingSystem;
        private static string _deviceModel;
        private static string _appIdentifier;
        private static string _unityVersion;
        // Preloads settings if needed
        public static void PreloadSettings()
        {
            if (_operatingSystem == null) _operatingSystem = UnityEngine.SystemInfo.operatingSystem;
            if (_deviceModel == null) _deviceModel = UnityEngine.SystemInfo.deviceModel;
            if (_appIdentifier == null) _appIdentifier = Application.identifier;
            if (_unityVersion == null) _unityVersion = Application.unityVersion;
        }

        private static string GetUserAgentHeader(IWitRequestConfiguration configuration)
        {
            // Preload settings jic
            PreloadSettings();

            // Generate user agent
            StringBuilder userAgent = new StringBuilder();

            // Append wit sdk version
            userAgent.Append($"wit-unity-{WitConstants.SDK_VERSION}");

            // Append operating system
            userAgent.Append($",\"{_operatingSystem}\"");
            // Append device model
            userAgent.Append($",\"{_deviceModel}\"");

            // Append configuration log id
            string logId = configuration.GetConfigurationId();
            if (string.IsNullOrEmpty(logId))
            {
                logId = WitConstants.HEADER_USERAGENT_CONFID_MISSING;
            }
            userAgent.Append($",{logId}");

            // Append app identifier
            userAgent.Append($",{_appIdentifier}");

            // Append editor identifier
            #if UNITY_EDITOR
            userAgent.Append(",Editor");
            #else
            userAgent.Append(",Runtime");
            #endif

            // Append unity version
            userAgent.Append($",{_unityVersion}");

            // Set custom user agent
            if (OnProvideCustomUserAgent != null)
            {
                foreach (Action<StringBuilder> del in OnProvideCustomUserAgent.GetInvocationList())
                {
                    del(userAgent);
                }
            }

            // Return user agent
            return userAgent.ToString();
        }
        #endregion
    }
}
