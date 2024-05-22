/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A database of mitigations for known error codes.
    /// </summary>
    [LogCategory(LogCategory.Logging, LogCategory.ErrorMitigator)]
    public class ErrorMitigator : IErrorMitigator
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        public ErrorMitigator()
        {
            try
            {
                // Note that the type of value is explicitly defined here (instead of var) to allow implicit conversion
                foreach (KnownErrorCode value in Enum.GetValues(typeof(KnownErrorCode)))
                {
                    var field = typeof(KnownErrorCode).GetField(value.ToString());
                    var attribute = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attribute != null)
                    {
                        _mitigations[value] = attribute.Description;
                    }
                    else
                    {
                        _log.Error(KnownErrorCode.KnownErrorMissingDescription, "Missing error description for {0}", value);
                        _mitigations[value] = "Please file a bug report.";
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to get known error mitigations. Exception: {e}");
            }
        }

        /// <summary>
        /// A list of all known errors and their mitigations.
        /// </summary>
        private readonly Dictionary<ErrorCode, string> _mitigations = new Dictionary<ErrorCode, string>();

        /// <summary>
        /// Returns the mitigation for an error code.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The mitigation.</returns>
        public string GetMitigation(ErrorCode errorCode)
        {
            if (_mitigations.ContainsKey(errorCode))
            {
                return _mitigations[errorCode];
            }
            else
            {
                return "There are no known mitigations. Please report to the Voice SDK team.";
            }
        }

        /// <summary>
        /// Adds or replaces a mitigation for an error code.
        /// This is typically used by external packages to provide their own mitigations.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="mitigation">The mitigation.</param>
        public void SetMitigation(ErrorCode errorCode, string mitigation)
        {
            _mitigations[errorCode] = mitigation;
        }
    }
}
