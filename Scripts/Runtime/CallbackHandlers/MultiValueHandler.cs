/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using com.facebook.witai.lib;
using UnityEngine;
using UnityEngine.Events;

namespace com.facebook.witai.callbackhandlers
{
    public class MultiValueHandler : WitResponseHandler
    {
        [Header("Intent Matching")]
        [SerializeField] public string intent;
        [Range(0, 1f)] [SerializeField] public float confidence = .6f;

        [Header("Value Matching")]
        [SerializeField] public string[] valuePaths;

        [Header("Output")]
        [Tooltip("Modify the string output, values can be inserted with {value} or {0}, {1}, {2}")]
        [SerializeField] public string format;

        [SerializeField] private ValueEvent onFormattedValueEvent = new ValueEvent();
        [SerializeField] private MultiValueEvent onMultiValueEvent = new MultiValueEvent();

        private WitResponseReference[] references;


        private static Regex valueRegex = new Regex(Regex.Escape("{value}"), RegexOptions.Compiled);

        private void Awake()
        {
            references = new WitResponseReference[valuePaths.Length];
            for (int i = 0; i < valuePaths.Length; i++)
            {
                references[i] = WitResultUtilities.GetWitResponseReference(valuePaths[i]);
            }
        }

        protected override void OnHandleResponse(WitResponseNode response)
        {
            var intentNode = response.GetFirstIntent();
            if (intent == intentNode["name"].Value && intentNode["confidence"].AsFloat > confidence)
            {
                List<string> values = new List<string>();
                for (int i = 0; i < references.Length; i++)
                {
                    var reference = references[i];
                    var value = reference.GetStringValue(response);
                    values.Add(value);
                    if (!string.IsNullOrEmpty(format))
                    {
                        format = valueRegex.Replace(format, value, 1);
                        format = format.Replace("{" + i + "}", value);
                    }
                }

                if (!string.IsNullOrEmpty(format))
                {
                    onFormattedValueEvent.Invoke(format);
                }

                onMultiValueEvent.Invoke(values.ToArray());
            }
        }
    }

    [Serializable]
    public class MultiValueEvent : UnityEvent<string[]> {}
}
