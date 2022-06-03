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
        public float MinConfidence { get; protected set; }
        public float MaxConfidence { get; protected set;}
        public bool AllowPartial { get; protected set;}
        
        protected ConduitActionAttribute(string intent = "", params string[] aliases)
        {
            this.Intent = intent;
            this.Aliases = aliases.ToList();
        }

        /// <summary>
        /// Triggers a method to be executed if it matches a voice command's intent.
        /// </summary>
        /// <param name="intent">The name of the intent to match.</param>
        /// <param name="minConfidence">The minimum confidence value (0-1) needed to match.</param>
        /// <param name="maxConfidence">The maximum confidence value(0-1) needed to match.</param>
        /// <param name="allowPartial">Whether to match intents with partial responses.</param>
        /// <param name="aliases">Other names to refer to this intent.</param>
        protected ConduitActionAttribute(string intent = "", float minConfidence = 0.9f, float maxConfidence = 1f,
            bool allowPartial = false, params string[] aliases)
        {
            this.Intent = intent;
            this.MinConfidence = minConfidence;
            this.MaxConfidence = maxConfidence;
            this.AllowPartial = allowPartial;
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
