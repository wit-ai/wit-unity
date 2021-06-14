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
using UnityEngine.Serialization;

namespace com.facebook.witai.callbackhandlers
{
    public class WitResponseMatcher : WitResponseHandler
    {
        [Header("Intent")]
        [SerializeField] public string intent;
        [Range(0, 1f)] [SerializeField] public float confidence = .6f;

        [FormerlySerializedAs("valuePaths")]
        [Header("Value Matching")]
        [SerializeField] public ValuePathMatcher[] valueMatchers;

        [Header("Output")]
        [SerializeField] private FormattedValueEvents[] formattedValueEvents;
        [SerializeField] private MultiValueEvent onMultiValueEvent = new MultiValueEvent();

        private WitResponseReference[] references;


        private static Regex valueRegex = new Regex(Regex.Escape("{value}"), RegexOptions.Compiled);

        private void Start()
        {
            references = new WitResponseReference[valueMatchers.Length];
            for (int i = 0; i < valueMatchers.Length; i++)
            {
                references[i] = WitResultUtilities.GetWitResponseReference(valueMatchers[i].path);
            }
        }

        protected override void OnHandleResponse(WitResponseNode response)
        {
            if (IntentMatches(response))
            {
                if (ValueMatches(response))
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
                                else if (result.Contains("{" + i + "}"))
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
            for (int i = 0; i < valueMatchers.Length && matches; i++)
            {
                var matcher = valueMatchers[i];
                var value = references[i].GetStringValue(response);
                matches &= !matcher.contentRequired || !string.IsNullOrEmpty(value);

                switch (matcher.matchMethod)
                {
                    case MatchMethod.RegularExpression:
                        matches &= Regex.Match(value, matcher.matchValue).Success;
                        break;
                    case MatchMethod.Text:
                        matches &= value == matcher.matchValue;
                        break;
                    case MatchMethod.IntegerComparison:
                        matches &= CompareInt(value, matcher.matchValue, matcher.comparisonMethod);
                        break;
                    case MatchMethod.FloatComparison:
                        matches &= CompareFloat(value, matcher.matchValue, matcher.comparisonMethod);
                        break;
                    case MatchMethod.DoubleComparison:
                        matches &= CompareDouble(value, matcher.matchValue, matcher.comparisonMethod);
                        break;
                }
            }

            return matches;
        }

        private bool CompareDouble(string value, string matchValue,
            ComparisonMethod comparisonMethod)
        {
            double dValue;

            // This one is freeform based on the input so we will retrun false if it is not parsable
            if (!double.TryParse(value, out dValue)) return false;

            // We will throw an exception if match value is not a numeric value. This is a developer
            // error.
            double dMatchValue = double.Parse(matchValue);

            switch (comparisonMethod)
            {
                case ComparisonMethod.Equals:
                    return Math.Abs(dValue - dMatchValue) < .0001f;
                case ComparisonMethod.NotEquals:
                    return Math.Abs(dValue - dMatchValue) > .0001f;
                case ComparisonMethod.Greater:
                    return dValue > dMatchValue;
                case ComparisonMethod.Less:
                    return dValue < dMatchValue;
                case ComparisonMethod.GreaterThanOrEqualTo:
                    return dValue >= dMatchValue;
                case ComparisonMethod.LessThanOrEqualTo:
                    return dValue <= dMatchValue;
            }

            return false;
        }

        private bool CompareFloat(string value, string matchValue,
            ComparisonMethod comparisonMethod)
        {
            float dValue;

            // This one is freeform based on the input so we will retrun false if it is not parsable
            if (!float.TryParse(value, out dValue)) return false;

            // We will throw an exception if match value is not a numeric value. This is a developer
            // error.
            float dMatchValue = float.Parse(matchValue);

            switch (comparisonMethod)
            {
                case ComparisonMethod.Equals:
                    return Math.Abs(dValue - dMatchValue) < .0001f;
                case ComparisonMethod.NotEquals:
                    return Math.Abs(dValue - dMatchValue) > .0001f;
                case ComparisonMethod.Greater:
                    return dValue > dMatchValue;
                case ComparisonMethod.Less:
                    return dValue < dMatchValue;
                case ComparisonMethod.GreaterThanOrEqualTo:
                    return dValue >= dMatchValue;
                case ComparisonMethod.LessThanOrEqualTo:
                    return dValue <= dMatchValue;
            }

            return false;
        }

        private bool CompareInt(string value, string matchValue,
            ComparisonMethod comparisonMethod)
        {
            int dValue;

            // This one is freeform based on the input so we will retrun false if it is not parsable
            if (!int.TryParse(value, out dValue)) return false;

            // We will throw an exception if match value is not a numeric value. This is a developer
            // error.
            int dMatchValue = int.Parse(matchValue);

            switch (comparisonMethod)
            {
                case ComparisonMethod.Equals:
                    return dValue == dMatchValue;
                case ComparisonMethod.NotEquals:
                    return dValue != dMatchValue;
                case ComparisonMethod.Greater:
                    return dValue > dMatchValue;
                case ComparisonMethod.Less:
                    return dValue < dMatchValue;
                case ComparisonMethod.GreaterThanOrEqualTo:
                    return dValue >= dMatchValue;
                case ComparisonMethod.LessThanOrEqualTo:
                    return dValue <= dMatchValue;
            }

            return false;
        }

        private bool IntentMatches(WitResponseNode response)
        {
            var intentNode = response.GetFirstIntent();
            return intent == intentNode["name"].Value &&
                   intentNode["confidence"].AsFloat > confidence;
        }
    }

    [Serializable]
    public class MultiValueEvent : UnityEvent<string[]>
    {
    }

    [Serializable]
    public class ValueEvent : UnityEvent<string>
    { }

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
        [Tooltip("If set the match value will be treated as a regular expression.")]
        public MatchMethod matchMethod;
        [Tooltip("The operator used to compare the value with the match value. Ex: response.value > matchValue")]
        public ComparisonMethod comparisonMethod;
        [Tooltip("Value used to compare with the result when Match Required is set")]
        public string matchValue;
    }

    public enum ComparisonMethod
    {
        Equals,
        NotEquals,
        Greater,
        GreaterThanOrEqualTo,
        Less,
        LessThanOrEqualTo
    }

    public enum MatchMethod
    {
        None,
        Text,
        RegularExpression,
        IntegerComparison,
        FloatComparison,
        DoubleComparison
    }
}
