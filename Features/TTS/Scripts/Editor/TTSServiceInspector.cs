/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.TTS.Data;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.TTS.Editor
{
    [CustomEditor(typeof(TTSService), true)]
    public class TTSServiceInspector : UnityEditor.Editor
    {
        // Service
        private TTSService _service;
        //
        public bool clipDropdown = false;
        // Maximum text for abbreviated
        private const int MAX_DISPLAY_TEXT = 20;

        // GUI
        public override void OnInspectorGUI()
        {
            // Display default ui
            base.OnInspectorGUI();

            // Ignore if in editor
            if (!Application.isPlaying)
            {
                return;
            }
            // Get service
            if (_service == null)
            {
                _service = target as TTSService;
            }

            // Add spaces
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            WitEditorUI.LayoutLabel("<b>Runtime Clip Cache</b>");

            // No clips
            TTSClipData[] clips = _service.GetAllRuntimeCachedClips();
            if (clips == null || clips.Length == 0)
            {
                WitEditorUI.LayoutErrorLabel("No clips found");
                return;
            }
            // Hidden
            clipDropdown = WitEditorUI.LayoutFoldout(new GUIContent($"Clips: {clips.Length}"), clipDropdown);
            if (clipDropdown)
            {
                EditorGUI.indentLevel++;
                // Iterate clips
                foreach (TTSClipData clip in clips)
                {
                    // Get display name
                    string displayName = clip.textToSpeak;
                    // Crop if too long
                    if (displayName.Length > MAX_DISPLAY_TEXT)
                    {
                        displayName = displayName.Substring(0, MAX_DISPLAY_TEXT);
                    }
                    // Add voice setting id
                    if (clip.voiceSettings != null)
                    {
                        displayName = $"{clip.voiceSettings.settingsID} - {displayName}";
                    }
                    // Foldout if desired
                    bool foldout = WitEditorUI.LayoutFoldout(new GUIContent(displayName), clip);
                    if (foldout)
                    {
                        EditorGUI.indentLevel++;
                        OnClipGUI(clip);
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }
        // Clip data
        private void OnClipGUI(TTSClipData clip)
        {
            // Generation Settings
            WitEditorUI.LayoutKeyLabel("Text", clip.textToSpeak);
            WitEditorUI.LayoutKeyObjectLabels("Voice Settings", clip.voiceSettings);
            WitEditorUI.LayoutKeyObjectLabels("Cache Settings", clip.diskCacheSettings);
            // Clip Settings
            EditorGUILayout.TextField("Clip ID", clip.clipID);
            EditorGUILayout.ObjectField("Clip", clip.clip, typeof(AudioClip), true);
            // Load Settings
            WitEditorUI.LayoutKeyLabel("Load State", clip.loadState.ToString());
            WitEditorUI.LayoutKeyLabel("Load Progress", (clip.loadProgress * 100f).ToString() + "%");
        }
    }
}
