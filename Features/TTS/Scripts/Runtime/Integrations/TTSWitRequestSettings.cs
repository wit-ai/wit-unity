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
        [SerializeField] internal WitConfiguration _configuration;

        /// <summary>
        /// The desired audio type from wit
        /// </summary>
        public TTSWitAudioType audioType;

        /// <summary>
        /// Whether or not audio should be streamed from wit if possible
        /// </summary>
        public bool audioStream;

        /// <summary>
        /// Whether or not events should be requested along with audio data
        /// </summary>
        public bool useEvents;
    }
}
