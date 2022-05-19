/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Conduit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Generates manifests from the codebase that capture the essence of what we need to expose to the backend.
    /// The manifest includes all the information necessary to train the backend services as well as dispatching the
    /// incoming requests to the right methods with the right parameters.
    /// </summary>
    public class ManifestGenerator
    {
        /// <summary>
        /// These are the types that we natively support.
        /// </summary>
        private readonly HashSet<Type> builtInTypes = new HashSet<Type>() { typeof(string), typeof(int) };

        /// <summary>
        /// The manifest version. This would only change if the schema of the manifest changes.
        /// </summary>
        private const string CurrentVersion = "0.1";

        /// <summary>
        /// Generate a manifest for assemblies marked with the <see cref="ConduitAssemblyAttribute"/> attribute.
        /// </summary>
        /// <param name="domain">A friendly name to use for this app.</param>
        /// <param name="id">The App ID.</param>
        /// <returns>A JSON representation of the manifest.</returns>
        public string GenerateManifest(string domain, string id)
        {
            return GenerateManifest(this.GetTargetAssemblies(), domain, id);
        }

        /// <summary>
        /// Generate a manifest for the supplied assemblies.
        /// </summary>
        /// <param name="assemblies">List of assemblies to process.</param>
        /// <param name="domain">A friendly name to use for this app.</param>
        /// <param name="id">The App ID.</param>
        /// <returns>A JSON representation of the manifest.</returns>
        private string GenerateManifest(IEnumerable<Assembly> assemblies, string domain, string id)
        {
            Debug.Log($"Generating manifest.");

            var entities = new List<ManifestEntity>();
            var actions = new List<ManifestAction>();
            foreach (var assembly in assemblies)
            {
                entities.AddRange(this.ExtractEntities(assembly));
                actions.AddRange(this.ExtractActions(assembly));
            }

            this.PruneUnreferencedEntities(ref entities, actions);

            var manifest = new Manifest()
            {
                ID = id,
                Version = CurrentVersion,
                Domain = domain,
                Entities = entities,
                Actions = actions
            };

            return manifest.ToJson();
        }

        /// <summary>
        /// Returns a list of all assemblies that should be processed.
        /// This currently selects assemblies that are marked with the <see cref="ConduitAssemblyAttribute"/> attribute.
        /// </summary>
        /// <returns>The list of assemblies.</returns>
        private IEnumerable<Assembly> GetTargetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.IsDefined(typeof(ConduitAssemblyAttribute)));
        }

        /// <summary>
        /// Removes unnecessary entities from the manifest to keep it restricted to what is required.
        /// </summary>
        /// <param name="entities">List of all entities. This list will be changed as a result.</param>
        /// <param name="actions">List of all actions.</param>
        private void PruneUnreferencedEntities(ref List<ManifestEntity> entities, List<ManifestAction> actions)
        {
            var referencedEntities = new HashSet<string>();

            foreach (var action in actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    referencedEntities.Add(parameter.EntityType);
                }
            }

            for (var i = 0; i < entities.Count; ++i)
            {
                if (referencedEntities.Contains(entities[i].ID))
                {
                    continue;
                }

                entities.RemoveAt(i--);
            }
        }

        /// <summary>
        /// Extracts all entities from the assembly. Entities represent the types used as parameters (such as Enums) of
        /// our methods.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <returns>The list of entities extracted.</returns>
        private List<ManifestEntity> ExtractEntities(Assembly assembly)
        {
            var entities = new List<ManifestEntity>();

            var enums = assembly.GetTypes().Where(p => p.IsEnum);
            foreach (var enumType in enums)
            {
                var enumUnderlyingType = Enum.GetUnderlyingType(enumType);
                Debug.Log(enumType.Name);
                Array enumValues;
                try
                {
                    if (enumType.GetCustomAttributes(typeof(ConduitEntityAttribute), false).Length == 0)
                    {
                        // This is not a tagged entity.
                        // TODO: In these cases we should only include the enum if it's referenced by any of the actions.
                    }

                    enumValues = enumType.GetEnumValues();
                }
                catch (Exception e)
                {
                    Debug.Log($"Failed to get enumeration values. {e}");
                    continue;
                }

                var entity = new ManifestEntity
                {
                    ID = $"{enumType.Name}",
                    Type = "Enum",
                    Name = $"{enumType.Name}"
                };

                var values = new List<string>();

                foreach (var enumValue in enumValues)
                {
                    object underlyingValue = Convert.ChangeType(enumValue, enumUnderlyingType);
                    Debug.Log($"{enumValue} = {underlyingValue}");
                    values.Add(enumValue.ToString() ?? string.Empty);
                }

                entity.Values = values;
                entities.Add(entity);
            }

            return entities;
        }

        /// <summary>
        /// This method extracts all the marked actions (methods) in the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <returns>List of actions extracted.</returns>
        private List<ManifestAction> ExtractActions(Assembly assembly)
        {
            var methods = assembly.GetTypes().SelectMany(type => type.GetMethods());

            var actions = new List<ManifestAction>();

            foreach (var method in methods)
            {
                const string indent = "   ";
                var logMessage = $"{method.DeclaringType.FullName}.{method.Name}()";

                if (method.GetCustomAttributes(typeof(ConduitActionAttribute), false).Length == 0)
                {
                    Debug.Log($"{logMessage} - Not tagged for assistant - Excluding");
                    continue;
                }

                if (method.IsStatic)
                {
                    Debug.Log($"{logMessage} - Static");
                }
                else
                {
                    Debug.Log($"{logMessage} - Instance");
                }

                var actionAttribute = method.GetCustomAttributes(typeof(ConduitActionAttribute), false).First() as ConduitActionAttribute;
                var actionName = actionAttribute.Name;
                if (string.IsNullOrEmpty(actionName))
                {
                    actionName = $"{method.Name}";
                }

                var parameters = new List<ManifestParameter>();

                var action = new ManifestAction()
                {
                    ID = $"{method.DeclaringType.FullName}.{method.Name}",
                    Name = actionName,
                    Aliases = actionAttribute.Aliases,
                    Assembly = assembly.FullName
                };

                var compatibleParameters = true;
                foreach (var parameter in method.GetParameters())
                {
                    Debug.Log($"{indent}{parameter.Name}:{parameter.ParameterType.Name}");

                    var supported = this.IsAssistantParameter(parameter);

                    if (!supported)
                    {
                        compatibleParameters = false;
                        Debug.Log(" (Not supported)");
                        continue;
                    }

                    Debug.Log(" (Supported)");
                    List<string> aliases;

                    if (parameter.GetCustomAttributes(typeof(ConduitParameterAttribute), false).Length > 0)
                    {
                        var parameterAttribute =
                            parameter.GetCustomAttributes(typeof(ConduitParameterAttribute), false).First() as
                                ConduitParameterAttribute;
                        aliases = parameterAttribute.Aliases;
                    }
                    else
                    {
                        aliases = new List<string>();
                    }

                    var snakeCaseName = ConduitUtilities.DelimitWithUnderscores(parameter.Name)
                        .ToLower().TrimStart('_');
                    var snakeCaseAction = action.ID.Replace('.', '_');

                    var manifestParameter = new ManifestParameter
                    {
                        Name = parameter.Name,
                        InternalName = parameter.Name,
                        EntityType = parameter.ParameterType.Name,
                        Aliases = aliases,
                        QualifiedName = $"{snakeCaseAction}_{snakeCaseName}"
                    };

                    parameters.Add(manifestParameter);

                }

                if (compatibleParameters)
                {
                    Debug.Log($"{indent}Eligible for Assistant");
                    action.Parameters = parameters;
                    actions.Add(action);
                }
                else
                {
                    Debug.Log($"{indent}Not eligible for Assistant");
                }
            }

            return actions;
        }

        private bool IsAssistantParameter(ParameterInfo parameter)
        {
            return parameter.ParameterType.IsEnum || this.builtInTypes.Contains(parameter.ParameterType);
        }
    }
}
