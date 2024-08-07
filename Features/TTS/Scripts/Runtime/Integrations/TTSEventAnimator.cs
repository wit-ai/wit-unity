/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.Voice.Logging;
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
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.TextToSpeech);

        /// <summary>
        /// The audio event player being used for playback
        /// </summary>
        public ITTSEventPlayer Player
        {
            get => _player as ITTSEventPlayer;
            set
            {
                if (value is UnityEngine.Object playerObj)
                {
                    _player = playerObj;
                }
                else
                {
                    if (value != null)
                    {
                        Logger.Error("Invalid ITTSEventPlayer type: {0}", value.GetType().Name);
                    }
                    _player = null;
                }
            }
        }
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
        /// The current TTS event container
        /// </summary>
        public TTSEventContainer EventContainer { get; private set; }

        // Local sample counter
        private int _sample = -1;
        private int _prevEventIndex;
        private TEvent _prevEvent;
        private TEvent _nextEvent;

        // Event placeholders
        private TEvent _minEvent;
        private TEvent _maxEvent;

        /// <summary>
        /// On awake, generate min and max events
        /// </summary>
        protected virtual void Awake()
        {
            _minEvent = Activator.CreateInstance<TEvent>();
            _maxEvent = Activator.CreateInstance<TEvent>();
        }

        /// <summary>
        /// Finds player if needed, adds delegates and refreshes events
        /// </summary>
        protected virtual void OnEnable()
        {
            // Find player if needed/possible
            if (Player == null)
            {
                _player = gameObject.GetComponentInChildren(typeof(ITTSEventPlayer));
                if (Player == null)
                {
                    VLog.E($"No ITTSEventPlayer found for {GetType().Name} on {name}");
                }
            }
            // Force a sample refresh
            RefreshSample(true);
        }

        /// <summary>
        /// Updated once per frame
        /// </summary>
        protected virtual void Update()
        {
            RefreshSample(false);
        }

        /// <summary>
        /// Updates currently set sample and lerps between events as specified
        /// </summary>
        /// <param name="force">If true, will force the sample set & lerping between events.
        /// If false the sample will only be set if it has changed.</param>
        protected virtual void RefreshSample(bool force)
        {
            // Get sample
            var sample = Player == null ? 0 : Player.ElapsedSamples;
            if (!force && sample == _sample)
            {
                return;
            }

            // Store current sample
            _sample = sample;

            // Invalid sample or event container
            var events = Player?.CurrentEvents;
            if (sample < 0 || events?.Events == null)
            {
                LerpEvent(_minEvent, _minEvent, 0);
                return;
            }

            // If rewinding, reset event index
            events.GetClosestEvents<TEvent>(_sample, ref _prevEventIndex, ref _prevEvent, ref _nextEvent);

            // Use min/max events if needed
            var previousEvent = _prevEvent ?? _minEvent;
            var nextEvent = _nextEvent ?? _maxEvent;
            if (Player != null)
            {
                _maxEvent.offset = Player.TotalSamples;
            }

            // Determine percentage from 0 to 1
            float percentage = GetSampleEventProgress(sample, previousEvent.SampleOffset, nextEvent.SampleOffset);

            // Lerp
            LerpEvent(previousEvent, nextEvent, percentage);
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
