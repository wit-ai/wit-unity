/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text.RegularExpressions;

namespace Meta.Conduit
{
    /// <summary>
    /// Utility class for Conduit.
    /// </summary>
    internal class ConduitUtilities
    {
        private static readonly Regex UnderscoreSplitter = new Regex("(\\B[A-Z])", RegexOptions.Compiled);

        /// <summary>
        /// Splits a string at word boundaries and delimits it with underscores.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string DelimitWithUnderscores(string input)
        {
            return UnderscoreSplitter.Replace(input, "_$1");
        }

        /// <summary>
        /// Returns true if <paramref name="stringToSearch"/> contains <paramref name="value"/> when ignoring whitespace.
        /// </summary>
        /// <param name="stringToSearch">The string to search it.</param>
        /// <param name="value">The substring to look for.</param>
        /// <returns>True if found. False otherwise.</returns>
        public static bool ContainsIgnoringWhitespace(string stringToSearch, string value)
        {
            stringToSearch = StripWhiteSpace(stringToSearch);
            value = StripWhiteSpace(value);
            return stringToSearch.Contains(value);
        }

        public static string StripWhiteSpace(string input)
        {
            return input.Replace(" ", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty);
        }

        /// <summary>
        /// Get sanitized entity class name
        /// </summary>
        /// <param name="entityRole">Role of an entity</param>
        /// <returns>Entity class name for specified role</returns>
        public static string GetEntityEnumName(string entityRole)
        {
            return $"Entity{SanitizeName(entityRole)}";
        }

        /// <summary>
        /// Get sanitized entity value
        /// </summary>
        /// <param name="entityRole"></param>
        /// <returns></returns>
        public static string GetEntityEnumValue(string entityValue)
        {
            return SanitizeString(entityValue);
        }

        /// <summary>
        /// Script that sanitizes string values
        /// to ensure they can be used as a class name
        /// </summary>
        /// <param name="input">Initial string to sanitize</param>
        /// <returns>Sanitized string</returns>
        public static string SanitizeName(string input)
        {
            // Ensure no empty/null names
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            // Standard sanitize
            string result = SanitizeString(input);
            // Capitalize first letter
            return result[0].ToString().ToUpper() + result.Substring(1);
        }

        /// <summary>
        /// Script that sanitizes string values
        /// to ensure they can be used in code
        /// </summary>
        /// <param name="input">Initial string to sanitize</param>
        /// <returns>Sanitized string</returns>
        public static string SanitizeString(string input)
        {
            // Ensure no empty/null strings
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            // Remove all non word characters, underscore & hyphen
            string result = Regex.Replace(input, @"[^\w_-]", "");
            // Starts with number, append N
            if (Regex.IsMatch(result[0].ToString(), @"^\d$"))
            {
                result = $"n{result}";
            }
            // Sanitized string
            return result;
        }
    }
}
