/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// Controls if a field is visible based on another boolean field/property's current value.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class HideIfAttribute : PropertyAttribute
    {
        public string conditionFieldName;

        /// <summary>
        ///  Controls if a field is visible based on another boolean field/property's current value.
        /// </summary>
        /// <param name="conditionFieldName">The name of a boolean field or property to use for visibility</param>
        public HideIfAttribute(string conditionFieldName)
        {
            this.conditionFieldName = conditionFieldName;
        }
    }
}
