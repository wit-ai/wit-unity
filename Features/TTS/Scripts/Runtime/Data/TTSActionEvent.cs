/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Json;

namespace Meta.WitAi.TTS.Data
{
    /// <summary>
    /// An audio event with string data for words
    /// </summary>
    [Serializable]
    public class TTSActionEvent : TTSStringEvent
    {
        private WitResponseNode response;

        public static readonly WitResponseNode EMPTY_RESPONSE = new WitResponseNode();

        public WitResponseNode Response {
            get
            {
                if (string.IsNullOrEmpty(Data)) return EMPTY_RESPONSE;
                if (null == response) response = WitResponseNode.Parse(Data);
                return response;
            }
        }
    }
}
