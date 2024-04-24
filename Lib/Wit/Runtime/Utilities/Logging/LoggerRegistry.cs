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
        public readonly ILogWriter DefaultLogWriter = new UnityLogWriter();

        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const string EDITOR_LOG_SUPPRESSION_LEVEL_KEY = "VSDK_EDITOR_LOG_SUPPRESSION_LEVEL";
        private const VLoggerVerbosity EDITOR_LOG_LEVEL_DEFAULT = VLoggerVerbosity.Warning;
        private const VLoggerVerbosity SUPPRESSION_LOG_LEVEL_DEFAULT = VLoggerVerbosity.Verbose;
        private readonly Dictionary<string, IVLogger> _loggers = new Dictionary<string, IVLogger>();

        private static Lazy<VLoggerVerbosity> _editorLogFilteringLevel = new(() =>
        {
#if UNITY_EDITOR
            var editorLogLevelString = EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, EDITOR_LOG_LEVEL_DEFAULT.ToString());

            return !Enum.TryParse(editorLogLevelString, out VLoggerVerbosity editorLogLevel) ? EDITOR_LOG_LEVEL_DEFAULT : editorLogLevel;
#else
            // Outside of editor, we always log verbose.
            return VLoggerVerbosity.Verbose;
#endif
        });

        private static Lazy<VLoggerVerbosity> _editorLogSuppressionLevel = new(() =>
        {
#if UNITY_EDITOR
            var suppressionLogLevelString = EditorPrefs.GetString(EDITOR_LOG_SUPPRESSION_LEVEL_KEY, SUPPRESSION_LOG_LEVEL_DEFAULT.ToString());

            return !Enum.TryParse(suppressionLogLevelString, out VLoggerVerbosity editorLogLevel) ? SUPPRESSION_LOG_LEVEL_DEFAULT : editorLogLevel;

#else
            // Outside of editor, we always suppress verbose.
            return VLoggerVerbosity.Verbose;
#endif
        });

        private static IErrorMitigator errorMitigator;

        /// <inheritdoc/>
        public IErrorMitigator ErrorMitigator
        {
            get
            {
                if (errorMitigator == null)
                {
                    errorMitigator = new ErrorMitigator();
                }

                return errorMitigator;
            }
            set => errorMitigator = value;
        }

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
        }

#if UNITY_EDITOR

        /// <summary>
        /// Initialize the registry.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {

        }
#endif

        /// <inheritdoc/>
        public IVLogger GetLogger(ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null)
        {
            var options = new Lazy<LoggerOptions>(()=> new LoggerOptions(
                verbosity ?? EditorLogFilteringLevel,
                _editorLogSuppressionLevel.Value,
                true,
                true));

            return GetLogger(options, logWriter);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, ILogWriter logWriter = null, VLoggerVerbosity? verbosity = null)
        {
            var options = new Lazy<LoggerOptions>(()=> new LoggerOptions(
                verbosity ?? EditorLogFilteringLevel,
                _editorLogSuppressionLevel.Value,
                true,
                true));

            return GetLogger(category, options, logWriter);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(Lazy<LoggerOptions> options, ILogWriter logWriter = null)
        {
            logWriter ??= DefaultLogWriter;

            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(1).FirstOrDefault(frame => frame?.GetMethod()?.DeclaringType != typeof(LoggerRegistry));
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetLogger(category, logWriter);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                return new VLogger(category, logWriter, options, new Lazy<IErrorMitigator>(()=>ErrorMitigator));
            }

            category = attribute.CategoryName;

            return GetLogger(category, options, logWriter);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, Lazy<LoggerOptions> options, ILogWriter logWriter = null)
        {
            logWriter ??= DefaultLogWriter;

            if (PoolLoggers)
            {
                if (!_loggers.ContainsKey(category))
                {
                    _loggers.Add(category, new VLogger(category, logWriter, options, new Lazy<IErrorMitigator>(()=>ErrorMitigator)));
                }

                return _loggers[category];
            }
            else
            {
                return new VLogger(category, logWriter, options, new Lazy<IErrorMitigator>(()=>ErrorMitigator));
            }
        }
    }
}
