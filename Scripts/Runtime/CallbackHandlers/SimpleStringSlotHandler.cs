/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.lib;
using UnityEngine;
using UnityEngine.Events;

namespace com.facebook.witai.callbackhandlers
{
    public class SimpleStringSlotHandler : WitResponseHandler
    {
        [SerializeField] public string intent;
        [SerializeField] public string entity;
        [Range(0, 1f)] [SerializeField] public float confidence = .9f;

        [SerializeField] public string format;

        [SerializeField] private StringSlotMatchEvent onIntentSlotTriggered
            = new StringSlotMatchEvent();

        public StringSlotMatchEvent OnIntentSlotTriggered => onIntentSlotTriggered;

        protected override void OnHandleResponse(WitResponseNode response)
        {
            var intentNode = WitResultUtilities.GetFirstIntent(response);
            if (intent == intentNode["name"].Value && intentNode["confidence"].AsFloat > confidence)
            {
                var slotValue = WitResultUtilities.GetFirstSlot(response, entity);
                if (!string.IsNullOrEmpty(format))
                {
                    onIntentSlotTriggered.Invoke(format.Replace("{value}", slotValue));
                }
                else
                {
                    onIntentSlotTriggered.Invoke(slotValue);
                }
            }
        }
    }

    [Serializable]
    public class StringSlotMatchEvent : UnityEvent<string> {}
}
