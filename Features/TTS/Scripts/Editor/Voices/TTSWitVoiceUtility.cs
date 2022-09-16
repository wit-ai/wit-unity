/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Json;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.TTS.Integrations;

namespace Facebook.WitAi.TTS.Editor.Voices
{
    public static class TTSWitVoiceUtility
    {
        // Wit voice data
        public static TTSWitVoiceData[] Voices
        {
            get
            {
                if (_voices == null)
                {
                    LoadVoices(null);
                }
                return _voices;
            }
        }
        private static TTSWitVoiceData[] _voices = null;

        // Wit voice ids
        public static List<string> VoiceNames
        {
            get
            {
                if (_voiceNames == null)
                {
                    LoadVoices(null);
                }
                return _voiceNames;
            }
        }
        private static List<string> _voiceNames = null;

        // Wit voices are loading
        public static bool IsLoading => _loading;
        private static bool _loading = false;

        // Wit voices are updating
        public static bool IsUpdating => _updating;
        private static bool _updating = false;

        // Init gui
        private static bool _isGuiInit = false;

        #region LOAD
        // Persistent cache file path for getting voices without network
        public static string GetVoiceFilePath()
        {
            return Application.dataPath.Replace("/Assets", "/ProjectSettings") + "/wit_voices.json";
        }
        // Load voices from disk
        public static void LoadVoices(Action<bool> onComplete = null)
        {
            // Add service GUI
            if (!_isGuiInit)
            {
                _isGuiInit = true;
                TTSServiceInspector.onAdditionalGUI += OnServiceGUI;
            }
            // Already loading/updating
            if (IsLoading || IsUpdating)
            {
                onComplete?.Invoke(false);
                return;
            }
            // Voice from disk missing
            string backupPath = GetVoiceFilePath();
            if (!File.Exists(backupPath))
            {
                onComplete?.Invoke(false);
                return;
            }

            // Loading
            _loading = true;

            // Load file
            string json = string.Empty;
            try
            {
                json = File.ReadAllText(backupPath);
                VLog.D($"Load Success\n{json}");
            }
            catch (Exception e)
            {
                VLog.E($"Load Failure\n{e}");
                _loading = false;
                onComplete?.Invoke(false);
                return;
            }

            // Parse if possible
            ParseVoices(json, onComplete);
        }
        // Parse voices
        private static void ParseVoices(string json, Action<bool> onComplete)
        {
            // Parse
            Dictionary<string, TTSWitVoiceData[]> voicesByLocale = JsonConvert.DeserializeObject<Dictionary<string, TTSWitVoiceData[]>>(json);
            if (voicesByLocale == null)
            {
                VLog.E($"Parse Failure\nCould not parse voices");
                _loading = false;
                onComplete?.Invoke(false);
                return;
            }

            // Deserialize
            DeserializeVoices(voicesByLocale, onComplete);
        }
        // Deserialize voices
        private static void DeserializeVoices(Dictionary<string, TTSWitVoiceData[]> voicesByLocale, Action<bool> onComplete)
        {
            // Deserialize
            if (voicesByLocale == null)
            {
                VLog.E($"Deserialize Failure\nCould not deserialize voices");
                _loading = false;
                onComplete?.Invoke(false);
                return;
            }

            // Add all voices
            List<TTSWitVoiceData> voiceList = new List<TTSWitVoiceData>();
            foreach (var locale in voicesByLocale.Keys)
            {
                voiceList.AddRange(voicesByLocale[locale]);
            }

            // Finish
            OnDecodeComplete(voiceList.ToArray(), onComplete);
        }
        // Cast to voice data
        public static TTSWitVoiceData AsTTSWitVoiceData(this WitResponseNode responseNode)
        {
            // Get result
            object result = new TTSWitVoiceData();
            Type voiceType = typeof(TTSWitVoiceData);

            // Get root & field names
            WitResponseClass voiceRoot = responseNode.AsObject;
            string[] voiceFieldNames = voiceRoot.ChildNodeNames;
            foreach (var voiceFieldName in voiceFieldNames)
            {
                FieldInfo field = voiceType.GetField(voiceFieldName);
                if (field != null && field.IsPublic && !field.IsStatic)
                {
                    // Get value
                    object val = null;
                    // String
                    if (field.FieldType == typeof(string))
                    {
                        val = voiceRoot[voiceFieldName].Value;
                    }
                    // String[]
                    else if (field.FieldType == typeof(string[]))
                    {
                        val = voiceRoot[voiceFieldName].AsStringArray;
                    }
                    // Set value
                    if (val != null)
                    {
                        field.SetValue(result, val);
                    }
                }
                else
                {
                    VLog.W($"Decode Warning\nUnknown field: {voiceFieldName}");
                }
            }

            // Return result
            return (TTSWitVoiceData)result;
        }
        // On decode complete
        private static void OnDecodeComplete(TTSWitVoiceData[] newVoices, Action<bool> onComplete)
        {
            // Decode failed
            if (newVoices == null || newVoices.Length == 0)
            {
                VLog.E($"Decode Failure\nNo voices found");
                _loading = false;
                onComplete?.Invoke(false);
                return;
            }

            // Apply voices & names
            _voices = newVoices;
            _voiceNames = new List<string>();
            StringBuilder voiceLog = new StringBuilder();
            foreach (var voice in _voices)
            {
                _voiceNames.Add(voice.name);
                voiceLog.AppendLine(voice.name);
                voiceLog.AppendLine($"\tLocale: {voice.locale}");
                voiceLog.AppendLine($"\tGender: {voice.gender}");
                if (voice.styles != null)
                {
                    StringBuilder styleLog = new StringBuilder();
                    foreach (var style in voice.styles)
                    {
                        if (styleLog.Length > 0)
                        {
                            styleLog.Append(", ");
                        }
                        styleLog.Append(style);
                    }
                    voiceLog.AppendLine($"\tStyles: {styleLog}");
                }
            }

            // Success
            VLog.D($"Decode Success\n{voiceLog}");

            // Complete
            _loading = false;
            onComplete?.Invoke(true);
        }
        #endregion

