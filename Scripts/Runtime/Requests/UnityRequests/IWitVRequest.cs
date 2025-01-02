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
    /// Interface for wit specific requests
    /// </summary>
    internal interface IWitVRequest : IVRequest
    {
        /// <summary>
        /// Get request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        Task<VRequestResponse<TValue>> RequestWitGet<TValue>(string endpoint,
            Dictionary<string, string> urlParameters,
            Action<TValue> onPartial = null);

        /// <summary>
        /// Post request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="payload">Text data that will be uploaded via post.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        Task<VRequestResponse<TValue>> RequestWitPost<TValue>(string endpoint,
            Dictionary<string, string> urlParameters,
            string payload,
            Action<TValue> onPartial = null);

        /// <summary>
        /// Put request to a wit endpoint
        /// </summary>
        /// <param name="endpoint">The wit endpoint for the request. Ex. 'synthesize'</param>
        /// <param name="urlParameters">Any parameters to be added to the url prior to request.</param>
        /// <param name="payload">Text data that will be uploaded via put.</param>
        /// <param name="onPartial">If provided, this will call back for every incremental json chunk.</param>
        /// <returns>An awaitable task thar contains the final decoded json results</returns>
        public Task<VRequestResponse<TValue>> RequestWitPut<TValue>(string endpoint,
            Dictionary<string, string> urlParameters,
            string payload,
            Action<TValue> onPartial = null);
    }
}
