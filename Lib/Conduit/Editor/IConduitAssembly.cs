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
    /// Wrapper for assemblies to provide convenience methods and abstract from CLR.
    /// </summary>
    internal interface IConduitAssembly
    {
        /// <summary>
        /// Extracts all entities from the assembly. Entities represent the types used as parameters (such as Enums) of
        /// our methods.
        /// </summary>
        /// <returns>The list of entities extracted.</returns>
        List<ManifestEntity> ExtractEntities();

        /// <summary>
        /// This method extracts all the marked actions (methods) in the specified assembly.
        /// </summary>
        /// <returns>List of actions extracted.</returns>
        List<ManifestAction> ExtractActions();
    }
}
