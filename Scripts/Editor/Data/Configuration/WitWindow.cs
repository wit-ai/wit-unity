/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Meta.Voice.Logging;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi.Windows
{
    public class WitWindow : WitConfigurationWindow
    {
        protected WitConfigurationEditor witInspector;
        protected string serverToken;
        protected override GUIContent Title => WitTexts.SettingsTitleContent;
        protected override string HeaderUrl => witInspector ? witInspector.HeaderUrl : base.HeaderUrl;

        // VLog log level
        private static int _logLevel = -1;
        private static int _logSuppressionLevel = 1;
        private static int _logStackTraceLevel = 1;
        private static string[] _logLevelNames;
        private static readonly VLoggerVerbosity[] _logLevels = (Enum.GetValues(typeof(VLoggerVerbosity)) as VLoggerVerbosity[])?.Reverse().ToArray();
        private string _newFilter;

#if VSDK_TELEMETRY_AVAILABLE
        private static int _telemetryLogLevel = -1;
        private static string[] _telemetryLogLevelNames;
        private static readonly TelemetryLogLevel[] _telemetryLogLevels = new TelemetryLogLevel[]
            { TelemetryLogLevel.Off, TelemetryLogLevel.Basic, TelemetryLogLevel.Verbose };
#endif
        public virtual bool ShowWitConfiguration => true;
        public virtual bool ShowGeneralSettings => true;

        public static bool ShowTooltips
        {
            get => EditorPrefs.GetBool("VSDK::Settings::Tooltips", true);
            set => EditorPrefs.SetBool("VSDK::Settings::Tooltips", value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (string.IsNullOrEmpty(serverToken))
            {
                serverToken = WitAuthUtility.ServerToken;
            }
            RefreshLogLevel();
            InitializeTelemetryLevelOptions();
            SetWitEditor();
        }

        protected virtual void SetWitEditor()
        {
            // Destroy inspector
            if (witInspector != null)
            {
                DestroyImmediate(witInspector);
                witInspector = null;
            }
            // Generate new inspector & initialize immediately
            if (witConfiguration)
            {
                witInspector = (WitConfigurationEditor)UnityEditor.Editor.CreateEditor(witConfiguration);
                witInspector.drawHeader = false;
                witInspector.Initialize();
            }
        }

        protected override void LayoutContent()
        {
            if (ShowGeneralSettings) DrawGeneralSettings();
            if (ShowWitConfiguration) DrawWitConfigurations();
        }

        private void DrawGeneralSettings()
        {
            // VLog level
            var updated = false;
            RefreshLogLevel();
            var logLevel = _logLevel;
            WitEditorUI.LayoutPopup(WitTexts.Texts.VLogLevelLabel, _logLevelNames, ref logLevel, ref updated);
            if (updated)
            {
                SetLogLevel(logLevel);
            }

            var logSuppressionLevel = _logSuppressionLevel;
            WitEditorUI.LayoutPopup(WitTexts.Texts.VLoggerSuppressionLevelLabel, _logLevelNames, ref logSuppressionLevel, ref updated);
            if (updated)
            {
                SetLogSuppressionLevel(logSuppressionLevel);
            }

            var stackTraceLevel = _logStackTraceLevel;
            WitEditorUI.LayoutPopup(WitTexts.Texts.VLoggerStackTraceLevelLabel, _logLevelNames, ref stackTraceLevel, ref updated);
            if (updated)
            {
                SetLogStackTraceLevel(stackTraceLevel);
            }

            if (WitEditorUI.LayoutTextButton(WitTexts.Texts.VLoggerFlushLabel))
            {
                var entries = new List<LogEntry>();
                foreach (var logger in LoggerRegistry.Instance.AllLoggers)
                {
                    if (logger is VLogger vlogger)
                    {
                        entries.AddRange(vlogger.ExtractAllEntries());
                    }
                }
                entries.Sort();
                foreach (var entry in entries)
                {
                    LoggerRegistry.Instance.LogSink.WriteEntry(entry);
                }
            }

            GUILayout.Label("Log Filters", EditorStyles.boldLabel);
            foreach (var tag in VLog.FilteredTags)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(tag);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    VLog.RemoveTagFilter(tag);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            _newFilter = GUILayout.TextField(_newFilter);
            if (!string.IsNullOrEmpty(_newFilter) && GUILayout.Button("Add"))
            {
                VLog.AddTagFilter(_newFilter);
            }
            GUILayout.EndHorizontal();

            var showTooltips = ShowTooltips;
            WitEditorUI.LayoutToggle(new GUIContent(WitTexts.Texts.ShowTooltipsLabel), ref showTooltips, ref updated);
            if (updated)
            {
                ShowTooltips = showTooltips;
            }

#if VSDK_TELEMETRY_AVAILABLE && UNITY_EDITOR_WIN
            var enableTelemetry = TelemetryConsentManager.ConsentProvided;
            WitEditorUI.LayoutToggle(new GUIContent(WitTexts.Texts.TelemetryEnabledLabel), ref enableTelemetry, ref updated);
            if (updated)
            {
                TelemetryConsentManager.ConsentProvided = enableTelemetry;
            }

            var telemetryLogLevel = _telemetryLogLevel;
            WitEditorUI.LayoutPopup(WitTexts.Texts.TelemetryLevelLabel, _telemetryLogLevelNames, ref telemetryLogLevel, ref updated);
            if (updated)
            {
                _telemetryLogLevel = Math.Max(0, telemetryLogLevel);
                Telemetry.LogLevel = _telemetryLogLevels[_telemetryLogLevel];
            }
#endif
        }

        private void DrawWitConfigurations()
        {
            // Configuration select
            base.LayoutContent();
            // Update inspector if needed
            if (witInspector == null || witConfiguration == null || witInspector.Configuration != witConfiguration)
            {
                SetWitEditor();
            }

            // Layout configuration inspector
            if (witConfiguration && witInspector)
            {
                witInspector.OnInspectorGUI();
            }
        }

        private static void RefreshLogLevel()
        {
            if (_logLevelNames != null && _logLevelNames.Length == _logLevels.Length)
            {
                return;
            }
            List<string> logLevelOptions = new List<string>();
            foreach (var level in _logLevels)
            {
                logLevelOptions.Add(level.ToString());
            }
            _logLevelNames = logLevelOptions.ToArray();
#if UNITY_EDITOR
            LoggerRegistry.Initialize();
#endif
            _logLevel = logLevelOptions.IndexOf(LoggerRegistry.Instance.EditorLogFilteringLevel.ToString());
            _logSuppressionLevel = logLevelOptions.IndexOf(LoggerRegistry.Instance.LogSuppressionLevel.ToString());
            _logStackTraceLevel = logLevelOptions.IndexOf(LoggerRegistry.Instance.LogStackTraceLevel.ToString());
        }
        private void SetLogLevel(int newLevel)
        {
            _logLevel = Mathf.Clamp(0, newLevel, _logLevels.Length);
            LoggerRegistry.Instance.EditorLogFilteringLevel = _logLevels[_logLevel];
        }

        private void SetLogSuppressionLevel(int newLevel)
        {
            _logSuppressionLevel = Mathf.Clamp(0, newLevel, _logLevels.Length);
            LoggerRegistry.Instance.LogSuppressionLevel = _logLevels[_logSuppressionLevel];
        }

        private void SetLogStackTraceLevel(int newLevel)
        {
            _logStackTraceLevel = Mathf.Clamp(0, newLevel, _logLevels.Length);
            LoggerRegistry.Instance.LogStackTraceLevel = _logLevels[_logStackTraceLevel];
        }

        private static void InitializeTelemetryLevelOptions()
        {
#if VSDK_TELEMETRY_AVAILABLE
            _telemetryLogLevelNames = new string [_telemetryLogLevels.Length];
            for (int i = 0; i < _telemetryLogLevelNames.Length; ++i)
            {
                _telemetryLogLevelNames[i] = _telemetryLogLevels[i].ToString();
            }

            var currentLevel = Telemetry.LogLevel.ToString();
            for (int i = 0; i < _telemetryLogLevelNames.Length; ++i)
            {
                if (_telemetryLogLevelNames[i] == currentLevel)
                {
                    _telemetryLogLevel = i;
                    return;
                }
            }
#endif
        }
    }
}
