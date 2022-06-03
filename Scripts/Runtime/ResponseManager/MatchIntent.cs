/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Conduit;

namespace Facebook.WitAi
{
    /// <summary>
    /// Triggers a method to be executed if it matches a voice command's intent
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class MatchIntent : ConduitActionAttribute
    {
        /// <summary>
        /// Triggers a method to be executed if it matches a voice command's intent
        /// </summary>
        /// <param name="intent">The name of the intent to match</param>
        /// <param name="minConfidence">The minimum confidence value (0-1) needed to match</param>
        /// <param name="maxConfidence">The maximum confidence value(0-1) needed to match</param>
        /// <param name="allowPartial">Whether to match intents with partial responses</param>
        public MatchIntent(string intent, float minConfidence = .9f, float maxConfidence = 1f,
            bool allowPartial = false) : base(intent, minConfidence, maxConfidence, allowPartial)
        {
        }
    }
}
