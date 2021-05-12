/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using UnityEngine.Events;

namespace com.facebook.witai.events
{
    [Serializable]
    public class WitEvents
    {
        [Header("Activation Result Events")]
        [Tooltip("Called when a response from wit has been received")]
        public WitResponseEvent OnResponse = new WitResponseEvent();

        [Tooltip("Called when there was an error with a WitRequest")]
        public WitErrorEvent OnError = new WitErrorEvent();

        [Header("Mic Events")]
        [Tooltip("Called when the volume level of the mic input has changed")]
        public WitMicLevelChangedEvent OnMicLevelChanged = new WitMicLevelChangedEvent();

        [Header("Activation/Deactivation Events")]
        [Tooltip("Called when the microphone has been activated during a Wit voice command activation")]
        public UnityEvent OnStartListening = new UnityEvent();

        [Tooltip(
            "Called when the microphone has stopped recording during a Wit voice command activation")]
        public UnityEvent OnStoppedListening = new UnityEvent();
    }
}
