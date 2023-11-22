/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Attributes;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Meta.WitAi.Interfaces;

namespace Meta.WitAi.Drawers
{
    [CustomEditor(typeof(IWitInspectorTools), true)]
    public class ButtonAttributeDrawer : Editor
    {
        private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        private Dictionary<string, object[]> methodParameters = new Dictionary<string, object[]>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MonoBehaviour monoBehaviour = target as MonoBehaviour;

            foreach (var method in monoBehaviour.GetType()
                         .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                // Check if the method has our Button attribute
                var buttonAttributes = method.GetCustomAttributes(typeof(ButtonAttribute), true);
                if (buttonAttributes.Length > 0)
                {
                    var buttonAttribute = buttonAttributes[0] as ButtonAttribute;
                    if (buttonAttribute.isRuntimeOnly && !Application.isPlaying) continue;
                    
                    string buttonName = buttonAttribute.displayName ?? method.Name;

                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                    
                    if (!foldouts.ContainsKey(buttonName))
                    {
                        foldouts[buttonName] = false;
                    }

                    GUILayout.BeginHorizontal();
                    
                    if (!methodParameters.TryGetValue(buttonName, out var parameters))
                    {
                        parameters = new object[method.GetParameters().Length];
                        methodParameters[buttonName] = parameters;
                    }

                    if (null != parameters && parameters.Length > 0)
                    {
                        foldouts[buttonName] = EditorGUILayout.Foldout(foldouts[buttonName], buttonAttribute.label ?? buttonName);
                    } else if (!string.IsNullOrEmpty(buttonAttribute.label))
                    {
                        GUILayout.Label(buttonAttribute.label);
                    }

                    if (GUILayout.Button(new GUIContent(buttonName, buttonAttribute.tooltip)))
                    {
                        method.Invoke(monoBehaviour, parameters);
                    }
                    GUILayout.EndHorizontal();

                    if (foldouts[buttonName])
                    {
                        DrawButtonFoldout(method, parameters);
                    }
                }
            }
        }

        private void DrawButtonFoldout(MethodInfo method, object[] parameters)
        {
            EditorGUILayout.BeginVertical("box");

            int paramIndex = 0;
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(int))
                {
                    parameters[paramIndex] = EditorGUILayout.IntField(parameter.Name,
                        (int)(parameters[paramIndex] ?? 0));
                }
                else if (parameter.ParameterType == typeof(float))
                {
                    parameters[paramIndex] = EditorGUILayout.FloatField(parameter.Name,
                        (float)(parameters[paramIndex] ?? 0f));
                }
                else if (parameter.ParameterType == typeof(string))
                {
                    parameters[paramIndex] =
                        EditorGUILayout.TextField(parameter.Name, (string)parameters[paramIndex]);
                }
                // ... add more types as needed

                paramIndex++;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
