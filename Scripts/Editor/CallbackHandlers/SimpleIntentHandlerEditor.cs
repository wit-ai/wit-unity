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
    #if !WIT_DISABLE_UI
    [CustomEditor(typeof(SimpleIntentHandler))]
    #endif
    public class SimpleIntentHandlerEditor : Editor
    {
        private SimpleIntentHandler handler;
        private string[] intentNames;
        private int intentIndex;

        private void OnEnable()
        {
            handler = target as SimpleIntentHandler;
        }

        public override void OnInspectorGUI()
        {
            if (!handler.wit)
            {
                GUILayout.Label(
                    "Wit component is not present in the scene. Add wit to scene to get intent and entity suggestions.",
                    EditorStyles.helpBox);
            }

            if (handler && handler.wit && null == intentNames)
            {
                handler.wit.Configuration.UpdateData();
                intentNames = handler.wit.Configuration.intents.Select(i => i.name).ToArray();
                intentIndex = Array.IndexOf(intentNames, handler.intent);
            }

            WitEditorUI.FallbackPopup(serializedObject, "intent",
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
