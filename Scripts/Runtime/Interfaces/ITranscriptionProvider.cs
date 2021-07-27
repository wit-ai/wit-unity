/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.events;

namespace com.facebook.witai.interfaces
{
    public interface ITranscriptionProvider
    {
        /// <summary>
        /// Provides the last transcription value (could be a partial transcription)
        /// </summary>
        string LastTranscription { get; }

        /// <summary>
        /// Callback used to notify Wit subscribers of a partial transcription.
        /// </summary>
        WitTranscriptionEvent OnPartialTranscription { get; }

        /// <summary>
        /// Callback used to notify Wit subscribers of a full transcription
        /// </summary>
        WitTranscriptionEvent OnFullTranscription { get; }

        /// <summary>
        /// Called when wit is activated
        /// </summary>
        void Activate();

        /// <summary>
        /// Called when
        /// </summary>
        void Deactivate();
    }
}
