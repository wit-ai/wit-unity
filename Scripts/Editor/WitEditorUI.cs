/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Configuration;

namespace Facebook.WitAi
{
    public static class WitEditorUI
    {
        #region LABELS
        public static void LayoutLabel(string text)
        {
            LayoutLabel(text, WitStyles.Label);
        }
        public static void LayoutHeaderLabel(string text)
        {
            LayoutLabel(text, WitStyles.LabelHeader);
        }
        public static void LayoutSubheaderLabel(string text)
        {
            LayoutLabel(text, WitStyles.LabelSubheader);
        }
        public static void LayoutErrorLabel(string text)
        {
            LayoutLabel(text, WitStyles.LabelError);
        }
        private static void LayoutLabel(string text, GUIStyle style)
        {
            EditorGUILayout.LabelField(text, style);
        }
        public static void LayoutKeyLabel(string key, string text)
        {
            EditorGUILayout.LabelField(key, text, WitStyles.TextField);
        }
        #endregion

        #region BUTTONS
        public static bool LayoutTextButton(string text)
        {
            GUIContent content = new GUIContent(text);
            float width = WitStyles.TextButton.CalcSize(content).x + WitStyles.TextButtonPadding * 2f;
            return LayoutButton(content, WitStyles.TextButton, new GUILayoutOption[] { GUILayout.Width(width) });
        }
        public static bool LayoutIconButton(GUIContent icon)
        {
            return LayoutButton(icon, WitStyles.IconButton, null);
        }
        public static void LayoutTabButtons(string[] tabTitles, ref int selection)
        {
            if (tabTitles != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * WitStyles.TextButtonPadding * 2f);
                for (int i = 0; i < tabTitles.Length; i++)
                {
                    GUI.enabled = selection != i;
                    if (LayoutTabButton(tabTitles[i]))
                    {
                        selection = i;
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        public static bool LayoutTabButton(string tabTitle)
        {
            return LayoutButton(new GUIContent(tabTitle), WitStyles.TabButton, null);
        }
        private static bool LayoutButton(GUIContent content, GUIStyle style, GUILayoutOption[] options)
        {
            return GUILayout.Button(content, style, options);
        }
        // Layout header button
        public static void LayoutHeaderButton(Texture2D headerTexture, string headerURL)
        {
            float headerWidth = headerTexture == null ? 0f : Mathf.Min(headerTexture.width, Mathf.Min(WitStyles.WindowMinWidth, EditorGUIUtility.currentViewWidth) - WitStyles.WindowPaddingLeft - WitStyles.WindowPaddingRight);
            LayoutHeaderButton(headerTexture, headerURL, headerWidth);
        }
        // Layout header button
        public static void LayoutHeaderButton(Texture2D headerTexture, string headerURL, float headerWidth)
        {
            if (headerTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                float headerHeight = headerWidth * (float)headerTexture.height / (float)headerTexture.width;
                if (GUILayout.Button(headerTexture, WitStyles.HeaderButton, GUILayout.Width(headerWidth), GUILayout.Height(headerHeight)) && !string.IsNullOrEmpty(headerURL))
                {
                    Application.OpenURL(headerURL);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }
        #endregion

        #region FOLDOUT
        // Foldout storage for bind objects
        private static Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        private static string GetFoldoutID(object bindObject)
        {
            return bindObject == null ? "" : bindObject.GetHashCode().ToString();
        }
        public static bool GetFoldoutValue(object bindObject)
        {
            string foldoutID = GetFoldoutID(bindObject);
            if (!string.IsNullOrEmpty(foldoutID) && foldouts.ContainsKey(foldoutID))
            {
                return foldouts[foldoutID];
            }
            return false;
        }
        public static void SetFoldoutValue(object bindObject, bool toFoldout)
        {
            string foldoutID = GetFoldoutID(bindObject);
            if (!string.IsNullOrEmpty(foldoutID))
            {
                foldouts[foldoutID] = toFoldout;
            }
        }
        public static bool LayoutFoldout(GUIContent key, object bindObject)
        {
            bool wasFoldout = GetFoldoutValue(bindObject);
            bool isFoldout = LayoutFoldout(key, wasFoldout);
            if (isFoldout != wasFoldout)
            {
                SetFoldoutValue(bindObject, isFoldout);
            }
            return isFoldout;
        }
        // GUI Layout handled without bind objects
        public static bool LayoutFoldout(GUIContent key, bool wasFoldout)
        {
            return EditorGUILayout.Foldout(wasFoldout, key, true, WitStyles.Foldout);
        }
        #endregion

        #region FIELDS
        public static void LayoutTextField(GUIContent key, ref string fieldValue, ref bool isUpdated)
        {
            // Ensure not null
            if (fieldValue == null)
            {
                fieldValue = string.Empty;
            }

            // Simple layout
            string newFieldValue = EditorGUILayout.TextField(key, fieldValue, WitStyles.TextField);

            // Update if changed
            if (!string.Equals(fieldValue, newFieldValue))
            {
                fieldValue = newFieldValue;
                isUpdated = true;
            }
        }
        public static void LayoutPasswordField(GUIContent key, ref string fieldValue, ref bool isUpdated)
        {
            // Ensure not null
            if (fieldValue == null)
            {
                fieldValue = string.Empty;
            }

            // Simple layout
            GUILayout.BeginHorizontal();
            string newFieldValue = EditorGUILayout.PasswordField(key, fieldValue, WitStyles.PasswordField);

            // Layout icon
            if (LayoutIconButton(WitStyles.PasteIcon))
            {
                newFieldValue = EditorGUIUtility.systemCopyBuffer;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // Update if changed
            if (!string.Equals(fieldValue, newFieldValue))
            {
                fieldValue = newFieldValue;
                isUpdated = true;
            }
        }
        public static void LayoutIntField(GUIContent key, ref int fieldValue, ref bool isUpdated)
        {
            // Simple layout
            int newFieldValue = EditorGUILayout.IntField(key, fieldValue, WitStyles.IntField);

            // Update
            if (fieldValue != newFieldValue)
            {
                fieldValue = newFieldValue;
                isUpdated = true;
            }
        }
        // Locked text field
        private static string unlockFieldId = "";
        private static string unlockFieldText = "";
        public static void LayoutLockedTextField(GUIContent key, ref string fieldValue, ref bool isUpdated)
        {
            // Determine if locked
            string fieldId = GetFoldoutID(key.text);
            bool isEditing = unlockFieldId.Equals(fieldId);

            // Begin Horizontal
            GUILayout.BeginHorizontal();

            // Hide if not editing
            GUI.enabled = isEditing;
            // Layout
            string newFieldValue = isEditing ? unlockFieldText : fieldValue;
            bool newUpdate = false;
            LayoutTextField(key, ref newFieldValue, ref newUpdate);
            // Allow next item
            GUI.enabled = true;

            // Updated
            if (newUpdate && isEditing)
            {
                unlockFieldText = newFieldValue;
            }

            // Cancel vs Apply Buttons
            if (isEditing)
            {
                if (LayoutIconButton(WitStyles.ResetIcon))
                {
                    unlockFieldId = string.Empty;
                    unlockFieldText = string.Empty;
                    GUI.FocusControl(null);
                }
                if (LayoutIconButton(WitStyles.AcceptIcon))
                {
                    if (!string.Equals(fieldValue, unlockFieldText))
                    {
                        fieldValue = unlockFieldText;
                        isUpdated = true;
                    }
                    unlockFieldId = string.Empty;
                    unlockFieldText = string.Empty;
                    GUI.FocusControl(null);
                }
            }
            // Edit button
            else
            {
                if (LayoutIconButton(WitStyles.EditIcon))
                {
                    unlockFieldId = fieldId;
                    unlockFieldText = fieldValue;
                }
            }

            // End Horizontal
            GUILayout.EndHorizontal();
        }
        #endregion

        #region MISCELANEOUS
        public static void LayoutToggle(GUIContent key, ref bool toggleValue, ref bool isUpdated)
        {
            // Simple layout
            bool newToggleValue = EditorGUILayout.Toggle(key, toggleValue, WitStyles.Toggle);

            // Update
            if (toggleValue != newToggleValue)
            {
                toggleValue = newToggleValue;
                isUpdated = true;
            }
        }
        public static void LayoutPopup(string key, string[] options, ref int selectionValue, ref bool isUpdated)
        {
            // Simple layout
            int newSelectionValue = EditorGUILayout.Popup(key, selectionValue, options, WitStyles.Popup);

            // Update
            if (selectionValue != newSelectionValue)
            {
                selectionValue = newSelectionValue;
                isUpdated = true;
            }
        }
        public static bool LayoutSerializedObjectPopup(SerializedObject serializedObject, string propertyName, string[] names, ref int index)
        {
            // Get property
            var property = serializedObject.FindProperty(propertyName);
            // Get intent
            string intent;
            bool updated = false;
            if (names != null && names.Length > 0)
            {
                index = Mathf.Clamp(index, 0, names.Length);
                LayoutPopup(property.displayName, names, ref index, ref updated);
                intent = names[index];
            }
            else
            {
                intent = property.stringValue;
                LayoutTextField(new GUIContent(property.displayName), ref intent, ref updated);
            }

            // Success
            if (intent != property.stringValue)
            {
                property.stringValue = intent;
                return true;
            }

            // Failed
            return false;
        }
        #endregion

        #region WINDOW
        public static void LayoutWindow(string windowTitle, Texture2D windowHeader, string windowHeaderUrl, Action windowContentLayout, ref Vector2 offset, out Vector2 size)
        {
            // Init styles
            WitStyles.Init();
            // Window width
            float windowWidth = EditorGUIUtility.currentViewWidth;

            // Begin scroll
            offset = GUILayout.BeginScrollView(offset);
            // Top padding
            GUILayout.Space(WitStyles.WindowPaddingTop);
            // Left padding
            GUILayout.BeginHorizontal();
            GUILayout.Space(WitStyles.WindowPaddingLeft);
            GUILayout.BeginVertical();

            // Layout header image
            if (windowHeader != null)
            {
                float headerWidth = Mathf.Min(windowHeader.width, Mathf.Min(WitStyles.WindowMinWidth, windowWidth) - WitStyles.WindowPaddingLeft - WitStyles.WindowPaddingRight);
                LayoutHeaderButton(windowHeader, windowHeaderUrl, headerWidth);
                GUILayout.Space(WitStyles.HeaderPaddingBottom);
            }

            // Layout header label
            if (!string.IsNullOrEmpty(windowTitle))
            {
                LayoutHeaderLabel(windowTitle);
            }

            // Layout content
            windowContentLayout?.Invoke();

            // Right padding
            GUILayout.EndVertical();
            GUILayout.Space(WitStyles.WindowPaddingRight);
            GUILayout.EndHorizontal();
            // Bottom padding
            GUILayout.Space(WitStyles.WindowPaddingBottom);
            // End scroll
            GUILayout.EndScrollView();

            // Return size
            size = new Vector2(WitStyles.WindowMinWidth, WitStyles.WindowMinHeight);
        }
        #endregion
    }
}
