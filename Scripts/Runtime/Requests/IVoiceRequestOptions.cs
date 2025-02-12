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
    /// The base interface for all request options
    /// </summary>
    public interface IVoiceRequestOptions
    {
        /// <summary>
        /// The unique request identifier
        /// </summary>
        string RequestId { get; }

        /// <summary>
        /// The unique client user identifier
        /// </summary>
        string ClientUserId { get; }

        /// <summary>
        /// The unique operation ID for tracking the conversation from end to end
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Request timeout is milliseconds
        /// </summary>
        int TimeoutMs { get; set; }
    }
}
