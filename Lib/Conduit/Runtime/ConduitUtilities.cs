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
    }
}
