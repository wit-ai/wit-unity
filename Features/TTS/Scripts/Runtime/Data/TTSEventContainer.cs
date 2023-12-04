/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi.Json;

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

        // Current event list, not thread safe due to appending on background thread
        private List<ITTSEvent> _events = new List<ITTSEvent>();
        // Lock to ensure appending of events does not conflict
        private List<string> _decoding = new List<string>();

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
        /// Delegate for event updates
        /// </summary>
        public event TTSEventContainerDelegate OnEventsUpdated;

        /// <summary>
        /// Method for applying event animation data
        /// </summary>
        public void AppendEvents(string eventJson)
        {
            // Ignore without callback or event json
            if (string.IsNullOrEmpty(eventJson))
            {
                return;
            }

            // Add to decode list
            _decoding.Add(eventJson);

            // If first, begin decoding the rest
            if (_decoding.Count == 1)
            {
                #pragma warning disable CS4014
                DecodeEventsAsync();
                #pragma warning restore CS4014
            }
        }

        // Decode text async
        private async Task DecodeEventsAsync()
        {
            // Get new events list
            List<ITTSEvent> newEvents = new List<ITTSEvent>();

            // Continue decoding while events exist
            while (_decoding.Count > 0)
            {
                // Get event json
                string eventJson = _decoding[0];

                // Deserialize json
                WitResponseNode decodedEvents = await JsonConvert.DeserializeTokenAsync(eventJson);
                if (decodedEvents == null)
                {
                    VLog.W(GetType().Name, $"TTS Audio Events Decode Failed\n{eventJson}\n");
                }
                else
                {
                    int index = 0;
                    foreach (var eventNode in decodedEvents.AsArray.Childs)
                    {
                        ITTSEvent ttsEvent = await DecodeEventAsync(eventNode);
                        if (ttsEvent == null)
                        {
                            VLog.W(GetType().Name, $"TTS Audio Event[{index}] Decode Failed\n{eventJson}\n");
                        }
                        else
                        {
                            newEvents.Add(ttsEvent);
                        }
                        index++;
                    }
                }

                // Remove decoded event
                _decoding.RemoveAt(0);
            }

            // Events added
            if (newEvents.Count > 0)
            {
                // Add events
                _events.AddRange(newEvents);

                // Events updated callback
                OnEventsUpdated?.Invoke(this);
            }
        }

        // Decodes event based on switch statement
        private async Task<ITTSEvent> DecodeEventAsync(WitResponseNode eventNode)
        {
            try
            {
                switch (eventNode[EVENT_TYPE_KEY].Value)
                {
                    case EVENT_WORD_TYPE_KEY:
                        return await JsonConvert.DeserializeObjectAsync<TTSWordEvent>(eventNode);
                    case EVENT_VISEME_TYPE_KEY:
                        return await JsonConvert.DeserializeObjectAsync<TTSVisemeEvent>(eventNode);
                    case EVENT_PHONEME_TYPE_KEY:
                        return await JsonConvert.DeserializeObjectAsync<TTSPhonemeEvent>(eventNode);
                    default:
                        return await JsonConvert.DeserializeObjectAsync<TTSStringEvent>(eventNode);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
