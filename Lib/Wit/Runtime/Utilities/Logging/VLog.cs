/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Meta.Voice.Logging;
using Meta.WitAi.Json;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// A class for internal Meta.Voice logs
    /// </summary>
    public static class VLog
    {
        private static readonly ILoggerRegistry LoggerRegistry = Voice.Logging.LoggerRegistry.Instance;

        #if UNITY_EDITOR
        /// <summary>
        /// If enabled, errors will log to console as warnings
        /// </summary>
        public static bool LogErrorsAsWarnings = false;
        private const string EDITOR_FILTER_LOG_KEY = "VSDK_FILTER_LOG";

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

        #endif

        /// <summary>
        /// Hides all errors from the console
        /// </summary>
        public static bool SuppressLogs { get; set; } = !Application.isEditor && !UnityEngine.Debug.isDebugBuild;

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level of Info
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        public static void I(object log) => Log(VLoggerVerbosity.Info, null, log);

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level of Info
        /// </summary>
        /// <param name="logCategory">The category of the log</param>
        /// <param name="log">The text to be debugged</param>
        [Obsolete("Use VLogger.Info() instead")]
        public static void I(string logCategory, object log) => Log(VLoggerVerbosity.Info, logCategory, log);

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        public static void D(object log) => Log(VLoggerVerbosity.Debug, null, log);

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        [Obsolete("Use VLogger.Debug() instead")]
        public static void D(string logCategory, object log) => Log(VLoggerVerbosity.Debug, logCategory, log);

        /// <summary>
        /// Performs a Debug.LogWarning with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        public static void W(object log, Exception e = null) => Log(VLoggerVerbosity.Warning, null, log, e);

        /// <summary>
        /// Performs a Debug.LogWarning with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void W(string logCategory, object log, Exception e = null) => Log(VLoggerVerbosity.Warning, logCategory, log, e);

        /// <summary>
        /// Performs a Debug.LogError with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        public static void E(object log, Exception e = null) => Log(VLoggerVerbosity.Error, null, log, e);

        /// <summary>
        /// Performs a Debug.LogError with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void E(string logCategory, object log, Exception e = null) => Log(VLoggerVerbosity.Error, logCategory, log, e);

        /// <summary>
        /// Filters out unwanted logs, appends category information
        /// and performs UnityEngine.Debug.Log as desired
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="log"></param>
        /// <param name="category"></param>
        private static void Log(VLoggerVerbosity logType, string logCategory, object log, Exception exception = null)
        {
            string category = logCategory;
            if (string.IsNullOrEmpty(category))
            {
                category = GetCallingCategory();
            }

            var logger = LoggerRegistry.GetLogger(category);

            switch (logType)
            {
                case VLoggerVerbosity.Error:
#if UNITY_EDITOR
                    if (LogErrorsAsWarnings)
                    {
                        logger.Warning(log + (exception == null ? "" : $"\n{exception}"));
                        return;
                    }
#endif
                    logger.Error(KnownErrorCode.Unknown, log + (exception == null ? "" : $"\n{exception}"));
                    break;
                case VLoggerVerbosity.Warning:
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
