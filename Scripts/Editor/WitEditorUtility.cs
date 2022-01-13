/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Windows;

namespace Facebook.WitAi
{
    public static class WitEditorUtility
    {
        #region CONFIGURATION
        // Wit configuration assets
        private static WitConfiguration[] witConfigs = Array.Empty<WitConfiguration>();
        public static WitConfiguration[] WitConfigs => witConfigs;

        // Wit configuration asset names
        private static string[] witConfigNames = Array.Empty<string>();
        public static string[] WitConfigNames => witConfigNames;

        // Refresh configuration asset list
        public static void RefreshConfigList()
        {
            // Find all Wit Configurations
            string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");

            // Store wit configuration data
            witConfigs = new WitConfiguration[guids.Length];
            witConfigNames = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                witConfigs[i] = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
                witConfigNames[i] = witConfigs[i].name;
            }
        }
        // Has configuration
        public static bool HasValidConfig()
        {
            // Refresh list
            RefreshConfigList();
            // Check configs
            for (int i = 0; i < witConfigs.Length; i++)
            {
                if (!string.IsNullOrEmpty(witConfigs[i].clientAccessToken))
                {
                    return true;
                }
            }
            // None found
            return false;
        }
        #endregion

        #region WINDOWS
        // Default types
        private readonly static Type defaultSetupWindowType = typeof(WitWelcomeWizard);
        private readonly static Type defaultConfigurationWindowType = typeof(WitWindow);
        private readonly static Type defaultUnderstandingWindowType = typeof(Utilities.WitUnderstandingViewer);

        // Window types
        public static Type setupWindowType = defaultSetupWindowType;
        public static Type configurationWindowType = defaultConfigurationWindowType;
        public static Type understandingWindowType = defaultUnderstandingWindowType;

        // Opens Setup Window
        public static void OpenSetupWindow(Action onSetupComplete)
        {
            // Get setup type
            Type type = GetSafeType(setupWindowType, defaultSetupWindowType);
            // Get wizard (Title is overwritten)
            WitWelcomeWizard wizard = (WitWelcomeWizard)ScriptableWizard.DisplayWizard("Setup Wizard", type, "Link");
            // Set success callback
            wizard.successAction = onSetupComplete;
        }
        // Opens Configuration Window
        public static void OpenConfigurationWindow()
        {
            // Setup if needed
            if (!HasValidConfig())
            {
                OpenSetupWindow(OpenConfigurationWindow);
                return;
            }

            // Get confuration type
            Type type = GetSafeType(configurationWindowType, defaultConfigurationWindowType);
            // Get window & show
            EditorWindow window = EditorWindow.GetWindow(type);
            window.autoRepaintOnSceneChange = true;
            window.Show();
        }
        // Opens Understanding Window
        public static void OpenUnderstandingWindow()
        {
            // Setup if needed
            if (!HasValidConfig())
            {
                OpenSetupWindow(OpenUnderstandingWindow);
                return;
            }

            // Get understanding type
            Type type = GetSafeType(understandingWindowType, defaultUnderstandingWindowType);
            // Get window & show
            EditorWindow window = EditorWindow.GetWindow(type);
            window.autoRepaintOnSceneChange = true;
            window.Show();
        }
        // Get safe type
        private static Type GetSafeType(Type desiredType, Type defaultType)
        {
            if (desiredType == null || (desiredType != defaultType && !desiredType.IsSubclassOf(defaultType)))
            {
                Debug.LogError("Wit Editor Utility - Invalid Window Type: " + (desiredType == null ? "NULL" : desiredType.ToString()) + "\nUsing: " + defaultType.ToString());
                return defaultType;
            }
            return desiredType;
        }
        #endregion
    }
}
