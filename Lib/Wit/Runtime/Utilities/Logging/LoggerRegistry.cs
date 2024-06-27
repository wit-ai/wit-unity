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
using System.Runtime.CompilerServices;
using Lib.Wit.Runtime.Utilities.Logging;
using UnityEditor;

namespace Meta.Voice.Logging
{
    public sealed class LoggerRegistry : ILoggerRegistry
    {
        public ILogSink LogSink { get; set; }
        public IVLoggerFactory VLoggerFactory { get; set; } = new VLoggerFactory();

        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const string EDITOR_LOG_SUPPRESSION_LEVEL_KEY = "VSDK_EDITOR_LOG_SUPPRESSION_LEVEL";
        private const string EDITOR_LOG_STACKTRACE_LEVEL_KEY = "VSDK_EDITOR_LOG_STACKTRACE_LEVEL";

        public LoggerOptions Options { get; }

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
        public static ILoggerRegistry Instance { get; } = new LoggerRegistry();

        /// <inheritdoc/>
        public IEnumerable<IVLogger> AllLoggers => _loggers.Values;

        /// <summary>
        /// A private constructor to prevent instantiation of this class.
        /// </summary>
        internal LoggerRegistry()
        {
            Options = new LoggerOptions(VLoggerVerbosity.Warning, VLoggerVerbosity.Verbose, VLoggerVerbosity.Error);
            ILogWriter defaultLogWriter = new UnityLogWriter();
            LogSink = new LogSink(defaultLogWriter, Options);
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
            Instance.LogSuppressionLevel = suppressionLevel;

            var stacktraceLogLevelString = EditorPrefs.GetString(EDITOR_LOG_STACKTRACE_LEVEL_KEY, Instance.Options.StackTraceLevel.ToString());
            Enum.TryParse(stacktraceLogLevelString, out VLoggerVerbosity stacktraceLogLevel);
            Instance.LogStackTraceLevel = stacktraceLogLevel;
        }
#endif

        /// <inheritdoc/>
        public IVLogger GetLogger(LogCategory logCategory, ILogSink logSink = null)
        {
            return new LazyLogger(() => GetCoreLogger(logCategory, logSink));
        }

        /// <inheritdoc/>
        /*public IVLogger GetLogger(ILogSink logSink = null)
        {
            // Send a depth of four to account for the lambda, the lazy logger, and the main logger call
            // that would have triggered the creation.
            return new LazyLogger(() => GetCoreLogger(logSink, 4));
        }*/

        /// <inheritdoc/>
        public IVLogger GetLogger(string category, ILogSink logSink)
        {
            return new LazyLogger(() => GetCoreLogger(category, logSink));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IVLogger GetCoreLogger(LogCategory category, ILogSink logSink)
        {
            return GetCoreLogger(category.ToString(), logSink);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IVLogger GetCoreLogger(string category, ILogSink logSink)
        {
            logSink ??= LogSink;
            logSink.Options = Options;

            if (PoolLoggers)
            {
                if (!_loggers.ContainsKey(category))
                {
                    _loggers.Add(category, VLoggerFactory.GetLogger(category, logSink));
                }

                return _loggers[category];
            }
            else
            {
                return VLoggerFactory.GetLogger(category, logSink);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="logSink">The sink to write to.</param>
        /// <param name="frameDepth">The number of frames to skip to find the real caller.
        /// This should be higher than 1 when called internally from the logger code.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private IVLogger GetCoreLogger(ILogSink logSink = null, int frameDepth = 1)
        {
            logSink ??= LogSink;

            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(frameDepth).FirstOrDefault(IsNonLoggingFrame);
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetCoreLogger(category, logSink);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                return GetCoreLogger(category, logSink);
            }

            category = attribute.CategoryName;

            return GetCoreLogger(category, logSink);
        }

        private bool IsNonLoggingFrame(StackFrame frame)
        {
            var method = frame?.GetMethod();
            if (method == null || method.DeclaringType == null)
            {
                return false;
            }

            if (typeof(LoggerRegistry).IsAssignableFrom(method.DeclaringType) || typeof(IVLogger).IsAssignableFrom(method.DeclaringType))
            {
                return false;
            }

            return method.DeclaringType.Namespace == null || !method.DeclaringType.Namespace.StartsWith("System");
        }
    }
}
