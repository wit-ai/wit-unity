/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace com.facebook.witai.callbackhandlers
{
    [CustomEditor(typeof(SimpleIntentHandler))]
    public class SimpleIntentHandlerEditor : Editor
    {
        private SimpleIntentHandler handler;
        private string[] intentNames;
        private int intentIndex;

        private void OnEnable()
        {
            handler = target as SimpleIntentHandler;
            handler.wit.Configuration.Update();
            intentNames = handler.wit.Configuration.intents.Select(i => i.name).ToArray();
            intentIndex = Array.IndexOf(intentNames, handler.intent);
        }

        public override void OnInspectorGUI()
        {
            string intent ;
            if (intentNames.Length > 0)
            {
                intentIndex = EditorGUILayout.Popup("Intent", intentIndex, intentNames);
                if (intentIndex > 0)
                {
                    intent = intentNames[intentIndex];
                }
                else
                {
                    intent = EditorGUILayout.TextField(handler.intent);
                }
            }
            else
            {
                intent = EditorGUILayout.TextField("Intent", handler.intent);
            }

            if (intent != handler.intent)
            {
                handler.intent = intent;
                EditorUtility.SetDirty(handler);
            }


            var confidenceProperty = serializedObject.FindProperty("confidence");
            EditorGUILayout.PropertyField(confidenceProperty);

            GUILayout.Space(16);

            var eventProperty = serializedObject.FindProperty("onIntentTriggered");
            EditorGUILayout.PropertyField(eventProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
