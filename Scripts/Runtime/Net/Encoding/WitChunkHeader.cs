/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Net.Encoding.Wit
{
    /// <summary>
    /// A struct that contains information on the following WitChunk
    /// </summary>
    public struct WitChunkHeader
    {
        /// <summary>
        /// Whether this WitChunk header has invalid data
        /// </summary>
        public bool invalid;

        /// <summary>
        /// The byte length of json string data
        /// </summary>
        public int jsonLength;

        /// <summary>
        /// The byte length of binary data
        /// </summary>
        public ulong binaryLength;
    }
}
