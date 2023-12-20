/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if !UNITY_2021_1_OR_NEWER
using System.Collections.Generic;
#endif
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Meta.WitAi;

namespace Meta.Conduit
{
    /// <summary>
    /// Utility class for Conduit.
    /// </summary>
    internal static class ConduitUtilities
    {
        /// <summary>
        /// A delegate for reporting progress. The progress value range is 0.0f to 1.0f.
        /// </summary>
        public delegate void ProgressDelegate(string status, float progress);

        /// <summary>
        /// The Regex pattern for splitting on underscores.
        /// </summary>
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
        /// An extension method that returns true if the type is nullable.
        /// This is local version of the implementation in the Castle library to prevent build issues.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type is nullable.</returns>
        public static bool IsNullableType(this Type type) => type.GetTypeInfo().IsGenericType && (object) type.GetGenericTypeDefinition() == (object) typeof (Nullable<>);

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

        /// <summary>
        /// Returns a typed value that matches the parameter type if possible.
        /// </summary>
        /// <param name="formalParameter">The formal parameter we are trying to supply</param>
        /// <param name="parameterValue">The raw value of the parameter.</param>
        /// <returns>The value in the correct type if a conversion was possible. Null otherwise.</returns>
        internal static object GetTypedParameterValue(ParameterInfo formalParameter, object parameterValue)
        {
            var formalType = formalParameter.ParameterType;
            return GetTypedParameterValue(formalType, parameterValue);
        }
        
        /// <summary>
        /// Returns a typed value that matches the parameter type if possible.
        /// </summary>
        /// <param name="parameterType">The data type we want to get the parameter mapped to.</param>
        /// <param name="parameterValue">The raw value of the parameter.</param>
        /// <returns>The value in the correct type if a conversion was possible. Null otherwise.</returns>
        internal static object GetTypedParameterValue(Type parameterType, object parameterValue)
        {
            if (parameterValue == null)
            {
                return null;
            }
            
            var formalType = parameterType;
            if (formalType.IsNullableType())
            {
                formalType = Nullable.GetUnderlyingType(formalType);
                if (formalType == null)
                {
                    VLog.E($"Got null underlying type for nullable parameter of type {parameterType}");
                    return null;
                }
            }
            
            if (formalType == typeof(string))
            {
                return parameterValue.ToString();
            }
            else if (formalType.IsEnum)
            {
                try
                {
                    return Enum.Parse(formalType, ConduitUtilities.SanitizeString(parameterValue.ToString()), true);
                }
                catch (Exception e)
                {
                    VLog.E(
                        $"Parameter value '{parameterValue}' could not be cast to enum\nEnum Type: {formalType.FullName}\n{e}");
                    throw;
                }
            }
            else
            {
                try
                {
                    return Convert.ChangeType(parameterValue, formalType);
                }
                catch (Exception e)
                {
                    VLog.E(
                        $"Nullable parameter value '{parameterValue}' could not be cast to {formalType.FullName}\n{e}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Get sanitized entity class name
        /// </summary>
        /// <param name="entityRole">Role of an entity</param>
        /// <returns>Entity class name for specified role</returns>
        public static string GetEntityEnumName(string entityRole)
        {
            return SanitizeName(entityRole);
        }

        /// <summary>
        /// Get sanitized entity value
        /// </summary>
        /// <param name="entityValue">The value of the entity.</param>
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
            var result = SanitizeString(input);
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
            var result = Regex.Replace(input, @"[^\w_-]", "");
            // Starts with number, append N
            if (Regex.IsMatch(result[0].ToString(), @"^\d$"))
            {
                result = $"N{result}";
            }
            // Sanitized string
            return result;
        }
        
        /// <summary>
        /// Strips spaces and newlines from a string.
        /// </summary>
        /// <param name="input">The string to strip.</param>
        /// <returns>The string without the whitespaces.</returns>
        private static string StripWhiteSpace(string input)
        {
            return string.IsNullOrEmpty(input) ? string.Empty :
                input.Replace(" ", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace("\r", string.Empty);
        }
    }

#if !UNITY_2021_1_OR_NEWER
    internal static class ListExtensions
    {
        public static HashSet<T> ToHashSet<T> (this List<T> source)
        {
            var output = new HashSet<T>();
            foreach (var element in source)
            {
                output.Add(element);
            }

            return output;
        }
    }
    #endif
}
