/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Attributes;
using Meta.WitAi.EditorUtilities;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi.Drawers
{
    /// <summary>
    /// A drawer to handle drawing fields with a [ShowIf] attribute. When fields have this attribute they will be shown
    /// in the inspector conditionally based on the evaluation of a field or property.
    /// </summary>
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = attribute as ShowIfAttribute;
            SerializedProperty conditionProperty = property.GetParent()?.FindPropertyRelative(showIf.conditionFieldName);

            if (conditionProperty == null || conditionProperty.boolValue)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = attribute as ShowIfAttribute;
            SerializedProperty conditionProperty = property.GetParent()?.FindPropertyRelative(showIf.conditionFieldName);

            if (conditionProperty == null || conditionProperty.boolValue)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            return 0f;
        }
    }
}
