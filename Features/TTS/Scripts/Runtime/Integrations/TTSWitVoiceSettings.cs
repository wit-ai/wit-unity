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
        /// The unique id that can be used to represent a specific voice setting
        /// </summary>
        public override string UniqueId
        {
            get
            {
                if (string.IsNullOrEmpty(_uniqueId))
                {
                    RefreshUniqueId();
                }
                return _uniqueId;
            }
        }
        private string _uniqueId;

        /// <summary>
        /// Refreshes the unique voice id
        /// </summary>
        public void RefreshUniqueId()
        {
            _uniqueId = string.Format("{0}|{1}|{2:00}|{3:00}",
                voice,
                style,
                speed,
                pitch);
        }

        /// <summary>
        /// Gets or generates a dictionary of all web service url request keys and values.
        /// That would generate a tts request with this voice setting.
        /// </summary>
        public override Dictionary<string, string> EncodedValues
        {
            get
            {
                if (_encoded.Keys.Count == 0)
                {
                    RefreshEncodedValues();
                }
                return _encoded;
            }
        }
        private Dictionary<string, string> _encoded = new Dictionary<string, string>();

        /// <summary>
        /// Encodes all setting parameters into a dictionary for transmission
        /// </summary>
        public void RefreshEncodedValues()
        {
            // Clear dictionary
            _encoded.Clear();

            // Use default if voice or style is empty
            _encoded[WitConstants.TTS_VOICE] = string.IsNullOrEmpty(voice) ? WitConstants.TTS_VOICE_DEFAULT : voice;
            _encoded[WitConstants.TTS_STYLE] = string.IsNullOrEmpty(style) ? WitConstants.TTS_STYLE_DEFAULT : style;

            // Clamp speed & don't send if it matches default
            int val = Mathf.Clamp(speed, WitConstants.TTS_SPEED_MIN, WitConstants.TTS_SPEED_MAX);
            if (val != WitConstants.TTS_SPEED_DEFAULT)
            {
                _encoded[WitConstants.TTS_SPEED] = val.ToString();
            }
            // Clamp pitch & don't send if it matches
            val = Mathf.Clamp(pitch, WitConstants.TTS_PITCH_MIN, WitConstants.TTS_PITCH_MAX);
            if (val != WitConstants.TTS_PITCH_DEFAULT)
            {
                _encoded[WitConstants.TTS_PITCH] = val.ToString();
            }
        }

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
            var obj = responseNode?.AsObject;
            return obj != null && obj.HasChild(WitConstants.ENDPOINT_TTS_PARAM) && obj.HasChild(WitConstants.TTS_VOICE);
        }

        /// <summary>
        /// Serialize all data using the encoded values
        /// </summary>
        public override bool SerializeObject(WitResponseClass jsonObject)
        {
            RefreshEncodedValues();
            var encoded = EncodedValues;
            if (encoded == null)
            {
                return false;
            }
            foreach (var keyVal in encoded)
            {
                jsonObject[keyVal.Key] = new WitResponseData(keyVal.Value);
            }
            return true;
        }

        /// <summary>
        /// Decodes all setting parameters from a provided json node
        /// </summary>
        public override bool DeserializeObject(WitResponseClass jsonObject)
        {
            voice = DecodeString(jsonObject, WitConstants.TTS_VOICE, WitConstants.TTS_VOICE_DEFAULT);
            style = DecodeString(jsonObject, WitConstants.TTS_STYLE, WitConstants.TTS_STYLE_DEFAULT);
            speed = DecodeInt(jsonObject, WitConstants.TTS_SPEED, WitConstants.TTS_SPEED_DEFAULT, WitConstants.TTS_SPEED_MIN, WitConstants.TTS_SPEED_MAX);
            pitch = DecodeInt(jsonObject, WitConstants.TTS_PITCH, WitConstants.TTS_PITCH_DEFAULT, WitConstants.TTS_PITCH_MIN, WitConstants.TTS_PITCH_MAX);
            RefreshUniqueId();
            RefreshEncodedValues();
            SettingsId = UniqueId;
            return !string.IsNullOrEmpty(voice);
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
