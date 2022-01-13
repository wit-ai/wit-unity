/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Facebook.WitAi.Windows
{
    // Edit Type
    public enum WitPropertyEditType
    {
        NoEdit,
        FreeEdit,
        LockEdit
    }

    // Handles layout of of property sub properties
    public class WitPropertyDrawer : PropertyDrawer
    {
        // Whether editing
        private int editIndex = -1;

        // Whether to use a foldout
        protected virtual bool FoldoutEnabled => true;
        // Determine edit type for this drawer
        protected virtual WitPropertyEditType EditType => WitPropertyEditType.NoEdit;
        
        // Remove padding
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 0;
        }

        // Handles gui layout
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // For layout
            float height = 0f;

            // Return error
            if (property.serializedObject == null)
            {
                string missingText = GetLocalizedText(property, WitStyles.LocalizationDrawerMissingID);
                WitEditorUI.LayoutErrorLabel(missingText, ref height);
                return;
            }

            // Show foldout if desired
            string titleText = GetLocalizedTitle(property);
            if (FoldoutEnabled)
            {
                property.isExpanded = WitEditorUI.LayoutFoldout(new GUIContent(titleText), property.isExpanded, ref height);
                if (!property.isExpanded)
                {
                    return;
                }
            }
            // Show title only
            else
            {
                WitEditorUI.LayoutLabel(titleText, ref height);
            }

            // Indent
            GUILayout.BeginVertical();
            EditorGUI.indentLevel++;

            // Pre fields
            OnGUIPreFields(position, property, label);

            // Iterate all subfields
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Type fieldType = fieldInfo.FieldType;
            if (fieldType.IsArray)
            {
                fieldType = fieldType.GetElementType();
            }
            FieldInfo[] subfields = fieldType.GetFields(flags);
            for (int s = 0; s < subfields.Length; s++)
            {
                FieldInfo subfield = subfields[s];
                if (ShouldLayoutField(subfield))
                {
                    LayoutField(s, property, subfield, EditType, ref height);
                }
            }

            // Post fields
            OnGUIPostFields(position, property, label);

            // Undent
            EditorGUI.indentLevel--;
            GUILayout.EndVertical();
        }
        // Override pre fields
        protected virtual void OnGUIPreFields(Rect position, SerializedProperty property, GUIContent label)
        {

        }
        // Draw a specific property
        protected virtual void LayoutField(int index, SerializedProperty property, FieldInfo subfield, WitPropertyEditType editType, ref float height)
        {
            // Begin layout
            GUILayout.BeginHorizontal();

            // Get label content
            string labelText = GetLocalizedText(property, subfield.Name);
            GUIContent labelContent = new GUIContent(labelText);

            // Determine if can edit
            bool canEdit = editType == WitPropertyEditType.FreeEdit || (editType == WitPropertyEditType.LockEdit && editIndex == index);
            bool couldEdit = GUI.enabled;
            GUI.enabled = canEdit;

            // Cannot edit, just show field
            SerializedProperty subfieldProperty = property.FindPropertyRelative(subfield.Name);
            if (!canEdit && subfieldProperty.type == "string")
            {
                // Get value text
                string valText = subfieldProperty.stringValue;
                if (string.IsNullOrEmpty(valText))
                {
                    valText = GetDefaultFieldValue(subfield);
                }

                // Layout key
                WitEditorUI.LayoutKeyLabel(labelText, valText, ref height);
            }
            // Can edit, allow edit
            else
            {
                GUILayout.BeginVertical();
                EditorGUILayout.PropertyField(subfieldProperty, labelContent);
                GUILayout.EndVertical();
            }

            // Reset
            GUI.enabled = couldEdit;

            // Lock Settings
            if (editType == WitPropertyEditType.LockEdit)
            {
                // Is Editing
                if (editIndex == index)
                {
                    // Clear Edit
                    if (WitEditorUI.LayoutIconButton(WitStyles.ResetIcon, ref height))
                    {
                        editIndex = -1;
                        string clearVal = "";
                        if (subfieldProperty.type != "string")
                        {
                            clearVal = GetDefaultFieldValue(subfield);
                        }
                        SetFieldStringValue(subfieldProperty, clearVal);
                        GUI.FocusControl(null);
                    }
                    // Accept Edit
                    if (WitEditorUI.LayoutIconButton(WitStyles.AcceptIcon, ref height))
                    {
                        editIndex = -1;
                        GUI.FocusControl(null);
                    }
                }
                // Not Editing
                else
                {
                    // Begin Editing
                    if (WitEditorUI.LayoutIconButton(WitStyles.EditIcon, ref height))
                    {
                        editIndex = index;
                        GUI.FocusControl(null);
                    }
                }
            }

            // End layout
            GUILayout.EndHorizontal();
        }
        // Override post fields
        protected virtual void OnGUIPostFields(Rect position, SerializedProperty property, GUIContent label)
        {

        }
        // Get localized category
        protected virtual string GetLocalizationCategory(SerializedProperty property)
        {
            return property.name;
        }
        // Get localized title
        protected virtual string GetLocalizedTitle(SerializedProperty property)
        {
            return GetLocalizedText(property, "");
        }
        // Get text for specified key
        protected virtual string GetLocalizedText(SerializedProperty property, string key)
        {
            return WitStyles.GetLocalizedText(GetLocalizationCategory(property), key);
        }
        // Way to ignore certain properties
        protected virtual bool ShouldLayoutField(FieldInfo subfield)
        {
            switch (subfield.Name)
            {
                case "witConfiguration":
                    return false;
            }
            return true;
        }
        // Get field default value if applicable
        protected virtual string GetDefaultFieldValue(FieldInfo subfield)
        {
            return string.Empty;
        }
        // Get subfield value
        protected virtual string GetFieldStringValue(SerializedProperty property, string fieldName)
        {
            SerializedProperty subfieldProperty = property.FindPropertyRelative(fieldName);
            return GetFieldStringValue(subfieldProperty);
        }
        // Get subfield value
        protected virtual string GetFieldStringValue(SerializedProperty subfieldProperty)
        {
            // Supported types
            switch (subfieldProperty.type)
            {
                case "string":
                    return subfieldProperty.stringValue;
                case "int":
                    return subfieldProperty.intValue.ToString();
                case "bool":
                    return subfieldProperty.boolValue.ToString();
            }
            // No others are currently supported
            return string.Empty;
        }
        // Set subfield value
        protected virtual void SetFieldStringValue(SerializedProperty subfieldProperty, string newFieldValue)
        {
            // Supported types
            switch (subfieldProperty.type)
            {
                case "string":
                    subfieldProperty.stringValue = newFieldValue;
                    break;
                case "int":
                    int rI;
                    if (int.TryParse(newFieldValue, out rI))
                    {
                        subfieldProperty.intValue = rI;
                    }
                    break;
                case "bool":
                    bool rB;
                    if (bool.TryParse(newFieldValue, out rB))
                    {
                        subfieldProperty.boolValue = rB;
                    }
                    break;
            }
        }
    }
}
