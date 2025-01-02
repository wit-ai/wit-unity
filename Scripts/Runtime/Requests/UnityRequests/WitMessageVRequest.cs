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

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Class used to perform message requests
    /// </summary>
    internal class WitMessageVRequest : WitVRequest
    {
        /// <summary>
        /// Constructor that takes in configuration and request id
        /// </summary>
        public WitMessageVRequest(IWitRequestConfiguration configuration, string requestId) : base(configuration, requestId)
        {

        }

        /// <summary>
        /// Voice message request
        /// </summary>
        /// <param name="endpoint">Endpoint to be used for possible overrides</param>
        /// <param name="post">Will perform a POST if true, will perform a GET otherwise</param>
        /// <param name="text">Text to be sent to message endpoint</param>
        /// <param name="urlParameters">Parameters to be sent to the endpoint</param>
        public Task<VRequestResponse<string>> MessageRequest(string endpoint,
            bool post,
            string text,
            Dictionary<string, string> urlParameters,
            Action<string> onPartial = null)
        {
            // Add text to uri parameters
            urlParameters ??= new Dictionary<string, string>();

            // Perform a get request
            if (!post)
            {
                urlParameters[WitConstants.ENDPOINT_MESSAGE_PARAM] = text;
                return RequestWitGet(endpoint, urlParameters, onPartial);
            }

            // Perform a post request
            return RequestWitPost(endpoint, urlParameters, text, onPartial);
        }
    }
}
