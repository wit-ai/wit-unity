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
    [CustomEditor(typeof(SimpleStringSlotHandler))]
    public class SimpleStringSlotHandlerEditor : Editor
    {
        private SimpleStringSlotHandler handler;
        private string[] intentNames;
        private int intentIndex;

        private void OnEnable()
        {
            handler = target as SimpleStringSlotHandler;
            handler.wit.Configuration.Update();
            intentNames = handler.wit.Configuration.intents.Select(i => i.name).ToArray();
            intentIndex = Array.IndexOf(intentNames, handler.intent);
        }

        public override void OnInspectorGUI()
        {
            var intentChanged = WitEditorUI.FallbackPopup(serializedObject,"intent", intentNames, ref intentIndex);
            if (intentChanged)
            {
                //handler.wit.Configuration.intents[intentIndex].entities[0].
            }

            var confidenceProperty = serializedObject.FindProperty("confidence");
            EditorGUILayout.PropertyField(confidenceProperty);

            EditorGUILayout.Space(16);
            var formatProperty = serializedObject.FindProperty("format");
            EditorGUILayout.PropertyField(formatProperty);

            GUILayout.Space(16);

            var eventProperty = serializedObject.FindProperty("onIntentSlotTriggered");
            EditorGUILayout.PropertyField(eventProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
