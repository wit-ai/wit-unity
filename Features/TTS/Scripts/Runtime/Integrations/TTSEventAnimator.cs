/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// A base class for performing audio event based animations
    /// </summary>
    public abstract class TTSEventAnimator<TEvent, TData> : MonoBehaviour
        where TEvent : TTSEvent<TData>
    {
        /// <summary>
        /// The audio event player being used for playback
        /// </summary>
        public ITTSEventPlayer Player => _player as ITTSEventPlayer;
        [SerializeField, ObjectType(typeof(ITTSEventPlayer))]
        private UnityEngine.Object _player;

        /// <summary>
        /// Whether easing should be skipped & values should just be set
        /// </summary>
        public bool easeIgnored = false;

        /// <summary>
        /// The easing curve to be used for event lerping
        /// </summary>
        public AnimationCurve easeCurve = AnimationCurve.Linear(0, 0, 1, 1);

        /// <summary>
        /// The current TTS events being used for animation
        /// </summary>
        public List<TEvent> Events { get; private set; }

        // Local sample counter
        private int _sample = -1;
        // Event placeholders
        private TEvent _minEvent;
        private TEvent _maxEvent;

        /// <summary>
        /// On awake, find IAudioEventPlayer for playback handling
        /// </summary>
        protected virtual void Awake()
        {
            // Find player if possible
            if (Player == null)
            {
                _player = gameObject.GetComponentInChildren(typeof(ITTSEventPlayer));
                if (Player == null)
                {
                    VLog.E($"No Player found for {GetType().Name}");
                }
            }
            // Generate min & max events
            _minEvent = Activator.CreateInstance<TEvent>();
            _maxEvent = Activator.CreateInstance<TEvent>();
        }

        /// <summary>
        /// Add animation updated methods
        /// </summary>
        protected virtual void OnEnable()
        {
            // Add delegates
            if (Player != null)
            {
                Player.OnEventsUpdated += OnEventsUpdated;
                Player.OnSampleUpdated += OnSampleUpdated;
            }
            // Refreshes event list & sample
            RefreshEvents(true);
        }

        /// <summary>
        /// Current events updated, refresh event list if needed
        /// </summary>
        protected virtual void OnEventsUpdated(TTSEventContainer eventContainer)
        {
            RefreshEvents(false);
        }

        /// <summary>
        /// Current sample updated, refresh event if needed
        /// </summary>
        protected virtual void OnSampleUpdated(int newSample)
        {
            RefreshSample(false);
        }

        /// <summary>
        /// Remove animation updated methods
        /// </summary>
        protected virtual void OnDisable()
        {
            // Remove delegates
            if (Player != null)
            {
                Player.OnEventsUpdated -= OnEventsUpdated;
                Player.OnSampleUpdated -= OnSampleUpdated;
            }
        }

        /// <summary>
        /// Updates animation event list
        /// </summary>
        protected virtual void RefreshEvents(bool force)
        {
            // Get events
            var events = GetEvents();

            // Ignore if desired
            if (!force && !ShouldUpdateEvents(events))
            {
                return;
            }

            // Apply events
            Events = events;

            // Refresh sample
            RefreshSample(true);
        }

        /// <summary>
        /// Getter method for events, can be overwritten if needed
        /// </summary>
        protected virtual List<TEvent> GetEvents() => Player?.CurrentEvents?.GetEvents<TEvent>();

        /// <summary>
        /// Getter method for events, can be overwritten if needed
        /// </summary>
        protected virtual bool ShouldUpdateEvents(List<TEvent> newEvents) =>
            Events == null || (newEvents != Events && newEvents != null && newEvents.Count != Events.Count);

        /// <summary>
        /// Updates currently set sample and lerps between events as specified
        /// </summary>
        /// <param name="force">If true, will force the sample set & lerping between events.
        /// If false the sample will only be set if it has changed.</param>
        protected virtual void RefreshSample(bool force)
        {
            // Get sample
            var sample = Player == null || Events == null ? -1 : Player.CurrentSample;
            if (!force && sample == _sample)
            {
                return;
            }

            // Store current sample
            _sample = sample;

            // Set to min event
            if (_sample < 0)
            {
                LerpEvent(_minEvent, _minEvent, 0f);
                return;
            }

            // Get to & from event
            GetSampleEvents(_sample, out var fromEvent, out var toEvent);

            // Determine percentage from 0 to 1
            float percentage = GetSampleEventProgress(sample, fromEvent.SampleOffset, toEvent.SampleOffset);

            // Lerp
            LerpEvent(fromEvent, toEvent, percentage);
        }

        // Gets the events before and after a sample
        private void GetSampleEvents(int sample, out TEvent previousEvent, out TEvent nextEvent)
        {
            // Set from & to event to the default for now
            previousEvent = _minEvent;
            nextEvent = _maxEvent;
            nextEvent.offset = Player == null ? int.MaxValue : Player.TotalSamples;

            // Ignore if invalid sample
            foreach (var animEvent in Events)
            {
                if (animEvent.SampleOffset == sample)
                {
                    previousEvent = animEvent;
                    nextEvent = animEvent;
                    break;
                }
                // If less, check if could be closest from event
                if (animEvent.SampleOffset < sample)
                {
                    if (animEvent.SampleOffset >= previousEvent.SampleOffset)
                    {
                        previousEvent = animEvent;
                    }
                }
                // If more, check if could be closest to event
                else
                {
                    if (animEvent.SampleOffset <= nextEvent.SampleOffset)
                    {
                        nextEvent = animEvent;
                    }
                }
            }
        }

        // Get the progress from 0 to 1 from the previous event to the next event
        private float GetSampleEventProgress(int sample, int previousEventSample, int nextEventSample)
        {
            // Start with 0
            float progress = 0f;
            // Only determine progress if not the same
            if (previousEventSample != nextEventSample)
            {
                progress = Mathf.Clamp01((float)(sample - previousEventSample) / (nextEventSample - previousEventSample));
            }
            // Ignore ease altogether
            if (easeIgnored)
            {
                progress = progress >= 1 ? 1f : 0f;
            }
            // Ease using the ease curve
            else
            {
                progress = easeCurve.Evaluate(progress);
            }
            return progress;
        }

        /// <summary>
        /// Performs a lerp from an event to another event
        /// </summary>
        /// <param name="fromEvent">The event starting point</param>
        /// <param name="toEvent">The event ending point</param>
        /// <param name="percentage">0 to 1 value with 0 meaning previous event & 1 meaning next event</param>
        protected abstract void LerpEvent(TEvent fromEvent, TEvent toEvent, float percentage);
    }
}
