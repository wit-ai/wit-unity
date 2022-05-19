/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace Conduit
{
    /// <summary>
    /// An action entry in the manifest.
    /// </summary>
    internal class ManifestAction
    {
        /// <summary>
        /// This is the internal fully qualified name of the method in the codebase.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// The fully qualified name of the assembly containing the code for the action.
        /// </summary>
        public string Assembly { get; set; }

        /// <summary>
        /// The name of the action as exposed to the backend.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The parameters used by the action.
        /// </summary>
        public List<ManifestParameter> Parameters { get; set; }

        /// <summary>
        /// Additional names by which the backend can refer to this action.
        /// </summary>
        public List<string> Aliases { get; set; }

        public static ManifestAction FromJson(ConduitNode actionNode)
        {
            ManifestAction action = new ManifestAction()
            {
                ID = actionNode["id"],
                Assembly = actionNode["assembly"],
                Name = actionNode["name"]
            };

            action.Parameters = new List<ManifestParameter>();
            var parameters = actionNode["parameters"].AsArray;
            for (int i = 0; i < parameters.Count; i++)
            {
                action.Parameters.Add(ManifestParameter.FromJson(parameters[i]));
            }

            var aliases = actionNode["aliases"];
            action.Aliases = new List<string>();
            for (int i = 0; i < aliases.Count; i++)
            {
                action.Aliases.Add(aliases[i]);
            }

            return action;
        }

        public ConduitObject ToJson()
        {
            var action = new ConduitObject();
            action["id"] = ID;
            action["assembly"] = Assembly;
            action["name"] = Name;

            var parameters = new ConduitArray();
            foreach (var parameter in Parameters)
            {
                parameters.Add(parameter.ToJson());
            }
            action["parameters"] = parameters;

            var aliases = new ConduitArray();
            foreach (var value in Aliases)
            {
                aliases.Add(value);
            }
            action["aliases"] = aliases;

            return action;
        }
    }
}
