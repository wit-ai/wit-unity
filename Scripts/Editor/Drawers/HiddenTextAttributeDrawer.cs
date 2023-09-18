/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Attributes;

namespace Meta.WitAi.Drawers
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Hides text with a Password field unless the user clicks the visibility icon to toggle between password and text mode.
    ///
    /// NOTE: Like any password inspector field, this is serialized in plain text. This is purely about visual obfuscation
    /// </summary>
    [CustomPropertyDrawer(typeof(HiddenTextAttribute))]
    public class HiddenTextAttributeDrawer : PropertyDrawer
    {
        private bool showText = false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Check if it's a string property
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Texture eyeIcon = showText ? EditorGUIUtility.IconContent("animationvisibilitytoggleon").image
                : EditorGUIUtility.IconContent("animationvisibilitytoggleoff").image;

            Rect buttonRect = new Rect(position.x + position.width - 20, position.y, 20, position.height);
            position.width -= 25;

            if (GUI.Button(buttonRect, eyeIcon, GUIStyle.none))
            {
                showText = !showText;
            }

            if (showText)
            {
                EditorGUI.PropertyField(position, property, label);
            }
            else
            {
                property.stringValue = EditorGUI.PasswordField(position, label, property.stringValue);
            }
        }
    }
}
