/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// Base class for ScriptableObject assets that extract and compare typed values
    /// from Wit.ai JSON response data. Supports creating reusable value extractors
    /// that can be configured in the Unity Inspector with JSON paths.
    /// </summary>
    /// <example>
    /// Create a custom value extractor asset:
    /// <code>
    /// // Create a float value extractor in the Unity Editor
    /// var confidenceValue = ScriptableObject.CreateInstance&lt;WitFloatValue&gt;();
    /// confidenceValue.path = "intents[0].confidence";
    ///
    /// // Use it to extract values from responses
    /// var response = WitResponseNode.Parse("{\"intents\":[{\"confidence\":0.95}]}");
    /// float confidence = (float)confidenceValue.GetValue(response);
    /// bool isHighConfidence = confidenceValue.Equals(response, 0.9f);
    /// </code>
    /// </example>
    public abstract class WitValue : ScriptableObject
    {
        /// <summary>
        /// JSON path to the value in the Wit.ai response (e.g., "intents[0].name", "entities.location[0].value").
        /// </summary>
        [SerializeField] public string path;
        private WitResponseReference reference;

        /// <summary>
        /// Gets the response reference object used to navigate the JSON path.
        /// Lazily initialized on first access and cached for subsequent calls.
        /// </summary>
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

        /// <summary>
        /// Extracts the typed value from the Wit.ai response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node to extract from</param>
        /// <returns>The extracted value as an object (actual type depends on subclass)</returns>
        public abstract object GetValue(WitResponseNode response);

        /// <summary>
        /// Compares the value at the configured path in the response with a target value.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node to extract from</param>
        /// <param name="value">The value to compare against</param>
        /// <returns>True if the values match according to the subclass's equality logic</returns>
        public abstract bool Equals(WitResponseNode response, object value);

        /// <summary>
        /// Gets the string representation of the value at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node to extract from</param>
        /// <returns>String representation of the value</returns>
        public string ToString(WitResponseNode response)
        {
            return Reference.GetStringValue(response);
        }
    }
}
