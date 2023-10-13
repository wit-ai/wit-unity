/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.WitAi.TTS.Data
{
    /// <summary>
    /// An audio event with string data for specific audio phoneme sound
    /// at a specified sample offset
    /// </summary>
    [Serializable]
    public class TTSPhonemeEvent : TTSStringEvent {}
}
