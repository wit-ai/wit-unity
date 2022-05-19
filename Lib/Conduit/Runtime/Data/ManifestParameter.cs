/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

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
    }
}
