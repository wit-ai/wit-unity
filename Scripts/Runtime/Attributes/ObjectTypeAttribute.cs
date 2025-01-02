/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.Text;
using UnityEditor;
#endif

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// An attribute for restricting object gui fields to one or more specified target types.  This allows for serialized
    /// interfaces and abstract class references that work on MonoBehaviours or SerializedAssets.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ObjectTypeAttribute : PropertyAttribute
    {
        /// <summary>
        /// The types this property will be restricted to
        /// </summary>
        public Type[] TargetTypes { get; }

        /// <summary>
        /// If true, all types must be included.
        /// If false, any of the specified types can be included
        /// </summary>
        public bool RequiresAllTypes { get; }

        /// <summary>
        /// Constructor that takes in the target type that any objects must be cast to
        /// </summary>
        public ObjectTypeAttribute(Type targetType, params Type[] additionalTargetTypes)
        {
            RequiresAllTypes = false;
            TargetTypes = VerifyTypes(targetType, additionalTargetTypes);
        }

        /// <summary>
        /// Constructor that takes in the target type that any objects must be cast to
        /// </summary>
        public ObjectTypeAttribute(bool requireAll, Type targetType, params Type[] additionalTargetTypes)
        {
            RequiresAllTypes = requireAll;
            TargetTypes = VerifyTypes(targetType, additionalTargetTypes);
        }

        /// <summary>
        /// Verifies each type and adds it to a list
        /// </summary>
        private Type[] VerifyTypes(Type targetType, Type[] additionalTargetTypes)
        {
            List<Type> results = new List<Type>();
            if (VerifyType(targetType))
            {
                results.Add(targetType);
            }
            if (additionalTargetTypes != null)
            {
                foreach (var additionalType in additionalTargetTypes)
                {
                    if (VerifyType(additionalType))
                    {
                        results.Add(additionalType);
                    }
                }
            }
            return results.ToArray();
        }

        /// <summary>
        /// Verifies a type & asserts error if encountered
        /// </summary>
        private bool VerifyType(Type targetType)
        {
            if (targetType == null)
            {
                Debug.LogError(GetType().Name + " cannot use null target type");
                return false;
            }
            return true;
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
                var target = HandleGameObjectDrag(position, property, objAttribute.TargetTypes, objAttribute.RequiresAllTypes);
                GetObjectInfo(target, objAttribute.TargetTypes, objAttribute.RequiresAllTypes, out Type objectRefType, out string objectTypeInfo);
                if (!string.IsNullOrEmpty(objectTypeInfo))
                {
                    label.text += $" [{objectTypeInfo}]";
                }
                property.objectReferenceValue = EditorGUI.ObjectField(position, label, property.objectReferenceValue, objectRefType, true);
                EditorGUI.EndProperty();
            }
            // Default layout
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        // Handles drag of a GameObject
        private UnityEngine.Object HandleGameObjectDrag(Rect position, SerializedProperty property, Type[] targetTypes, bool requiresAll)
        {
            // Ignore if not over
            Event currentEvent = Event.current;
            if (!position.Contains(currentEvent.mousePosition))
            {
                return property.objectReferenceValue;
            }

            // Get component from drag & drop GameObject if possible
            Component targetComponent = null;
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is GameObject go)
            {
                targetComponent = GetValidComponent(go, targetTypes, requiresAll);
            }

            // Ignore if no component
            if (targetComponent == null)
            {
                return property.objectReferenceValue;
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

            // Return target
            return targetComponent;
        }

        // Get valid component
        private Component GetValidComponent(GameObject gameObject, Type[] targetTypes, bool requiresAll)
        {
            // Ignore without types
            if (targetTypes == null)
            {
                return null;
            }
            // Return first found
            if (!requiresAll)
            {
                foreach (var targetType in targetTypes)
                {
                    var component = gameObject.GetComponent(targetType);
                    if (component != null)
                    {
                        return component;
                    }
                }
            }
            // Return if all found
            else
            {
                var components = gameObject.GetComponents(targetTypes[0]);
                foreach (var component in components)
                {
                    // Invalidate component if not instance of every type
                    bool all = true;
                    for (int i = 1; i < targetTypes.Length; i++)
                    {
                        Type targetType = targetTypes[i];
                        if (!targetType.IsInstanceOfType(component))
                        {
                            all = false;
                            break;
                        }
                    }
                    // Instance of all provided target types
                    if (all)
                    {
                        return component;
                    }
                }
            }
            // None found
            return null;
        }

        // Get object reference type & label addition if applicable
        private void GetObjectInfo(UnityEngine.Object objectValue, Type[] targetTypes, bool requiresAll, out Type objectRefType, out string objectTypeInfo)
        {
            // Ignore without multiples
            if (targetTypes == null || targetTypes.Length <= 1)
            {
                objectRefType = targetTypes == null || targetTypes.Length == 0 ? typeof(UnityEngine.Object) : targetTypes[0];
                objectTypeInfo = null;
                return;
            }

            // Use default type
            Type typeFound = null;
            StringBuilder typeInfo = new StringBuilder();
            for (int i = 0; i < targetTypes.Length; i++)
            {
                // Target type
                Type targetType = targetTypes[i];
                // If nothing set, apply target name
                bool appendName = false;
                if (objectValue == null)
                {
                    appendName = true;
                }
                // If instance of type, return target & append name
                else if (targetType.IsInstanceOfType(objectValue))
                {
                    if (typeFound == null)
                    {
                        typeFound = targetType;
                    }
                    appendName = true;
                }
                if (appendName)
                {
                    if (typeInfo.Length > 0)
                    {
                        if (objectValue == null && i == targetTypes.Length - 1)
                        {
                            typeInfo.Append(requiresAll ? " and " : " or ");
                        }
                        else
                        {
                            typeInfo.Append(", ");
                        }
                    }
                    typeInfo.Append(targetType.Name);
                }
            }

            // Apply results
            objectRefType = typeFound ?? targetTypes[0];
            objectTypeInfo = typeInfo.ToString();
        }
    }
#endif
}
