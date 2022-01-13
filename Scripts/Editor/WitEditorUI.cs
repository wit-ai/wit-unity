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
        // Default
        public static void LayoutLabel(string text, ref float height)
        {
            LayoutLabel(text, WitStyles.Label, ref height);
        }
        // Header
        public static void LayoutHeaderLabel(string text, ref float height)
        {
            LayoutLabel(text, WitStyles.LabelHeader, ref height);
        }
        // Subheader
        public static void LayoutSubheaderLabel(string text, ref float height)
        {
            LayoutLabel(text, WitStyles.LabelSubheader, ref height);
        }
        // Error
        public static void LayoutErrorLabel(string text, ref float height)
        {
            LayoutLabel(text, WitStyles.LabelError, ref height);
        }
        // Local only
        private static void LayoutLabel(string text, GUIStyle style, ref float height)
        {
            // Simple layout
            EditorGUILayout.LabelField(text, style);
            // Add height
            height += style.CalcSize(new GUIContent(text)).y;
        }
        // Layout key label
        public static void LayoutKeyLabel(string key, string text, ref float height)
        {
            // Simple layout
            EditorGUILayout.LabelField(key, text, WitStyles.TextField);
            // Add height
            height += WitStyles.TextField.CalcSize(new GUIContent(text)).y;
        }
        #endregion

        #region BUTTONS
        // Layout text button
        public static bool LayoutTextButton(string text, ref float height)
        {
            GUIContent content = new GUIContent(text);
            float width = WitStyles.TextButton.CalcSize(content).x + WitStyles.TextButtonPadding * 2f;
            return LayoutButton(content, WitStyles.TextButton, new GUILayoutOption[] { GUILayout.Width(width) }, ref height);
        }
        // Layout icon button
        public static bool LayoutIconButton(GUIContent icon, ref float height)
        {
            return LayoutButton(icon, WitStyles.IconButton, null, ref height);
        }
        // Layout tab buttons
        public static void LayoutTabButtons(string[] tabTitles, ref int selection, ref float height)
        {
            if (tabTitles != null)
            {
                float maxHeight = 0f;
                GUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * WitStyles.TextButtonPadding * 2f);
                for (int t = 0; t < tabTitles.Length; t++)
                {
                    float newHeight = 0f;
                    GUI.enabled = selection != t;
                    if (LayoutTabButton(tabTitles[t], ref newHeight))
                    {
                        selection = t;
                    }
                    maxHeight = Mathf.Max(maxHeight, newHeight);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        // Layout tab button
        public static bool LayoutTabButton(string tabTitle, ref float height)
        {
            return LayoutButton(new GUIContent(tabTitle), WitStyles.TabButton, null, ref height);
        }
        // Layout button
        private static bool LayoutButton(GUIContent content, GUIStyle style, GUILayoutOption[] options, ref float height)
        {
            // Simple layout
            bool result = GUILayout.Button(content, style, options);
            // Add height
            height += style.CalcSize(content).y;
            // Return
            return result;
        }
        // Layout header button
        public static void LayoutHeaderButton(Texture2D headerTexture, string headerURL, ref float height)
        {
            float headerWidth = headerTexture == null ? 0f : Mathf.Min(headerTexture.width, Mathf.Min(WitStyles.WindowMinWidth, EditorGUIUtility.currentViewWidth) - WitStyles.WindowPaddingLeft - WitStyles.WindowPaddingRight);
            LayoutHeaderButton(headerTexture, headerURL, headerWidth, ref height);
        }
        // Layout header button
        public static void LayoutHeaderButton(Texture2D headerTexture, string headerURL, float headerWidth, ref float height)
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
                height += headerHeight;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }
        #endregion

        #region FOLDOUT
        // Foldout storage
        private static Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        // Foldout settings
        private static string GetFoldoutID(object bindObject)
        {
            return bindObject == null ? "" : bindObject.GetHashCode().ToString();
        }
        // Get foldout
        public static bool GetFoldoutValue(object bindObject)
        {
            string foldoutID = GetFoldoutID(bindObject);
            if (!string.IsNullOrEmpty(foldoutID) && foldouts.ContainsKey(foldoutID))
            {
                return foldouts[foldoutID];
            }
            return false;
        }
        // Apply foldout
        public static void SetFoldoutValue(object bindObject, bool toFoldout)
        {
            string foldoutID = GetFoldoutID(bindObject);
            if (!string.IsNullOrEmpty(foldoutID))
            {
                foldouts[foldoutID] = toFoldout;
            }
        }
        // GUI Layout
        public static bool LayoutFoldout(GUIContent key, object bindObject, ref float height)
        {
            bool wasFoldout = GetFoldoutValue(bindObject);
            bool isFoldout = LayoutFoldout(key, wasFoldout, ref height);
            if (isFoldout != wasFoldout)
            {
                SetFoldoutValue(bindObject, isFoldout);
            }
            return isFoldout;
        }
        // GUI Layout handled without bind objects
        public static bool LayoutFoldout(GUIContent key, bool wasFoldout, ref float height)
        {
            bool isFoldout = EditorGUILayout.Foldout(wasFoldout, key, true, WitStyles.Foldout);
            height += WitStyles.Foldout.CalcSize(key).y;
            return isFoldout;
        }
        #endregion

        #region FIELDS
        // Simple text field
        public static void LayoutTextField(GUIContent key, ref string value, ref bool isUpdated, ref float height)
        {
            // Ensure not null
            if (value == null)
            {
                value = string.Empty;
            }

            // Simple layout
            string newValue = EditorGUILayout.TextField(key, value, WitStyles.TextField);

            // Update
            if (!newValue.Equals(value))
            {
                value = newValue;
                isUpdated = true;
            }

            // Add height
            height += WitStyles.TextField.CalcSize(key).y;
        }
        // Simple password field
        public static void LayoutPasswordField(GUIContent key, ref string value, ref bool isUpdated, ref float height)
        {
            // Begin horizontal
            GUILayout.BeginHorizontal();

            // Simple layout
            string newValue = EditorGUILayout.PasswordField(key, value, WitStyles.PasswordField);

            // Layout icon
            float h = 0f;
            if (LayoutIconButton(WitStyles.PasteIcon, ref h))
            {
                newValue = EditorGUIUtility.systemCopyBuffer;
                GUI.FocusControl(null);
            }

            // End horizontal
            GUILayout.EndHorizontal();

            // Update
            if (!newValue.Equals(value))
            {
                value = newValue;
                isUpdated = true;
            }

            // Add height
            h = Mathf.Max(h, WitStyles.PasswordField.CalcSize(key).y);
            height += h;
        }
        // Simple int field
        public static void LayoutIntField(GUIContent key, ref int value, ref bool isUpdated, ref float height)
        {
            // Simple layout
            int newValue = EditorGUILayout.IntField(key, value, WitStyles.IntField);

            // Update
            if (newValue != value)
            {
                value = newValue;
                isUpdated = true;
            }

            // Add height
            height += WitStyles.IntField.CalcSize(key).y;
        }
        // Locked text field
        private static string unlockID = "";
        private static string unlockText = "";
        public static void LayoutLockedTextField(GUIContent key, ref string value, ref bool isUpdated, ref float height)
        {
            // Determine if locked
            string id = GetFoldoutID(key.text);
            bool isEditing = unlockID.Equals(id);

            // Begin Horizontal
            float h = 0f;
            GUILayout.BeginHorizontal();

            // Hide if not editing
            GUI.enabled = isEditing;
            // Layout
            string newValue = isEditing ? unlockText : value;
            bool newUpdate = false;
            LayoutTextField(key, ref newValue, ref newUpdate, ref h);
            // Allow next item
            GUI.enabled = true;

            // Updated
            if (newUpdate && isEditing)
            {
                unlockText = newValue;
            }

            // Cancel vs Apply Buttons
            float h2 = 0f;
            if (isEditing)
            {
                if (LayoutIconButton(WitStyles.ResetIcon, ref h2))
                {
                    unlockID = string.Empty;
                    unlockText = string.Empty;
                    GUI.FocusControl(null);
                }
                h = Mathf.Max(h, h2);
                if (LayoutIconButton(WitStyles.AcceptIcon, ref h2))
                {
                    if (!unlockText.Equals(value))
                    {
                        value = unlockText;
                        isUpdated = true;
                    }
                    unlockID = string.Empty;
                    unlockText = string.Empty;
                    GUI.FocusControl(null);
                }
                h = Mathf.Max(h, h2);
            }
            // Edit button
            else
            {
                if (LayoutIconButton(WitStyles.EditIcon, ref h2))
                {
                    unlockID = id;
                    unlockText = value;
                }
                h = Mathf.Max(h, h2);
            }

            // End Horizontal
            GUILayout.EndHorizontal();

            // Add height
            height += h;
        }
        #endregion

        #region MISCELANEOUS
        // Simple toggle
        public static void LayoutToggle(GUIContent key, ref bool value, ref bool isUpdated, ref float height)
        {
            // Simple layout
            bool newValue = EditorGUILayout.Toggle(key, value, WitStyles.Toggle);

            // Update
            if (value != newValue)
            {
                value = newValue;
                isUpdated = true;
            }

            // Add height
            height += WitStyles.Toggle.CalcSize(key).y;
        }
        // Simple popup/dropdown
        public static void LayoutPopup(string key, string[] options, ref int selection, ref bool isUpdated, ref float height)
        {
            // Simple layout
            int newValue = EditorGUILayout.Popup(key, selection, options, WitStyles.Popup);

            // Update
            if (selection != newValue)
            {
                selection = newValue;
                isUpdated = true;
            }

            // Add height
            height += WitStyles.Popup.CalcSize(new GUIContent(key)).y;
        }
        // Handles
        public static bool LayoutSerializedObjectPopup(SerializedObject serializedObject, string propertyName, string[] names, ref int index)
        {
            // Get property
            var property = serializedObject.FindProperty(propertyName);
            // Get intent
            string intent;
            float height = 0f;
            bool updated = false;
            if (names != null && names.Length > 0)
            {
                index = Mathf.Clamp(index, 0, names.Length);
                LayoutPopup(property.displayName, names, ref index, ref updated, ref height);
                intent = names[index];
            }
            else
            {
                intent = property.stringValue;
                LayoutTextField(new GUIContent(property.displayName), ref intent, ref updated, ref height);
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
        /// <summary>
        /// Handle Window Layout
        /// </summary>
        public static void LayoutWindow(string windowTitle, Texture2D windowHeader, string windowHeaderURL, Func<float> windowContentLayout, ref Vector2 offset, out Vector2 size)
        {
            // Window width
            float windowWidth = EditorGUIUtility.currentViewWidth;

            // Begin scroll
            offset = GUILayout.BeginScrollView(offset);
            // Top padding
            float height = WitStyles.WindowPaddingTop;
            GUILayout.Space(WitStyles.WindowPaddingTop);
            // Left padding
            GUILayout.BeginHorizontal();
            GUILayout.Space(WitStyles.WindowPaddingLeft);
            GUILayout.BeginVertical();

            // Layout header image
            if (windowHeader != null)
            {
                float headerWidth = Mathf.Min(windowHeader.width, Mathf.Min(WitStyles.WindowMinWidth, windowWidth) - WitStyles.WindowPaddingLeft - WitStyles.WindowPaddingRight);
                LayoutHeaderButton(windowHeader, windowHeaderURL, headerWidth, ref height);
                GUILayout.Space(WitStyles.HeaderPaddingBottom);
                height += WitStyles.HeaderPaddingBottom;
            }

            // Layout header label
            if (!string.IsNullOrEmpty(windowTitle))
            {
                LayoutHeaderLabel(windowTitle, ref height);
            }

            // Layout content
            if (windowContentLayout != null)
            {
                height += windowContentLayout();
            }

            // Right padding
            GUILayout.EndVertical();
            GUILayout.Space(WitStyles.WindowPaddingRight);
            GUILayout.EndHorizontal();
            // Bottom padding
            height += WitStyles.WindowPaddingBottom;
            GUILayout.Space(WitStyles.WindowPaddingBottom);
            // End scroll
            GUILayout.EndScrollView();

            // Return size
            size = new Vector2(WitStyles.WindowMinWidth, Mathf.Max(height, WitStyles.WindowMinHeight));
        }
        #endregion
    }
}