        #region UPDATE
        // Obtain voices
        public static void UpdateVoices(WitConfiguration configuration, Action<bool> onComplete)
        {
            // Ignore if already updating
            if (IsUpdating || IsLoading)
            {
                onComplete?.Invoke(false);
                return;
            }
            // No configuration
            if (configuration == null)
            {
                VLog.E($"Voice Update Failed\nNo wit configuration found on TTSWit");
                return;
            }

            // Begin update
            _updating = true;

            // Download
            VLog.D("Voice Update Begin");
            WitRequestUtility.RequestTTSVoices<TTSWitVoiceData>(configuration, null, (result, error) =>
            {
                // Failed
                if (result == null)
                {
                    error = "Parse Failed";
                }
                if (!string.IsNullOrEmpty(error))
                {
                    VLog.E($"Voice Update Failed\n{error}");
                    OnUpdateComplete(false, onComplete);
                    return;
                }

                // Success
                VLog.D($"Voice Update Success");

                // Decode if possible
                DeserializeVoices(result, (success) =>
                {
                    // Decoded successfully, then save
                    if (success)
                    {
                        string backupPath = GetVoiceFilePath();
                        try
                        {
                            File.WriteAllText(backupPath, result.ToString());
                        }
                        catch (Exception e)
                        {
                            VLog.E($"Voice Update Save Failed\nPath: {backupPath}\n{e}");
                        }
                    }

                    // Complete
                    OnUpdateComplete(success, onComplete);
                });
            });
        }
        // Voices decoded
        private static void OnUpdateComplete(bool success, Action<bool> onComplete)
        {
            // Stop update
            _updating = false;

            // Failed & no voices, try loading
            if (!success && _voices == null)
            {
                LoadVoices((loadSuccess) => onComplete?.Invoke(success));
                return;
            }

            // Invoke
            onComplete?.Invoke(success);
        }
        #endregion

        #region GUI
        // Updating GUI
        private static bool _forcedUpdate = false;
        private static void OnServiceGUI(TTSService service)
        {
            // Wrong type
            if (service.GetType() != typeof(TTSWit) || Application.isPlaying)
            {
                return;
            }

            // Get data
            string text = "Update Voice List";
            bool canUpdate = true;
            if (IsUpdating)
            {
                text = "Updating Voice List";
                canUpdate = false;
            }
            else if (IsLoading)
            {
                text = "Loading Voice List";
                canUpdate = false;
            }

            // Layout update
            GUI.enabled = canUpdate;
            if (WitEditorUI.LayoutTextButton(text) && canUpdate)
            {
                TTSWit wit = service as TTSWit;
                UpdateVoices(wit.RequestSettings.configuration, null);
            }
            GUI.enabled = true;

            // Force an update
            if (!_forcedUpdate && canUpdate && (_voices == null || _voices.Length == 0))
            {
                _forcedUpdate = true;
                TTSWit wit = service as TTSWit;
                UpdateVoices(wit.RequestSettings.configuration, null);
            }
        }
        #endregion
    }
}
