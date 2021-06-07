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
    public class ValueHandler : WitResponseHandler
    {
        [Header("Intent Matching")]
        [SerializeField] public string intent;
        [Range(0, 1f)] [SerializeField] public float confidence = .6f;

        [Header("Value Matching")]
        [SerializeField] public string valuePath;

        [Header("Output")]
        [Tooltip("Modify the string output, values can be inserted with {value}")]
        [SerializeField] public string format;

        [SerializeField] private ValueEvent onValueEvent = new ValueEvent();

        private WitResponseReference reference;

        private void Awake()
        {
            reference = WitResultUtilities.GetWitResponseReference(valuePath);
        }

        protected override void OnHandleResponse(WitResponseNode response)
        {
            var intentNode = response.GetFirstIntent();
            if (intent == intentNode["name"].Value && intentNode["confidence"].AsFloat > confidence)
            {
                var entityValue = reference.GetStringValue(response);
                if (!string.IsNullOrEmpty(format))
                {
                    onValueEvent.Invoke(format.Replace("{value}", entityValue));
                }
                else
                {
                    onValueEvent.Invoke(entityValue);
                }
            }
        }
    }

    [Serializable]
    public class ValueEvent : UnityEvent<string> {}
}
