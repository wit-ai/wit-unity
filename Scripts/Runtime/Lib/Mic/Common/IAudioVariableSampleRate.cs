/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// An interface option for IAudioInputSource that
    /// </summary>
    public interface IAudioVariableSampleRate
    {
        /// <summary>
        /// When true, sample rate will attempt to be determined
        /// </summary>
        bool NeedsSampleRateCalculation { get; }

        /// <summary>
        /// Total ms to skip due to throttling during initial samplerate calculation
        /// </summary>
        int SkipInitialSamplesInMs { get; }
    }
}
