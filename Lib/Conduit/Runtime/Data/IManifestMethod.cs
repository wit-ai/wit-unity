/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Conduit
{
    internal interface IManifestMethod
    {
        /// <summary>
        /// This is the internal fully qualified name of the method in the codebase.
        /// </summary>
        string ID { get; set; }

        /// <summary>
        /// The parameters used by the action.
        /// </summary>
        List<ManifestParameter> Parameters { get; set; }
        
        /// <summary>
        /// The fully qualified name of the assembly containing the code for the action.
        /// </summary>
        string Assembly { get; set; }

    }
}
