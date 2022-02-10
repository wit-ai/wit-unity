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
            public string WitAppsUrl;
            public string WitAppSettingsEndpoint;
            public string WitAppUnderstandingEndpoint;
            public string WitOpenButtonLabel;
            public string ConfigurationFileManagerLabel;
            public string ConfigurationFileNameLabel;
            public string ConfigurationSelectLabel;
            public string ConfigurationSelectMissingLabel;
            [Header("Setup Settings Texts")]
            public string SetupTitleLabel;
            public string SetupSubheaderLabel;
            public string SetupServerTokenLabel;
            public string SetupSubmitButtonLabel;
            public string SetupSubmitFailLabel;
            [Header("Understanding Viewer Texts")]
            public string UnderstandingViewerLabel;
            public string UnderstandingViewerMissingConfigLabel;
            public string UnderstandingViewerNoAppLabel;
            public string UnderstandingViewerSettingsButtonLabel;
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

        // Wit
        public const string WitUrl = "https://wit.ai";

        // Window Layout Data
        public const float WindowMinWidth = 0f;
        public const float WindowMaxWidth = 450f;
        public const float WindowMinHeight = 400f;
        public const float WindowMaxSize = 5000f;
        public const float WindowPaddingTop = 8f;
        public const float WindowPaddingBottom = 8f;
        public const float WindowPaddingLeft = 8f;
        public const float WindowPaddingRight = 8f;
        public const float WindowScrollBarSize = 15f;
        // Spacing
        public const float HeaderWidth = 350f;
        public const float HeaderPaddingBottom = 8f;
        public const float WizardFieldPadding = 16f;
        // Text padding
        public const float ButtonMargin = 5f;

        // Icons
        public static Texture2D TitleIcon;
        public static Texture2D HeaderIcon;
        public static GUIContent PasteIcon;
        public static GUIContent EditIcon;
        public static GUIContent ResetIcon;
        public static GUIContent AcceptIcon;
        public static GUIContent ObjectPickerIcon;
        public static GUIContent HelpIcon;
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
        public static GUIStyle LabelWrap;
        public static GUIStyle LabelError;
        public static GUIStyle LabelHeader;
        public static GUIStyle LabelSubheader;
        public static GUIStyle LabelStatus;

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
        // Wit link color
        public static string WitLinkColor = "#ccccff"; // "blue" if not pro
        public const string WitLinkKey = "[COLOR]";

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
        // Texture
        public static Texture2D TextureBlack25P;

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
            HelpIcon = EditorGUIUtility.IconContent("_Help");

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
            LabelWrap = new GUIStyle(Label);
            LabelWrap.wordWrap = true;
            LabelSubheader = new GUIStyle(Label);
            LabelSubheader.fontSize = 14;
            LabelHeader = new GUIStyle(Label);
            LabelHeader.fontSize = 24;
            LabelHeader.padding = new RectOffset(0, 0, 10, 10);
            LabelHeader.margin = new RectOffset(0, 0, 10, 10);
            LabelHeader.wordWrap = true;
            LabelError = new GUIStyle(Label);
            LabelError.wordWrap = true;
            LabelError.normal.textColor = Color.red;
            LabelStatus = new GUIStyle(Label);
            TextureBlack25P = new Texture2D(1, 1);
            TextureBlack25P.SetPixel(0, 0, new Color(0, 0, 0, .25f));
            TextureBlack25P.Apply();
            LabelStatus.normal.background = TextureBlack25P;
            LabelStatus.wordWrap = true;
            LabelStatus.fontSize++;
            LabelStatus.alignment = TextAnchor.LowerLeft;
            LabelStatus.margin = new RectOffset(0, 0, 0, 0);
            // Set to blue if not pro
            if (!EditorGUIUtility.isProSkin)
            {
                WitLinkColor = "blue";
            }

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
        public enum WitAppEndpointType
        {
            Settings,
            Understanding
        }
        public static string GetAppURL(string appId, WitAppEndpointType endpointType)
        {
            // Return apps url without id
            string url = WitUrl + Texts.WitAppsUrl;
            if (string.IsNullOrEmpty(appId))
            {
                return url;
            }
            // Determine endpoint
            string endpoint;
            switch (endpointType)
            {
                case WitAppEndpointType.Understanding:
                    endpoint = Texts.WitAppUnderstandingEndpoint;
                    break;
                case WitAppEndpointType.Settings:
                    endpoint = Texts.WitAppSettingsEndpoint;
                    break;
                default:
                    endpoint = Texts.WitAppSettingsEndpoint;
                    break;
            }
            // Ensure endpoint is set
            if (string.IsNullOrEmpty(endpoint))
            {
                return url;
            }
            // Replace app id key with desired app id
            endpoint = endpoint.Replace("[APP_ID]", appId);
            // Return full url
            return url + endpoint;
        }
    }
}
