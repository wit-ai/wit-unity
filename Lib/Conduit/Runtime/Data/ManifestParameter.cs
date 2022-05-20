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

namespace Conduit
{
    /// <summary>
    /// Represents a method parameter/argument in the manifest.
    /// </summary>
    internal class ManifestParameter
    {
        private string name;

        /// <summary>
        /// This is the parameter name as exposed to the backend (slot or role)
        /// </summary>
        public string Name
        {
            get => name;
            set => name = ConduitUtilities.DelimitWithUnderscores(value).ToLower();
        }

        /// <summary>
        /// This is the technical name of the parameter in the actual method in codebase.
        /// </summary>
        public string InternalName { get; set; }

        /// <summary>
        /// A fully qualified name exposed to the backend for uniqueness.
        /// </summary>
        public string QualifiedName { get; set; }

        /// <summary>
        /// This is the data type of the parameter, exposed as an entity type.
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Additional names by which the backend can refer to this parameter.
        /// </summary>
        public List<string> Aliases { get; set; }
        
        public static ManifestParameter FromJson(ConduitNode parameterNode)
        {
            ManifestParameter parameter = new ManifestParameter()
            {
                name = parameterNode["name"],
                InternalName = parameterNode["internalName"],
                QualifiedName = parameterNode["qualifiedName"],
                EntityType = parameterNode["entityType"]
            };

            var aliases = parameterNode["aliases"];
            parameter.Aliases = new List<string>();
            for (int i = 0; i < aliases.Count; i++)
            {
                parameter.Aliases.Add(aliases[i]);
            }

            return parameter;
        }

        public ConduitObject ToJson()
        {
            var parameter = new ConduitObject();
            parameter["name"] = Name;
            parameter["internalName"] = InternalName;
            parameter["qualifiedName"] = QualifiedName;
            parameter["entityType"] = EntityType;

            var aliases = new ConduitArray();
            foreach (var value in Aliases)
            {
                aliases.Add(value);
            }

            parameter["aliases"] = aliases;

            return parameter;
        }

        public override bool Equals(object obj)
        {
            return obj is ManifestParameter other && this.Equals(other);
        }

        private bool Equals(ManifestParameter other)
        {
            return this.InternalName.Equals(other.InternalName) && this.QualifiedName.Equals(other.QualifiedName) && this.EntityType.Equals(other.EntityType) && this.Aliases.SequenceEqual(other.Aliases);
        }
    }
}
