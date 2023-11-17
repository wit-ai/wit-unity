/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Serialization;

namespace Meta.WitAi.TTS.Data
{
    public abstract class TTSVoiceSettings
    {
        [Tooltip("A unique id used for linking these voice settings to a TTS Speaker")]
        [FormerlySerializedAs("settingsID")]
        public string SettingsId;

        [Tooltip("Text that is added to the front of any TTS request using this voice setting")]
        [TextArea]
        public string PrependedText;

        [TextArea]
        [Tooltip("Text that is added to the end of any TTS request using this voice setting")]
        public string AppendedText;

        /// <summary>
        /// Encodes all setting parameters into a dictionary for transmission
        /// </summary>
        public abstract Dictionary<string, string> Encode();

        /// <summary>
        /// Decodes all setting parameters from a provided json node
        /// </summary>
        public abstract void Decode(WitResponseNode responseNode);
    }
}
