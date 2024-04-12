/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Meta.Voice.Logging;
using Meta.WitAi.Json;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// The various logging options for VLog
    /// </summary>
    public enum VLogLevel
    {
        /// <summary>
        /// Error log. Usually indicates a bug in the code.
        /// </summary>
        Error = 0,

        /// <summary>
        /// Something that is a red flag and could potentially be a problem, but not necessarily.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Debug logs. Useful for debugging specific things.
        /// </summary>
        Log = 2,

        /// <summary>
        /// Information logs. Normal tracing.
        /// </summary>
        Info = 3,
    }

    /// <summary>
    /// A class for internal Meta.Voice logs
    /// </summary>
    public static class VLog
    {
        private static readonly ILoggerRegistry _loggerRegistry = LoggerRegistry.Instance;

        #if UNITY_EDITOR
        /// <summary>
        /// If enabled, errors will log to console as warnings
        /// </summary>
        public static bool LogErrorsAsWarnings = false;

        /// <summary>
        /// Ignores logs in editor if less than log level (Error = 0, Warning = 2, Log = 3)
        /// </summary>
        public static VLogLevel EditorLogLevel
        {
            get => _editorLogLevel;
            set
            {
                _editorLogLevel = value;
                EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, _editorLogLevel.ToString());
            }
        }
        private static VLogLevel _editorLogLevel = (VLogLevel)(-1);
        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const string EDITOR_FILTER_LOG_KEY = "VSDK_FILTER_LOG";
        private const VLogLevel EDITOR_LOG_LEVEL_DEFAULT = VLogLevel.Warning;

        private static HashSet<string> _filteredTagSet;
        private static List<string> _filteredTagList;

        public static List<String> FilteredTags
        {
            get
            {
                if (null == _filteredTagList)
                {
                    _filteredTagList = new List<string>();
                    var filtered = EditorPrefs.GetString(EDITOR_FILTER_LOG_KEY, null);
                    if (!string.IsNullOrEmpty(filtered))
                    {
                        _filteredTagList = JsonConvert.DeserializeObject<List<string>>(filtered);
                    }
                }

                return _filteredTagList;
            }
        }

        internal static HashSet<string> FilteredTagSet
        {
            get
            {
                if (null == _filteredTagSet)
                {
                    _filteredTagSet = new HashSet<string>();
                    foreach (var tag in FilteredTags)
                    {
                        _filteredTagSet.Add(tag);
                    }
                }

                return _filteredTagSet;
            }
        }

        public static void AddTagFilter(string filteredTag)
        {
            if (!FilteredTagSet.Contains(filteredTag))
            {
                _filteredTagList.Add(filteredTag);
                _filteredTagSet.Add(filteredTag);
                SaveFilters();
            }
        }

        public static void RemoveTagFilter(string filteredTag)
        {
            if (FilteredTagSet.Contains(filteredTag))
            {
                _filteredTagList.Remove(filteredTag);
                _filteredTagSet.Remove(filteredTag);
                SaveFilters();
            }
        }

        private static void SaveFilters()
        {
            _filteredTagList.Sort();
            var list = JsonConvert.SerializeObject(_filteredTagList);
            EditorPrefs.SetString(EDITOR_FILTER_LOG_KEY, list);
        }

        // Init on load
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            // Already init
            if (_editorLogLevel != (VLogLevel) (-1))
            {
                return;
            }

            // Load log
            string editorLogLevel = UnityEditor.EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, EDITOR_LOG_LEVEL_DEFAULT.ToString());

            // Try parsing
            if (!Enum.TryParse(editorLogLevel, out _editorLogLevel))
            {
                // If parsing fails, use default log level
                EditorLogLevel = EDITOR_LOG_LEVEL_DEFAULT;
            }
        }
        #endif

        /// <summary>
        /// Hides all errors from the console
        /// </summary>
        public static bool SuppressLogs { get; set; } = !Application.isEditor && !UnityEngine.Debug.isDebugBuild;

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level of Info
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void I(object log) => Log(VLogLevel.Info, null, log);
        public static void I(string logCategory, object log) => Log(VLogLevel.Info, logCategory, log);

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void D(object log) => Log(VLogLevel.Log, null, log);
        public static void D(string logCategory, object log) => Log(VLogLevel.Log, logCategory, log);

        /// <summary>
        /// Performs a Debug.LogWarning with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void W(object log, Exception e = null) => Log(VLogLevel.Warning, null, log, e);
        public static void W(string logCategory, object log, Exception e = null) => Log(VLogLevel.Warning, logCategory, log, e);

        /// <summary>
        /// Performs a Debug.LogError with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void E(object log, Exception e = null) => Log(VLogLevel.Error, null, log, e);
        public static void E(string logCategory, object log, Exception e = null) => Log(VLogLevel.Error, logCategory, log, e);

        /// <summary>
        /// Filters out unwanted logs, appends category information
        /// and performs UnityEngine.Debug.Log as desired
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="log"></param>
        /// <param name="category"></param>
        public static void Log(VLogLevel logType, string logCategory, object log, Exception exception = null)
        {
            string category = logCategory;
            if (string.IsNullOrEmpty(category))
            {
                category = GetCallingCategory();
            }

            var logger = _loggerRegistry.GetLogger(category);

            switch (logType)
            {
                case VLogLevel.Error:
#if UNITY_EDITOR
                    if (LogErrorsAsWarnings)
                    {
                        logger.Warning(log + (exception == null ? "" : $"\n{exception}"));
                        return;
                    }
#endif
                    logger.Error(KnownErrorCode.Unknown, log + (exception == null ? "" : $"\n{exception}"));
                    break;
                case VLogLevel.Warning:
                    logger.Warning(log.ToString());
                    break;
                default:
                    logger.Debug(log.ToString());
                    break;
            }
        }

        /// <summary>
        /// Determines a category from the script name that called the previous method
        /// </summary>
        /// <returns>Assembly name</returns>
        private static string GetCallingCategory()
        {
            // Get stack trace method
            string path = new StackTrace()?.GetFrame(3)?.GetMethod().DeclaringType.Name;
            if (string.IsNullOrEmpty(path))
            {
                return "NoStacktrace";
            }
            // Return path
            return path;
        }
    }
}
