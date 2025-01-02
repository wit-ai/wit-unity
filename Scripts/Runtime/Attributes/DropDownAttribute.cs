/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text.RegularExpressions;
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
        /// Returns true if the data options for the field list are empty and displays the property value instead.
        /// </summary>
        public bool ShowPropertyIfListIsEmpty { get; }

        /// <summary>
        /// If true a refresh button will be shown
        /// </summary>
        public bool ShowRefreshButton { get; }

        /// <summary>
        /// If set, this method will be used when the refresh button is pressed. This may be a longer manual refresh if
        /// needed and may or may not be the same as the refresh method used for refreshing on paint.
        /// </summary>
        public string RefreshMethodName { get; }

        /// <summary>
        /// If set a search button will appear to allow for text based searching of parameters
        /// </summary>
        public bool ShowSearch { get; }

        /// <summary>
        /// Constructor for drop down attribute
        /// </summary>
        public DropDownAttribute(string optionListGetterName, bool refreshOnRepaint = false, bool allowInvalid = false,
            bool showPropertyIfListIsEmpty = true, bool showRefreshButton = true, string refreshMethodName = null,
            bool showSearch = false)
        {
            OptionListGetterName = optionListGetterName;
            RefreshOnRepaint = refreshOnRepaint;
            AllowInvalid = allowInvalid;
            ShowPropertyIfListIsEmpty = showPropertyIfListIsEmpty;
            ShowRefreshButton = showRefreshButton;
            RefreshMethodName = refreshMethodName;
            ShowSearch = showSearch;
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

        private readonly float _refreshSpace =
            EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private readonly float _utilityButtonSize =
            EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing;

        private readonly float _searchHeight =
            (EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing) * 8;

        private MethodInfo _refreshMethod;

        private Dictionary<string, Search> _searchContext = new Dictionary<string, Search>();

        /// <summary>
        /// Search context used to handle individual drawable's current search state/visibility.
        /// </summary>
        private class Search
        {
            public bool show;
            public string term;
            public Vector2 scroll;
            public string[] filteredList;
        }

        /// <summary>
        /// Gets the search context for a given property so the GUI can decide if it should be shown or not.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private Search SearchContext(SerializedProperty property)
        {
            if (!_searchContext.TryGetValue(property.propertyPath, out var search))
            {
                search = new Search();
                _searchContext[property.propertyPath] = search;
            }

            return search;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = base.GetPropertyHeight(property, label);
            if (SearchContext(property).show) height += _searchHeight + EditorGUIUtility.singleLineHeight;
            return height;
        }

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

            if (!EnsureGetterMethod(property, dropDownAttribute))
            {
                string typeName = property.serializedObject.targetObject.GetType().Name;
                string methodName = dropDownAttribute.OptionListGetterName;
                Color old = GUI.color;
                GUI.color = Color.red;
                EditorGUI.LabelField(position, label.text,
                    $"{typeName}.{methodName}() : IEnumerable<string> method not found");
                GUI.color = old;
                return;
            }

            var search = SearchContext(property);
            if (search.show)
            {
                var searchRect = new Rect(position.x, position.yMax - _searchHeight - EditorGUIUtility.singleLineHeight,
                    position.width, _searchHeight);
                GUI.Box(searchRect, "");
                var searchTermRect = new Rect(searchRect);
                searchTermRect.height = EditorGUIUtility.singleLineHeight;
                var term = GUI.TextField(searchTermRect, search.term);
                if (term != search.term || null == search.filteredList)
                {
                    var cleanTerm = CleanSearch(term);
                    search.filteredList = string.IsNullOrEmpty(term)
                        ? _options
                        : _options.Where(option => CleanSearch(option).Contains(cleanTerm)).ToArray();
                    search.term = term;
                }

                var matchesRect = new Rect(searchRect);
                matchesRect.height -= EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing;
                matchesRect.y += searchTermRect.height + EditorGUIUtility.singleLineHeight;
                search.scroll = GUI.BeginScrollView(matchesRect, search.scroll, new Rect(0, 0,
                    matchesRect.width, EditorGUIUtility.singleLineHeight * search.filteredList.Length));
                int index = 0;
                foreach (var option in search.filteredList)
                {
                    var optionRect = new Rect(0, EditorGUIUtility.singleLineHeight * index, matchesRect.width,
                        EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(optionRect, option, EditorStyles.textField))
                    {
                        property.stringValue = option;
                        search.show = false;
                        GUIUtility.keyboardControl = 0; // This line removes keyboard focus from the search field
                    }

                    index++;
                }

                GUI.EndScrollView();
            }

            if (dropDownAttribute.ShowSearch)
            {
                DrawSearchButton(ref position, property, dropDownAttribute);
            }

            if (dropDownAttribute.ShowRefreshButton)
            {
                DrawRefreshButton(ref position, property, dropDownAttribute);
            }

            // Obtain options/lookup if empty or required on repaint
            if ((_options == null
                 || dropDownAttribute.ShowPropertyIfListIsEmpty && _options.Length == 0
                 || dropDownAttribute.RefreshOnRepaint) && !RefreshOptions(property))
            {
                EditorGUI.PropertyField(position, property, label);
            }
            else
            {
                LayoutDropdown(position, property, label, dropDownAttribute);
            }
        }

        private string CleanSearch(string term)
        {
            if (string.IsNullOrEmpty(term)) return string.Empty;
            return Regex.Replace(term.ToLower(), @"[^\w0-9]+", "");
        }

        private bool DrawButton(string iconName, string tooltip, ref Rect position)
        {
            Rect buttonRect = new Rect(position.xMax - _utilityButtonSize, position.y,
                _utilityButtonSize, _utilityButtonSize);
            GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
            iconContent.tooltip = tooltip;
            position.xMax -= _utilityButtonSize + EditorGUIUtility.standardVerticalSpacing;

            return GUI.Button(buttonRect, iconContent, GUIStyle.none);
        }

        private void DrawSearchButton(ref Rect position, SerializedProperty property,
            DropDownAttribute dropDownAttribute)
        {

            GUIContent iconContent = EditorGUIUtility.IconContent("d_Search Icon");
            iconContent.tooltip = "Search";

            var search = SearchContext(property);
            if (DrawButton("d_Search Icon", "Search", ref position))
            {
                search.show = !search.show;
            }
        }

        private void DrawRefreshButton(ref Rect position, SerializedProperty property,
            DropDownAttribute dropDownAttribute)
        {
            if (DrawButton("d_Refresh", "Refresh", ref position))
            {
                if (string.IsNullOrEmpty(dropDownAttribute.RefreshMethodName))
                {
                    RefreshOptions(property);
                }
                else
                {
                    FullRefresh(property);
                }
            }
        }

        /// <summary>
        /// Makes sure the main getter method has been resolved
        /// </summary>
        /// <param name="property"></param>
        /// <param name="dropDownAttribute"></param>
        /// <returns></returns>
        private bool EnsureGetterMethod(SerializedProperty property, DropDownAttribute dropDownAttribute)
        {
            return null != _method || ResolveGetterMethod(property, dropDownAttribute);
        }

        /// <summary>
        /// Makes sure the refresh method has been resolved if needed
        /// </summary>
        /// <param name="property"></param>
        /// <param name="dropDownAttribute"></param>
        /// <returns></returns>
        private bool EnsureRefreshMethod(SerializedProperty property, DropDownAttribute dropDownAttribute)
        {
            // Refresh method is optional, so ensure can return true if it isn't set.
            return string.IsNullOrEmpty(dropDownAttribute.RefreshMethodName) ||
                   null != _refreshMethod || ResolveRefreshMethod(property, dropDownAttribute);
        }

        /// <summary>
        /// Attempts to obtain the MethodInfo for the specified method name
        /// </summary>
        /// <returns>Returns true if method is found that contains correct number of parameters & results</returns>
        private bool ResolveGetterMethod(SerializedProperty property, DropDownAttribute dropDownAttribute)
        {
            Type baseType = property.serializedObject.targetObject.GetType();
            _method = baseType.GetMethod(dropDownAttribute.OptionListGetterName,
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static);
            _options = null;
            _optionLookup.Clear();
            return _method != null && _method.GetParameters().Length == 0 &&
                   _method.ReturnType.IsAssignableFrom(typeof(IEnumerable<string>));
        }

        /// <summary>
        /// Attempts to obtain the MethodInfo for the specified method name
        /// </summary>
        /// <returns>Returns true if method is found that contains correct number of parameters & results</returns>
        private bool ResolveRefreshMethod(SerializedProperty property, DropDownAttribute dropDownAttribute)
        {
            if (null != _refreshMethod) return true;
            Type baseType = property.serializedObject.targetObject.GetType();
            _refreshMethod = baseType.GetMethod(dropDownAttribute.RefreshMethodName,
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.Static);
            return _method != null;
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
        /// Performs the MethodInfo call, casts the options returned into an array & generates
        /// an option lookup dictionary for easier index lookup.
        /// </summary>
        /// <returns>Returns true if more than one option is found</returns>
        private bool FullRefresh(SerializedProperty property)
        {
            _refreshMethod.Invoke(property.serializedObject.targetObject, null);
            return RefreshOptions(property);
        }

        /// <summary>
        /// Performs a dropdown layout & applies selected string to the property
        /// </summary>
        private void LayoutDropdown(Rect position, SerializedProperty property, GUIContent label,
            DropDownAttribute dropDownAttribute)
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
