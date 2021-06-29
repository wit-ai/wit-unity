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
    [CustomEditor(typeof(SimpleStringEntityHandler))]
    #endif
    public class SimpleStringEntityHandlerEditor : Editor
    {
        private SimpleStringEntityHandler handler;
        private string[] intentNames;
        private int intentIndex;
        private string[] entityNames;
        private int entityIndex;

        private void OnEnable()
        {
            handler = target as SimpleStringEntityHandler;
            if (handler && handler.wit)
            {
                handler.wit.Configuration.UpdateData();
                intentNames = handler.wit.Configuration.intents.Select(i => i.name).ToArray();
                intentIndex = Array.IndexOf(intentNames, handler.intent);
            }
        }

        public override void OnInspectorGUI()
        {
            var handler = target as SimpleStringEntityHandler;
            if (!handler) return;
            if (!handler.wit)
            {
                GUILayout.Label("Wit component is not present in the scene. Add wit to scene to get intent and entity suggestions.", EditorStyles.helpBox);
            }

            var intentChanged = WitEditorUI.FallbackPopup(serializedObject,"intent", intentNames, ref intentIndex);
            if (intentChanged || null != intentNames && intentNames.Length > 0 && null == entityNames)
            {
                var entities = handler?.wit?.Configuration?.intents[intentIndex]?.entities;
                if (null != entities)
                {
                    entityNames = entities.Select((e) => e.name).ToArray();
                    entityIndex = Array.IndexOf(entityNames, handler.entity);
                }
            }

            WitEditorUI.FallbackPopup(serializedObject, "entity", entityNames, ref entityIndex);

            var confidenceProperty = serializedObject.FindProperty("confidence");
            EditorGUILayout.PropertyField(confidenceProperty);

            EditorGUILayout.Space(16);
            var formatProperty = serializedObject.FindProperty("format");
            EditorGUILayout.PropertyField(formatProperty);

            GUILayout.Space(16);

            var eventProperty = serializedObject.FindProperty("onIntentEntityTriggered");
            EditorGUILayout.PropertyField(eventProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
