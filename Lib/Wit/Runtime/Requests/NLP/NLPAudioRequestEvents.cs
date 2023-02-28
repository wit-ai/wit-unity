/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.Voice
{
    [Serializable]
    public class NLPAudioRequestEvents<TUnityEvent>
        : TranscriptionRequestEvents<TUnityEvent>,
            INLPAudioRequestEvents<TUnityEvent>
        where TUnityEvent : UnityEventBase
    {
        /// <summary>
        /// Called on request language processing while audio is still being analyzed
        /// </summary>
        public TUnityEvent OnEarlyResponse { get; private set; } = Activator.CreateInstance<TUnityEvent>();
    }
}
