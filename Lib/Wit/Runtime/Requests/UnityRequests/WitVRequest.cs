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
    internal class WitVRequest : VRequest, IWitVRequest
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
        public WitVRequest(IWitRequestConfiguration configuration, string requestId, bool useServerToken = false)
            : base()
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

        // Check for local file
        protected bool IsLocalFile()
            => !string.IsNullOrEmpty(Url) && Url.StartsWith(FilePrepend);

        // Gets uri using the specified url and parameters
        protected override Uri GetUri()
        {
            if (IsLocalFile())
            {
                return base.GetUri();
            }
            return WitRequestSettings.GetUri(Configuration, Url, UrlParameters);
        }

        // Gets wit headers using static header generation
        protected override Dictionary<string, string> GetHeaders()
        {
            if (IsLocalFile())
            {
                return base.GetHeaders();
            }
            return WitRequestSettings.GetHeaders(Configuration, RequestId, _useServerToken);
        }

        /// <summary>
        /// Override base class to ensure configuration is correct
        /// </summary>
        public override async Task<VRequestResponse<TValue>> Request<TValue>(VRequestDecodeDelegate<TValue> decoder)
        {
            if (Configuration == null)
            {
                return new VRequestResponse<TValue>(WitConstants.ERROR_CODE_GENERAL, "No wit configuration set");
            }
            return await base.Request(decoder);
        }

        /// <summary>
        /// Get request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        public async Task<VRequestResponse<TValue>> RequestWitGet<TValue>(string endpoint,
            Dictionary<string, string> urlParameters = null,
            Action<TValue> onPartial = null)
        {
            Url = endpoint;
            UrlParameters = urlParameters;
            return await RequestJsonGet(onPartial);
        }

        /// <summary>
        /// Post request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="payload">Text data that will be uploaded via post.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        public async Task<VRequestResponse<TValue>> RequestWitPost<TValue>(string endpoint,
            Dictionary<string, string> urlParameters,
            string payload,
            Action<TValue> onPartial = null)
        {
            Url = endpoint;
            UrlParameters = urlParameters;
            return await RequestJsonPost(payload, onPartial);
        }

        /// <summary>
        /// Put request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="payload">Text data that will be uploaded via put.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        public async Task<VRequestResponse<TValue>> RequestWitPut<TValue>(string endpoint,
            Dictionary<string, string> urlParameters,
            string payload,
            Action<TValue> onPartial = null)
        {
            Url = endpoint;
            UrlParameters = urlParameters;
            return await RequestJsonPut(payload, onPartial);
        }
    }
}
