/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.events;
using UnityEngine;

namespace com.facebook.witai.interfaces
{
    public abstract class CustomTranscriptionProvider : MonoBehaviour, ITranscriptionProvider
    {
        public string LastTranscription { get; }
        public abstract WitTranscriptionEvent OnPartialTranscription { get; }
        public abstract WitTranscriptionEvent OnFullTranscription { get; }
        public abstract void Activate();
        public abstract void Deactivate();
    }
}
