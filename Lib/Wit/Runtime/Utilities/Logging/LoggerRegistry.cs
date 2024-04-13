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
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Meta.Voice.Logging
{
    public sealed class LoggerRegistry : ILoggerRegistry
    {
        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const VLoggerVerbosity EDITOR_LOG_LEVEL_DEFAULT = VLoggerVerbosity.Warning;
        private readonly Dictionary<string, IVLogger> _loggers = new Dictionary<string, IVLogger>();
        private static readonly ILogWriter DefaultLogWriter = new UnityLogWriter();
        private static VLoggerVerbosity _editorLogLevel = (VLoggerVerbosity)(-1);

        /// <inheritdoc/>
        public VLoggerVerbosity EditorLogLevel
        {
            get => _editorLogLevel;
            set
            {
                _editorLogLevel = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, _editorLogLevel.ToString());
#endif
                foreach (var logger in _loggers.Values)
                {
                    logger.MinimumVerbosity = value;
                }
            }
        }

        /// <summary>
        /// The singleton instance of the registry.
        /// </summary>
        public static ILoggerRegistry Instance { get; } = new LoggerRegistry();

        /// <summary>
        /// A private constructor to prevent instantiation of this class.
        /// </summary>
        private LoggerRegistry()
        {
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initialize the registry.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            // Already init
            if (Instance.EditorLogLevel != (VLoggerVerbosity) (-1))
            {
                return;
            }

            // Load log
            var editorLogLevel = EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, EDITOR_LOG_LEVEL_DEFAULT.ToString());

            // Try parsing
            if (!Enum.TryParse(editorLogLevel, out _editorLogLevel))
            {
                // If parsing fails, use default log level
                Instance.EditorLogLevel = EDITOR_LOG_LEVEL_DEFAULT;
            }
        }
#endif

        /// <inheritdoc/>
        public IVLogger GetLogger(ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null)
        {
            logWriter ??= DefaultLogWriter;

            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(1).FirstOrDefault(frame => frame?.GetMethod()?.DeclaringType != typeof(LoggerRegistry));
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetLogger(category);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                if (verbosity.HasValue)
                {
                    return new VLogger(category, logWriter, verbosity.Value);
                }
                else
                {
#if UNITY_EDITOR
                    return new VLogger(category, logWriter, EditorLogLevel);
#else
                    return new VLogger(category, logWriter, VLoggerVerbosity.Verbose);
#endif
                }
            }

            category = attribute.CategoryName;

            return GetLogger(category);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null)
        {
            logWriter ??= DefaultLogWriter;

            if (!_loggers.ContainsKey(category))
            {
                if (verbosity.HasValue)
                {
                    _loggers.Add(category, new VLogger(category, logWriter, verbosity.Value));
                }
                else
                {
                    _loggers.Add(category, new VLogger(category, logWriter, EditorLogLevel));
                }
            }

            return _loggers[category];
        }
    }
}
