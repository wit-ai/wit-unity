/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.WitAi.TTS.Events.EventHandlers
{
    public class EmoteEventHandler : TTSEventTrigger<TTSEmoteEvent, string>
    {
        [SerializeField] private UnityEvent<string> onEmoteStart = new UnityEvent<string>();
        [SerializeField] private UnityEvent<string> onEmoteStop = new UnityEvent<string>();

        public UnityEvent<string> OnEmoteStart => onEmoteStart;
        public UnityEvent<string> OnEmoteStop => onEmoteStop;

        private TTSEmoteEvent _lastEmote;

        protected override void OnEventTriggered(TTSEmoteEvent queuedEvent)
        {
            if(null != _lastEmote) onEmoteStop?.Invoke(_lastEmote.Data);
            _lastEmote = queuedEvent;
            onEmoteStart?.Invoke(queuedEvent.Data);
        }
    }
}
