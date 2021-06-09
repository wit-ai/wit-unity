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
        [SerializeField] public ValuePathMatcher[] valuePaths;

        [Header("Output")]
        [SerializeField] private FormattedValueEvents[] formattedValueEvents;
        [SerializeField] private MultiValueEvent onMultiValueEvent = new MultiValueEvent();

        private WitResponseReference[] references;


        private static Regex valueRegex = new Regex(Regex.Escape("{value}"), RegexOptions.Compiled);

        private void Start()
        {
            references = new WitResponseReference[valuePaths.Length];
            for (int i = 0; i < valuePaths.Length; i++)
            {
                references[i] = WitResultUtilities.GetWitResponseReference(valuePaths[i].path);
            }
        }

        protected override void OnHandleResponse(WitResponseNode response)
        {
            if (IntentMatches(response) && ValueMatches(response))
            {
                for (int j = 0; j < formattedValueEvents.Length; j++)
                {
                    var formatEvent = formattedValueEvents[j];
                    var result = formatEvent.format;
                    for (int i = 0; i < references.Length; i++)
                    {
                        var reference = references[i];
                        var value = reference.GetStringValue(response);
                        if (!string.IsNullOrEmpty(formatEvent.format))
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                result = valueRegex.Replace(result, value, 1);
                                result = result.Replace("{" + i + "}", value);
                            }
                            else if(result.Contains("{" + i + "}"))
                            {
                                result = "";
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(result))
                    {
                        formatEvent.onFormattedValueEvent?.Invoke(result);
                    }
                }

                List<string> values = new List<string>();
                for (int i = 0; i < references.Length; i++)
                {
                    var reference = references[i];
                    var value = reference.GetStringValue(response);
                    values.Add(value);
                }

                onMultiValueEvent.Invoke(values.ToArray());
            }
        }

        private bool ValueMatches(WitResponseNode response)
        {
            bool matches = true;
            for (int i = 0; i < valuePaths.Length && matches; i++)
            {
                var matcher = valuePaths[i];
                var value = references[i].GetStringValue(response);
                matches &= !matcher.contentRequired || !string.IsNullOrEmpty(value);
                if (matcher.matchRequired)
                {
                    if (matcher.useRegularExpression)
                    {
                        matches &= Regex.Match(value, matcher.matchValue).Success;
                    }
                    else
                    {
                        matches &= value == matcher.matchValue;
                    }
                }
            }

            return matches;
        }

        private bool IntentMatches(WitResponseNode response)
        {
            var intentNode = response.GetFirstIntent();
            return intent == intentNode["name"].Value &&
                   intentNode["confidence"].AsFloat > confidence;
        }
    }

    [Serializable]
    public class MultiValueEvent : UnityEvent<string[]> {}

    [Serializable]
    public class FormattedValueEvents
    {
        [Tooltip("Modify the string output, values can be inserted with {value} or {0}, {1}, {2}")]
        public string format;
        public ValueEvent onFormattedValueEvent = new ValueEvent();
    }

    [Serializable]
    public class ValuePathMatcher
    {
        [Tooltip("The path to a value within a WitResponseNode")]
        public string path;
        [Tooltip("Does this path need to have text in the value to be considered a match")]
        public bool contentRequired = true;
        [Tooltip("Should the value of this content match the value in Match Value to be considered a match")]
        public bool matchRequired;
        [Tooltip("Value used to compare with the result when Match Required is set")]
        public string matchValue;
        [Tooltip("If set the match value will be treated as a regular expression.")]
        public bool useRegularExpression;
    }
}
