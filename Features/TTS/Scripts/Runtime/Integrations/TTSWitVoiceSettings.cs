/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.Integrations
{
    [Serializable]
    public class TTSWitVoiceSettings : TTSVoiceSettings
    {
        /// <summary>
        /// Default voice name used if no voice is provided
        /// </summary>
        public const string DEFAULT_VOICE = "Charlie";
        /// <summary>
        /// Default style used if no style is provided
        /// </summary>
        public const string DEFAULT_STYLE = "default";

        /// <summary>
        /// Unique voice name
        /// </summary>
        public string voice = DEFAULT_VOICE;
        /// <summary>
        /// Voice style (ex. formal, fast)
        /// </summary>
        public string style = DEFAULT_STYLE;
        /// <summary>
        /// Text-to-speech speed percentage
        /// </summary>
        [Range(50, 200)]
        public int speed = 100;
        /// <summary>
        /// Text-to-speech audio pitch percentage
        /// </summary>
        [Range(25, 200)]
        public int pitch = 100;

        /// <summary>
        /// Checks if request can be decoded for TTS data
        /// Example Data:
        /// {
        ///    "q": "Text to be spoken"
        ///    "voice": "Charlie
        /// }
        /// </summary>
        /// <param name="responseNode">The deserialized json data class</param>
        /// <returns>True if request can be decoded</returns>
        public static bool CanDecode(WitResponseNode responseNode)
        {
            return responseNode != null && responseNode.AsObject.HasChild(WitConstants.ENDPOINT_TTS_PARAM) && responseNode.AsObject.HasChild("voice");
        }
    }
}
