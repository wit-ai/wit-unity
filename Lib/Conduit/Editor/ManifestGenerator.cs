/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */


using System.Text;
using Meta.Wit.LitJson;

namespace Meta.Conduit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Generates manifests from the codebase that capture the essence of what we need to expose to the backend.
    /// The manifest includes all the information necessary to train the backend services as well as dispatching the
    /// incoming requests to the right methods with the right parameters.
    /// </summary>
    internal class ManifestGenerator
    {
        /// <summary>
        /// Provides access to available assemblies.
        /// </summary>
        private readonly IAssemblyWalker assemblyWalker;
        
        /// <summary>
        /// The manifest version. This would only change if the schema of the manifest changes.
        /// </summary>
        private const string CurrentVersion = "0.1";

        internal ManifestGenerator(IAssemblyWalker assemblyWalker)
        {
            this.assemblyWalker = assemblyWalker;
        }

        /// <summary>
        /// Generate a manifest for assemblies marked with the <see cref="ConduitAssemblyAttribute"/> attribute.
        /// </summary>
        /// <param name="domain">A friendly name to use for this app.</param>
        /// <param name="id">The App ID.</param>
        /// <returns>A JSON representation of the manifest.</returns>
        public string GenerateManifest(string domain, string id)
        {
            return GenerateManifest(assemblyWalker.GetTargetAssemblies(), domain, id);
        }

        /// <summary>
        /// Generate a manifest for the supplied assemblies.
        /// </summary>
        /// <param name="assemblies">List of assemblies to process.</param>
        /// <param name="domain">A friendly name to use for this app.</param>
        /// <param name="id">The App ID.</param>
        /// <returns>A JSON representation of the manifest.</returns>
        private string GenerateManifest(IEnumerable<IConduitAssembly> assemblies, string domain, string id)
        {
            Debug.Log($"Generating manifest.");

            var entities = new List<ManifestEntity>();
            var actions = new List<ManifestAction>();
            foreach (var assembly in assemblies)
            {
                entities.AddRange(assembly.ExtractEntities());
                actions.AddRange(assembly.ExtractActions());
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

            var sb = new StringBuilder();
            var jsonWriter = new JsonWriter(sb)
            {
                PrettyPrint = true,
                IndentValue = 4,
            };
            
            JsonMapper.ToJson(manifest, jsonWriter);
            return sb.ToString();
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
    }
}
