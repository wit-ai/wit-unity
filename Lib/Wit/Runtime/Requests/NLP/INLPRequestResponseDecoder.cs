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
    /// Interface for NLP request events callbacks
    /// </summary>
    public interface INLPRequestResponseDecoder<TResults>
    {
        /// <summary>
        /// Asynchronously decodes text into a class/struct
        /// </summary>
        /// <param name="rawResponse">The text response from the server</param>
        /// <returns>Returns decoded results</returns>
        TResults Decode(string rawResponse);

        /// <summary>
        /// Determine the response code from the provided results
        /// </summary>
        int GetResponseStatusCode(TResults results);

        /// <summary>
        /// Determine the response error from the provided results
        /// </summary>
        string GetResponseError(TResults results);

        /// <summary>
        /// Determine if the response has a data for a partial response callback
        /// </summary>
        bool GetResponseHasPartial(TResults results);

        /// <summary>
        /// Determine the response transcription if applicable
        /// </summary>
        string GetResponseTranscription(TResults results);

        /// <summary>
        /// Determine if the response has a valid transcription
        /// </summary>
        bool GetResponseHasTranscription(TResults results);

        /// <summary>
        /// Determine if the response's transcription is full
        /// </summary>
        bool GetResponseIsTranscriptionFull(TResults results);
    }
}
