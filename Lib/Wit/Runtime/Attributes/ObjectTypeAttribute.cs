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
using UnityEditor;
#endif

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// An attribute for restricting object gui fields to a specified target type.  This allows for serialized
    /// interfaces and abstract class references that work on MonoBehaviours or SerializedAssets.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ObjectTypeAttribute : PropertyAttribute
    {
        /// <summary>
        /// The type this property will be restricted to
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Constructor that takes in the target type that any objects must be cast to
        /// </summary>
        public ObjectTypeAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom drawer that adjusts all ObjectTypeAttribute object fields to a specific type as described
    /// by the ObjectTypeAttribute.TargetType.  This allows for serialized interfaces and abstract class references
    /// that work on MonoBehaviours or SerializedAssets.
    /// </summary>
    [CustomPropertyDrawer(typeof(ObjectTypeAttribute))]
    public class ObjectTypeAttributePropertyDrawer : PropertyDrawer
    {
        // Layout object field with target type
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Set property type
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                ObjectTypeAttribute objAttribute = (ObjectTypeAttribute)attribute;
                EditorGUI.BeginProperty(position, label, property);
                HandleGameObjectDrag(position, property, objAttribute.TargetType);
                property.objectReferenceValue = EditorGUI.ObjectField(position, label, property.objectReferenceValue, objAttribute.TargetType, true);
                EditorGUI.EndProperty();
            }
            // Default layout
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        // Handles drag of a GameObject
        private void HandleGameObjectDrag(Rect position, SerializedProperty property, Type targetType)
        {
            // Ignore if not over
            Event currentEvent = Event.current;
            if (!position.Contains(currentEvent.mousePosition))
            {
                return;
            }

            // Get component from drag & drop GameObject if possible
            Component targetComponent = null;
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is GameObject go)
            {
                targetComponent = go.GetComponent(targetType);
            }

            // Ignore if no component
            if (targetComponent == null)
            {
                return;
            }

            // Ignore other events
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    currentEvent.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    property.objectReferenceValue = targetComponent;
                    currentEvent.Use();
                    break;
            }
        }
    }
#endif
}
