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
    /// The base interface for all request results
    /// </summary>
    public interface IVoiceRequestResults
    {
        /// <summary>
        /// The current request status code
        /// </summary>
        int StatusCode { get; }

        /// <summary>
        /// A message received from either an error
        /// or a cancellation request
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Sets results to cancellation status code with a specified reason
        /// </summary>
        void SetCancel(string reason);

        /// <summary>
        /// Sets results error message & error status
        /// </summary>
        void SetError(int errorStatusCode, string error);
    }
}
