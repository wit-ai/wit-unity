﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Data
{
    /// <summary>
    /// An interface for providing a custom audio buffer
    /// </summary>
    public interface IAudioBufferProvider
    {
        AudioBuffer InstantiateAudioBuffer();
    }
}
