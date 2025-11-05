/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// ScriptableObject asset that extracts float values from Wit.ai JSON responses.
    /// Useful for confidence scores, numerical values, and other floating-point data.
    /// Supports configurable tolerance for equality comparisons.
    /// </summary>
    /// <example>
    /// Usage example:
    /// <code>
    /// // Create in Unity Editor or via script
    /// var confidenceExtractor = ScriptableObject.CreateInstance&lt;WitFloatValue&gt;();
    /// confidenceExtractor.path = "intents[0].confidence";
    /// confidenceExtractor.equalityTolerance = 0.01f;
    ///
    /// // Extract float value from response
    /// var response = WitResponseNode.Parse("{\"intents\":[{\"confidence\":0.95}]}");
    /// float confidence = confidenceExtractor.GetFloatValue(response);
    ///
    /// // Compare with tolerance
    /// bool isConfident = confidenceExtractor.Equals(response, 0.9f); // true within tolerance
    /// </code>
    /// </example>
    public class WitFloatValue : WitValue
    {
        /// <summary>
        /// Tolerance for floating-point equality comparisons. Two floats are considered
        /// equal if their absolute difference is less than this value. Default is 0.0001.
        /// </summary>
        [SerializeField] public float equalityTolerance = .0001f;

        /// <summary>
        /// Extracts the float value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted float value as an object</returns>
        public override object GetValue(WitResponseNode response)
        {
            return GetFloatValue(response);
        }

        /// <summary>
        /// Compares the float value in the response with a target value using the configured tolerance.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <param name="value">The value to compare (can be float, int, or parsable string)</param>
        /// <returns>True if values are within equalityTolerance of each other</returns>
        public override bool Equals(WitResponseNode response, object value)
        {
            float fValue = 0;
            if (value is float f)
            {
                fValue = f;
            }
            else if(null != value && !float.TryParse("" + value, out fValue))
            {
                return false;
            }

            return Math.Abs(GetFloatValue(response) - fValue) < equalityTolerance;
        }

        /// <summary>
        /// Extracts the float value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted float value</returns>
        public float GetFloatValue(WitResponseNode response)
        {
            return Reference.GetFloatValue(response);
        }
    }
}
