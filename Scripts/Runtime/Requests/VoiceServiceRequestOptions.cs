/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.Voice;
using Meta.Voice.TelemetryUtilities;

namespace Meta.WitAi.Requests
{
    public class VoiceServiceRequestOptions : INLPRequestOptions
    {
        /// <summary>
        /// Unique request id used for request tracking internally & externally
        /// </summary>
        public string RequestId { get; private set; }
        /// <summary>
        /// Unique client user id used for tracking user that made specified request
        /// </summary>
        public string ClientUserId { get; private set; }

        /// <summary>
        /// Unique operation ID to assist with telemetry through communications
        /// with other processes
        /// </summary>
        public string OperationId { get; set; }

        /// <inheritdoc/>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Additional request query parameters to be sent with the request
        /// </summary>
        public Dictionary<string, string> QueryParams { get; private set; }
        public class QueryParam
        {
            public string key;
            public string value;
        }

        /// <summary>
        /// The input type to be used for VoiceServiceRequests
        /// </summary>
        public NLPRequestInputType InputType { get; set; }
        /// <summary>
        /// The text to be submitted for a text request
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// The threshold to be used for an audio request
        /// </summary>
        public float AudioThreshold { get; set; }

        /// <summary>
        /// Setup with a specific request id and user id
        /// </summary>
        public VoiceServiceRequestOptions(string newRequestId, string newClientUserId, string newOperationId, params QueryParam[] newParams)
        {
            RequestId = string.IsNullOrEmpty(newRequestId) ? WitConstants.GetUniqueId() : newRequestId;
            ClientUserId = string.IsNullOrEmpty(newClientUserId) ? WitRequestSettings.LocalClientUserId : newClientUserId;
            OperationId = string.IsNullOrEmpty(newOperationId) ? WitConstants.GetUniqueId() : newOperationId;
            QueryParams = ConvertQueryParams(newParams);
        }

        /// <summary>
        /// Setup with a randomly generated guid
        /// </summary>
        public VoiceServiceRequestOptions(params QueryParam[] newParams)
            : this(null, newParams){}

        /// <summary>
        /// Setup with a specific request guid
        /// </summary>
        public VoiceServiceRequestOptions(string newRequestId, params QueryParam[] newParams)
            : this(newRequestId, null, null, newParams){}

        /// <summary>
        /// Generates a dictionary of key/value strings from a query param array
        /// </summary>
        public static Dictionary<string, string> ConvertQueryParams(QueryParam[] newParams)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            if (newParams != null) {
              foreach (var param in newParams)
              {
                if (!string.IsNullOrEmpty(param.key))
                {
                  results[param.key] = results[param.value];
                }
              }
            }
            return results;
        }

        /// <summary>
        /// Change the opId for options
        /// Useful to track who's altering it rather than just making it's setter public
        /// </summary>
        public void SetOperationId(string opId) {
          OperationId = opId;
        }
    }
}
