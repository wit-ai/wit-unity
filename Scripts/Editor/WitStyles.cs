/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi
{
    public static class WitStyles
    {
        // Localized text
        private static Dictionary<string, string> Localizations;
        public const string LocalizationDrawerSeparator = "|";
        public const string LocalizationDrawerMissingID = "MISSING";

        // Title
        public static Texture2D TitleIcon;

        // Header Data
        public static Texture2D HeaderIcon;
        public const string HeaderLinkURL = "https://wit.ai";
        public const float HeaderPaddingBottom = 10f;

        // Setup Window Data
        public const string SetupTitleText = "Welcome to Wit.ai";
        public static GUIContent SetupTitleContent;

        // Settings Window Data
        public const string SettingsTitleText = "Wit.ai Settings";
        public static GUIContent SettingsTitleContent;

        // Understanding Viewer Window Data
        public const string UnderstandingTitleText = "Wit.ai Understanding Viewer";
        public static GUIContent UnderstandingTitleContent;

        // Configuration Data
        public const float ConfigurationPadding = 5f;
        public const string ConfigurationSelectText = "Wit Configuration";
        public const string ConfigurationMissingText = "No Wit Configurations Found";
        public const string ConfigurationRelinkText = "Relink";
        public const string ConfigurationCreationText = "New";
        public const string ConfigurationHeaderText = "Application Configuration";
        public const string ConfigurationRefreshText = "Refresh";
        public const string ConfigurationRefreshingText = "Refreshing";
        public static GUIContent ConfigurationServerTokenLabel;
        private const string ConfigurationServerTokenText = "Server Access Token";
        private const string ConfigurationServerTokenDescription = "The shared server access token.";
        public static GUIContent ConfigurationClientTokenLabel;
        private const string ConfigurationClientTokenText = "Client Access Token";
        private const string ConfigurationClientTokenDescription = "The specific configuration client token.";
        public static GUIContent ConfigurationRequestTimeoutLabel;
        private const string ConfigurationRequestTimeoutText = "Request Timeout (ms)";
        private const string ConfigurationRequestTimeoutDescription = "The amount of time a server request can take to respond to voice.";
        public const string ConfigurationEndpointKey = "endpointConfiguration";
        // Configuration Response Data
        public const string ConfigurationResponseApplicationKey = "application";
        public const string ConfigurationResponseIntentsKey = "intents";
        public const string ConfigurationResponseEntitiesKey = "entities";
        public const string ConfigurationResponseTraitsKey = "traits";
        public static string[] ConfigurationResponseTabKeys;
        public static string[] ConfigurationResponseTabTexts;
        public const float ConfigurationTabMargins = 20f;
        public const string ConfigurationEntitiesRoleMissingText = "No roles available";
        public const string ConfigurationEntitiesLookupMissingText = "No lookups available";
        public const string ConfigurationTraitsValueMissingText = "No trait values available";

        // Window Layout Data
        public const float WindowMinWidth = 450f;
        public const float WindowMinHeight = 300f;
        public const float WindowPaddingTop = 20f;
        public const float WindowPaddingBottom = 20f;
        public const float WindowPaddingLeft = 20f;
        public const float WindowPaddingRight = 20f;

        // Various icons
        public static GUIContent PasteIcon;
        public static GUIContent EditIcon;
        public static GUIContent ResetIcon;
        public static GUIContent AcceptIcon;
        public static GUIContent ObjectPickerIcon;

        // Label Styles
        public static GUIStyle Label;
        public static GUIStyle LabelError;
        private static Color LabelErrorColor = Color.red;
        public static GUIStyle LabelHeader;
        private const int LabelHeaderFontSize = 24;
        private static RectOffset LabelHeaderMargin = new RectOffset(5, 5, 5, 5);
        public static GUIStyle LabelSubheader;
        private const int LabelSubheaderFontSize = 14;
        private static RectOffset LabelSubheaderMargin = new RectOffset(5, 5, 5, 5);

        // Button styles
        public static GUIStyle TextButton;
        private const float TextButtonHeight = 25f;
        public const float TextButtonPadding = 5f;
        public static GUIStyle IconButton;
        private const float IconButtonSize = 16f; // Width & Height
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

        public const int IconButtonWidth = 20;

        static WitStyles()
        {
            // Setup icons
            TitleIcon = (Texture2D) Resources.Load("witai");
            HeaderIcon = (Texture2D) Resources.Load("wit-ai-title");
            PasteIcon = EditorGUIUtility.IconContent("Clipboard");
            EditIcon = EditorGUIUtility.IconContent("editicon.sml");
            ResetIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            AcceptIcon = EditorGUIUtility.IconContent("FilterSelectedOnly");
            ObjectPickerIcon = EditorGUIUtility.IconContent("d_Record Off");

            // TODO: Add localizations from file
            Localizations = new Dictionary<string, string>();
            string lc = ConfigurationEndpointKey;
            Localizations[lc] = "Endpoint Configuration";
            lc += LocalizationDrawerSeparator;
            Localizations[lc + "uriScheme"] = "Uri Scheme";
            Localizations[lc + "authority"] = "Host";
            Localizations[lc + "port"] = "Port";
            Localizations[lc + "witApiVersion"] = "Wit API Version";
            Localizations[lc + "speech"] = "Speech";
            lc = ConfigurationResponseApplicationKey;
            Localizations[lc] = "Application";
            lc += LocalizationDrawerSeparator;
            Localizations[lc + LocalizationDrawerMissingID] = "Loading...";
            Localizations[lc + "name"] = "Name";
            Localizations[lc + "id"] = "ID";
            Localizations[lc + "lang"] = "Language";
            Localizations[lc + "createdAt"] = "Created";
            Localizations[lc + "isPrivate"] = "Private";
            lc = ConfigurationResponseIntentsKey;
            Localizations[lc] = "Intents";
            lc += LocalizationDrawerSeparator;
            Localizations[lc + LocalizationDrawerMissingID] = "No intents found";
            Localizations[lc + "id"] = "Intent ID";
            Localizations[lc + "entities"] = "Intent Entities";
            lc = ConfigurationResponseEntitiesKey;
            Localizations[lc] = "Entities";
            lc += LocalizationDrawerSeparator;
            Localizations[lc + LocalizationDrawerMissingID] = "No entities found";
            Localizations[lc + "id"] = "Entity ID";
            Localizations[lc + "lookups"] = "Entity Lookups";
            Localizations[lc + "roles"] = "Entity Roles";
            lc = ConfigurationResponseTraitsKey;
            Localizations[lc] = "Traits";
            lc += LocalizationDrawerSeparator;
            Localizations[lc + LocalizationDrawerMissingID] = "No traits found";
            Localizations[lc + "id"] = "Trait ID";
            Localizations[lc + "values"] = "Trait Values";

            // Setup titles
            SetupTitleContent = new GUIContent(SetupTitleText, TitleIcon);
            SettingsTitleContent = new GUIContent(SettingsTitleText, TitleIcon);
            UnderstandingTitleContent = new GUIContent(UnderstandingTitleText, TitleIcon);
            ConfigurationServerTokenLabel = new GUIContent(ConfigurationServerTokenText, ConfigurationServerTokenDescription);
            ConfigurationClientTokenLabel = new GUIContent(ConfigurationClientTokenText, ConfigurationClientTokenDescription);
            ConfigurationRequestTimeoutLabel = new GUIContent(ConfigurationRequestTimeoutText, ConfigurationRequestTimeoutDescription);
            ConfigurationResponseTabKeys = new string[] { ConfigurationResponseApplicationKey, ConfigurationResponseIntentsKey, ConfigurationResponseEntitiesKey, ConfigurationResponseTraitsKey };
            ConfigurationResponseTabTexts = new string[ConfigurationResponseTabKeys.Length];
            for (int i = 0; i < ConfigurationResponseTabKeys.Length; i++)
            {
                ConfigurationResponseTabTexts[i] = GetLocalizedText(ConfigurationResponseTabKeys[i]);
            }

            // Label Styles
            Label = new GUIStyle(EditorStyles.label);
            Label.richText = true;
            Label.wordWrap = false;
            LabelHeader = new GUIStyle(Label);
            LabelHeader.fontSize = LabelHeaderFontSize;
            LabelHeader.margin = LabelHeaderMargin;
            LabelSubheader = new GUIStyle(Label);
            LabelSubheader.fontSize = LabelSubheaderFontSize;
            LabelSubheader.margin = LabelSubheaderMargin;
            LabelError = new GUIStyle(Label);
            LabelError.normal.textColor = LabelErrorColor;

            // Button Styles
            TextButton = new GUIStyle(EditorStyles.miniButton);
            TextButton.fixedHeight = TextButtonHeight;
            TabButton = new GUIStyle(TextButton);
            TabButton.fixedHeight = TabButtonHeight;
            IconButton = new GUIStyle(Label);
            IconButton.margin = new RectOffset();
            IconButton.fixedWidth = IconButtonSize;
            IconButton.fixedHeight = IconButtonSize;
            HeaderButton = new GUIStyle(Label);
            HeaderButton.normal.textColor = HeaderTextColor;

            // Text Field Styles
            TextField = new GUIStyle(EditorStyles.textField);
            //TextField.normal.background = TextureTextField;
            //TextField.normal.textColor = Color.black;
            PasswordField = new GUIStyle(TextField);
            IntField = new GUIStyle(TextField);

            // Miscelaneous Styles
            Foldout = new GUIStyle(EditorStyles.foldout);
            Toggle = new GUIStyle(EditorStyles.toggle);
            Popup = new GUIStyle(EditorStyles.popup);

            /*
            LabelWordWrapped = new GUIStyle(labelStyle);
            LabelWordWrapped.wordWrap = true;
            Popup = new GUIStyle(EditorStyles.popup);
            Link = new GUIStyle(Label);
            Link.normal.textColor = LinkColor;
            Button = new GUIStyle(EditorStyles.miniButton);
            Button.fixedHeight = 0;
            ImageButton = new GUIStyle(labelStyle);
            ImageButton.fixedWidth = 16;
            ImageButton.fixedHeight = 16;

            ContinueButton = (Texture2D) Resources.Load("continue-with-fb");

            TextureWhite = new Texture2D(1, 1);
            TextureWhite.SetPixel(0, 0, Color.white);
            TextureWhite.Apply();

            TextureWhite25P = new Texture2D(1, 1);
            TextureWhite25P.SetPixel(0, 0, new Color(1, 1, 1, .25f));
            TextureWhite25P.Apply();

            TextureBlack25P = new Texture2D(1, 1);
            TextureBlack25P.SetPixel(0, 0, new Color(0, 0, 0, .25f));
            TextureBlack25P.Apply();

            TextureFBBlue = new Texture2D(1, 1);
            TextureFBBlue.SetPixel(0, 0, LinkColor);
            TextureFBBlue.Apply();

            TextureTextField = new Texture2D(1, 1);
            TextureTextField.SetPixel(0, 0, new Color(.85f, .85f, .95f));
            TextureTextField.Apply();

            TextureWitDark = new Texture2D(1, 1);
            TextureWitDark.SetPixel(0,0, new Color(0.267f, 0.286f, 0.31f));
            TextureWitDark.Apply();

            BackgroundWhite = new GUIStyle();
            BackgroundWhite.normal.background = TextureWhite;

            BackgroundWhite25P = new GUIStyle();
            BackgroundWhite25P.normal.background = TextureWhite25P;

            BackgroundBlack25P = new GUIStyle();
            BackgroundBlack25P.normal.background = TextureBlack25P;
            BackgroundBlack25P.normal.textColor = Color.white;

            BackgroundWitDark = new GUIStyle();
            BackgroundWitDark.normal.background = TextureWitDark;

            FacebookButton = new GUIStyle(EditorStyles.miniButton);



            TextField = new GUIStyle(EditorStyles.textField);
            //TextField.normal.background = TextureTextField;
            //TextField.normal.textColor = Color.black;
            */
        }

        // Return configuration id
        public static string GetConfigurationURL(string configurationID)
        {
            if (string.IsNullOrEmpty(configurationID))
            {
                return HeaderLinkURL + "/apps";
            }
            return $"https://wit.ai/apps/{configurationID}/settings";
        }
        // Get localized text with a category
        public static string GetLocalizedText(string category, string text)
        {
            string r = category;
            if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(text))
            {
                r += LocalizationDrawerSeparator;
            }
            r += text;
            return GetLocalizedText(r);
        }
        // Read from localization file
        public static string GetLocalizedText(string key)
        {
            // Return nothing for empty key
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            // Return localized text
            if (Localizations.ContainsKey(key))
            {
                return Localizations[key];
            }
            return key;
        }
    }
}
