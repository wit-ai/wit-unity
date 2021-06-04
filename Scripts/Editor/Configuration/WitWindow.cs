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
    public class WitWindow : EditorWindow
    {
        class Content
        {
            private static Texture2D WitIcon;
            public static Texture2D MainHeader;
            public static Texture2D ContinueButton;

            public static Texture2D TextureWhite;
            public static Texture2D TextureWhite25P;
            public static Texture2D TextureWitBlueBg;
            public static Texture2D TextureFBBlue;
            public static Texture2D TextureTextField;

            public static GUIStyle BackgroundWhite;
            public static GUIStyle BackgroundWhite25P;
            public static GUIStyle BackgroundWitBlue;
            public static GUIStyle BackgroundFBBlue;

            public static GUIStyle LabelHeader;
            public static GUIStyle LabelHeader2;
            public static GUIStyle Label;
            public static GUIStyle FacebookButton;

            public static GUIStyle TextField;

            public static Color ColorFB = new Color(0.09f, 0.47f, 0.95f);
            public static GUIStyle Link;

            public static GUIContent titleContent;
            public static GUIContent welcomeTitleContent;
            public static GUIContent PasteIcon;

            static Content()
            {
                WitIcon = (Texture2D) Resources.Load("witai");
                MainHeader = (Texture2D) Resources.Load("wit-ai-title");
                ContinueButton = (Texture2D) Resources.Load("continue-with-fb");

                TextureWhite = new Texture2D(1, 1);
                TextureWhite.SetPixel(0, 0, Color.white);
                TextureWhite.Apply();

                TextureWhite25P = new Texture2D(1, 1);
                TextureWhite25P.SetPixel(0, 0, new Color(1, 1, 1, .25f));
                TextureWhite25P.Apply();

                TextureWitBlueBg = new Texture2D(1, 1);
                TextureWitBlueBg.SetPixel(0, 0, new Color(0.95f, 0.96f, 0.98f));
                TextureWitBlueBg.Apply();

                TextureFBBlue = new Texture2D(1, 1);
                TextureFBBlue.SetPixel(0, 0, ColorFB);
                TextureFBBlue.Apply();

                TextureTextField = new Texture2D(1, 1);
                TextureTextField.SetPixel(0, 0, new Color(.85f, .85f, .95f));
                TextureTextField.Apply();

                BackgroundWhite = new GUIStyle();
                BackgroundWhite.normal.background = TextureWhite;

                BackgroundWhite25P = new GUIStyle();
                BackgroundWhite25P.normal.background = TextureWhite25P;

                BackgroundWitBlue = new GUIStyle();
                BackgroundWitBlue.normal.background = TextureWitBlueBg;

                FacebookButton = new GUIStyle(EditorStyles.miniButton);
                FacebookButton.normal.background = TextureWitBlueBg;

                Label = new GUIStyle(EditorStyles.label);
                Label.normal.background = TextureWitBlueBg;
                Label.normal.textColor = Color.black;
                Label.wordWrap = true;

                LabelHeader = new GUIStyle(Label);
                LabelHeader.fontSize = 64;

                LabelHeader2 = new GUIStyle(Label);
                LabelHeader2.fontSize = 32;

                Link = new GUIStyle(Label);
                Link.normal.textColor = ColorFB;

                TextField = new GUIStyle(EditorStyles.textField);
                TextField.normal.background = TextureTextField;
                TextField.normal.textColor = Color.black;

                titleContent = new GUIContent("Wit.ai", WitIcon);
                welcomeTitleContent = new GUIContent("Welcome to Wit.ai", WitIcon);

                PasteIcon = EditorGUIUtility.IconContent("Clipboard");
            }
        }

        [SerializeField] private WitConfiguration witConfiguration;

        [MenuItem("Window/Wit/Wit Configuration")]
        public static void ShowWindow()
        {
            WitWindow window = GetWindow<WitWindow>("Welcome to Wit.ai");
            window.minSize = new Vector2(450, 686);
            window.maxSize = new Vector2(450, 686);
        }

        private Texture2D tex;
        private bool manualToken;
        private Vector2 scroll;
        private WitConfigurationEditor witEditor;
        private WitConfiguration[] witConfigs;
        private string[] witConfigNames;
        private int witConfigIndex = -1;

        private void OnGUI()
        {

            if (!WitAuthUtility.IsIDETokenValid)
            {
                DrawWelcome();
            }
            else
            {
                DrawWit();
            }

        }

        private void RefreshContent()
        {
            if(witConfiguration) witConfiguration.UpdateData();
        }

        private void OnEnable()
        {
            if (witEditor)
            {
                witEditor.OnEnable();
            }

            RefreshConfigList();
        }

        private void OnProjectChange()
        {
            RefreshConfigList();
        }

        private void DrawWit()
        {
            // Recommended max size based on EditorWindow.maxSize doc for resizable window.
            maxSize = new Vector2(4000, 4000);

            DrawHeader(Content.BackgroundWhite25P);
            scroll = GUILayout.BeginScrollView(scroll,
                GUILayout.ExpandHeight(true));
            titleContent = Content.titleContent;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            var token = EditorGUILayout.PasswordField("IDE Token", WitAuthUtility.IDEToken);
            if (token != WitAuthUtility.IDEToken)
            {
                WitAuthUtility.IDEToken = token;
                RefreshContent();
            }

            GUILayout.BeginHorizontal();
            var index = EditorGUILayout.Popup("Wit Configuration", witConfigIndex, witConfigNames);
            if (GUILayout.Button("Create", GUILayout.Width(75)))
            {
                CreateConfiguration();
            }
            GUILayout.EndHorizontal();

            if (index == -1 && witConfigs.Length > 0) index = 0;

            if (index != witConfigIndex)
            {
                witConfigIndex = index;
                witConfiguration = witConfigs[index];
                witEditor = (WitConfigurationEditor) Editor.CreateEditor(witConfiguration);
                witEditor.OnEnable();
            }

            if(witConfiguration && witEditor) witEditor.OnInspectorGUI();

            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }

        private void CreateConfiguration()
        {
            var path = EditorUtility.SaveFilePanel("Create Wit Configuration", Application.dataPath,
                "WitConfiguration", "asset");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                WitConfiguration asset = ScriptableObject.CreateInstance<WitConfiguration>();

                asset.application = new WitApplication()
                {
                    id = WitAuthUtility.AppId,
                    witConfiguration = asset
                };
                asset.application.UpdateData();
                asset.clientAccessToken = WitAuthUtility.ClientToken;

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
            DrawHeader(Content.BackgroundWhite);
            scroll = GUILayout.BeginScrollView(scroll, Content.BackgroundWitBlue,
                GUILayout.ExpandHeight(true));
            titleContent = Content.welcomeTitleContent;
            minSize = new Vector2(450, 686);
            maxSize = new Vector2(450, 686);



            GUILayout.Label("Build Natural Language Experiences", Content.LabelHeader);
            GUILayout.Label(
                "Enable people to interact with your products using voice and text.",
                Content.LabelHeader2);
            GUILayout.Space(32);


            BeginCenter(296);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Paste your IDE Token here", Content.Label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Content.PasteIcon, Content.Label))
            {
                WitAuthUtility.IDEToken = EditorGUIUtility.systemCopyBuffer;
            }
            GUILayout.EndHorizontal();
            var token = EditorGUILayout.PasswordField(WitAuthUtility.IDEToken, Content.TextField);
            if (token != WitAuthUtility.IDEToken)
            {
                WitAuthUtility.IDEToken = token;
            }
            EndCenter();

            BeginCenter();
            GUILayout.Label("or", Content.Label);
            EndCenter();

            BeginCenter();

            if (GUILayout.Button(Content.ContinueButton, Content.Label, GUILayout.Height(50),
                GUILayout.Width(296)))
            {
                Application.OpenURL("https://wit.ai");
            }

            GUILayout.Label(
                "Please connect with Facebook login to continue using Wit.ai by clicking on the “Continue with Github Login” and following the instructions provided.",
                Content.Label,
                GUILayout.Width(296));
            EndCenter();

            BeginCenter();
            GUILayout.Space(16);

            EndCenter();
            GUILayout.EndScrollView();
        }

        private void BeginCenter(int width = -1)
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

        private void EndCenter()
        {
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawHeader(GUIStyle background)
        {
            GUILayout.BeginVertical(background);
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Content.MainHeader, "Label"))
            {
                Application.OpenURL("https://wit.ai");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            GUILayout.EndVertical();
        }

        private void RefreshConfigList()
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
    }
}
