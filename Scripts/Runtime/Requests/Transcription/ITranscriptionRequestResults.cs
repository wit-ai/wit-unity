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
    /// Interface for audio transcription specific requests
    /// </summary>
    public interface ITranscriptionRequestResults : IVoiceRequestResults
    {
        /// <summary>
        /// The current audio transcription received from the request
        /// Should only be set by TranscriptionRequests
        /// </summary>
        string Transcription { get; }

        /// <summary>
        /// An array of all finalized transcriptions
        /// </summary>
        string[] FinalTranscriptions { get; }

        /// <summary>
        /// A setter for transcriptions
        /// </summary>
        /// <param name="transcription">The transcription to be set</param>
        /// <param name="full">Whether the transcription should be considered
        /// full or partially complete</param>
        void SetTranscription(string transcription, bool full);
    }
}
