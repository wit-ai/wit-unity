/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meta.Conduit
{
    /// <summary>
    /// The dispatcher is responsible for deciding which method to invoke when a request is received as well as parsing
    /// the parameters and passing them to the handling method.
    /// </summary>
    internal class ConduitDispatcher : IConduitDispatcher
    {
        /// <summary>
        /// The Conduit manifest which captures the structure of the voice-enabled methods.
        /// </summary>
        private Manifest manifest;

        /// <summary>
        /// The manifest loader.
        /// </summary>
        private readonly IManifestLoader manifestLoader;

        /// <summary>
        /// Resolves instances (objects) based on their type.
        /// </summary>
        private readonly IInstanceResolver instanceResolver;

        /// <summary>
        /// Maps internal parameter names to fully qualified parameter names (roles).
        /// </summary>
        private readonly Dictionary<string, string> parameterToRoleMap = new Dictionary<string, string>();

        public ConduitDispatcher(IManifestLoader manifestLoader, IInstanceResolver instanceResolver)
        {
            this.manifestLoader = manifestLoader;
            this.instanceResolver = instanceResolver;
        }

        /// <summary>
        /// Parses the manifest provided and registers its callbacks for dispatching.
        /// </summary>
        /// <param name="manifestFilePath">The path to the manifest file.</param>
        public void Initialize(string manifestFilePath)
        {
            if (this.manifest != null)
            {
                return;
            }
            
            manifest = this.manifestLoader.LoadManifest(manifestFilePath);
            
            // Map fully qualified role names to internal parameters.
            foreach (var action in manifest.Actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    parameterToRoleMap.Add(parameter.InternalName, parameter.QualifiedName);
                }
            }
        }

        /// <summary>
        /// Invokes the method matching the specified action ID.
        /// This should NOT be called before the dispatcher is initialized.
        /// </summary>
        /// <param name="actionId">The action ID (which is also the intent name).</param>
        /// <param name="parameters">Dictionary of parameters mapping parameter name to value.</param>
        public bool InvokeAction(string actionId, Dictionary<string, string> parameters)
        {
            if (!manifest.ContainsAction(actionId))
            {
                Console.WriteLine($"Failed to find action ID: {actionId}");
                return false;
            }

            var invocationContext = manifest.GetInvocationContext(actionId);
            var method = invocationContext.MethodInfo;
            var parametersInfo = method.GetParameters();
            var parameterObjects = new object[parametersInfo.Length];
            for (var i = 0; i < parametersInfo.Length; i++)
            {
                var parameter = parametersInfo[i];
                var parameterName = parameter.Name;
                if (!parameters.ContainsKey(parameterName))
                {
                    if (!parameterToRoleMap.ContainsKey(parameterName))
                    {
                        Debug.LogError($"Parameter {parameterName} is missing");
                        return false;
                    }

                    parameterName = parameterToRoleMap[parameterName];
                }
                
                var parameterValue = parameters[parameterName];
                if (parameter.ParameterType == typeof(string))
                {
                    parameterObjects[i] = parameterValue;
                }
                else if (parameter.ParameterType.IsEnum)
                {
                    try
                    {
                        parameterObjects[i] = Enum.Parse(parameter.ParameterType, parameterValue, true);
                    }
                    catch (Exception e)
                    {
                        var error = $"Failed to cast {parameterValue} to enum of type {parameter.ParameterType}. {e}";
                        Debug.LogError(error);
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        parameterObjects[i] = Convert.ChangeType(parameterValue, parameter.ParameterType);
                    }
                    catch (Exception e)
                    {
                        var error = $"Failed to convert {parameterValue} to {parameter.ParameterType}. {e}";
                        Debug.LogError(error);
                        return false;
                    }
                    
                }
            }

            if (method.IsStatic)
            {
                Debug.Log($"About to invoke {method.Name}");
                try
                {
                    method.Invoke(null, parameterObjects.ToArray());
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to invoke static method {method.Name}. {e}");
                    return false;
                }
                
                return true;
            }
            else
            {
                Debug.Log($"About to invoke {method.Name} on all instances");
                bool allSucceeded = true;
                foreach (var obj in this.instanceResolver.GetObjectsOfType(invocationContext.Type))
                {
                    try
                    {
                        method.Invoke(obj, parameterObjects.ToArray());
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to method {method.Name}. {e} on {obj}");
                        allSucceeded = false;
                        continue;
                    }
                }

                return allSucceeded;
            }
        }
    }
}
