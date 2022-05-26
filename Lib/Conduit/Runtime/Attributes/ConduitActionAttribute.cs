/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Linq;

namespace Meta.Conduit
{
    using System;

    /// <summary>
    /// Marks the method as a callback for voice commands. The method will be mapped to an intent and invoked whenever
    /// that intent is resolved by the backend.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ConduitActionAttribute : Attribute
    {
        protected ConduitActionAttribute(string intent = "", params string[] aliases)
        {
            this.Intent = intent;
            this.Aliases = aliases.ToList();
        }

        /// <summary>
        /// The intent name matching this method. If left blank, the method name will be used to infer the intent name.
        /// </summary>
        public string Intent { get; protected set; }
        
        /// <summary>
        /// Additional aliases to refer to the intent this method represent.
        /// </summary>
        public List<string> Aliases { get; }
    }
}
