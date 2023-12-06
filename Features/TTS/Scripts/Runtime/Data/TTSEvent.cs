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
    /// An interface for handling lists of events
    /// </summary>
    public interface ITTSEvent
    {
        /// <summary>
        /// Getter for event type
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// Getter for audio sample offset from sample start
        /// </summary>
        int SampleOffset { get; }
    }

    /// <summary>
    /// A class that contains tts event data
    /// </summary>
    [Serializable]
    public class TTSEvent<TData> : ITTSEvent
    {
        /// <summary>
        /// The type of clip event
        /// </summary>
        [JsonProperty]
        internal string type;
        public string EventType => type;

        /// <summary>
        /// The audio sample offset from the start of the associated audio stream
        /// </summary>
        [JsonProperty]
        internal int offset;
        public int SampleOffset => offset;

        /// <summary>
        /// The data to be parsed for this clip event
        /// </summary>
        [JsonProperty]
        internal TData data;
        public TData Data => data;
    }
}
