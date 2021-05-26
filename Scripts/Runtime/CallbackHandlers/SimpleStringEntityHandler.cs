﻿/*
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
    public class SimpleStringEntityHandler : WitResponseHandler
    {
        [SerializeField] public string intent;
        [SerializeField] public string entity;
        [Range(0, 1f)] [SerializeField] public float confidence = .9f;

        [SerializeField] public string format;

        [SerializeField] private StringEntityMatchEvent onIntentEntityTriggered
            = new StringEntityMatchEvent();

        public StringEntityMatchEvent OnIntentEntityTriggered => onIntentEntityTriggered;

        protected override void OnHandleResponse(WitResponseNode response)
        {
            var intentNode = WitResultUtilities.GetFirstIntent(response);
            if (intent == intentNode["name"].Value && intentNode["confidence"].AsFloat > confidence)
            {
                var entityValue = WitResultUtilities.GetFirstEntityValue(response, entity);
                if (!string.IsNullOrEmpty(format))
                {
                    onIntentEntityTriggered.Invoke(format.Replace("{value}", entityValue));
                }
                else
                {
                    onIntentEntityTriggered.Invoke(entityValue);
                }
            }
        }
    }

    [Serializable]
    public class StringEntityMatchEvent : UnityEvent<string> {}
}
