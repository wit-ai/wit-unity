/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice
{
    /// <summary>
    /// Interface for NLP request results
    /// </summary>
    /// <typeparam name="TResponseData">Type of NLP data received from the request</typeparam>
    public interface INLPRequestResults<TResponseData> : ITranscriptionRequestResults
    {
        /// <summary>
        /// Processed data from the request
        /// Should only be set by NLPRequests
        /// </summary>
        TResponseData ResponseData { get; }

        /// <summary>
        /// A setter for response data
        /// </summary>
        /// <param name="responseData">The response data to be set</param>
        void SetResponseData(TResponseData responseData);
    }
}
