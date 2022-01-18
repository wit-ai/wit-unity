/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi
{
    public static class WitStyles
    {
        // Localized text
        public static WitStylesTexts Texts;
        [System.Serializable]
        public struct WitStylesTexts
        {
            [Header("Shared Settings Texts")]
            public string LanguageID;
            public string WitUrl;
            public string WitAppsUrl;
            public string WitAppSettingsUrl;
            public string WitOpenButtonLabel;
            public string ConfigurationSelectLabel;
            public string ConfigurationSelectMissingLabel;
            [Header("Setup Settings Texts")]
            public string SetupTitleLabel;
            public string SetupSubheaderLabel;
            public string SetupSubmitButtonLabel;
            public string SetupSubmitFailLabel;
            [Header("Understanding Viewer Texts")]
            public string UnderstandingViewerLabel;
            public string UnderstandingViewerMissingConfigLabel;
            public string UnderstandingViewerNoAppLabel;
            public string UnderstandingViewerUtteranceLabel;
            public string UnderstandingViewerPromptLabel;
            public string UnderstandingViewerSubmitButtonLabel;
            public string UnderstandingViewerActivateButtonLabel;
            public string UnderstandingViewerDeactivateButtonLabel;
            public string UnderstandingViewerAbortButtonLabel;
            public string UnderstandingViewerListeningLabel;
            public string UnderstandingViewerLoadingLabel;
            [Header("Settings Texts")]
            public string SettingsTitleLabel;
            public string SettingsServerTokenLabel;
            public string SettingsServerTokenTooltip;
            public string SettingsRelinkButtonLabel;
            public string SettingsAddButtonLabel;
            [Header("Configuration Texts")]
            public string ConfigurationHeaderLabel;
            public string ConfigurationRefreshButtonLabel;
            public string ConfigurationRefreshingButtonLabel;
            public string ConfigurationServerTokenLabel;
            public string ConfigurationClientTokenLabel;
            public string ConfigurationRequestTimeoutLabel;

            [Header("Configuration Endpoint Texts")]
            public string ConfigurationEndpointTitleLabel;
            public string ConfigurationEndpointUriLabel;
            public string ConfigurationEndpointAuthLabel;
            public string ConfigurationEndpointPortLabel;
            public string ConfigurationEndpointApiLabel;
            public string ConfigurationEndpointSpeechLabel;
            [Header("Configuration Application Texts")]
            public string ConfigurationApplicationTabLabel;
            public string ConfigurationApplicationMissingLabel;
            public string ConfigurationApplicationNameLabel;
            public string ConfigurationApplicationIdLabel;
            public string ConfigurationApplicationLanguageLabel;
            public string ConfigurationApplicationPrivateLabel;
            public string ConfigurationApplicationCreatedLabel;

            [Header("Configuration Intent Texts")]
            public string ConfigurationIntentsTabLabel;
            public string ConfigurationIntentsMissingLabel;
            public string ConfigurationIntentsIdLabel;
            public string ConfigurationIntentsEntitiesLabel;
            [Header("Configuration Entity Texts")]
            public string ConfigurationEntitiesTabLabel;
            public string ConfigurationEntitiesMissingLabel;
            public string ConfigurationEntitiesIdLabel;
            public string ConfigurationEntitiesLookupsLabel;
            public string ConfigurationEntitiesRolesLabel;
            [Header("Configuration Trait Texts")]
            public string ConfigurationTraitsTabLabel;
            public string ConfigurationTraitsMissingLabel;
            public string ConfigurationTraitsIdLabel;
            public string ConfigurationTraitsValuesLabel;
        }
        // Window Layout Data
        public const float WindowMinWidth = 450f;
        public const float WindowMinHeight = 300f;
        public const float WindowPaddingTop = 20f;
        public const float WindowPaddingBottom = 20f;
        public const float WindowPaddingLeft = 20f;
        public const float WindowPaddingRight = 20f;
        // Spacing
        public const float HeaderPaddingBottom = 10f;

        // Icons
        public static Texture2D TitleIcon;
        public static Texture2D HeaderIcon;
        public static GUIContent PasteIcon;
        public static GUIContent EditIcon;
        public static GUIContent ResetIcon;
        public static GUIContent AcceptIcon;
        public static GUIContent ObjectPickerIcon;
        // Title Contents
        public static GUIContent SetupTitleContent;
        public static GUIContent UnderstandingTitleContent;
        public static GUIContent SettingsTitleContent;
        public static GUIContent SettingsServerTokenContent;
        public static GUIContent ConfigurationServerTokenContent;
        public static GUIContent ConfigurationClientTokenContent;
        public static GUIContent ConfigurationRequestTimeoutContent;
        // Label Styles
        public static GUIStyle Label;
        public static GUIStyle LabelError;
        public static GUIStyle LabelHeader;
        public static GUIStyle LabelSubheader;

        // Button styles
        public static GUIStyle TextButton;
        private const float TextButtonHeight = 25f;
        public const float TextButtonPadding = 5f;
        public static GUIStyle IconButton;
        public const float IconButtonSize = 16f; // Width & Height
        public static GUIStyle TabButton;
        private const float TabButtonHeight = 40f;
        public static GUIStyle HeaderButton;
        public static Color HeaderTextColor = new Color(0.09f, 0.47f, 0.95f); // FB

        // Text Field Styles
        public static GUIStyle TextField;
        public static GUIStyle IntField;
        public static GUIStyle PasswordField;
        // Foldout Style
        public static GUIStyle Foldout;
        // Toggle Style
        public static GUIStyle Toggle;
        // Popup/Dropdown Styles
        public static GUIStyle Popup;

        // Init
        private static bool Initialized = false;
        public static void Init()
        {
            if (Initialized)
            {
                return;
            }
            try
            {
                if (EditorStyles.label == null)
                {
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("WitStyles - Wait for EditorStyles\nError: " + e);
                return;
            }
            // Get text
            string languageID = "en-us";
            string textFilePath = $"witai_texts_{languageID}";
            TextAsset textAsset = Resources.Load<TextAsset>(textFilePath);
            if (textAsset == null)
            {
                Debug.LogError($"WitStyles - Add localization to Resources/{textFilePath}\nLanguage: {languageID}");
                return;
            }
            Texts = JsonUtility.FromJson<WitStylesTexts>(textAsset.text);
            // Setup icons
            TitleIcon = (Texture2D) Resources.Load("witai");
            HeaderIcon = (Texture2D) Resources.Load("wit-ai-title");
            PasteIcon = EditorGUIUtility.IconContent("Clipboard");
            EditIcon = EditorGUIUtility.IconContent("editicon.sml");
            ResetIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            AcceptIcon = EditorGUIUtility.IconContent("FilterSelectedOnly");
            ObjectPickerIcon = EditorGUIUtility.IconContent("d_Record Off");

            // Setup titles
            SetupTitleContent = new GUIContent(Texts.SetupTitleLabel, TitleIcon);
            SettingsTitleContent = new GUIContent(Texts.SettingsTitleLabel, TitleIcon);
            SettingsServerTokenContent = new GUIContent(Texts.SettingsServerTokenLabel, Texts.SettingsServerTokenTooltip);
            UnderstandingTitleContent = new GUIContent(Texts.UnderstandingViewerLabel, TitleIcon);
            ConfigurationServerTokenContent = new GUIContent(Texts.ConfigurationServerTokenLabel);
            ConfigurationClientTokenContent = new GUIContent(Texts.ConfigurationClientTokenLabel);
            ConfigurationRequestTimeoutContent = new GUIContent(Texts.ConfigurationRequestTimeoutLabel);

            // Label Styles
            Label = new GUIStyle();
            Label.fontSize = 11;
            Label.padding = new RectOffset(5, 5, 0, 0);
            Label.margin = new RectOffset(5, 5, 0, 0);
            Label.alignment = TextAnchor.MiddleLeft;
            Label.normal.textColor = Color.white;
            Label.hover.textColor = Color.white;
            Label.active.textColor = Color.white;
            Label.richText = true;
            Label.wordWrap = false;
            LabelSubheader = new GUIStyle(Label);
            LabelSubheader.fontSize = 14;
            LabelHeader = new GUIStyle(Label);
            LabelHeader.fontSize = 24;
            LabelHeader.padding = new RectOffset(0, 0, 10, 10);
            LabelHeader.margin = new RectOffset(0, 0, 10, 10);
            LabelError = new GUIStyle(Label);
            LabelError.normal.textColor = Color.red;

            // Button Styles
            TextButton = new GUIStyle(EditorStyles.miniButton);
            TextButton.alignment = TextAnchor.MiddleCenter;
            TextButton.fixedHeight = TextButtonHeight;
            TabButton = new GUIStyle(TextButton);
            TabButton.fixedHeight = TabButtonHeight;
            IconButton = new GUIStyle(Label);
            IconButton.margin = new RectOffset(0, 0, 0, 0);
            IconButton.padding = new RectOffset(0, 0, 0, 0);
            IconButton.fixedWidth = IconButtonSize;
            IconButton.fixedHeight = IconButtonSize;
            HeaderButton = new GUIStyle(Label);
            HeaderButton.normal.textColor = HeaderTextColor;

            // Text Field Styles
            TextField = new GUIStyle(EditorStyles.textField);
            TextField.padding = Label.padding;
            TextField.margin = Label.margin;
            TextField.alignment = Label.alignment;
            PasswordField = new GUIStyle(TextField);
            IntField = new GUIStyle(TextField);
            // Miscellaneous
            Foldout = new GUIStyle(EditorStyles.foldout);
            Toggle = new GUIStyle(EditorStyles.toggle);
            Popup = new GUIStyle(EditorStyles.popup);
            // Initialized
            Initialized = true;
        }
        // Return settings id for application id
        public static string GetSettingsURL(string appId)
        {
            if (string.IsNullOrEmpty(Texts.WitUrl))
            {
                return "";
            }
            string url = Texts.WitUrl + Texts.WitAppsUrl;
            if (string.IsNullOrEmpty(appId))
            {
                return url;
            }
            return url + Texts.WitAppSettingsUrl.Replace("[APP_ID]", appId);
        }
    }
}
