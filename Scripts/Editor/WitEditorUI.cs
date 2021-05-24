using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai
{
    public class WitEditorUI
    {
        public static bool FallbackPopup(SerializedObject serializedObject, string propertyName,
            string[] names, ref int index)
        {
            var property = serializedObject.FindProperty(propertyName);
            string intent;
            if (names.Length > 0)
            {
                index = EditorGUILayout.Popup(property.displayName, index, names);
                if (index > 0)
                {
                    intent = names[index];
                }
                else
                {
                    intent = EditorGUILayout.TextField(property.stringValue);
                }
            }
            else
            {
                intent = EditorGUILayout.TextField(property.displayName, property.stringValue);
            }

            if (intent != property.stringValue)
            {
                property.stringValue = intent;
                return true;
            }

            return false;
        }
    }
}
