/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using Meta.WitAi.Utilities;

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// Allows custom overriding of attributes
    /// </summary>
    public class LabelAttribute : PropertyAttribute
    {
        public string LabelFieldPropertyOrMethodName { get; }
        public string TooltipFieldPropertyOrMethodName { get; }

        public LabelAttribute(string labelFieldPropertyOrMethodName, string tooltipFieldPropertyOrMethodName = "")
        {
            LabelFieldPropertyOrMethodName = labelFieldPropertyOrMethodName;
            TooltipFieldPropertyOrMethodName = tooltipFieldPropertyOrMethodName;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public class LabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            LabelAttribute attr = (LabelAttribute)attribute;

            if (!string.IsNullOrEmpty(attr.LabelFieldPropertyOrMethodName))
            {
                string newName =
                    ReflectionUtils.ReflectPropertyValue<string>(property, attr.LabelFieldPropertyOrMethodName);

                // If newName is valid, set it as the label.
                if (!string.IsNullOrEmpty(newName))
                {
                    label.text = newName;
                }
            }

            if (!string.IsNullOrEmpty(attr.TooltipFieldPropertyOrMethodName))
            {
                string newTooltip =
                    ReflectionUtils.ReflectPropertyValue<string>(property, attr.TooltipFieldPropertyOrMethodName);

                // If newName is valid, set it as the label.
                if (!string.IsNullOrEmpty(newTooltip))
                {
                    label.tooltip = newTooltip;
                }
            }

            EditorGUI.PropertyField(position, property, label);
        }
    }
#endif

}
