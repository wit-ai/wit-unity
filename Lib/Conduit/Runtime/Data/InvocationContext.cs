/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Reflection;

namespace Meta.Conduit
{
    /// <summary>
    /// Holds the details required to invoke a method at runtime.
    /// </summary>
    internal class InvocationContext
    {
        /// <summary>
        /// The type that declares the method.
        /// </summary>
        public Type Type { get; set; }
        
        /// <summary>
        /// The method information.
        /// </summary>
        public MethodInfo MethodInfo { get; set; }
    }
}
