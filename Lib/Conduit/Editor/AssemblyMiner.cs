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
    /// Mines assemblies for callback methods and entities.
    /// </summary>
    internal class AssemblyMiner : IAssemblyMiner
    {
        /// <summary>
        /// Validates that parameters are compatible. 
        /// </summary>
        private readonly IParameterValidator _parameterValidator;
        
        /// <summary>
        /// Initializes the class with a target assembly.
        /// </summary>
        /// <param name="parameterValidator">The parameter validator.</param>
        public AssemblyMiner(IParameterValidator parameterValidator)
        {
            this._parameterValidator = parameterValidator;
        }

        /// <summary>
        /// Extracts all entities from the assembly. Entities represent the types used as parameters (such as Enums) of
        /// our methods.
        /// </summary>
        /// <param name="assembly">The assembly to process.</param>
        /// <returns>The list of entities extracted.</returns>
        public List<ManifestEntity> ExtractEntities(IConduitAssembly assembly)
        {
            var entities = new List<ManifestEntity>();

            var enums = assembly.GetEnumTypes();
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
        public List<ManifestAction> ExtractActions(IConduitAssembly assembly)
        {
            var methods = assembly.GetMethods();

            var actions = new List<ManifestAction>();

            foreach (var method in methods)
            {
                const string indent = "   ";
                var logMessage = $"{method.DeclaringType.FullName}.{method.Name}()";

                var attributes = method.GetCustomAttributes(typeof(ConduitActionAttribute), false);
                if (attributes.Length == 0)
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

                var actionAttribute = attributes.First() as ConduitActionAttribute;
                var actionName = actionAttribute.Intent;
                if (string.IsNullOrEmpty(actionName))
                {
                    actionName = $"{method.Name}";
                }

                var parameters = new List<ManifestParameter>();

                var action = new ManifestAction()
                {
                    ID = $"{method.DeclaringType.FullName}.{method.Name}",
                    Name = actionName,
                    Assembly = assembly.FullName
                };

                var compatibleParameters = true;
                foreach (var parameter in method.GetParameters())
                {
                    Debug.Log($"{indent}{parameter.Name}:{parameter.ParameterType.Name}");

                    var supported = this._parameterValidator.IsSupportedParameterType(parameter.ParameterType);

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

                    var snakeCaseName= ConduitUtilities.DelimitWithUnderscores(parameter.Name).ToLower().TrimStart('_');
                    var snakeCaseAction = action.ID.Replace('.', '_');
                    
                    var manifestParameter = new ManifestParameter
                    {
                        Name = parameter.Name,
                        InternalName = parameter.Name,
                        QualifiedTypeName = parameter.ParameterType.FullName,
                        TypeAssembly = parameter.ParameterType.Assembly.FullName,
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
    }
}
