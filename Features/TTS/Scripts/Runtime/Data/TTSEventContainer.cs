/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi.Json;
using UnityEngine.Scripting;

namespace Meta.WitAi.TTS.Data
{
    /// <summary>
    /// Callback method that includes audio event animation data
    /// </summary>
    public delegate void TTSEventContainerDelegate(TTSEventContainer container);

    /// <summary>
    /// A class that contains all tts events returned from the service
    /// </summary>
    [Serializable]
    public class TTSEventContainer
    {
        /// <summary>
        /// All events provided in a access safe list
        /// </summary>
        public List<ITTSEvent> Events => GetEvents<ITTSEvent>();
        /// <summary>
        /// Thread safe current event count
        /// </summary>
        public int EventCount => _events == null ? 0 : _events.Count;
        /// <summary>
        /// All events for specified tts word start sample
        /// </summary>
        public List<TTSWordEvent> WordEvents => GetEvents<TTSWordEvent>();
        /// <summary>
        /// All events for specified tts mouth position start sample
        /// </summary>
        public List<TTSVisemeEvent> VisemeEvents => GetEvents<TTSVisemeEvent>();

        /// <summary>
        /// Delegate for event updates
        /// </summary>
        public event TTSEventContainerDelegate OnEventsUpdated;

        // Current event list, not thread safe due to appending on background thread
        private List<ITTSEvent> _events = new List<ITTSEvent>();

        // Json response keys
        internal const string EVENT_TYPE_KEY = "type";
        internal const string EVENT_WORD_TYPE_KEY = "WORD";
        internal const string EVENT_VISEME_TYPE_KEY = "VISEME";
        internal const string EVENT_PHONEME_TYPE_KEY = "PHONE";

        /// <summary>
        /// Getters for a list of events based on keys
        /// </summary>
        /// <param name="eventTypeKey">The type key for the specified event</param>
        /// <typeparam name="TEvent">The event type to be returned</typeparam>
        public List<TEvent> GetEvents<TEvent>(string eventTypeKey = null)
            where TEvent : ITTSEvent
        {
            // Get new list
            var results = new List<TEvent>();
            // Cannot use foreach due to bg thread access
            for (int e = 0; e < EventCount; e++)
            {
                // If desired type & matching event type (if provided) then return event
                if (_events[e] is TEvent ttsEvent && (string.IsNullOrEmpty(eventTypeKey) || eventTypeKey.Equals(ttsEvent.EventType)))
                {
                    results.Add(ttsEvent);
                }
            }
            // Return results
            return results;
        }

        /// <summary>
        /// Decodes and appends an event included in multiple json nodes.
        /// </summary>
        public void AddEvents(List<WitResponseNode> events)
        {
            // Ignore if null
            if (events == null || events.Count == 0)
            {
                return;
            }

            // Decode events
            var count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                ITTSEvent ttsEvent = DecodeEvent(events[i]);
                if (ttsEvent != null)
                {
                    _events.Add(ttsEvent);
                    count++;
                }
            }

            // Stop if none were added
            if (count == 0)
            {
                return;
            }

            // Raise events changed on main thread
            _ = ThreadUtility.CallOnMainThread(RaiseEventsUpdated);
        }

        // Decodes event based on switch statement
        private ITTSEvent DecodeEvent(WitResponseNode eventNode)
        {
            try
            {
                switch (eventNode[EVENT_TYPE_KEY].Value)
                {
                    case EVENT_WORD_TYPE_KEY:
                        return JsonConvert.DeserializeObject<TTSWordEvent>(eventNode);
                    case EVENT_VISEME_TYPE_KEY:
                        return JsonConvert.DeserializeObject<TTSVisemeEvent>(eventNode);
                    case EVENT_PHONEME_TYPE_KEY:
                        return JsonConvert.DeserializeObject<TTSPhonemeEvent>(eventNode);
                    default:
                        return JsonConvert.DeserializeObject<TTSStringEvent>(eventNode);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Callback for events updated
        [Preserve]
        private void RaiseEventsUpdated() => OnEventsUpdated?.Invoke(this);
    }
}
