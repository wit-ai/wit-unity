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
using UnityEngine.Networking;

namespace Meta.WitAi
{
    /// <summary>
    /// A static script for obtaining header information related to wit requests
    /// </summary>
    public static class WitRequestSettings
    {
        // User-agent specific information
        private static string _operatingSystem;
        private static string _deviceModel;
        private static string _appIdentifier;
        private static string _unityVersion;

        /// <summary>
        /// Uri customization delegate
        /// </summary>
        public static Func<UriBuilder, UriBuilder> OnProvideCustomUri;
        /// <summary>
        /// Header customization delegate
        /// </summary>
        public static Action<Dictionary<string, string>> OnProvideCustomHeaders;
        /// <summary>
        /// User agent customization delegate
        /// </summary>
        public static Action<StringBuilder> OnProvideCustomUserAgent;

        /// <summary>
        /// Preloads all user-agent data
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            if (_operatingSystem == null) _operatingSystem = SystemInfo.operatingSystem;
            if (_deviceModel == null) _deviceModel = SystemInfo.deviceModel;
            if (_appIdentifier == null) _appIdentifier = Application.identifier;
            if (_unityVersion == null) _unityVersion = Application.unityVersion;
        }

        /// <summary>
        /// Get custom wit uri using a specific path & query parameters
        /// </summary>
        public static Uri GetUri(IWitRequestConfiguration configuration, string path, Dictionary<string, string> queryParams = null)
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
                    var value = queryParams[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = UnityWebRequest.EscapeURL(value).Replace("+", "%20");
                        uriBuilder.Query += $"&{key}={value}";
                    }
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
        public static Dictionary<string, string> GetHeaders(IWitRequestConfiguration configuration, string requestId, bool useServerToken)
        {
            // Ensure init method is called prior to get headers
            // Needed since this can be called in editor as well
            Init();

            // Get headers
            Dictionary<string, string> headers = new Dictionary<string, string>();

            // Set authorization
            headers[WitConstants.HEADER_AUTH] = GetAuthorizationHeader(configuration, useServerToken);

            #if UNITY_EDITOR || !UNITY_WEBGL
            // Set request id
            headers[WitConstants.HEADER_REQUEST_ID] = string.IsNullOrEmpty(requestId) ? WitConstants.GetUniqueId() : requestId;
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

        /// <summary>
        /// Build all user agent header data using specified information
        /// </summary>
        private static string GetUserAgentHeader(IWitRequestConfiguration configuration)
        {
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
    }
}
