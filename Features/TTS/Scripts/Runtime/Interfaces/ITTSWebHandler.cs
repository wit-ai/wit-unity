/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Threading.Tasks;
using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.Interfaces
{
    public interface ITTSWebHandler
    {
        /// <summary>
        /// Method for determining if there are problems that will arise
        /// with performing a web request prior to doing so
        /// </summary>
        /// <param name="clipData">The clip data to be used for the request</param>
        /// <returns>Invalid error(s).  It will be empty if there are none</returns>
        string GetWebErrors(TTSClipData clipData);

        /// <summary>
        /// Streams audio from a web service and returns an error if applicable.
        /// </summary>
        /// <param name="clipData">Information about the clip being requested.</param>
        /// <param name="onReady">Callback on request is ready for playback.</param>
        Task<string> RequestStreamFromWeb(TTSClipData clipData,
            Action<TTSClipData> onReady);

        /// <summary>
        /// Performs a check to determine if a file is cached to disk or not
        /// </summary>
        /// <param name="diskPath">The path to be checked</param>
        /// <returns>Returns an error if the path cannot be found</returns>
        Task<string> IsDownloadedToDisk(string diskPath);

        /// <summary>
        /// Downloads audio from a web service and returns an error if applicable.
        /// </summary>
        /// <param name="clipData">Information about the clip being requested.</param>
        /// <param name="diskPath">The specific disk path of the file.</param>
        /// <param name="onReady">Callback on request is ready for playback.</param>
        Task<string> RequestStreamFromDisk(TTSClipData clipData,
            string diskPath,
            Action<TTSClipData> onReady);

        /// <summary>
        /// Method for performing a web download request
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <param name="diskPath">The specific disk path the file should be downloaded to</param>
        Task<string> RequestDownloadFromWeb(TTSClipData clipData,
            string diskPath);

        /// <summary>
        /// Cancels any request for a specific clip
        /// </summary>
        bool CancelRequests(TTSClipData clipData);
    }
}
