/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using PlasticPipe.PlasticProtocol.Messages;

namespace Conduit
{
    /// <summary>
    /// An entity entry in the manifest (for example an enum). Typically used as a method parameter type.
    /// </summary>
    internal class ManifestEntity
    {
        /// <summary>
        /// The is the internal name of the entity/parameter in the codebase.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// The data type for the entity.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// This is the name of the entity as understood by the backend.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// List of values this entity could  assume. For an enum, these would be the enum values.
        /// </summary>
        public List<string> Values { get; set; } = new List<string>();

        public static ManifestEntity FromJson(ConduitNode entityNode)
        {
            ManifestEntity entity = new ManifestEntity()
            {
                ID = entityNode["id"],
                Type = entityNode["type"],
                Name = entityNode["name"]
            };

            var values = entityNode["values"];
            entity.Values = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                entity.Values.Add(values[i]);
            }

            return entity;
        }

        public ConduitObject ToJson()
        {
            var entity = new ConduitObject();
            entity["id"] = ID;
            entity["type"] = Type;
            entity["name"] = Name;
            var values = new ConduitArray();
            foreach (var value in Values)
            {
                values.Add(value);
            }
            entity["values"] = values;
            return entity;
        }
    }
}
