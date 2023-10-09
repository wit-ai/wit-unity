/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Meta.WitAi.Events.Editor
{
    /// <summary>
    /// Draws a dropdown of categories of events denoted with the EventCategory attribute,
    /// followed by a dropdown of events of those categories, with the ability to add an action
    /// handler for the any selected event.
    /// All this is wrapped in an 'Events' foldout.
    /// </summary>
    /// <typeparam name="T">The class type to inspect for tagged events.</typeparam>
    public abstract class EventPropertyDrawer<T> : PropertyDrawer
    {
        private const int CONTROL_SPACING = 5;
        private const int UNSELECTED = -1;
        private const int BUTTON_WIDTH = 75;
        private const int PROPERTY_FIELD_SPACING = 25;

        private bool showEvents = false;

        private int selectedCategoryIndex = 0;
        private int selectedEventIndex = 0;

        private int propertyOffset;

        private static Dictionary<string, string[]> _eventCategories;

        public virtual string DocumentationUrl => string.Empty;
        public virtual string DocumentationTooltip => string.Empty;

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Uses reflection to internally load tagged event fields from the given type and its base type.
        /// </summary>
        private void InitializeEventCategories(Type eventsType)
        {
            // Get all category events in type & base type
            Dictionary<string, List<string>> categoryLists = new Dictionary<string, List<string>>();
            foreach (var field in eventsType.GetFields(FLAGS))
            {
                AddCustomField(field, categoryLists);
            }
            foreach (var baseField in eventsType.BaseType.GetFields(FLAGS))
            {
                AddCustomField(baseField, categoryLists);
            }

            // Apply
            _eventCategories = new Dictionary<string, string[]>();
            foreach (var category in categoryLists.Keys)
            {
                _eventCategories[category] = categoryLists[category].ToArray();
            }
        }

        /// <summary>
        /// Retrieves all the events tagged with an EventCategory attribute and adds them
        /// to their corresponding category marked in the attribute
        /// </summary>
        /// <param name="field">the field containing the EventCategory tagged events</param>
        /// <param name="categoryLists">the collection of events by category</param>
        private void AddCustomField(FieldInfo field, Dictionary<string, List<string>> categoryLists)
        {
            if (!ShouldShowField(field))
            {
                return;
            }
            EventCategoryAttribute[] attributes = field.GetCustomAttributes(
                typeof(EventCategoryAttribute), false) as EventCategoryAttribute[];
            if (attributes == null || attributes.Length == 0)
            {
                return;
            }
            foreach (var eventCategory in attributes)
            {
                List<string> values = categoryLists.ContainsKey(eventCategory.Category) ? categoryLists[eventCategory.Category] : new List<string>();
                string fieldName = GetDisplayFieldName(field);
                if (!values.Contains(fieldName))
                {
                    values.Add(fieldName);
                }
                categoryLists[eventCategory.Category] = values;
            }
        }

        private bool ShouldShowField(FieldInfo field)
        {
            if (field.IsStatic)
            {
                return false;
            }
            if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField)))
            {
                return false;
            }
            if (Attribute.IsDefined(field, typeof(HideInInspector)))
            {
                return false;
            }
            return Attribute.IsDefined(field, typeof(EventCategoryAttribute));
        }

        private string GetDisplayFieldName(FieldInfo field)
        {
            string result = field.Name.TrimStart('_');
            return result[0].ToString().ToUpper() + result.Substring(1, result.Length - 1);
        }
        private string GetFieldNameFromDisplay(string fieldDisplayName)
        {
            return "_" + fieldDisplayName[0].ToString().ToLower() + fieldDisplayName.Substring(1, fieldDisplayName.Length - 1);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var eventObject = fieldInfo.GetValue(property.serializedObject.targetObject) as EventRegistry;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lines = 1;
            var height = 0;

            // Allocate enough lines to display dropdown elements depending on which ones are showing.
            if (showEvents && Selection.activeTransform)
                lines++;

            if (showEvents && selectedCategoryIndex != UNSELECTED)
                lines++;

            height = Mathf.RoundToInt(lineHeight * lines);

            // By default, the property elements appear directly below the dropdowns.
            propertyOffset = height + (int)WitStyles.TextButtonPadding;

            // If the Events foldout is expanded and there are overridden properties, allocate space for them.
            if (eventObject != null && eventObject.OverriddenCallbacks.Count != 0 && showEvents)
            {
                var callbacksArray = eventObject.OverriddenCallbacks.ToArray();

                foreach (var callback in callbacksArray)
                {
                    var fieldProperty = GetPropertyFromDisplayFieldName(property, callback);
                    if (fieldProperty != null)
                    {
                        height += Mathf.RoundToInt(EditorGUI.GetPropertyHeight(fieldProperty, true) + CONTROL_SPACING);
                    }
                }

                // Add some extra space so the last property field's +/- buttons don't overlap the next control.
                height += PROPERTY_FIELD_SPACING;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            showEvents = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), showEvents, "Events");

            ShowDocumentationButton(position);

            if (showEvents && Selection.activeTransform)
            {
                if (_eventCategories == null)
                    InitializeEventCategories(fieldInfo.FieldType);

                var eventObject = fieldInfo.GetValue(property.serializedObject.targetObject) as EventRegistry;

                var eventCategoriesKeyArray = _eventCategories.Keys.ToArray();

                EditorGUI.indentLevel++;

                // Shift the control rectangle down one line to accomodate the category dropdown.
                position.y += EditorGUIUtility.singleLineHeight;
                position.height = EditorGUIUtility.singleLineHeight;

                selectedCategoryIndex = EditorGUI.Popup(position, "Event Category",
                    selectedCategoryIndex, eventCategoriesKeyArray);

                DrawEventsDropdownForCategory(position, eventCategoriesKeyArray, eventObject);

                ShowOverriddenCallbacks(position, property, eventObject);

                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draws the dropdown containing the events for the selected category
        /// </summary>
        /// <param name="position">Where to draw it</param>
        /// <param name="eventCategoriesKeyArray">collection of the category names</param>
        /// <param name="eventRegistry">the registry to which new callbacks should be added</param>
        private void DrawEventsDropdownForCategory(Rect position, string[] eventCategoriesKeyArray, EventRegistry eventRegistry)
        {
            if (selectedCategoryIndex == UNSELECTED)
                return;

            var eventsArray = _eventCategories[eventCategoriesKeyArray[selectedCategoryIndex]];

            if (selectedEventIndex >= eventsArray.Length)
                selectedEventIndex = 0;

            // Create a new rectangle to position the events dropdown and Add button.
            var selectedEventDropdownPosition = new Rect(position);

            selectedEventDropdownPosition.y += EditorGUIUtility.singleLineHeight + 2;
            selectedEventDropdownPosition.width = position.width - (BUTTON_WIDTH + (int)WitStyles.TextButtonPadding);

            selectedEventIndex = EditorGUI.Popup(selectedEventDropdownPosition, "Event", selectedEventIndex,
                eventsArray);

            var selectedEventButtonPosition = new Rect(selectedEventDropdownPosition);

            selectedEventButtonPosition.width = BUTTON_WIDTH;
            selectedEventButtonPosition.x =
                selectedEventDropdownPosition.x + selectedEventDropdownPosition.width + CONTROL_SPACING;

            var eventName = _eventCategories[eventCategoriesKeyArray[selectedCategoryIndex]][selectedEventIndex];

            if (eventRegistry.IsCallbackOverridden(eventName))
            {
                if (eventRegistry.IsCallbackOverridden(eventName) && GUI.Button(selectedEventButtonPosition, "Remove"))
                {
                    eventRegistry.RemoveOverriddenCallback(eventName);
                }
            }
            else
            {
                if (GUI.Button(selectedEventButtonPosition, "Add"))
                {
                    RegisterCallbackOverride(eventName, eventRegistry);
                }
            }
        }

        /// <summary>
        /// If any overrides have been added to the property, show them for editing
        /// </summary>
        /// <param name="position">where to show them</param>
        /// <param name="property">the property which this drawer is drawing</param>
        /// <param name="eventRegistry">where the events are stored </param>
        private void ShowOverriddenCallbacks(Rect position, SerializedProperty property, EventRegistry eventRegistry)
        {
            if (eventRegistry == null || eventRegistry.OverriddenCallbacks.Count == 0)
                return;

            var propertyRect = new Rect(position.x, position.y + propertyOffset, position.width, 0);

            foreach (var callback in eventRegistry.OverriddenCallbacks)
            {
                var fieldProperty = GetPropertyFromDisplayFieldName(property, callback);
                if (fieldProperty == null)
                {
                    continue;
                }

                propertyRect.height = EditorGUI.GetPropertyHeight(fieldProperty, true);

                EditorGUI.PropertyField(propertyRect, fieldProperty);

                propertyRect.y += propertyRect.height + CONTROL_SPACING;
            }
        }

        /// <summary>
        /// Registers the given callback event to the given registry so that it'll be displayed as
        /// a thing to which a callback may be added
        /// </summary>
        /// <param name="eventName">name of the event to show</param>
        /// <param name="eventRegistry">the registry to use.</param>
        private void RegisterCallbackOverride(string eventName, EventRegistry eventRegistry)
        {
            if (eventRegistry != null && selectedEventIndex != UNSELECTED &&
                !eventRegistry.IsCallbackOverridden(eventName))
            {
                var fieldName = GetFieldNameFromDisplay(eventName);
                if (eventRegistry.IsCallbackOverridden(fieldName))
                {
                    eventRegistry.RemoveOverriddenCallback(fieldName);
                }
                fieldName = GetFieldNameFromDisplay(eventName).Substring(1);
                if (eventRegistry.IsCallbackOverridden(fieldName))
                {
                    eventRegistry.RemoveOverriddenCallback(fieldName);
                }
                eventRegistry.RegisterOverriddenCallback(eventName);
            }
        }

        private void ShowDocumentationButton(Rect position)
        {
            string url = DocumentationUrl;
            if (string.IsNullOrEmpty(url))
                return;

            Texture texture = WitStyles.HelpIcon.image;
            if (texture == null)
                return;

            // Add a ? button
            Vector2 textureSize = WitStyles.IconButton.CalcSize(WitStyles.HelpIcon);
            Rect buttonRect = new Rect(position.x + position.width - textureSize.x, position.y, textureSize.x, textureSize.y);
            if (GUI.Button(buttonRect,
                new GUIContent(WitStyles.HelpIcon.image, DocumentationTooltip), WitStyles.IconButton))
            {
                Application.OpenURL(url);
            }
            // Add a tooltip
            if (!string.IsNullOrEmpty(DocumentationTooltip))
            {
                GUI.Label(buttonRect, GUI.tooltip);
            }
        }
        private SerializedProperty GetPropertyFromDisplayFieldName(SerializedProperty property, string fieldName)
        {
            SerializedProperty result = property.FindPropertyRelative(fieldName);
            if (result == null)
            {
                string fieldName2 = GetFieldNameFromDisplay(fieldName);
                result = property.FindPropertyRelative(fieldName2);
                if (result == null)
                {
                    Debug.LogError($"Could not find serialized property field: {fieldName} ({fieldName2})");
                }
            }
            return result;
        }
    }
}
