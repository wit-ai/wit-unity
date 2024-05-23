/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Meta.WitAi;

namespace Meta.Voice.Logging
{
    public class LoggingContext
    {
        private readonly StackTrace _stackTrace;
        private readonly string _callSiteMemberName;
        private readonly string _callSiteSourceFilePath;
        private readonly int _callSiteSourceLineNumber;

        readonly string _workingDirectory = Directory.GetCurrentDirectory();

        internal LoggingContext(StackTrace stackTrace)
        {
            _stackTrace = stackTrace;
            _callSiteSourceLineNumber = -1;
        }

        internal LoggingContext(
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            _stackTrace = null;
            _callSiteMemberName = memberName;
            _callSiteSourceFilePath = sourceFilePath;
            _callSiteSourceLineNumber = sourceLineNumber;
        }

        public override string ToString()
        {
            return _stackTrace?.ToString();
        }

        private void AppendSingleFrame(StringBuilder sb, bool colorLogs)
        {
            if (_callSiteSourceLineNumber > 0)
            {
                sb.Append("=== STACK TRACE ===\n");
                var fileName = _callSiteSourceFilePath;
#if UNITY_EDITOR
                sb.Append($"<a href=\"{fileName}\" line=\"{_callSiteSourceLineNumber}\">[{fileName}:{_callSiteSourceLineNumber}] </a>");
#else
                sb.Append($"[{fileName}:{_callSiteSourceLineNumber}] ");
#endif

                sb.Append(colorLogs ? $"<color=#39CC8F>{_callSiteMemberName}</color>(?)\n" : $"{_callSiteMemberName}(?)\n");
            }
        }

        private void AppendFullStack(StringBuilder sb, bool colorLogs, StackFrame [] frames)
        {
            sb.Append("=== STACK TRACE ===\n");
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method.DeclaringType;
                if (declaringType == null || IsLoggingClass(method.DeclaringType) || IsSystemClass(method.DeclaringType))
                {
                    continue;
                }

                var filePath = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name}"));

                if (!string.IsNullOrEmpty(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    var relativeFilePath = filePath.Replace(_workingDirectory, "");

#if UNITY_EDITOR
                    sb.Append($"<a href=\"{relativeFilePath}\" line=\"{lineNumber}\">[{fileName}:{lineNumber}] </a>");
#else
                    sb.Append($"[{fileName}:{lineNumber}] ");
#endif
                }

                var methodName = $"{method.Name}";
                sb.Append(declaringType?.Name);
                sb.Append('.');
                sb.Append(colorLogs ? $"<color=#39CC8F>{method.Name}</color>" : $"{methodName}");
                sb.Append($"({parameters})\n");
            }
        }

        public void AppendRelevantContext(StringBuilder sb, bool colorLogs)
        {
            var frames = _stackTrace?.GetFrames();
            if (frames == null)
            {
                AppendSingleFrame(sb, colorLogs);
                return;
            }

            AppendFullStack(sb, colorLogs, frames);
        }

        public (string fileName, int lineNumber) GetCallSite()
        {
            if (_callSiteSourceLineNumber > 0)
            {
                return (_callSiteSourceFilePath, _callSiteSourceLineNumber);
            }

            if (_stackTrace == null)
            {
                return (string.Empty, 0);
            }

            for (int i = 1; i < _stackTrace.FrameCount; i++)
            {
                var stackFrame = _stackTrace.GetFrame(i);
                var method = stackFrame.GetMethod();
                if (method.DeclaringType == null || IsLoggingClass(method.DeclaringType) || IsSystemClass(method.DeclaringType))
                {
                    continue;
                }

                var callingFileName = stackFrame.GetFileName()?.Replace('\\', '/');
                var callingFileLineNumber = stackFrame.GetFileLineNumber();
                return (callingFileName, callingFileLineNumber);
            }

            return (string.Empty, 0);
        }

        private static bool IsLoggingClass(Type type)
        {
            return typeof(ICoreLogger).IsAssignableFrom(type) || typeof(ILogWriter).IsAssignableFrom(type) || type == typeof(VLog);
        }

        private static bool IsSystemClass(Type type)
        {
            var nameSpace = type.Namespace;
            if (nameSpace == null)
            {
                return false;
            }
            return nameSpace.StartsWith("Unity") ||
                   nameSpace.StartsWith("System") ||
                   nameSpace.StartsWith("Microsoft");
        }
    }
}
