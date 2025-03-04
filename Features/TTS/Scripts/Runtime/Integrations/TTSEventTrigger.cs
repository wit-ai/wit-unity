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
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;
using UnityEngine;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// A base class for performing audio event based animations
    /// </summary>
    public abstract class TTSEventTrigger<TEvent, TData> : MonoBehaviour
        where TEvent : TTSEvent<TData>
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.TextToSpeech);

        // Local sample counter
        private int _sample = -1;

        private Queue<ITTSEvent> queuedEvents = new Queue<ITTSEvent>();

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

        private TTSEventContainer _currentEvents;

        protected virtual void OnEnable()
        {

        }

        protected virtual void OnDisable()
        {
            ClearCurrentEvents();
        }

        private void ClearCurrentEvents()
        {
            if (null != _currentEvents)
            {
                _currentEvents.OnEventAdded -= OnEventAdded;
                _currentEvents = null;
            }
        }

        private void OnEventAdded(ITTSEvent ev)
        {
            if(ev is TEvent) queuedEvents.Enqueue(ev);
        }

        /// <summary>
        /// Updated once per frame
        /// </summary>
        protected virtual void Update()
        {
            if (null == Player || null == Player.CurrentEvents) return;

            if (_currentEvents != Player.CurrentEvents)
            {
                ClearCurrentEvents();
                _currentEvents = Player.CurrentEvents;
                if (null != _currentEvents)
                {
                    _currentEvents.OnEventAdded += OnEventAdded;
                    foreach (var ev in Player.CurrentEvents.Events)
                    {
                        if (ev is TEvent) queuedEvents.Enqueue(ev);
                    }
                }
            }
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

            while (queuedEvents.Count > 0 && _sample > queuedEvents.Peek().SampleOffset)
            {
                OnEventTriggered((TEvent) queuedEvents.Dequeue());
            }
        }

        protected abstract void OnEventTriggered(TEvent queuedEvent);
    }
}
