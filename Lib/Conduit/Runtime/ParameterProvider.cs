/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Meta.WitAi;

namespace Meta.Conduit
{
    /// <summary>
    /// Resolves parameters for invoking callbacks. This can be derived to support additional parameter types.
    /// </summary>
    internal class ParameterProvider : IParameterProvider
    {
        protected Dictionary<string, object> ActualParameters = new Dictionary<string, object>();

        /// <summary>
        /// Maps internal parameter names to fully qualified parameter names (roles/slots).
        /// </summary>
        private Dictionary<string, string> parameterToRoleMap = new Dictionary<string, string>();

        /// <summary>
        /// Must be called after all parameters have been obtained and mapped but before any are extracted.
        /// </summary>
        public void Populate(Dictionary<string, object> actualParameters, Dictionary<string, string> parameterToRoleMap)
        {
            this.ActualParameters = actualParameters;
            this.parameterToRoleMap = parameterToRoleMap;
        }

        /// <summary>
        /// Returns true if a parameter with the specified name can be provided.
        /// </summary>
        /// <param name="parameter">The name of the parameter.</param>
        /// <returns>True if a parameter with the specified name can be provided.</returns>
        public bool ContainsParameter(ParameterInfo parameter, StringBuilder log)
        {
            if (this.SupportedSpecializedParameter(parameter))
            {
                return true;
            }
            if (!ActualParameters.ContainsKey(parameter.Name))
            {
                log.AppendLine($"\tParameter '{parameter.Name}' not sent in invoke");
                return false;
            }
            if (!this.parameterToRoleMap.ContainsKey(parameter.Name))
            {
                log.AppendLine($"\tParameter '{parameter.Name}' not found in role map");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Provides the actual parameter value matching the supplied formal parameter.
        /// </summary>
        /// <param name="formalParameter">The formal parameter.</param>
        /// <returns>The actual parameter value matching the formal parameter.</returns>
        public object GetParameterValue(ParameterInfo formalParameter)
        {
            var formalParameterName = formalParameter.Name;
            if (!this.ActualParameters.ContainsKey(formalParameterName))
            {
                if (!this.parameterToRoleMap.ContainsKey(formalParameterName))
                {
                    VLog.E($"Parameter '{formalParameterName}' is missing");
                    return false;
                }

                formalParameterName = this.parameterToRoleMap[formalParameterName];
            }

            //var parameterValue = this.ActualParameters[formalParameterName];
            if (this.ActualParameters.TryGetValue(formalParameterName, out var parameterValue))
            {
                if (formalParameter.ParameterType == typeof(string))
                {
                    return parameterValue.ToString();
                }
                else if (formalParameter.ParameterType.IsEnum)
                {
                    try
                    {
                        return Enum.Parse(formalParameter.ParameterType, ConduitUtilities.SanitizeString(parameterValue.ToString()), true);
                    }
                    catch (Exception e)
                    {
                        VLog.E($"Parameter Provider - Parameter '{parameterValue}' could not be cast to enum\nEnum Type: {formalParameter.ParameterType.FullName}\n{e}");
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        return Convert.ChangeType(parameterValue, formalParameter.ParameterType);
                    }
                    catch (Exception e)
                    {
                        VLog.E($"Parameter Provider - Parameter '{parameterValue}' could not be cast\nType: {formalParameter.ParameterType.FullName}\n{e}");
                        return false;
                    }

                }
            }
            else if (SupportedSpecializedParameter(formalParameter))
            {
                return this.GetSpecializedParameter(formalParameter);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the specified parameter can be resolved. GetSpecializedParameter must be able to return
        /// a valid value if this method returns true.
        /// </summary>
        /// <param name="formalParameter">The formal parameter.</param>
        /// <returns>True if this parameter can be resolved. False otherwise.</returns>
        protected virtual bool SupportedSpecializedParameter(ParameterInfo formalParameter)
        {
            return false;
        }

        /// <summary>
        /// Returns the value of the specified parameter.
        /// </summary>
        /// <param name="formalParameter">The formal parameter.</param>
        /// <returns>The actual (supplied) invocation value for the parameter.</returns>
        protected virtual object GetSpecializedParameter(ParameterInfo formalParameter)
        {
            throw new NotSupportedException();
        }
    }
}
