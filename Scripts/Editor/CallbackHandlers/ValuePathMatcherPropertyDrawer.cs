/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai.callbackhandlers
{
    [CustomPropertyDrawer(typeof(ValuePathMatcher))]
    public class ValuePathMatcherPropertyDrawer : PropertyDrawer
    {
        private string currentEditPath;

        class Properties
        {
            public const string path = "path";
            public const string contentRequired = "contentRequired";
            public const string matchMethod = "matchMethod";
            public const string comparisonMethod = "comparisonMethod";
            public const string matchValue = "matchValue";

            public const string floatingPointComparisonTolerance =
                "floatingPointComparisonTolerance";
        }

        private Dictionary<string, bool> foldouts =
            new Dictionary<string, bool>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {

            float height = 0;

            // Path
            height += EditorGUIUtility.singleLineHeight;

            if (IsExpanded(property))
            {
                // Content Required
                height += EditorGUIUtility.singleLineHeight;
                // Match Method
                height += EditorGUIUtility.singleLineHeight;

                if (ComparisonMethodsVisible(property))
                {
                    // Comparison Method
                    height += EditorGUIUtility.singleLineHeight;
                }

                if (ComparisonValueVisible(property))
                {
                    // Comparison Value
                    height += EditorGUIUtility.singleLineHeight;
                }

                if (FloatingToleranceVisible(property))
                {
                    // Floating Point Tolerance
                    height += EditorGUIUtility.singleLineHeight;
                }

                height += 4;
            }

            return height;
        }

        private bool IsExpanded(SerializedProperty property)
        {
            return foldouts.TryGetValue(property.propertyPath, out bool value) && value;
        }

        private bool Foldout(Rect rect, SerializedProperty property)
        {
            if (!foldouts.TryGetValue(property.propertyPath, out var value))
            {
                foldouts[property.propertyPath] = false;
            }

            foldouts[property.propertyPath] = EditorGUI.Foldout(rect, value, "");
            return foldouts[property.propertyPath];
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var rect = new Rect(position);
            rect.height = EditorGUIUtility.singleLineHeight;
            var path = property.FindPropertyRelative(Properties.path);

            var editIconWidth = 16;
            var pathRect = new Rect(rect);
            pathRect.width -= editIconWidth;
            if (currentEditPath == property.propertyPath || string.IsNullOrEmpty(path.stringValue))
            {
                var value = EditorGUI.TextField(pathRect, path.stringValue);
                if (value != path.stringValue)
                {
                    currentEditPath = property.propertyPath;
                    path.stringValue = value;
                }
            }
            else
            {
                EditorGUI.LabelField(pathRect, path.stringValue);
            }

            var editRect = new Rect(rect);
            editRect.x = pathRect.x + pathRect.width;

            if (Foldout(rect, property))
            {
                if (GUI.Button(editRect, WitStyles.EditIcon, "Label"))
                {
                    if (currentEditPath == property.propertyPath)
                    {
                        currentEditPath = null;
                    }
                    else
                    {
                        currentEditPath = property.propertyPath;
                    }
                }

                rect.x += 20;
                rect.width -= 20;
                rect.y += rect.height;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(Properties.contentRequired));
                rect.y += rect.height;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(Properties.matchMethod));

                if (ComparisonMethodsVisible(property))
                {
                    rect.y += rect.height;
                    EditorGUI.PropertyField(rect,
                        property.FindPropertyRelative(Properties.comparisonMethod));
                }

                if (ComparisonValueVisible(property))
                {
                    rect.y += rect.height;
                    EditorGUI.PropertyField(rect,
                        property.FindPropertyRelative(Properties.matchValue));
                }

                if (FloatingToleranceVisible(property))
                {
                    rect.y += rect.height;
                    EditorGUI.PropertyField(rect,
                        property.FindPropertyRelative(Properties.floatingPointComparisonTolerance));
                }
            }
        }

        private bool ComparisonMethodsVisible(SerializedProperty property)
        {
            var matchedMethodProperty = property.FindPropertyRelative(Properties.matchMethod);
            return matchedMethodProperty.enumValueIndex > (int) MatchMethod.RegularExpression;
        }

        private bool ComparisonValueVisible(SerializedProperty property)
        {
            var matchedMethodProperty = property.FindPropertyRelative(Properties.matchMethod);
            return matchedMethodProperty.enumValueIndex > 0;
        }

        private bool FloatingToleranceVisible(SerializedProperty property)
        {
            var matchedMethodProperty = property.FindPropertyRelative(Properties.matchMethod);
            var comparisonMethodProperty =
                property.FindPropertyRelative(Properties.comparisonMethod);

            var comparisonMethod = comparisonMethodProperty.enumValueIndex;
            return matchedMethodProperty.enumValueIndex >= (int) MatchMethod.FloatComparison &&
                   (comparisonMethod == (int) ComparisonMethod.Equals || comparisonMethod == (int) ComparisonMethod.NotEquals);
        }
    }
}
