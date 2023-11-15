/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi.TTS
{
    [CustomEditor(typeof(TTSService), true)]
    public class TTSServiceInspector : Editor
    {
        // Service
        private TTSService _service;
        // Dropdown
        private bool _clipFoldout = false;
        // Maximum text for abbreviated
        private const int MAX_DISPLAY_TEXT = 20;

        // GUI
        public sealed override void OnInspectorGUI()
        {
            // Display default ui
            OnEditTimeGUI();
            OnPlaytimeGUI();
        }

        protected virtual void OnEditTimeGUI()
        {
            base.OnInspectorGUI();
        }

        protected virtual void OnPlaytimeGUI()
        {
            // Ignore if in editor
            if (!Application.isPlaying)
            {
                return;
            }

            // Get service
            if (!_service)
            {
                _service = target as TTSService;
            }

            // Add spaces
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Clip Cache", EditorStyles.boldLabel);

            // No clips
            TTSClipData[] clips = _service.GetAllRuntimeCachedClips();
            if (clips == null || clips.Length == 0)
            {
                WitEditorUI.LayoutErrorLabel("No clips found");
                return;
            }
            // Has clips
            _clipFoldout = WitEditorUI.LayoutFoldout(new GUIContent($"Clips: {clips.Length}"), _clipFoldout);
            if (_clipFoldout)
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
                        displayName = $"{clip.voiceSettings.SettingsId} - {displayName}";
                    }
                    // Foldout if desired
                    bool foldout = WitEditorUI.LayoutFoldout(new GUIContent(displayName), clip);
                    if (foldout)
                    {
                        EditorGUI.indentLevel++;
                        DrawClipGUI(clip);
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        // Clip data
        public static void DrawClipGUI(TTSClipData clip)
        {
            // Generation Settings
            WitEditorUI.LayoutKeyLabel("Text", clip.textToSpeak);
            EditorGUILayout.TextField("Clip ID", clip.clipID);
            EditorGUILayout.ObjectField("Clip", clip.clip, typeof(AudioClip), true);
            EditorGUILayout.LabelField("Length", (clip.clipStream == null ? 0 : clip.clipStream.Length).ToString());
            EditorGUILayout.LabelField("Samples", (clip.clipStream == null ? 0 : clip.clipStream.TotalSamples).ToString());

            // Loaded
            TTSClipLoadState loadState = clip.loadState;
            if (loadState != TTSClipLoadState.Preparing)
            {
                WitEditorUI.LayoutKeyLabel("Load State", loadState.ToString());
            }
            // Loading with progress
            else
            {
                EditorGUILayout.BeginHorizontal();
                int loadProgress = Mathf.FloorToInt(clip.loadProgress * 100f);
                WitEditorUI.LayoutKeyLabel("Load State", $"{loadState} ({loadProgress}%)");
                GUILayout.HorizontalSlider(loadProgress, 0, 100);
                EditorGUILayout.EndHorizontal();
            }

            // Additional Settings
            WitEditorUI.LayoutKeyObjectLabels("Voice Settings", clip.voiceSettings);
            WitEditorUI.LayoutKeyObjectLabels("Cache Settings", clip.diskCacheSettings);

            // Events
            DrawAudioEventAnimation(clip.Events);
        }

        private static void DrawAudioEventAnimation(TTSEventContainer eventContainer)
        {
            if (eventContainer == null)
            {
                return;
            }
            bool foldout = WitEditorUI.LayoutFoldout(new GUIContent("Audio Events"), eventContainer);
            if (!foldout)
            {
                return;
            }
            EditorGUI.indentLevel++;
            var events = eventContainer.Events;
            if (events != null && events.Count > 0)
            {
                int count = 0;
                foreach (var ttsEvent in events)
                {
                    string key = $"{ttsEvent.GetType().Name}[{count}]";
                    if (ttsEvent is TTSVisemeEvent visemeEvent)
                    {
                        WitEditorUI.LayoutKeyObjectLabels(key, visemeEvent);
                    }
                    else if (ttsEvent is TTSStringEvent stringEvent)
                    {
                        WitEditorUI.LayoutKeyObjectLabels(key, stringEvent);
                    }
                    else
                    {
                        if (WitEditorUI.LayoutFoldout(new GUIContent(key), ttsEvent))
                        {
                            WitEditorUI.LayoutLabel("Unsupported Type");
                        }
                    }
                    count++;
                }
            }
            else
            {
                WitEditorUI.LayoutLabel("No TTSEvents");
            }
            EditorGUI.indentLevel--;
        }
    }
}
