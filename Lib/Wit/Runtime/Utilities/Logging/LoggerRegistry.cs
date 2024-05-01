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
        public ILogSink LogSink { get; set; }

        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const string EDITOR_LOG_SUPPRESSION_LEVEL_KEY = "VSDK_EDITOR_LOG_SUPPRESSION_LEVEL";
        private const string EDITOR_LOG_STACKTRACE_LEVEL_KEY = "VSDK_EDITOR_LOG_STACKTRACE_LEVEL";

        public LoggerOptions Options { get; } =
            new LoggerOptions(VLoggerVerbosity.Warning, VLoggerVerbosity.Verbose, VLoggerVerbosity.Error);

        private readonly Dictionary<string, IVLogger> _loggers = new Dictionary<string, IVLogger>();

        /// <inheritdoc/>
        public bool PoolLoggers { get; set; } = true;

        public VLoggerVerbosity LogStackTraceLevel
        {
            get
            {
                return Options.StackTraceLevel;
            }
            set
            {
                if (Options.StackTraceLevel == value)
                {
                    return;
                }

                Options.StackTraceLevel = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_STACKTRACE_LEVEL_KEY, Options.StackTraceLevel.ToString());
#endif
            }
        }

        /// <inheritdoc/>
        public VLoggerVerbosity LogSuppressionLevel
        {
            get
            {
                return Options.SuppressionLevel;
            }
            set
            {
                if (Options.SuppressionLevel == value)
                {
                    return;
                }

                Options.SuppressionLevel = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_SUPPRESSION_LEVEL_KEY, Options.SuppressionLevel.ToString());
#endif
            }
        }

        /// <inheritdoc/>
        public VLoggerVerbosity EditorLogFilteringLevel
        {
            get
            {
                return Options.MinimumVerbosity;
            }
            set
            {
                if (Options.MinimumVerbosity == value)
                {
                    return;
                }

                Options.MinimumVerbosity = value;
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, Options.MinimumVerbosity.ToString());
#endif
            }
        }

        /// <summary>
        /// The singleton instance of the registry.
        /// </summary>
        public static LoggerRegistry Instance { get; } = new LoggerRegistry();

        /// <inheritdoc/>
        public IEnumerable<IVLogger> AllLoggers => _loggers.Values;

        /// <summary>
        /// A private constructor to prevent instantiation of this class.
        /// </summary>
        private LoggerRegistry()
        {
            ILogWriter defaultLogWriter = new UnityLogWriter();
            LogSink = new LogSink(defaultLogWriter, new LoggerOptions(EditorLogFilteringLevel, LogSuppressionLevel, LogStackTraceLevel));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initialize the registry.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            var editorLogLevelString = EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, Instance.Options.MinimumVerbosity.ToString());
            Enum.TryParse(editorLogLevelString, out VLoggerVerbosity logLevel);
            Instance.EditorLogFilteringLevel = logLevel;

            var suppressionLogLevelString = EditorPrefs.GetString(EDITOR_LOG_SUPPRESSION_LEVEL_KEY, Instance.Options.SuppressionLevel.ToString());
            Enum.TryParse(suppressionLogLevelString, out VLoggerVerbosity suppressionLevel);
            Instance.EditorLogFilteringLevel = suppressionLevel;

            var stacktraceLogLevelString = EditorPrefs.GetString(EDITOR_LOG_STACKTRACE_LEVEL_KEY, Instance.Options.StackTraceLevel.ToString());
            Enum.TryParse(stacktraceLogLevelString, out VLoggerVerbosity stacktraceLogLevel);
            Instance.LogStackTraceLevel = stacktraceLogLevel;

            UnityEngine.Debug.Log($"LoggerRegistry initialized: {editorLogLevelString}-{suppressionLogLevelString}-{stacktraceLogLevelString}");
        }
#endif

        /// <inheritdoc/>
        public IVLogger GetLogger(ILogSink logSink = null)
        {
            logSink ??= LogSink;

            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(1).FirstOrDefault(frame => frame?.GetMethod()?.DeclaringType != typeof(LoggerRegistry));
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetLogger(category, logSink);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                return new VLogger(category, logSink);
            }

            category = attribute.CategoryName;

            return GetLogger(category, logSink);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, ILogSink logSink)
        {
            logSink ??= LogSink;
            logSink.Options = Options;

            if (PoolLoggers)
            {
                if (!_loggers.ContainsKey(category))
                {
                    _loggers.Add(category, new VLogger(category, logSink));
                }

                return _loggers[category];
            }
            else
            {
                return new VLogger(category, logSink);
            }
        }
    }
}
