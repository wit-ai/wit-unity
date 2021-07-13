/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.data;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai
{
    public abstract class BaseWitWindow : EditorWindow
    {
        public enum WindowStyles
        {
            Themed,
            Editor
        }

        protected virtual WindowStyles WindowStyle => WindowStyles.Editor;

        protected WitConfiguration[] witConfigs;
        protected string[] witConfigNames;
        protected int witConfigIndex = -1;
        protected WitConfiguration witConfiguration;


        protected virtual void OnEnable()
        {
            RefreshConfigList();
        }

        protected virtual void OnDisable()
        {

        }

        protected virtual void OnProjectChange()
        {
            RefreshConfigList();
        }

        protected void RefreshContent()
        {
            if (witConfiguration) witConfiguration.UpdateData();
        }

        protected void RefreshConfigList()
        {
            string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");
            witConfigs = new WitConfiguration[guids.Length];
            witConfigNames = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++) //probably could get optimized
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                witConfigs[i] = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
                witConfigNames[i] = witConfigs[i].name;
            }
        }

        protected virtual void OnGUI()
        {
            minSize = new Vector2(450, 300);
            DrawHeader(WindowStyle == WindowStyles.Themed ? WitStyles.BackgroundWhite : WitStyles.BackgroundWitDark);

            if (WindowStyle == WindowStyles.Themed)
            {
                GUILayout.BeginVertical(WitStyles.BackgroundWitBlue, GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
            }
            else
            {
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            }
            OnDrawContent();
            GUILayout.EndVertical();
        }

        protected abstract void OnDrawContent();

        protected void DrawHeader(GUIStyle headerBackground = null)
        {
            GUILayout.BeginVertical(null == headerBackground ? WitStyles.BackgroundWhite : headerBackground);
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(WitStyles.MainHeader, "Label"))
            {
                Application.OpenURL("https://wit.ai");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            GUILayout.EndVertical();
        }

        protected bool DrawWitConfigurationPopup()
        {
            if (null == witConfigs) return false;

            bool changed = false;
            if (witConfigs.Length == 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Wit Configuration");
                EditorGUILayout.LabelField(witConfigNames[0], EditorStyles.popup);
                GUILayout.EndHorizontal();
            }
            else
            {
                var selectedConfig = EditorGUILayout.Popup("Wit Configuration", witConfigIndex, witConfigNames);
                if (selectedConfig != witConfigIndex)
                {
                    witConfigIndex = selectedConfig;
                    changed = true;
                }
            }

            if (changed || witConfigs.Length > 0 && !witConfiguration)
            {
                if (witConfigIndex < 0)
                {
                    witConfigIndex = 0;
                }
                witConfiguration = witConfigs[witConfigIndex];
                RefreshContent();
            }

            return changed;
        }

        protected void BeginCenter(int width = -1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (width > 0)
            {
                GUILayout.BeginVertical(GUILayout.Width(width));
            }
            else
            {
                GUILayout.BeginVertical();
            }
        }

        protected void EndCenter()
        {
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
