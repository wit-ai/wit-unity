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
using UnityEngine;
using Meta.Voice.Audio.Decoding;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Interface for all request options
    /// </summary>
    internal interface IVRequest
    {
        /// <summary>
        /// Cancel any requests
        /// </summary>
        void Cancel();

        /// <summary>
        /// Performs a request from the main thread or a background thread
        /// </summary>
        Task<VRequestResponse<TValue>> Request<TValue>(VRequestDecodeDelegate<TValue> decoder);

        /// <summary>
        /// Performs a header request on a uri
        /// </summary>
        Task<VRequestResponse<Dictionary<string, string>>> RequestFileHeaders(string url);

        /// <summary>
        /// Performs a get request on a file url
        /// </summary>
        Task<VRequestResponse<byte[]>> RequestFile(string url);

        /// <summary>
        /// Performs a download from a specified location to a new location
        /// </summary>
        Task<VRequestResponse<bool>> RequestFileDownload(string url,
            string downloadPath);

        /// <summary>
        /// Checks if a file exists at a specified location using async calls
        /// </summary>
        Task<VRequestResponse<bool>> RequestFileExists(string url);

        /// <summary>
        /// Performs a text request with an option partial response callback
        /// </summary>
        Task<VRequestResponse<string>> RequestText(Action<string> onPartial = null);

        /// <summary>
        /// Performs a request for text & decodes it into json
        /// </summary>
        Task<VRequestResponse<TData>> RequestJson<TData>(Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json get request with the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonGet<TData>(Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json get request with the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPost<TData>(Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json post request with raw data and the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPost<TData>(byte[] postData,
            Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json post request with string data and the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPost<TData>(string postText,
            Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json put request with the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPut<TData>(Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json put request with raw data and the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPut<TData>(byte[] postData,
            Action<TData> onPartial = null);

        /// <summary>
        /// Perform a json put request with string data and the option for a partial response
        /// </summary>
        Task<VRequestResponse<TData>> RequestJsonPut<TData>(string postText,
            Action<TData> onPartial = null);
    }
}
