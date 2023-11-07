/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.Integrations
{
    [Serializable]
    public class TTSWitVoiceSettings : TTSVoiceSettings
    {
        /// <summary>
        /// Unique voice name
        /// </summary>
        public string voice = VOICE_DEFAULT;
        /// <summary>
        /// Voice style (ex. formal, fast)
        /// </summary>
        public string style = STYLE_DEFAULT;
        /// <summary>
        /// Text-to-speech speed percentage
        /// </summary>
        [Range(SPEED_MIN, SPEED_MAX)]
        public int speed = SPEED_DEFAULT;
        /// <summary>
        /// Text-to-speech audio pitch percentage
        /// </summary>
        [Range(PITCH_MIN, PITCH_MAX)]
        public int pitch = PITCH_DEFAULT;


        /// <summary>
        /// Default voice name used if no voice is provided
        /// </summary>
        private const string VOICE_DEFAULT = "Charlie";
        /// <summary>
        /// Default style used if no style is provided
        /// </summary>
        private const string STYLE_DEFAULT = "default";

        /// <summary>
        /// Minimum speed supported by the endpoint (50%)
        /// </summary>
        public const int SPEED_MIN = 50;
        /// <summary>
        /// Default speed used if no speed is provided
        /// </summary>
        private const int SPEED_DEFAULT = 100;
        /// <summary>
        /// Maximum speed supported by the endpoint (200%)
        /// </summary>
        public const int SPEED_MAX = 200;

        /// <summary>
        /// Minimum pitch supported by the endpoint (25%)
        /// </summary>
        public const int PITCH_MIN = 25;
        /// <summary>
        /// Default pitch used if no speed is provided (100%)
        /// </summary>
        private const int PITCH_DEFAULT = 100;
        /// <summary>
        /// Maximum pitch supported by the endpoint (200%)
        /// </summary>
        public const int PITCH_MAX = 200;

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

        /// <summary>
        /// Encodes all setting parameters into a dictionary for transmission
        /// </summary>
        public override Dictionary<string, string> Encode()
        {
            // Generated data dictionary
            Dictionary<string, string> data = new Dictionary<string, string>();

            // Use default if voice or style is empty
            data["voice"] = string.IsNullOrEmpty(voice) ? VOICE_DEFAULT : voice;
            data["style"] = string.IsNullOrEmpty(style) ? STYLE_DEFAULT : style;

            // Clamp speed & don't send if it matches default
            int val = Mathf.Clamp(speed, SPEED_MIN, SPEED_MAX);
            if (val != SPEED_DEFAULT)
            {
                data["speed"] = val.ToString();
            }
            // Clamp pitch & don't send if it matches
            val = Mathf.Clamp(pitch, PITCH_MIN, PITCH_MAX);
            if (val != PITCH_DEFAULT)
            {
                data["pitch"] = val.ToString();
            }

            // Return data
            return data;
        }
    }
}
