/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Composer;
using Meta.WitAi.Events;
using UnityEngine.Events;

namespace Facebook.WitAi.Windows
{
    public class WitUnderstandingViewerComposerServiceAPI : WitUnderstandingViewerServiceAPI
    {
        private ComposerService _service;

        public WitUnderstandingViewerComposerServiceAPI(ComposerService service) : base(service)
        {
            _service = service;

            HasVoiceActivation = true;
            HasTextActivation = true;
        }

        public override bool Active
        {
            get => _service.VoiceService.Active;
        }

        public override bool MicActive
        {
            get => _service.VoiceService.MicActive;
        }

        public override bool IsRequestActive
        {
            get => _service.VoiceService.IsRequestActive;
        }

        public override void Activate()
        {
            _service.VoiceService.Activate();
        }

        public override void Deactivate()
        {
            _service.VoiceService.Deactivate();
        }

        public override void Activate(string text)
        {
            _service.VoiceService.Activate(text);
        }

        public override void DeactivateAndAbortRequest()
        {
            _service.VoiceService.DeactivateAndAbortRequest();
        }

        public override WitRequestCreatedEvent OnRequestCreated
        {
            get => _service.VoiceService.VoiceEvents.OnRequestCreated;
        }

        public override WitErrorEvent OnError
        {
            get => _service.VoiceService.VoiceEvents.OnError;
        }

        public override WitResponseEvent OnResponse
        {
            get => _service.VoiceService.VoiceEvents.OnResponse;
        }

        public override WitTranscriptionEvent OnFullTranscription
        {
            get => _service.VoiceService.VoiceEvents.onFullTranscription;
        }

        public override WitTranscriptionEvent OnPartialTranscription
        {
            get => _service.VoiceService.VoiceEvents.OnPartialTranscription;
        }

        public override UnityEvent OnStoppedListening
        {
            get => _service.VoiceService.VoiceEvents.OnStoppedListening;
        }
    }
}
