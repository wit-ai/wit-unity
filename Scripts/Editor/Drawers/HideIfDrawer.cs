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
    /// A drawer to handle drawing fields with a [HideIf] attribute. When fields have this attribute they will be hidden
    /// in the inspector conditionally based on the evaluation of a field or property.
    /// </summary>
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    public class HideIfDrawer : PropertyDrawer
    {
        private bool IsVisible(SerializedProperty property)
        {
            HideIfAttribute hideIf = attribute as HideIfAttribute;
            SerializedProperty conditionProperty =
                property.GetParent()?.FindPropertyRelative(hideIf.conditionFieldName);
            // If it wasn't found relative to the property, check siblings.
            if (null == conditionProperty)
            {
                conditionProperty = property.serializedObject.FindProperty(hideIf.conditionFieldName);
            }

            if (conditionProperty != null)
            {
                if (conditionProperty.type == "bool") return !conditionProperty.boolValue;
                return conditionProperty.objectReferenceValue == null;
            }

            return true;
        }


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if(IsVisible(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsVisible(property))
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            return 0f;
        }
    }
}
