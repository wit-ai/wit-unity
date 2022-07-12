/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.TTS.Editor.Voices;
using Facebook.WitAi.TTS.Integrations;
using Facebook.WitAi.Utilities;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.TTS.Editor
{
    public static class TTSEditorUtilities
    {
        // Instantiate TTS Service into the scene
        public static void InstantiateTTSService()
        {
            // Ignore if found
            TTSService instance = GameObject.FindObjectOfType<TTSService>();
            if (instance != null)
            {
                Debug.LogWarning($"TTS Service - A TTSService is already in scene\nGameObject: {instance.gameObject.name}");
                return;
            }

            // Found
            TTSService servicePrefab = AssetDatabaseUtility.FindUnityAsset<TTSService>("t:prefab");
            if (servicePrefab == null)
            {
                Debug.LogError("TTS Service - No prefabs found with TTSService script");
                return;
            }

            // Instantiate
            Selection.activeObject = PrefabUtility.InstantiatePrefab(servicePrefab.gameObject);
            instance = Selection.activeGameObject.GetComponent<TTSService>();
            Debug.Log($"TTS Service - Instantiated {servicePrefab.gameObject.name}\nPrefab Path: {AssetDatabase.GetAssetPath(servicePrefab)}");

            // Attempt to assign configuration
            if (instance.GetType() == typeof(TTSWit))
            {
                // Cast
                TTSWit ttsWit = instance as TTSWit;
                if (ttsWit.RequestSettings.configuration == null)
                {
                    // Attempt to find & assign a wit configuration
                    if (WitConfigurationUtility.WitConfigs == null)
                    {
                        WitConfigurationUtility.ReloadConfigurationData();
                    }
                    // Assign wit configuration if possible
                    if (WitConfigurationUtility.WitConfigs != null && WitConfigurationUtility.WitConfigs.Length > 0)
                    {
                        ttsWit.RequestSettings.configuration = WitConfigurationUtility.WitConfigs[0];
                        Debug.Log($"TTS Service - Assigned Wit Configuration {ttsWit.RequestSettings.configuration.name}");
                    }
                }
                // Log failure
                if (ttsWit.RequestSettings.configuration == null)
                {
                    Debug.LogWarning($"TTS Service - Make sure you assign a Wit Configuration to your TTSWit script");
                }
                // Update voice list
                else
                {
                    TTSWitVoiceUtility.UpdateVoices(ttsWit.RequestSettings.configuration, null);
                }
            }
        }
    }
}
