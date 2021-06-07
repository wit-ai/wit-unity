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
    public class ValueMatchHandler : WitResponseHandler
    {
        [Header("Intent Matching")] [SerializeField]
        public string intent;
        [Range(0, 1f)] [SerializeField] public float confidence = .6f;

        [Header("Value Matching")]
        [SerializeField] public ValueMatch[] valueMatches;

        public UnityEvent onValueMatch = new UnityEvent();

        protected override void OnHandleResponse(WitResponseNode response)
        {
            var intentNode = response.GetFirstIntent();
            if (intent == intentNode["name"].Value && intentNode["confidence"].AsFloat > confidence)
            {
                for (int i = 0; i < valueMatches.Length; i++)
                {
                    var matcher = valueMatches[i];
                    if (matcher.expectedValue != matcher.Reference.GetStringValue(response))
                    {
                        return;
                    }
                }

                onValueMatch.Invoke();
            }
        }
    }

    [Serializable]
    public class ValueMatch
    {
        public string path;
        public string expectedValue;

        [HideInInspector]
        [NonSerialized]
        private WitResponseReference reference;

        public WitResponseReference Reference
        {
            get
            {
                if (null == reference)
                {
                    reference = WitResultUtilities.GetWitResponseReference(path);
                }

                return reference;
            }
        }
    }
}
