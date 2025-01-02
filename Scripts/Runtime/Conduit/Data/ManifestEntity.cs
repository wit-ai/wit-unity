﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Linq;
using Meta.WitAi.Data.Info;
using UnityEngine.Scripting;

namespace Meta.Conduit
{
    /// <summary>
    /// An entity entry in the manifest (for example an enum). Typically used as a method parameter type.
    /// </summary>
    internal class ManifestEntity
    {
        /// <summary>
        /// Called via JSON reflection, need preserver or it will be stripped on compile
        /// </summary>
        [Preserve]
        public ManifestEntity() { }

        /// <summary>
        /// The is the internal name of the entity/parameter in the codebase.
        /// </summary>
        [Preserve]
        public string ID { get; set; }

        /// <summary>
        /// The namespace that contains the entity/enum in the code.
        /// </summary>
        [Preserve]
        public string Namespace { get; set; }

        /// <summary>
        /// The data type for the entity on the backend. For example, wit$number.
        /// </summary>
        [Preserve]
        public string Type { get; set; }

        /// <summary>
        /// This is the name of the entity as understood by the backend.
        /// </summary>
        [Preserve]
        public string Name { get; set; }

        /// <summary>
        /// List of values this entity could assume. For an enum, these would be the enum values.
        /// </summary>
        [Preserve]
        public List<WitKeyword> Values { get; set; } = new List<WitKeyword>();

        /// <summary>
        /// The fully qualified name of the assembly containing the code for the entity.
        /// </summary>
        [Preserve]
        public string Assembly { get; set; }

        /// <summary>
        /// Extracts Wit.Ai entity info from this entity.
        /// </summary>
        /// <returns>The Wit entity info object.</returns>
        public WitEntityInfo GetAsInfo()
        {
            var keywords = new WitEntityKeywordInfo [Values.Count];
            for (var i = 0; i < Values.Count; ++i)
            {
                keywords[i] = Values[i].GetAsInfo();
            }

            return new WitEntityInfo()
            {
                name = Name,
                keywords = keywords,
                roles = new WitEntityRoleInfo[]{}
            };
        }

        public string GetQualifiedTypeName()
        {
            return string.IsNullOrEmpty(Namespace)
                ? $"{ID}"
                : $"{Namespace}.{ID}";
        }

        public override bool Equals(object obj)
        {
            return obj is ManifestEntity other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 31 + ID.GetHashCode();
            hash = hash * 31 + Type.GetHashCode();
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + Values.GetHashCode();
            hash = hash * 31 + Namespace.GetHashCode();
            hash = hash * 31 + Assembly.GetHashCode();
            return hash;
        }

        private bool Equals(ManifestEntity other)
        {
            return ID == other.ID && Type == other.Type && Name == other.Name && Namespace == other.Namespace
                   && Assembly == other.Assembly && this.Values.SequenceEqual(other.Values);
        }
    }
}
