/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.Interfaces
{
    /// <summary>
    /// Delegate that receives the latest sample index on sample change
    /// </summary>
    public delegate void TTSEventSampleDelegate(int newSample);

    /// <summary>
    /// An interface to be implemented for any script that can perform audio event playback
    /// across the lifecycle of an audio file.
    /// </summary>
    public interface ITTSEventPlayer
    {
        /// <summary>
        /// The current number of elapsed samples for the playing tts audio data
        /// </summary>
        int ElapsedSamples { get; }

        /// <summary>
        /// The total samples available for the events
        /// </summary>
        int TotalSamples { get; }

        /// <summary>
        /// The callback following a change to the current sample
        /// </summary>
        TTSEventSampleDelegate OnSampleUpdated { get; set; }

        /// <summary>
        /// The current TTS events if applicable
        /// </summary>
        TTSEventContainer CurrentEvents { get; }

        /// <summary>
        /// The callback following a event update
        /// </summary>
        TTSEventContainerDelegate OnEventsUpdated { get; set; }
    }
}
