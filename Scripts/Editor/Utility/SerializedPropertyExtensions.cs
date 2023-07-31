/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;

namespace Meta.WitAi.EditorUtilities
{
    public static class SerializedPropertyExtensions
    {
        /// <summary>
        /// Gets the parent property of a SerializedProperty
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static SerializedProperty GetParent(this SerializedProperty property)
        {
            var segments = property.propertyPath.Split(new char[] {'.'});
            SerializedProperty matchedProperty = property.serializedObject.FindProperty(segments[0]);
            for (int i = 1; i < segments.Length - 1 && null != matchedProperty; i++)
            {
                matchedProperty = matchedProperty.FindPropertyRelative(segments[i]);
            }

            return matchedProperty;
        }
    }
}
