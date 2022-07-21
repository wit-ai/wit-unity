/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;

namespace Facebook.WitAi.Events.Editor
{
    public abstract class EventPropertyDrawer<T> : PropertyDrawer
    {
        private const int UNSELECTED = -1;

        private bool showEvents = false;

        private int selectedCategoryIndex = UNSELECTED;
        private int selectedEventIndex = UNSELECTED;

        private static Dictionary<string, List<string>> eventCategories;

        private void InitializeEventCategories(Type eventsType)
        {
            EventCategoryAttribute[] eventCategoryAttributes;

            eventCategories = new Dictionary<string, List<string>>();

            foreach (var field in eventsType.GetFields())
            {
                eventCategoryAttributes = field.GetCustomAttributes(
                    typeof(EventCategoryAttribute), false) as EventCategoryAttribute[];

                if (eventCategoryAttributes != null && eventCategoryAttributes.Length != 0)
                {
                    foreach (var eventCategory in eventCategoryAttributes)
                    {
                        if (!eventCategories.TryGetValue(eventCategory.Category, out var categories))
                        {
                            eventCategories[eventCategory.Category] = new List<string>();
                        }

                        eventCategories[eventCategory.Category].Add(field.Name);
                    }
                }
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            showEvents = EditorGUI.Foldout(position, showEvents, "Events");

            if (showEvents && Selection.activeTransform)
            {
                if (eventCategories == null)
                    InitializeEventCategories(fieldInfo.FieldType);

                var voiceEvents = fieldInfo.GetValue(property.serializedObject.targetObject) as VoiceEvents;

                var eventCategoriesKeyArray = eventCategories.Keys.ToArray();

                EditorGUI.indentLevel++;

                selectedCategoryIndex = EditorGUILayout.Popup("Event Category", selectedCategoryIndex,
                    eventCategoriesKeyArray);

                if (selectedCategoryIndex != UNSELECTED)
                {
                    GUILayout.BeginHorizontal();

                    selectedEventIndex = EditorGUILayout.Popup("Event", selectedEventIndex,
                        eventCategories[eventCategoriesKeyArray[selectedCategoryIndex]].ToArray());

                    if (GUILayout.Button("Add"))
                    {
                        var eventName = eventCategories[eventCategoriesKeyArray[selectedCategoryIndex]][
                            selectedEventIndex];

                        if (voiceEvents != null && selectedEventIndex != UNSELECTED &&
                            !voiceEvents.IsCallbackOverridden(eventName))
                        {
                            voiceEvents.RegisterOverriddenCallback(eventName);
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                if (voiceEvents != null && voiceEvents.OverriddenCallbacks.Count != 0)
                {
                    foreach (var callback in voiceEvents.OverriddenCallbacks)
                    {
                        EditorGUILayout.PropertyField(property.FindPropertyRelative(callback));
                    }
                }

                EditorGUI.indentLevel--;
            }
        }
    }
}
