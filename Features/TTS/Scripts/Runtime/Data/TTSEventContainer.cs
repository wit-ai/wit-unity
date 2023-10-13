/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
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
        /// All events for specified tts word start sample
        /// </summary>
        public IEnumerable<TTSWordEvent> WordEvents => GetEvents<TTSWordEvent>();
        /// <summary>
        /// All events for specified tts mouth position start sample
        /// </summary>
        public IEnumerable<TTSVisemeEvent> VisemeEvents => GetEvents<TTSVisemeEvent>();

        // Current event lookup dictionary
        public List<ITTSEvent> Events { get; private set; } = new List<ITTSEvent>();
        // Lock to ensure getting/setting of events does not trample previous
        private Object _eventsLock = new Object();

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
        public IEnumerable<TEvent> GetEvents<TEvent>(string eventTypeKey = null)
            where TEvent : ITTSEvent
        {
            var results = Events.OfType<TEvent>();
            if (!string.IsNullOrEmpty(eventTypeKey))
            {
                results = results.Where((ttsEvent) => string.Equals(eventTypeKey, ttsEvent.EventType));
            }
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

            // Decode async
            #pragma warning disable CS4014
            DecodeEventsAsync(eventJson);
            #pragma warning restore CS4014
        }

        // Decode text async
        private async Task DecodeEventsAsync(string eventJson)
        {
            // Decode token
            WitResponseNode decodedEvents = await JsonConvert.DeserializeTokenAsync(eventJson);
            if (decodedEvents == null)
            {
                VLog.W(GetType().Name, $"Audio Events Decode Failed\n{eventJson}\n");
                return;
            }

            // Decode each event
            int index = 0;
            List<ITTSEvent> newEvents = new List<ITTSEvent>();
            foreach (var eventNode in decodedEvents.AsArray.Childs)
            {
                ITTSEvent ttsEvent = await DecodeEventAsync(index, eventNode);
                if (ttsEvent != null)
                {
                    newEvents.Add(ttsEvent);
                }
                index++;
            }

            // Update event list
            if (newEvents.Count > 0)
            {
                lock (_eventsLock)
                {
                    // Insert previous
                    if (Events.Count > 0)
                    {
                        newEvents.InsertRange(0, Events);
                    }
                    // Replace
                    Events = newEvents;
                }
                // Sort
                Events.Sort(CompareEvents);
                // Callback
                OnEventsUpdated?.Invoke(this);
            }
        }

        // Decodes event based on switch statement
        private async Task<ITTSEvent> DecodeEventAsync(int index, WitResponseNode eventNode)
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
            catch (Exception e)
            {
                VLog.W(GetType().Name, $"TTS Event[{index}] Decode Failed\n{e}\n\n{eventNode}\n");
                return null;
            }
        }

        // Sort compare
        protected virtual int CompareEvents(ITTSEvent event1, ITTSEvent event2) => event1.SampleOffset.CompareTo(event2.SampleOffset);
    }
}
