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
    /// ScriptableObject asset that extracts string values from Wit.ai JSON responses.
    /// Useful for intent names, entity values, text transcriptions, and other string data.
    /// </summary>
    /// <example>
    /// Usage example:
    /// <code>
    /// // Create in Unity Editor or via script
    /// var intentExtractor = ScriptableObject.CreateInstance&lt;WitStringValue&gt;();
    /// intentExtractor.path = "intents[0].name";
    ///
    /// // Extract string value from response
    /// var response = WitResponseNode.Parse("{\"intents\":[{\"name\":\"play_music\"}]}");
    /// string intentName = intentExtractor.GetStringValue(response);
    ///
    /// // Compare values (case-sensitive)
    /// bool isPlayMusic = intentExtractor.Equals(response, "play_music"); // true
    /// bool fromInt = intentExtractor.Equals(response, 42); // converts to "42" for comparison
    /// </code>
    /// </example>
    public class WitStringValue : WitValue
    {
        /// <summary>
        /// Extracts the string value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted string value as an object</returns>
        public override object GetValue(WitResponseNode response)
        {
            return GetStringValue(response);
        }

        /// <summary>
        /// Compares the string value in the response with a target value.
        /// Performs case-sensitive string comparison. Non-string values are converted to strings.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <param name="value">The value to compare (strings compared directly, other types converted)</param>
        /// <returns>True if values are equal (case-sensitive)</returns>
        public override bool Equals(WitResponseNode response, object value)
        {
            if (value is string sValue)
            {
                return GetStringValue(response) == sValue;
            }

            return "" + value == GetStringValue(response);
        }

        /// <summary>
        /// Extracts the string value from the response at the configured path.
        /// </summary>
        /// <param name="response">The Wit.ai JSON response node</param>
        /// <returns>The extracted string value</returns>
        public string GetStringValue(WitResponseNode response)
        {
            return Reference.GetStringValue(response);
        }
    }
}
