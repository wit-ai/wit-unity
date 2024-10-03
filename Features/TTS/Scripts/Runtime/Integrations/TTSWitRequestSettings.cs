/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using UnityEngine.Serialization;
using Meta.WitAi.Data.Configuration;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// Wit request specific settings
    /// </summary>
    [Serializable]
    public struct TTSWitRequestSettings
    {
        /// <summary>
        /// The configuration used for audio requests
        /// </summary>
        [FormerlySerializedAs("configuration")]
        [Tooltip("The configuration used for audio requests.")]
        [SerializeField] internal WitConfiguration _configuration;

        /// <summary>
        /// The desired audio type to be requested from wit
        /// </summary>
        [Tooltip("The desired audio type to be requested from wit.")]
        public TTSWitAudioType audioType;

        /// <summary>
        /// Whether or not audio should be streamed from wit if possible
        /// </summary>
        [Tooltip("Whether or not audio should be streamed from wit if possible.")]
        public bool audioStream;

        /// <summary>
        /// Whether or not events should be requested along with audio data
        /// </summary>
        [Tooltip("Whether or not events should be requested along with audio data.")]
        public bool useEvents;

        /// <summary>
        /// The amount of milliseconds required before stream times out
        /// </summary>
        [Tooltip("The amount of milliseconds required before stream times out")]
        public int audioStreamTimeoutMs;

        /// <summary>
        /// Number of audio clip streams to pool immediately on first enable.
        /// </summary>
        [Tooltip("Number of audio clip streams to pool immediately on first enable.")]
        public int audioStreamPreloadCount;

        /// <summary>
        /// The total number of seconds to be buffered in order to consider ready
        /// </summary>
        [Tooltip("The total number of seconds to be buffered in order to consider ready.")]
        public float audioReadyDuration;

        /// <summary>
        /// Maximum length of audio clip stream in seconds.
        /// </summary>
        [Tooltip("Maximum length of audio clip stream in seconds.")]
        public float audioMaxDuration;
    }
}
