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
    /// An audio event with viseme data
    /// </summary>
    [Serializable]
    public class TTSVisemeEvent : TTSEvent<Viseme> {}
}
