/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// An attribute to show a Button in the inspector for a method in a MonoBehaviour script.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : Attribute
    {
        public readonly string displayName;
        public readonly string label;
        public readonly string tooltip;
        public readonly bool isRuntimeOnly;

        /// <summary>
        /// An attribute to show a Button in the inspector for a method in a MonoBehaviour script. 
        /// </summary>
        /// <param name="displayName">The name to be shown on the button. If not provided the function name will be used.</param>
        /// <param name="label">The label to be used for the foldout. If not provided displayName will be shown.</param>
        /// <param name="tooltip">Tooltip to show on the button</param>
        /// <param name="isRuntimeOnly">Only show if the game is running.</param>
        public ButtonAttribute(string displayName = null, string label = null, string tooltip = null, bool isRuntimeOnly = false)
        {
            this.displayName = displayName;
            this.label = label;
            this.tooltip = tooltip;
            this.isRuntimeOnly = isRuntimeOnly;
        }
    }
}
