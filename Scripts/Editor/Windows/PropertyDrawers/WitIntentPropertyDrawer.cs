/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Intents;
using System.Reflection;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitIntent))]
    public class WitIntentPropertyDrawer : WitPropertyDrawer
    {
        // Use name value for title if possible
        protected override string GetLocalizedText(SerializedProperty property, string key)
        {
            // Determine by ids
            switch (key)
            {
                case LocalizedTitleKey:
                    string title = GetFieldStringValue(property, "name");
                    if (!string.IsNullOrEmpty(title))
                    {
                        return title;
                    }
                    break;
                case "id":
                    return WitStyles.Texts.ConfigurationIntentsIdLabel;
                case "entities":
                    return WitStyles.Texts.ConfigurationIntentsEntitiesLabel;
            }
            
            // Default to base
            return base.GetLocalizedText(property, key);
        }
        // Layout entity override
        protected override void LayoutPropertyField(FieldInfo subfield, SerializedProperty subfieldProperty, GUIContent labelContent, bool canEdit)
        {
            // Handle all the same except entities
            if (canEdit || !string.Equals(subfield.Name, "entities"))
            {
                base.LayoutPropertyField(subfield, subfieldProperty, labelContent, canEdit);
                return;
            }
            
            // Entity foldout
            subfieldProperty.isExpanded = WitEditorUI.LayoutFoldout(labelContent, subfieldProperty.isExpanded);
            if (subfieldProperty.isExpanded)
            {
                EditorGUI.indentLevel++;
                if (subfieldProperty.arraySize == 0)
                {
                    WitEditorUI.LayoutErrorLabel(WitStyles.Texts.ConfigurationEntitiesMissingLabel);
                }
                else
                {
                    for (int e = 0; e < subfieldProperty.arraySize; e++)
                    {
                        SerializedProperty entityProp = subfieldProperty.GetArrayElementAtIndex(e);
                        string entityPropName = entityProp.FindPropertyRelative("name").stringValue;
                        WitEditorUI.LayoutLabel(entityPropName);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }
        // Determine if should layout field
        protected override bool ShouldLayoutField(FieldInfo subfield)
        {
            switch (subfield.Name)
            {
                case "name":
                    return false;
            }
            return base.ShouldLayoutField(subfield);
        }
    }
}
