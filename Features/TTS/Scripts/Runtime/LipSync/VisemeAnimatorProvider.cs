/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.LipSync
{
    public interface IVisemeAnimatorProvider
    {
        /// <summary>
        /// The last viseme passed during audio playback
        /// </summary>
        Viseme LastViseme { get; }

        /// <summary>
        /// Fired when entering or passing a sample with this specified viseme.
        /// </summary>
        VisemeChangedEvent OnVisemeStarted { get; }

        /// <summary>
        /// Fired when entering or passing a new sample with a different specified viseme.
        /// </summary>
        VisemeChangedEvent OnVisemeFinished { get; }

        /// <summary>
        /// Fired once per frame with the previous viseme and next viseme as well as
        /// a percentage of the current frame in between each viseme.
        /// </summary>
        VisemeLerpEvent OnVisemeLerp { get; }
    }
}
