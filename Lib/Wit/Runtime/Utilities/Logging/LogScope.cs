/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Meta.Voice.TelemetryUtilities.PerformanceTracing;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A logging scope to be used in "using" blocks.
    /// </summary>
    public class LogScope : ILogScope
    {
        private readonly ICoreLogger _logger;
        private readonly int _sequenceId;
        private ConcurrentDictionary<int, string> _activeSamples = new ConcurrentDictionary<int, string>();

        /// <summary>
        /// Constructs a logging scope to be used in "using" blocks.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="verbosity">The verbosity.</param>
        /// <param name="correlationID">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        public LogScope(ICoreLogger logger, VLoggerVerbosity verbosity, CorrelationID correlationID, string message, object [] parameters)
        {
            CorrelationID = correlationID;
            _logger = logger;
            _sequenceId = _logger.Start(correlationID, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public CorrelationID CorrelationID { get; set; }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Verbose, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Verbose, message, parameters);
        }

        public void Verbose(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _logger.Verbose(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Info, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null, string memberName = "",
            string sourceFilePath = "", int sourceLineNumber = 0)
        {
            _logger.Info(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null, string memberName = "",
            string sourceFilePath = "", int sourceLineNumber = 0)
        {
            _logger.Debug(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Debug, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Warning, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object [] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Error, message, errorCode, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object [] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Error, message, errorCode, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, string message, params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Error, exception, KnownErrorCode.Unknown, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message, params object[] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Verbose, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, string message = "", params object[] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Error, exception, KnownErrorCode.Unknown);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            _logger.Log(correlationId, VLoggerVerbosity.Verbose, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, string message, params object[] parameters)
        {
            _logger.Log(correlationId, VLoggerVerbosity.Error, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(string message, params object[] parameters)
        {
            _logger.Log(CorrelationID, VLoggerVerbosity.Error, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            Correlate(correlationId, CorrelationID);
            var sequenceId = _logger.Start(correlationId, verbosity, message, parameters);
            StartProfiling(sequenceId, message);
            return sequenceId;
        }

        /// <inheritdoc/>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            VsdkProfiler.BeginSample(message);
            var sequenceId = _logger.Start(verbosity, message, parameters);
            StartProfiling(sequenceId, message);
            return sequenceId;
        }

        /// <summary>
        /// If profiling is enabled starts profiling for the given sequence id
        /// </summary>
        /// <param name="sequenceId">The sequence ID to start sampling</param>
        /// <param name="message">The message used by the profiler/logger to associate the sample</param>
        private void StartProfiling(int sequenceId, string message)
        {
            if (VsdkProfiler.profilingEnabled)
            {
                VsdkProfiler.BeginSample(message);
                _activeSamples[sequenceId] = message;
            }
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            if (VsdkProfiler.profilingEnabled)
            {
                if (_activeSamples.TryRemove(sequenceId, out var message))
                {
                    VsdkProfiler.EndSample(message);
                }
            }

            _logger.End(sequenceId);
        }

        public void Correlate(CorrelationID newCorrelationId, CorrelationID rootCorrelationId)
        {
            _logger.Correlate(newCorrelationId, rootCorrelationId);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, message, parameters);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, Exception exception,
            ErrorCode errorCode,
            string message, params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, exception, errorCode, message, parameters);
        }

        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            _logger.Log(correlationId, verbosity, errorCode, message, parameters);
        }

        /// <summary>
        /// Disposes the scope.
        /// </summary>
        public void Dispose()
        {
            _logger.End(_sequenceId);
        }
    }
}
