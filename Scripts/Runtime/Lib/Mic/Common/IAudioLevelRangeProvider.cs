/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Lib
{
    /// <summary>
    /// An interface for returning min and maximum audio levels
    /// </summary>
    public interface IAudioLevelRangeProvider
    {
        /// <summary>
        /// Minimum unsigned level supported (0 to 1)
        /// </summary>
        float MinAudioLevel { get; }

        /// <summary>
        /// Maximum unsigned level supported (0 to 1)
        /// </summary>
        float MaxAudioLevel { get; }
    }
}
