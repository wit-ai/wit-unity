/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Runtime.CompilerServices;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A lazy loaded logger to be used to delay the initialization of the logger until first use.
    /// </summary>
    internal class LazyLogger : Lazy<IVLogger>, IVLogger
    {
        public LazyLogger(Func<IVLogger> initializer):base(initializer)
        {
        }

        /// <inheritdoc/>
        public CorrelationID CorrelationID
        {
            get => Value.CorrelationID;
            set => Value.CorrelationID = value;
        }

        /// <inheritdoc/>
        public void Verbose(string message, params object[] parameters)
        {
            Value.Verbose(message, parameters);
        }

        /// <inheritdoc/>
        public void Verbose(CorrelationID correlationId, string message, params object[] parameters)
        {
            Value.Verbose(correlationId, message, parameters);
        }

        public void Verbose(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Value.Verbose(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Info(string message, params object[] parameters)
        {
            Value.Info(message, parameters);
        }

        /// <inheritdoc/>
        public void Info(CorrelationID correlationId, string message, params object[] parameters)
        {
            Value.Info(correlationId, message, parameters);
        }

        public void Info(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null, string memberName = "",
            string sourceFilePath = "", int sourceLineNumber = 0)
        {

            Value.Info(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Debug(string message, params object[] parameters)
        {
            Value.Debug(message, parameters);
        }

        public void Debug(string message, object p1 = null, object p2 = null, object p3 = null, object p4 = null, string memberName = "",
            string sourceFilePath = "", int sourceLineNumber = 0)
        {
            Value.Debug(message, p1, p2, p3, p4, memberName, sourceFilePath: sourceFilePath, sourceLineNumber: sourceLineNumber);
        }

        /// <inheritdoc/>
        public void Debug(CorrelationID correlationId, string message, params object[] parameters)
        {
            Value.Debug(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(CorrelationID correlationId, string message, params object[] parameters)
        {
            Value.Warning(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Warning(string message, params object[] parameters)
        {
            Value.Warning(message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, ErrorCode errorCode, string message, params object[] parameters)
        {
            Value.Error(correlationId, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(ErrorCode errorCode, string message, params object[] parameters)
        {
            Value.Error(errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            Value.Error(correlationId, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, string message, params object[] parameters)
        {
            Value.Error(correlationId, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(string message, params object[] parameters)
        {
            Value.Error(message, parameters);
        }

        /// <inheritdoc/>
        public void Error(CorrelationID correlationId, Exception exception, string message = "", params object[] parameters)
        {
            Value.Error(correlationId, exception, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, ErrorCode errorCode, string message = "", params object[] parameters)
        {
            Value.Error(exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, string message = "", params object[] parameters)
        {
            Value.Error(exception, message, parameters);
        }

        /// <inheritdoc/>
        public ILogScope Scope(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return Value.Scope(verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public ILogScope Scope(CorrelationID correlationId, VLoggerVerbosity verbosity, string message,
            params object[] parameters)
        {
            return Value.Scope(correlationId, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(CorrelationID correlationId, VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return Value.Start(correlationId, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public int Start(VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            return Value.Start(verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public void End(int sequenceId)
        {
            Value.End(sequenceId);
        }

        /// <inheritdoc/>
        public void Correlate(CorrelationID newCorrelationId, CorrelationID rootCorrelationId)
        {
            Value.Correlate(newCorrelationId, rootCorrelationId);
        }

        /// <inheritdoc/>
        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, string message, params object[] parameters)
        {
            Value.Log(correlationId, verbosity, message, parameters);
        }

        /// <inheritdoc/>
        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, Exception exception, ErrorCode errorCode,
            string message = "", params object[] parameters)
        {
            Value.Log(correlationId, verbosity, exception, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Log(CorrelationID correlationId, VLoggerVerbosity verbosity, ErrorCode errorCode, string message,
            params object[] parameters)
        {
            Value.Log(correlationId, verbosity, errorCode, message, parameters);
        }

        /// <inheritdoc/>
        public void Flush(CorrelationID correlationId)
        {
            Value.Flush(correlationId);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            Value.Flush();
        }
    }
}
