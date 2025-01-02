/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Linq;
using Meta.WitAi.Json;

namespace Meta.Voice.Net.Encoding.Wit
{
    /// <summary>
    /// A class used to store json data & binary data
    /// </summary>
    public struct WitChunk
    {
        /// <summary>
        /// The header to be used for the chunk
        /// </summary>
        public WitChunkHeader header;

        /// <summary>
        /// Json string from prior to json data decode
        /// </summary>
        public string jsonString;

        /// <summary>
        /// Decoded json data
        /// </summary>
        public WitResponseNode jsonData;

        /// <summary>
        /// Binary data ready for ingestion
        /// </summary>
        public byte[] binaryData;

        public override bool Equals(object other)
        {
            if (other is WitChunk otherChunk)
            {
                return Equals(otherChunk);
            }

            return false;
        }

        private bool Equals(WitChunk other)
        {
            return header.Equals(other.header) && jsonString == other.jsonString && Equals(jsonData, other.jsonData) && binaryData.SequenceEqual(other.binaryData);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(header, jsonString, jsonData, binaryData);
        }
    }
}
