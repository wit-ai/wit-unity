/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Meta.WitAi;

namespace Lib.Wit.Runtime.Utilities.Logging
{
    internal class VLogger : IVLogger
    {
        private readonly Dictionary<int, LogEntry> _logEntries = new Dictionary<int, LogEntry>();
        private int _nextSequenceId = 1;
        private static readonly ThreadLocal<string> CorrelationIDThreadLocal = new ThreadLocal<string>();

        /// <inheritdoc/>
        public CorrelationID CorrelationID
        {
            get
            {
                if (!CorrelationIDThreadLocal.IsValueCreated)
                {
                    CorrelationIDThreadLocal.Value = Guid.NewGuid().ToString();
                }

                return CorrelationIDThreadLocal.Value;
            }
            set => CorrelationIDThreadLocal.Value = value;
        }

        private readonly string _category;

        public VLogger(string category)
        {
            _category = category;
        }

        /// <inheritdoc/>
        public void Verbose(string message, params object [] parameters)
        {
            Log(VLogLevel.Verbose, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLogLevel.Verbose, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(string message, params object [] parameters)
        {
            Log(VLogLevel.Info, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLogLevel.Info, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object [] parameters)
        {
            Log(VLogLevel.Log, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLogLevel.Log, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object [] parameters)
        {
            Log(VLogLevel.Warning, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLogLevel.Error, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(string message, params object [] parameters)
        {
            Log(VLogLevel.Error, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object [] parameters)
        {
            Log(VLogLevel.Warning, correlationId, message, parameters);
        }

        private void Log(VLogLevel verbosity, CorrelationID correlationID, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationID, message, parameters);
            Write(logEntry);
        }

        /// <inheritdoc/>
        public LogScope Scope(VLogLevel verbosity, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, CorrelationID, message, parameters);
        }

        /// <inheritdoc/>
        public LogScope Scope(VLogLevel verbosity, CorrelationID correlationId, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(VLogLevel verbosity, CorrelationID correlationId, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, correlationId, message, parameters);
            _logEntries.Add(_nextSequenceId, logEntry);

            Write(logEntry, "Started: ");

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public int Start(VLogLevel verbosity, string message, params object[] parameters)
        {
            var logEntry = new LogEntry(_category, verbosity, CorrelationID, message, parameters);
            _logEntries.Add(_nextSequenceId, logEntry);

            Write(logEntry, "Started: ");

            return _nextSequenceId++;
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            if (!_logEntries.ContainsKey(sequenceId))
            {
                Error("Attempted to end a scope that was not started. Scope ID: {0}", sequenceId);
                return;
            }

            var openingEntry = _logEntries[sequenceId];
            Write(openingEntry, "Finished: ");

            _logEntries.Remove(sequenceId);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            foreach (var logEntry in _logEntries)
            {
                Write(logEntry.Value);
            }
        }

        private void Write(LogEntry logEntry, string prefix)
        {
            UnityEngine.Debug.Log($"{prefix}{logEntry}");
        }

        private void Write(LogEntry logEntry)
        {
            UnityEngine.Debug.Log(logEntry);
        }

        private void Write(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        private struct LogEntry
        {
            public string Category { get; }
            public DateTime TimeStamp { get; }
            public string Message { get; }
            public object[] Parameters { get; }
            public CorrelationID CorrelationID { get; }
            public VLogLevel Verbosity { get; }

            public LogEntry(string category, VLogLevel verbosity, CorrelationID correlationId, string message, object [] parameters)
            {
                Category = category;
                TimeStamp = DateTime.UtcNow;
                Message = message;
                Parameters = parameters;
                Verbosity = verbosity;
                CorrelationID = correlationId;
            }

            public override string ToString()
            {
                return string.Format(Message, Parameters) + $" [{CorrelationID}]";
            }
        }
    }
}
