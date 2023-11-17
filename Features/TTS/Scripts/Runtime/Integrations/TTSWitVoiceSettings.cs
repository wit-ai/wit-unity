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
        public string voice = WitConstants.TTS_VOICE_DEFAULT;
        /// <summary>
        /// Voice style (ex. formal, fast)
        /// </summary>
        public string style = WitConstants.TTS_STYLE_DEFAULT;
        /// <summary>
        /// Text-to-speech speed percentage
        /// </summary>
        [Range(WitConstants.TTS_SPEED_MIN, WitConstants.TTS_SPEED_MAX)]
        public int speed = WitConstants.TTS_SPEED_DEFAULT;
        /// <summary>
        /// Text-to-speech audio pitch percentage
        /// </summary>
        [Range(WitConstants.TTS_PITCH_MIN, WitConstants.TTS_PITCH_MAX)]
        public int pitch = WitConstants.TTS_PITCH_DEFAULT;

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
            data[WitConstants.TTS_VOICE] = string.IsNullOrEmpty(voice) ? WitConstants.TTS_VOICE_DEFAULT : voice;
            data[WitConstants.TTS_STYLE] = string.IsNullOrEmpty(style) ? WitConstants.TTS_STYLE_DEFAULT : style;

            // Clamp speed & don't send if it matches default
            int val = Mathf.Clamp(speed, WitConstants.TTS_SPEED_MIN, WitConstants.TTS_SPEED_MAX);
            if (val != WitConstants.TTS_SPEED_DEFAULT)
            {
                data[WitConstants.TTS_SPEED] = val.ToString();
            }
            // Clamp pitch & don't send if it matches
            val = Mathf.Clamp(pitch, WitConstants.TTS_PITCH_MIN, WitConstants.TTS_PITCH_MAX);
            if (val != WitConstants.TTS_PITCH_DEFAULT)
            {
                data[WitConstants.TTS_PITCH] = val.ToString();
            }

            // Return data
            return data;
        }

        /// <summary>
        /// Decodes all setting parameters from a provided json node
        /// </summary>
        public override void Decode(WitResponseNode responseNode)
        {
            var responseClass = responseNode.AsObject;
            voice = DecodeString(responseClass, WitConstants.TTS_VOICE, WitConstants.TTS_VOICE_DEFAULT);
            style = DecodeString(responseClass, WitConstants.TTS_STYLE, WitConstants.TTS_STYLE_DEFAULT);
            speed = DecodeInt(responseClass, WitConstants.TTS_SPEED, WitConstants.TTS_SPEED_DEFAULT, WitConstants.TTS_SPEED_MIN, WitConstants.TTS_SPEED_MAX);
            pitch = DecodeInt(responseClass, WitConstants.TTS_PITCH, WitConstants.TTS_PITCH_DEFAULT, WitConstants.TTS_PITCH_MIN, WitConstants.TTS_PITCH_MAX);
        }

        // Decodes a string if possible
        private string DecodeString(WitResponseClass responseClass, string id, string defaultValue)
        {
            if (responseClass.HasChild(id))
            {
                return responseClass[id];
            }
            return defaultValue;
        }

        // Decodes an int if possible
        private int DecodeInt(WitResponseClass responseClass, string id, int defaultValue, int minValue, int maxValue)
        {
            if (responseClass.HasChild(id))
            {
                return Mathf.Clamp(responseClass[id].AsInt, minValue, maxValue);
            }
            return defaultValue;
        }
    }
}
