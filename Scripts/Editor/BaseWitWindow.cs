/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Facebook.WitAi.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi
{
    public abstract class BaseWitWindow : EditorWindow
    {
        // Selected wit configuration
        protected int witConfigIndex = -1;
        protected WitConfiguration witConfiguration;
        // Header link?
        protected virtual string HeaderLink => null;

        protected virtual string GetTitleText()
        {
            return GetType().ToString();
        }
        protected virtual Texture2D GetTitleIcon()
        {
            return WitStyles.WitIcon;
        }
        protected virtual void OnEnable()
        {
            titleContent = new GUIContent(GetTitleText(), GetTitleIcon());
            WitEditorUtility.RefreshConfigList();
        }
        protected virtual void OnDisable()
        {

        }

        protected virtual void OnProjectChange()
        {
            WitEditorUtility.RefreshConfigList();
        }

        protected void RefreshContent()
        {
            if (witConfiguration) witConfiguration.UpdateData();
        }

        protected virtual void OnGUI()
        {
            minSize = new Vector2(450, 300);
            DrawHeader();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            OnDrawContent();
            GUILayout.EndVertical();
        }

        protected abstract void OnDrawContent();

        protected void DrawHeader()
        {
            DrawHeader(HeaderLink);
        }

        public static void DrawHeader(string headerLink = null, Texture2D header = null)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!header) header = WitStyles.MainHeader;
            var headerWidth = Mathf.Min(header.width, EditorGUIUtility.currentViewWidth - 64);
            var headerHeight =
                header.height * headerWidth / header.width;
            if (GUILayout.Button(header, "Label", GUILayout.Width(headerWidth), GUILayout.Height(headerHeight)))
            {
                Application.OpenURL(!string.IsNullOrEmpty(headerLink)
                    ? headerLink
                    : "https://wit.ai");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            GUILayout.EndVertical();
        }

        protected bool DrawWitConfigurationPopup()
        {
            WitConfiguration[] witConfigs = WitEditorUtility.WitConfigs;
            string[] witConfigNames = WitEditorUtility.WitConfigNames;
            if (witConfigs == null)
            {
                return false;
            }

            bool changed = false;
            if (witConfigs.Length == 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Wit Configuration");
                EditorGUILayout.LabelField(witConfigs[0].name, EditorStyles.popup);
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
                if (witConfigIndex < 0 || witConfigIndex >= witConfigs.Length)
                {
                    witConfigIndex = 0;
                }
                witConfiguration = witConfigs[witConfigIndex];
                RefreshContent();
            }

            return changed;
        }

        public static void BeginCenter(int width = -1)
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

        public static void EndCenter()
        {
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
