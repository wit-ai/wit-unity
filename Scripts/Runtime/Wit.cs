/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Configuration;
using Facebook.WitAi.Events;
using Facebook.WitAi.Interfaces;
using UnityEngine;

namespace Facebook.WitAi
{
    // A wrapper class around WitService that is used as the public interface of
    // Wit.ai to send speech commands to Wit server.
    public class Wit : VoiceService
    {
        //public VoiceEvents events = new VoiceEvents();
        [SerializeField] private WitRuntimeConfiguration witRuntimeConfiguration;

        public WitRuntimeConfiguration RuntimeConfiguration
        {
            get => witRuntimeConfiguration;
            set => witRuntimeConfiguration = value;
        }


        public VoiceEvents VoiceEvents { get => ((IVoiceService)_witService).VoiceEvents; set => ((IVoiceService)_witService).VoiceEvents = value; }
        [HideInInspector] public WitService WrappedWitService { get => _witService;}

        public override bool Active => _witService.Active;

        public override bool IsRequestActive => _witService.IsRequestActive;

        public override ITranscriptionProvider TranscriptionProvider { get => _witService.TranscriptionProvider; set => _witService.TranscriptionProvider = value; }

        public override bool MicActive => _witService.MicActive;

        protected override bool ShouldSendMicData => throw new System.NotImplementedException();

        private WitService _witService; // The wrapped class.

        private void Awake()
        {
            _witService = gameObject.AddComponent<WitService>();
        }

        private void Start()
        {
            _witService.RuntimeConfiguration = witRuntimeConfiguration;
            _witService.events = events;
        }

        public override void Activate()
        {
            WitRequest witRequest = RuntimeConfiguration.witConfiguration.SpeechRequest(new WitRequestOptions());
            _witService.RecordingWitRequest = witRequest;
            _witService.ActivateImmediately();
        }



        public override void Activate(WitRequestOptions requestOptions)
        {
            _witService.Activate(requestOptions);
        }

        public override void ActivateImmediately()
        {
            _witService.ActivateImmediately();
        }

        public override void ActivateImmediately(WitRequestOptions requestOptions)
        {
            _witService.ActivateImmediately(requestOptions);
        }

        public override void Deactivate()
        {
            _witService.Deactivate();
        }

        public override void DeactivateAndAbortRequest()
        {
            _witService.DeactivateAndAbortRequest();
        }

        public override void Activate(string text)
        {
            _witService.Activate(text);
        }

        public override void Activate(string text, WitRequestOptions requestOptions)
        {
            _witService.Activate(text, requestOptions);
        }
    }
}
