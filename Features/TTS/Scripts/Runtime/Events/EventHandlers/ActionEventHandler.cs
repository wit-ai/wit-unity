/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using UnityEngine;
using UnityEngine.Events;

namespace Meta.WitAi.TTS.Events.EventHandlers
{
    public class ActionEventHandler : TTSEventTrigger<TTSActionEvent, string>
    {
        [SerializeField] private UnityEvent<WitResponseNode> onEvent = new UnityEvent<WitResponseNode>();

        public UnityEvent<WitResponseNode> OnEvent => onEvent;

        protected override void OnEventTriggered(TTSActionEvent queuedEvent)
        {
            onEvent?.Invoke(queuedEvent.Response);
        }
    }
}
