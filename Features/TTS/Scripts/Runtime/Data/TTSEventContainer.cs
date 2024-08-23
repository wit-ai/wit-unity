/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        /// Accessible enumerable for all events
        /// </summary>
        public IEnumerable<ITTSEvent> Events => _events;
        /// <summary>
        /// All events for specified tts word start sample
        /// </summary>
        public IEnumerable<TTSWordEvent> WordEvents => GetEvents<TTSWordEvent>();
        /// <summary>
        /// All events for specified tts mouth position start sample
        /// </summary>
        public IEnumerable<TTSVisemeEvent> VisemeEvents => GetEvents<TTSVisemeEvent>();

        /// <summary>
        /// The currently used events
        /// </summary>
        private ConcurrentQueue<ITTSEvent> _events = new ConcurrentQueue<ITTSEvent>();

        // Json response keys
        internal const string EVENT_TYPE_KEY = "type";
        internal const string EVENT_WORD_TYPE_KEY = "WORD";
        internal const string EVENT_VISEME_TYPE_KEY = "VISEME";
        internal const string EVENT_PHONEME_TYPE_KEY = "PHONE";

        /// <summary>
        /// Callback for each added event
        /// </summary>
        public event Action<WitResponseNode> OnEventJsonAdded;
        /// <summary>
        /// Callback for each added event
        /// </summary>
        public event Action<ITTSEvent> OnEventAdded;

        /// <summary>
        /// Getters for a list of events based on keys
        /// </summary>
        /// <typeparam name="TEvent">The event type to be returned</typeparam>
        /// <param name="eventTypeKey">An optional type key for the specified event</param>
        public IEnumerable<TEvent> GetEvents<TEvent>(string eventTypeKey = null)
            where TEvent : ITTSEvent
        {
            var results = _events.Where(e => e is TEvent
                                             && (string.IsNullOrEmpty(eventTypeKey) || eventTypeKey.Equals(e.EventType)));
            return results.Select(e => (TEvent)e);
        }

        /// <summary>
        /// Decodes and appends an event included in multiple json nodes.
        /// </summary>
        public void AddEvents(IEnumerable<WitResponseNode> events)
        {
            if (events == null)
            {
                return;
            }
            foreach (var e in events)
            {
                AddEvent(e);
            }
        }

        /// <summary>
        /// Safely decodes and adds an event to the events list
        /// </summary>
        public bool AddEvent(WitResponseNode eventNode)
        {
            ITTSEvent ttsEvent = DecodeEvent(eventNode);
            if (ttsEvent == null)
            {
                return false;
            }
            _events.Enqueue(ttsEvent);
            OnEventJsonAdded?.Invoke(eventNode);
            OnEventAdded?.Invoke(ttsEvent);
            return true;
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

        /// <summary>
        /// Called frequently to determine the closest events of a specific type to the specified sample
        /// </summary>
        public void GetClosestEvents<TEvent>(int sample, ref int previousEventIndex, ref TEvent previousEvent, ref TEvent nextEvent) where TEvent : ITTSEvent
        {
            // If no previous event or sample now before previous event, start at beginning
            if (previousEvent == null || sample < previousEvent.SampleOffset)
            {
                previousEventIndex = 0;
            }

            // Iterate from previous event index
            nextEvent = default(TEvent);
            int i = 0;
            foreach (var e in _events)
            {
                if (i >= previousEventIndex && e is TEvent tEvent)
                {
                    if (sample >= tEvent.SampleOffset)
                    {
                        previousEventIndex = i;
                        previousEvent = tEvent;
                    }
                    else
                    {
                        nextEvent = tEvent;
                        break;
                    }
                }
                i++;
            }
        }
    }
}
