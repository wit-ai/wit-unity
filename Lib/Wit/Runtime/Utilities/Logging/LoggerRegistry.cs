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
        private static VLoggerVerbosity _editorLogLevel = VLoggerVerbosity.Warning;
        private static VLoggerVerbosity _suppressionLogLevel = VLoggerVerbosity.Verbose;
        private readonly Dictionary<string, IVLogger> _loggers = new Dictionary<string, IVLogger>();

        private static Lazy<VLoggerVerbosity> _editorLogFilteringLevel = new(() => _editorLogLevel);

        private static Lazy<VLoggerVerbosity> _editorLogSuppressionLevel = new(() => _suppressionLogLevel);

        /// <inheritdoc/>
        public bool PoolLoggers { get; set; } = true;

        /// <inheritdoc/>
        public VLoggerVerbosity LogSuppressionLevel
        {
            get
            {
                return _editorLogSuppressionLevel.Value;
            }
            set
            {
                if (_editorLogSuppressionLevel.Value == value)
                {
                    return;
                }

                _editorLogSuppressionLevel = new Lazy<VLoggerVerbosity>(value);
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_SUPPRESSION_LEVEL_KEY, _editorLogSuppressionLevel.ToString());
#endif
                foreach (var logger in _loggers.Values)
                {
                    logger.SuppressionLevel = value;
                }
            }
        }

        /// <inheritdoc/>
        public VLoggerVerbosity EditorLogFilteringLevel
        {
            get
            {
                return _editorLogFilteringLevel.Value;
            }
            set
            {
                if (_editorLogFilteringLevel.Value == value)
                {
                    return;
                }

                _editorLogFilteringLevel = new Lazy<VLoggerVerbosity>(value);
#if UNITY_EDITOR
                EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, _editorLogFilteringLevel.ToString());
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
        public static LoggerRegistry Instance { get; } = new LoggerRegistry();

        /// <inheritdoc/>
        public IEnumerable<IVLogger> AllLoggers => _loggers.Values;

        /// <summary>
        /// A private constructor to prevent instantiation of this class.
        /// </summary>
        private LoggerRegistry()
        {
            ILogWriter defaultLogWriter = new UnityLogWriter();
            LogSink = new LogSink(defaultLogWriter, new Lazy<LoggerOptions>(new LoggerOptions()));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initialize the registry.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            var editorLogLevelString = EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, _editorLogLevel.ToString());
            Enum.TryParse(editorLogLevelString, out VLoggerVerbosity logLevel);
            Instance.EditorLogFilteringLevel = logLevel;

            var suppressionLogLevelString = EditorPrefs.GetString(EDITOR_LOG_SUPPRESSION_LEVEL_KEY);
            Enum.TryParse(suppressionLogLevelString, out VLoggerVerbosity suppressionLevel);
            Instance.EditorLogFilteringLevel = suppressionLevel;

            UnityEngine.Debug.Log($"LoggerRegistry initialized: {_editorLogLevel}-{_suppressionLogLevel}");
        }
#endif

        /// <inheritdoc/>
        public IVLogger GetLogger(ILogSink logSink = null, VLoggerVerbosity? verbosity = null)
        {
            var options = new Lazy<LoggerOptions>(()=> new LoggerOptions(
                verbosity ?? EditorLogFilteringLevel,
                _editorLogSuppressionLevel.Value,
                true,
                true));

            return GetLogger(options, logSink);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category)
        {
            var options = new Lazy<LoggerOptions>(()=> new LoggerOptions(
                EditorLogFilteringLevel,
                _editorLogSuppressionLevel.Value,
                true,
                true));

            return GetLogger(category, options);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(Lazy<LoggerOptions> options, ILogSink logSink = null)
        {
            logSink ??= LogSink;

            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(1).FirstOrDefault(frame => frame?.GetMethod()?.DeclaringType != typeof(LoggerRegistry));
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetLogger(category, options, logSink);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                return new VLogger(category, logSink, options);
            }

            category = attribute.CategoryName;

            return GetLogger(category, options, logSink);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, Lazy<LoggerOptions> options, ILogSink logSink = null)
        {
            logSink ??= LogSink;

            if (PoolLoggers)
            {
                if (!_loggers.ContainsKey(category))
                {
                    _loggers.Add(category, new VLogger(category, logSink, options));
                }

                return _loggers[category];
            }
            else
            {
                return new VLogger(category, logSink, options);
            }
        }
    }
}
