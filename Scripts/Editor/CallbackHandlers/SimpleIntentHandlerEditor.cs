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
            handler.wit.Configuration.UpdateData();
            intentNames = handler.wit.Configuration.intents.Select(i => i.name).ToArray();
            intentIndex = Array.IndexOf(intentNames, handler.intent);
        }

        public override void OnInspectorGUI()
        {
            bool intentChanged = WitEditorUI.FallbackPopup(serializedObject, "intent",
                intentNames, ref intentIndex);


            var confidenceProperty = serializedObject.FindProperty("confidence");
            EditorGUILayout.PropertyField(confidenceProperty);

            GUILayout.Space(16);

            var eventProperty = serializedObject.FindProperty("onIntentTriggered");
            EditorGUILayout.PropertyField(eventProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
