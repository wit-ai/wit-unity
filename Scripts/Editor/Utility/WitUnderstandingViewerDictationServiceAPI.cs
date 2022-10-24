/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Dictation;
using Meta.WitAi.Events;
using UnityEngine;
using UnityEngine.Events;

namespace Facebook.WitAi.Windows
{
    public class WitUnderstandingViewerDictationServiceAPI : WitUnderstandingViewerServiceAPI
    {
        private DictationService _service;

        public WitUnderstandingViewerDictationServiceAPI(DictationService service) : base(service)
        {
            _service = service;

            HasVoiceActivation = true;
            HasTextActivation = true;
        }

        public override bool Active
        {
            get => _service.Active;
        }

        public override bool MicActive
        {
            get => _service.MicActive;
        }

        public override bool IsRequestActive
        {
            get => _service.IsRequestActive;
        }

        public override void Activate()
        {
            _service.Activate();
        }

        public override void Activate(string text)
        {
            Debug.LogWarning("Activate(text) not supported for this API");
        }

        public override void Deactivate()
        {
            _service.Deactivate();
        }

        public override void DeactivateAndAbortRequest()
        {
            Debug.LogWarning("DeactivateAndAbortRequest() not supported for this API");
        }

        public override WitRequestCreatedEvent OnRequestCreated
        {
            get => null;
        }

        public override WitErrorEvent OnError
        {
            get => _service.DictationEvents.onError;
        }

        public override WitResponseEvent OnResponse
        {
            get => _service.DictationEvents.onResponse;
        }

        public override WitTranscriptionEvent OnFullTranscription
        {
            get => _service.DictationEvents.onFullTranscription;
        }

        public override WitTranscriptionEvent OnPartialTranscription
        {
            get => _service.DictationEvents.OnPartialTranscription;
        }

        public override UnityEvent OnStoppedListening
        {
            get => null;
        }
    }
}
