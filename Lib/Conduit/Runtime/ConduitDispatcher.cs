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
using System.Reflection;
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
        /// Resolves the actual parameters for method invocations. 
        /// </summary>
        private readonly IParameterProvider parameterProvider;

        /// <summary>
        /// Maps internal parameter names to fully qualified parameter names (roles/slots).
        /// </summary>
        private readonly Dictionary<string, string> parameterToRoleMap = new Dictionary<string, string>();

        public ConduitDispatcher(IManifestLoader manifestLoader, IInstanceResolver instanceResolver, IParameterProvider parameterProvider)
        {
            this.manifestLoader = manifestLoader;
            this.instanceResolver = instanceResolver;
            this.parameterProvider = parameterProvider;
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
                    parameterToRoleMap.TryAdd(parameter.InternalName, parameter.QualifiedName);
                }
            }
        }

        private InvocationContext ResolveInvocationContext(string actionId, Dictionary<string, object> actualParameters)
        {
            var invocationContexts = manifest.GetInvocationContexts(actionId);
            if (invocationContexts.Count == 1)
            {
                // There is a single method with the specified name.
                return invocationContexts[0];
            }
            
            // We have multiple overloads, find the correct match.
            foreach (var invocationContext in invocationContexts.Where(invocationContext => CompatibleInvocationContext(invocationContext)))
            {
                // Given that the invocations are sorted with most parameters first, we can return the first found.
                return invocationContext;
            }
            
            return null;
        }

        /// <summary>
        /// Returns true if the invocation context is compatible with the actual parameters the parameter provider
        /// is supplying. False otherwise.
        /// </summary>
        /// <param name="invocationContext">The invocation context.</param>
        /// <returns>True if the invocation can be made with the actual parameters. False otherwise.</returns>
        private bool CompatibleInvocationContext(InvocationContext invocationContext)
        {
            var parameters = invocationContext.MethodInfo.GetParameters();

            return parameters.All(parameter => this.parameterProvider.ContainsParameter(parameter));
        }


        /// <summary>
        /// Invokes the method matching the specified action ID.
        /// This should NOT be called before the dispatcher is initialized.
        /// </summary>
        /// <param name="actionId">The action ID (which is also the intent name).</param>
        /// <param name="actualParameters">Dictionary of parameters mapping parameter name to value.</param>
        public bool InvokeAction(string actionId, Dictionary<string, object> actualParameters)
        {
            if (!manifest.ContainsAction(actionId))
            {
                Console.WriteLine($"Failed to find action ID: {actionId}");
                return false;
            }

            this.parameterProvider.Populate(actualParameters, this.parameterToRoleMap);
            
            var invocationContext = this.ResolveInvocationContext(actionId, actualParameters);
            if (invocationContext == null)
            {
                Debug.LogError($"Failed to find execution context for {actionId}. Parameters could not be matched");
                return false;
            }
            
            var method = invocationContext.MethodInfo;
            var formalParametersInfo = method.GetParameters();
            var parameterObjects = new object[formalParametersInfo.Length];
            for (var i = 0; i < formalParametersInfo.Length; i++)
            {
                if (!parameterProvider.ContainsParameter(formalParametersInfo[i]))
                {
                    Debug.LogError($"Failed to find parameter {formalParametersInfo[i].Name} while invoking {method.Name}");
                    return false;
                }
                parameterObjects[i] = parameterProvider.GetParameterValue(formalParametersInfo[i]);
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
