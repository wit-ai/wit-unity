/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Threading.Tasks;
using Meta.Voice;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A decoder for WitResponseNode response data
    /// </summary>
    public class WitResponseDecoder : INLPRequestResponseDecoder<WitResponseNode>
    {
        /// <inheritdoc/>
        public WitResponseNode Decode(string rawResponse) =>
            JsonConvert.DeserializeToken(rawResponse);

        /// <inheritdoc/>
        public int GetResponseStatusCode(WitResponseNode results) =>
            results.GetStatusCode();

        /// <inheritdoc/>
        public string GetResponseError(WitResponseNode results) =>
            results.GetError();

        /// <inheritdoc/>
        public bool GetResponseHasPartial(WitResponseNode results) =>
            !results.GetHasTranscription();

        /// <inheritdoc/>
        public string GetResponseTranscription(WitResponseNode results) =>
            results.GetTranscription();

        /// <inheritdoc/>
        public bool GetResponseHasTranscription(WitResponseNode results) =>
            results.GetHasTranscription();

        /// <inheritdoc/>
        public bool GetResponseIsTranscriptionFull(WitResponseNode results) =>
            results.GetIsTranscriptionFinal();
    }
}
