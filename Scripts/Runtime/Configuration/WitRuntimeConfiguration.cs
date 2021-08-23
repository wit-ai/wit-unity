/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.interfaces;
using UnityEngine;
using UnityEngine.Serialization;

namespace com.facebook.witai.data
{
    [Serializable]
    public class WitRuntimeConfiguration
    {
        [Header("Keepalive")]
        [Tooltip("The minimum volume from the mic needed to keep the activation alive")]
        [SerializeField]
        public float minKeepAliveVolume = .0005f;

        [FormerlySerializedAs("minKeepAliveTime")]
        [Tooltip(
            "The amount of time in seconds an activation will be kept open after volume is under the keep alive threshold")]
        [SerializeField]
        public float minKeepAliveTimeInSeconds = 2f;

        [FormerlySerializedAs("minTranscriptionKeepAliveTime")]
        [Tooltip(
            "The amount of time in seconds an activation will be kept open after words have been detected in the live transcription")]
        [SerializeField]
        public float minTranscriptionKeepAliveTimeInSeconds = 1f;

        [Tooltip("The maximum amount of time in seconds the mic will stay active")]
        [Range(0, 10f)]
        [SerializeField]
        public float maxRecordingTime = 10;

        [Header("Sound Activation")] [SerializeField]
        public float soundWakeThreshold = .0005f;

        [Range(10, 500)] [SerializeField] public int sampleLengthInMs = 10;
        [SerializeField] public float micBufferLengthInSeconds = 1;

        [Header("Custom Transcription")]
        [Tooltip(
            "If true, the audio recorded in the activation will be sent to Wit.ai for processing. If a custom transcription provider is set and this is false, only the transcription will be sent to Wit.ai for processing")]
        [SerializeField]
        public bool sendAudioToWit = true;

        [SerializeField] public CustomTranscriptionProvider customTranscriptionProvider;
    }
}
