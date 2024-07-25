/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi.Composer.Attributes;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Interfaces;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Meta.WitAi.Composer.Drawers
{
    [CustomPropertyDrawer(typeof(VersionTagDropdownAttribute))]
    public class VersionDropdownDrawer : PropertyDrawer
    {
        private WitConfiguration _configuration;
        private string[] _versionTagNames;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Ensure configuration and version tags are setup
            EnsureSetup(property);
            if (_configuration == null)
            {
                EditorGUILayout.LabelField(label.text, "No wit configuration.");
                return;
            }
            // Default to property field if needed
            if (property.propertyType != SerializedPropertyType.String
                || _versionTagNames == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // Determine last index
            var lastIndex = Array.IndexOf(_versionTagNames, property.stringValue);

            // If not found but not empty, show string field
            // This ensures version removals do not wipe stored values
            if (lastIndex == -1 && !string.IsNullOrEmpty(property.stringValue))
            {
                var oldColor = GUI.color;
                GUI.color = Color.red;
                EditorGUI.PropertyField(position, property, new GUIContent($"{label.text} (Not Found)"));
                GUI.color = oldColor;
                return;
            }

            // Clamp and get selected index
            lastIndex = Mathf.Max(0, lastIndex);
            var selectedIndex = EditorGUI.Popup(position, label.text, lastIndex, _versionTagNames);
            if (lastIndex != selectedIndex)
            {
                property.stringValue = selectedIndex < 1 ? string.Empty : _versionTagNames[selectedIndex];
            }
        }

        private void EnsureSetup(SerializedProperty property)
        {
            WitConfiguration configuration = GetConfiguration(property);
            if (_versionTagNames == null || _configuration != configuration)
            {
                SetupTagVersionDropDown(configuration);
            }
        }

        private WitConfiguration GetConfiguration(SerializedProperty property)
        {
            var targetObject = property?.serializedObject?.targetObject;
            if (targetObject is IWitConfigurationProvider configProvider)
            {
                return configProvider.Configuration;
            }
            if (targetObject is WitConfiguration)
            {
                return (WitConfiguration)targetObject;
            }
            return null;
        }

        private void SetupTagVersionDropDown(WitConfiguration configuration)
        {
            // Set configuration
            _configuration = configuration;

            // Nullify tags
            if (_configuration == null)
            {
                _versionTagNames = null;
                return;
            }

            var versionTags = _configuration.GetApplicationInfo().versionTags;
            var names = null != versionTags ? versionTags.Select(instance => instance.name).ToList() : new List<string>();
            names.Insert(0, "Current");
            _versionTagNames = names.ToArray();
        }
    }
}
