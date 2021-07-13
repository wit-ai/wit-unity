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

namespace com.facebook.witai.configuration
{
    public class WitWindow : BaseWitWindow
    {
        protected override WindowStyles WindowStyle => WitAuthUtility.IsServerTokenValid
            ? WindowStyles.Editor
            : WindowStyles.Themed;

        #if !WIT_DISABLE_UI
        [MenuItem("Window/Wit/Wit Configuration")]
        public static void ShowWindow()
        {
            WitWindow window = GetWindow<WitWindow>("Welcome to Wit.ai");
            window.maxSize = new Vector2(450, 686);
        }
        #endif

        private Texture2D tex;
        private bool manualToken;
        private Vector2 scroll;
        private WitConfigurationEditor witEditor;

        protected override void OnDrawContent()
        {
            if (!WitAuthUtility.IsServerTokenValid)
            {
                DrawWelcome();
            }
            else
            {
                DrawWit();
            }
        }

        protected override void OnEnable()
        {
            WitAuthUtility.InitEditorTokens();

            if (witConfiguration)
            {
                witEditor = (WitConfigurationEditor) Editor.CreateEditor(witConfiguration);
                witEditor.OnEnable();
            }

            RefreshConfigList();
        }

        private void DrawWit()
        {
            // Recommended max size based on EditorWindow.maxSize doc for resizable window.
            maxSize = new Vector2(4000, 4000);
            titleContent = new GUIContent("Wit Configuration");

            GUILayout.BeginVertical(EditorStyles.helpBox);
            var token = EditorGUILayout.PasswordField("Server Access Token", WitAuthUtility.ServerToken);
            if (token != WitAuthUtility.ServerToken)
            {
                WitAuthUtility.ServerToken = token;
                RefreshContent();
            }

            GUILayout.BeginHorizontal();
            var configChanged = DrawWitConfigurationPopup();
            if (GUILayout.Button("Create", GUILayout.Width(75)))
            {
                CreateConfiguration();
            }
            GUILayout.EndHorizontal();

            if (witConfiguration && (configChanged || !witEditor))
            {
                witEditor = (WitConfigurationEditor) Editor.CreateEditor(witConfiguration);
                witEditor.OnEnable();
            }

            if(witConfiguration && witEditor) witEditor.OnInspectorGUI();

            GUILayout.EndVertical();
        }

        private void CreateConfiguration()
        {
            var path = EditorUtility.SaveFilePanel("Create Wit Configuration", Application.dataPath,
                "WitConfiguration", "asset");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                WitConfiguration asset = ScriptableObject.CreateInstance<WitConfiguration>();

                asset.FetchAppConfigFromServerToken(Repaint);

                path = path.Substring(Application.dataPath.Length - 6);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();

                RefreshConfigList();
                witConfigIndex = Array.IndexOf(witConfigs, asset);
                witConfiguration = asset;
            }
        }

        private void DrawWelcome()
        {
            titleContent = WitStyles.welcomeTitleContent;
            minSize = new Vector2(450, 686);
            maxSize = new Vector2(450, 686);

            GUILayout.Label("Build Natural Language Experiences", WitStyles.LabelHeader);
            GUILayout.Label(
                "Enable people to interact with your products using voice and text.",
                WitStyles.LabelHeader2);
            GUILayout.Space(32);


            BeginCenter(296);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Paste your Server Access Token here", WitStyles.Label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(WitStyles.PasteIcon, WitStyles.Label))
            {
                WitAuthUtility.ServerToken = EditorGUIUtility.systemCopyBuffer;
            }
            GUILayout.EndHorizontal();
            var token = EditorGUILayout.PasswordField(WitAuthUtility.ServerToken, WitStyles.TextField);
            if (token != WitAuthUtility.ServerToken)
            {
                WitAuthUtility.ServerToken = token;
            }
            EndCenter();

            BeginCenter();
            GUILayout.Label("or", WitStyles.Label);
            EndCenter();

            BeginCenter();

            if (GUILayout.Button(WitStyles.ContinueButton, WitStyles.Label, GUILayout.Height(50),
                GUILayout.Width(296)))
            {
                Application.OpenURL("https://wit.ai");
            }

            GUILayout.Label(
                "Please connect with Facebook login to continue using Wit.ai by clicking on the “Continue with Github Login” and following the instructions provided.",
                WitStyles.Label,
                GUILayout.Width(296));
            EndCenter();

            BeginCenter();
            GUILayout.Space(16);

            EndCenter();
        }
    }
}
