/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Linq;
using Meta.WitAi.TTS.Integrations;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi.TTS
{
    [CustomEditor(typeof(TTSWit), true)]
    public class TTSWitInspector : TTSServiceInspector
    {
        private int selectedBaseVoice;

        protected override void OnEditTimeGUI()
        {
            base.OnEditTimeGUI();

            var ttsWit = (TTSWit)target;
            var config = ttsWit.RequestSettings.configuration;
            if (!config) return;

            var appInfo = config.GetApplicationInfo();

            if (null != appInfo.voices && appInfo.voices.Length > 0)
            {
                var voiceNames = appInfo.voices.Select(v => v.name).ToArray();

                GUILayout.BeginHorizontal();
                selectedBaseVoice = EditorGUILayout.Popup(selectedBaseVoice, voiceNames);
                if (GUILayout.Button("Add Preset", GUILayout.Width(75)))
                {
                    var presets = ttsWit.PresetWitVoiceSettings.ToList();
                    presets.Add(new TTSWitVoiceSettings
                    {
                        voice = voiceNames[selectedBaseVoice],
                        SettingsId = voiceNames[selectedBaseVoice]
                    });
                    ttsWit.SetVoiceSettings(presets.ToArray());
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("There are currently no base presets available. Click refresh to check for updates.", EditorStyles.helpBox);
            }

            if (GUILayout.Button("Add All Voices as Presets"))
            {
                var presets = ttsWit.PresetWitVoiceSettings.ToList();
                for (int i = 0; i < appInfo.voices.Length; i++)
                {
                    presets.Add(new TTSWitVoiceSettings
                    {
                        voice = appInfo.voices[i].name,
                        SettingsId = appInfo.voices[i].name
                    });
                }
                ttsWit.SetVoiceSettings(presets.ToArray());

            }
            if (GUILayout.Button("Refresh Presets"))
            {
                TTSEditorUtilities.RefreshAvailableVoices((TTSWit) target, info =>
                {
                    Repaint();
                });
            }
        }
    }
}
