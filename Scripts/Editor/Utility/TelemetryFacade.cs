/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if !VSDK_TELEMETRY_AVAILABLE
using System.Collections.Generic;

namespace Meta.Voice.TelemetryUtilities
{
    /// <summary>
    /// This is a No-Op stub for telemetry when it's not available.
    /// </summary>
    public class Telemetry
    {
        internal static TelemetryLogLevel LogLevel
        {
            get
            {
                return TelemetryLogLevel.Off;
            }
            set
            {
                // Ignore the request. Telemetry can't be turned on.
            }
        }

        internal static void SetConsentStatus(bool consentProvided)
        {
        }

        internal static int StartEvent(TelemetryEventId eventId)
        {
            return -1;
        }

        public static void LogInstantEvent(TelemetryEventId eventId,
            Dictionary<AnnotationKey, string> annotations = null)
        {
        }

        internal static void AnnotateEvent(int instanceKey, AnnotationKey key,
            string value)
        {
        }

        internal static void AnnotateCurrentSession(AnnotationKey key, string value)
        {
        }

        internal static void EndEvent(int instanceKey, ResultType result)
        {
        }

        internal static void EndEventWithFailure(int instanceKey, string error = null)
        {
        }

        /// <summary>
        /// The result of an event.
        /// </summary>
        public enum ResultType : short
        {
            Success = 2,
            Failure = 3,
            Cancel = 4
        }

        /// <summary>
        /// Mock event IDs.
        /// </summary>
        public enum TelemetryEventId
        {
            Unknown = 0,
            Session = 0,
            SupplyToken = 0,
            CheckAutoTrain = 0,
            AutoTrain = 0,
            ToggleCheckbox = 0,
            SyncEntities = 0,
            ClickButton = 0,
            AssignIntentMatcherInInspector = 0,
            SelectOption = 0,
            GenerateManifest = 0,
            LoadManifest = 0,
            NavigateToCodeFromInspector = 0,
            OpenUi = 0,
        }

        /// <summary>
        /// The annotation keys used for the key-value annotations.
        /// </summary>
        public enum AnnotationKey
        {
            CompatibleSignatures,
            IncompatibleSignatures,
            IsAvailable,
            ControlId,
            Value,
            Type,
            Unknown,
            UnrecognizedEvent,
            UnrecognizedAnnotationKey,
            EnvironmentId,
            SessionId,
            StartTimeStamp,
            EndTimeStamp,
            Error,
            PageId,
            WitSdkVersion,
            WitApiVersion
        }
    }

    internal enum TelemetryLogLevel
    {
        // No logging whatsoever
        Off
    }
}
#endif
