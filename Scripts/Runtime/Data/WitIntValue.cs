/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// ScriptableObject asset that extracts integer values from Wit.ai JSON responses.
    /// Useful for counts, indices, numerical identifiers, and other integer data.
    /// </summary>
    /// <example>
    /// Usage example:
    /// <code>
    /// // Create in Unity Editor or via script
    /// var countExtractor = ScriptableObject.CreateInstance&lt;WitIntValue&gt;();
    /// countExtractor.path = "entities.number[0].value";
    ///
    /// // Extract int value from response
    /// var response = WitResponseNode.Parse("{\"entities\":{\"number\":[{\"value\":42}]}}");
    /// int count = countExtractor.GetIntValue(response);
    ///
    /// // Compare values
    /// bool isFortyTwo = countExtractor.Equals(response, 42); // true
    /// bool fromString = countExtractor.Equals(response, "42"); // also true (parsed)
    /// </code>
    /// </example>
    public class WitIntValue : WitValue
    {
        /// <summary>
        /// Extracts the integer value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted integer value as an object</returns>
        public override object GetValue(WitResponseNode response)
        {
            return GetIntValue(response);
        }

        /// <summary>
        /// Compares the integer value in the response with a target value.
        /// Supports comparing with int values or parsable strings.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <param name="value">The value to compare (can be int or parsable string)</param>
        /// <returns>True if values are equal</returns>
        public override bool Equals(WitResponseNode response, object value)
        {
            int iValue = 0;
            if (value is int i)
            {
                iValue = i;
            }
            else if (null != value && !int.TryParse("" + value, out iValue))
            {
                return false;
            }

            return GetIntValue(response) == iValue;
        }

        /// <summary>
        /// Extracts the integer value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted integer value</returns>
        public int GetIntValue(WitResponseNode response)
        {
            return Reference.GetIntValue(response);
        }
    }
}
