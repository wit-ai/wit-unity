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
    /// The various types of NLP request datas
    /// </summary>
    public enum NLPRequestInputType
    {
        Text,
        Audio
    }

    /// <summary>
    /// Interface for NLP request parameters
    /// </summary>
    public interface INLPRequestOptions : ITranscriptionRequestOptions
    {
        /// <summary>
        /// The input type used by the NLP request
        /// </summary>
        NLPRequestInputType InputType { get; set; }

        /// <summary>
        /// The text to be processed via a text based NLP request
        /// </summary>
        string Text { get; set; }
    }
}
