/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Json;
using UnityEngine.Events;

namespace Meta.Voice
{
    /// <summary>
    /// A unity event that returns a decoded WitResponseNode
    /// </summary>
    [Serializable]
    public class NLPRequestResponseEvent : UnityEvent<WitResponseNode> {}

    /// <summary>
    /// Interface for NLP request events callbacks
    /// </summary>
    public interface INLPRequestEvents<TUnityEvent>
        : ITranscriptionRequestEvents<TUnityEvent>
        where TUnityEvent : UnityEventBase
    {
        /// <summary>
        /// Called on request language processing raw text received
        /// </summary>
        TranscriptionRequestEvent OnRawResponse { get; }

        /// <summary>
        /// Called on request language processing when received early
        /// </summary>
        NLPRequestResponseEvent OnPartialResponse { get; }

        /// <summary>
        /// Called on request language processing once completely analyzed
        /// </summary>
        NLPRequestResponseEvent OnFullResponse { get; }
    }
}
