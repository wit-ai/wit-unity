/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;

#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
#endif

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// An attribute for serialized strings that allow them to be used with
    /// a custom dropdown editor via a reflection method call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DropDownAttribute : PropertyAttribute
    {
        /// <summary>
        /// The method name to be used for obtaining an IEnumerable<string> of options available
        /// for the drop down menu.
        /// </summary>
        public string OptionListGetterName { get; }
        /// <summary>
        /// Whether or not the dropdown options getter method should be invoked on every repaint or cached.  This should
        /// be set to true if the list is expected to change throughout interactions with the GUI.
        /// </summary>
        public bool RefreshOnRepaint { get; }
        /// <summary>
        /// Whether or not the dropdown option can be invalid in which case it will be left at index -1.  If false,
        /// this will always clamp the dropdown to the first option.
        /// </summary>
        public bool AllowInvalid { get; }

        /// <summary>
        /// Constructor for drop down attribute
        /// </summary>
        public DropDownAttribute(string optionListGetterName, bool refreshOnRepaint = false, bool allowInvalid = false)
        {
            OptionListGetterName = optionListGetterName;
            RefreshOnRepaint = refreshOnRepaint;
            AllowInvalid = allowInvalid;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// The property drawer automatically used for this attribute that creates a dropdown
    /// after performing a method call that returns a list of options.
    /// </summary>
    [CustomPropertyDrawer(typeof(DropDownAttribute))]
    public class DropDownAttributePropertyDrawer : PropertyDrawer
    {
        // Method to be used for obtaining all options
        private MethodInfo _method;

        // Options available
        private string[] _options;
        // Quick lookup for index
        private Dictionary<string, int> _optionLookup = new Dictionary<string, int>();

        // Layout string field as dropdown if possible
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Ignore non string properties
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // Obtain method info if missing
            var dropDownAttribute = (DropDownAttribute)attribute;
            if (_method == null && !RefreshMethod(property, dropDownAttribute))
            {
                string typeName = property.serializedObject.targetObject.GetType().Name;
                string methodName = dropDownAttribute.OptionListGetterName;
                Color old = GUI.color;
                GUI.color = Color.red;
                EditorGUI.LabelField(position, label.text, $"{typeName}.{methodName}() : IEnumerable<string> method not found");
                GUI.color = old;
                return;
            }

            // Obtain options/lookup if empty or required on repaint
            if ((_options == null || dropDownAttribute.RefreshOnRepaint) && !RefreshOptions(property))
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // Layout dropdown
            LayoutDropdown(position, property, label, dropDownAttribute);
        }

        /// <summary>
        /// Attempts to obtain the MethodInfo for the specified method name
        /// </summary>
        /// <returns>Returns true if method is found that contains correct number of parameters & results</returns>
        private bool RefreshMethod(SerializedProperty property, DropDownAttribute dropDownAttribute)
        {
            Type baseType = property.serializedObject.targetObject.GetType();
            _method = baseType.GetMethod(dropDownAttribute.OptionListGetterName,
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static);
            _options = null;
            _optionLookup.Clear();
            return _method != null && _method.GetParameters().Length == 0 && _method.ReturnType.IsAssignableFrom(typeof(IEnumerable<string>));
        }

        /// <summary>
        /// Performs the MethodInfo call, casts the options returned into an array & generates
        /// an option lookup dictionary for easier index lookup.
        /// </summary>
        /// <returns>Returns true if more than one option is found</returns>
        private bool RefreshOptions(SerializedProperty property)
        {
            // Perform method invoke
            var options = _method.Invoke(property.serializedObject.targetObject, null);

            // If result is usable
            if (options is IEnumerable<string> optionList)
            {
                // Get options as array
                _options = optionList.ToArray();

                // Perform lookup for quicker index determination
                _optionLookup.Clear();
                for (int o = 0; o < _options.Length; o++)
                {
                    _optionLookup[_options[o]] = o;
                }
            }

            // Successful if more than one option exists
            return _options != null && _options.Length > 0;
        }

        /// <summary>
        /// Performs a dropdown layout & applies selected string to the property
        /// </summary>
        private void LayoutDropdown(Rect position, SerializedProperty property, GUIContent label, DropDownAttribute dropDownAttribute)
        {
            // Begin property
            EditorGUI.BeginProperty(position, label, property);

            // Determine current index
            _optionLookup.TryGetValue(property.stringValue, out int oldIndex);

            // Show popup & get new index if changed
            int newIndex = EditorGUI.Popup(position, label.text, oldIndex, _options);

            // If invalid is not allowed, ensure new index is clamped to option size
            if (!dropDownAttribute.AllowInvalid)
            {
                newIndex = Mathf.Clamp(newIndex, 0, _options.Length);
            }

            // If index changed & is valid, apply new string option
            if (oldIndex != newIndex && newIndex >= 0 && newIndex < _options.Length)
            {
                property.stringValue = _options[newIndex];
            }

            // Property layout complete
            EditorGUI.EndProperty();
        }
    }
#endif
}
