/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;

namespace Meta.Voice.Net.Encoding.Wit
{
    /// <summary>
    /// A class used to store json data & binary data
    /// </summary>
    public class WitChunk
    {
        /// <summary>
        /// Json data ready to be converted
        /// </summary>
        public WitResponseNode jsonData;

        /// <summary>
        /// Binary data ready for ingestion
        /// </summary>
        public byte[] binaryData;

        /// <summary>
        /// ToString override for additional info on json data & binary data
        /// </summary>
        public override string ToString()
        {
            return $"{GetType().Name}\n\tJson Keys: {jsonData?.Count.ToString() ?? "Null"}\n\tBinary Data: {binaryData?.Length.ToString() ?? "Null"}";
        }
    }
}
