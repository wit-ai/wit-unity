/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.Conduit;
using Meta.Voice;
using Meta.Voice.TelemetryUtilities;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Data.Intents;
using Meta.WitAi.Events;
using Meta.WitAi.Events.UnityEventListeners;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using UnityEngine;

namespace Meta.WitAi
{
    public abstract class VoiceService : BaseSpeechService, IVoiceService, IInstanceResolver, IAudioEventProvider
    {
        /// <summary>
        /// Enables/disables all attribute based intent handling
        /// </summary>
        private bool UseIntentAttributes => WitConfiguration && WitConfiguration.useIntentAttributes;

        /// <summary>
        /// When set to true, Conduit will be used. Otherwise, the legacy dispatching will be used.
        /// </summary>
        private bool UseConduit => UseIntentAttributes && WitConfiguration.useConduit;

        /// <summary>
        /// When set to true, the service will use platform integration.
        /// </summary>
        public virtual bool UsePlatformIntegrations
        {
            get => false;
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Wit configuration accessor via IWitConfigurationProvider
        /// </summary>
        public WitConfiguration WitConfiguration
        {
            get
            {
                if (_witConfiguration == null && this && isActiveAndEnabled)
                {
                    _witConfiguration = GetComponent<IWitConfigurationProvider>()?.Configuration;
                }
                return _witConfiguration;
            }
            set => _witConfiguration = value;
        }
        private WitConfiguration _witConfiguration;

        /// <summary>
        /// The Conduit parameter provider.
        /// </summary>
        private readonly IParameterProvider _conduitParameterProvider = new ParameterProvider();

        /// <summary>
        /// This field should not be accessed outside the Wit-Unity library. If you need access
        /// to events you should be using the VoiceService.VoiceEvents property instead.
        /// </summary>
        [Tooltip("Events that will fire before, during and after an activation")] [SerializeField]
        protected VoiceEvents events = new VoiceEvents();

        ///<summary>
        /// Internal events used to report telemetry. These events are reserved for internal
        /// use only and should not be used for any other purpose.
        /// </summary>
        protected TelemetryEvents telemetryEvents = new TelemetryEvents();

        /// <summary>
        /// The Conduit-based dispatcher that dispatches incoming invocations based on a manifest.
        /// </summary>
        internal IConduitDispatcher ConduitDispatcher { get; set; }

        /// <summary>
        /// Returns true if the service is actively communicating with Wit.ai during an Activation. The mic may or may not still be active while this is true.
        /// </summary>
        public virtual bool IsRequestActive => base.Active;

        /// <summary>
        /// Gets/Sets a custom transcription provider. This can be used to replace any built in asr
        /// with an on device model or other provided source
        /// </summary>
        public abstract ITranscriptionProvider TranscriptionProvider { get; set; }

        /// <summary>
        /// Returns true if this voice service is currently reading data from the microphone
        /// </summary>
        public abstract bool MicActive { get; }

        public virtual VoiceEvents VoiceEvents
        {
            get => events;
            set => events = value;
        }

        // Return voice events
        protected override SpeechEvents GetSpeechEvents() => VoiceEvents;

        public virtual TelemetryEvents TelemetryEvents
        {
            get => telemetryEvents;
            set => telemetryEvents = value;
        }

        /// <summary>
        /// A subset of events around collection of audio data
        /// </summary>
        public IAudioInputEvents AudioEvents => VoiceEvents;

        /// <summary>
        /// A subset of events around receiving transcriptions
        /// </summary>
        public ITranscriptionEvent TranscriptionEvents => VoiceEvents;

        /// <summary>
        /// Returns true if the audio input should be read in an activation
        /// </summary>
        protected abstract bool ShouldSendMicData { get; }

        /// <summary>
        /// Constructs a <see cref="VoiceService"/>
        /// </summary>
        protected VoiceService()
        {
            _conduitParameterProvider.SetSpecializedParameter(ParameterProvider.WitResponseNodeReservedName, typeof(WitResponseNode));
            _conduitParameterProvider.SetSpecializedParameter(ParameterProvider.VoiceSessionReservedName, typeof(VoiceSession));
            var conduitDispatcherFactory = new ConduitDispatcherFactory(this);
            ConduitDispatcher = conduitDispatcherFactory.GetDispatcher();
        }

        #region TEXT REQUESTS
        /// <summary>
        /// Send text data for NLU processing. Results will return the same way a voice based activation would.
        /// </summary>
        /// <param name="text">Text to be used for NLU processing</param>
        public void Activate(string text) => ThreadUtility.BackgroundAsync(Logger, async () => await Activate(text, new WitRequestOptions())).WrapErrors();
        /// <summary>
        /// Send text data for NLU processing. Results will return the same way a voice based activation would.
        /// </summary>
        /// <param name="text">Text to be used for NLU processing</param>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        public Task<VoiceServiceRequest> Activate(string text, WitRequestOptions requestOptions) => Activate(text, requestOptions, new VoiceServiceRequestEvents());
        /// <summary>
        /// Send text data for NLU processing. Results will return the same way a voice based activation would.
        /// </summary>
        /// <param name="text">Text to be used for NLU processing</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public Task<VoiceServiceRequest> Activate(string text, VoiceServiceRequestEvents requestEvents) => Activate(text, new WitRequestOptions(), requestEvents);

        /// <summary>
        /// Send text data for NLU processing with custom request options.
        /// </summary>
        /// <param name="text">Text to be used for NLU processing</param>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public abstract Task<VoiceServiceRequest> Activate(string text, WitRequestOptions requestOptions,
            VoiceServiceRequestEvents requestEvents);
        #endregion TEXT REQUESTS

        #region AUDIO REQUESTS
        protected bool _waitingForFirstPartialAudio = true;

        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        public void Activate() => Activate(new WitRequestOptions());
        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        public void Activate(WitRequestOptions requestOptions) =>
            Activate(requestOptions, new VoiceServiceRequestEvents());
        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public VoiceServiceRequest Activate(VoiceServiceRequestEvents requestEvents) =>
            Activate(new WitRequestOptions(), requestEvents);
        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public abstract VoiceServiceRequest Activate(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents);

        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        public void ActivateImmediately() => ActivateImmediately(new WitRequestOptions());
        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        public void ActivateImmediately(WitRequestOptions requestOptions) =>
            ActivateImmediately(requestOptions, new VoiceServiceRequestEvents());
        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public VoiceServiceRequest ActivateImmediately(VoiceServiceRequestEvents requestEvents) =>
            ActivateImmediately(new WitRequestOptions(), requestEvents);
        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        public abstract VoiceServiceRequest ActivateImmediately(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents);
        #endregion AUDIO REQUESTS

        // Called when VoiceServiceRequest OnPartialResponse is returned & tries to end early if possible
        protected override void OnRequestPartialResponse(VoiceServiceRequest request, WitResponseNode responseNode)
        {
            if (_waitingForFirstPartialAudio) {
              _waitingForFirstPartialAudio = false;
              RuntimeTelemetry.Instance.LogPoint((OperationID)request.Options.OperationId, RuntimeTelemetryPoint.FirstPartialAudioFromServer);
            }
            base.OnRequestPartialResponse(request, responseNode);
            OnValidateEarly(request, responseNode);
        }

        // Request send resets partial audio flag
        protected override void OnRequestSend(VoiceServiceRequest request) {
            _waitingForFirstPartialAudio = true;
            base.OnRequestSend(request);
        }

        // Attempts to validate early if possible
        protected virtual void OnValidateEarly(VoiceServiceRequest request, WitResponseNode responseNode)
        {
            // Ignore unless can be validated
            if (request == null
                || request.State != VoiceRequestState.Transmitting
                || responseNode == null
                || VoiceEvents.OnValidatePartialResponse == null)
            {
                return;
            }

            // Create short response data
            VoiceSession validationData = GetVoiceSession(responseNode);

            // Call short response
            VoiceEvents.OnValidatePartialResponse.Invoke(validationData);

            // Invoke
            if (UseConduit)
            {
                // Ignore without an intent
                WitIntentData intent = responseNode.GetFirstIntentData();
                if (intent != null)
                {
                    _conduitParameterProvider.PopulateParametersFromNode(responseNode);
                    _conduitParameterProvider.AddParameter(ParameterProvider.VoiceSessionReservedName,
                        validationData);
                    _conduitParameterProvider.AddParameter(ParameterProvider.WitResponseNodeReservedName, responseNode);
                    ConduitDispatcher.InvokeAction(_conduitParameterProvider, intent.name, _witConfiguration.relaxedResolution, intent.confidence, true);
                }
            }

            // Deactivate & abort immediately but use the response data as results
            if (validationData.validResponse)
            {
                VLog.I("Validated Early");
                request.CompleteEarly();
            }
        }

        /// <summary>
        /// Returns objects of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Objects of the specified type.</returns>
        public IEnumerable<object> GetObjectsOfType(Type type)
        {
            return FindObjectsByType(type, FindObjectsSortMode.None);
        }

        protected virtual void Awake()
        {
            InitializeEventListeners();
        }

        private void InitializeEventListeners()
        {
            var audioEventListener = GetComponent<AudioEventListener>();
            if (!audioEventListener)
            {
                gameObject.AddComponent<AudioEventListener>();
            }

            var transcriptionEventListener = GetComponent<TranscriptionEventListener>();
            if (!transcriptionEventListener)
            {
                gameObject.AddComponent<TranscriptionEventListener>();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (UseConduit)
            {
                InitializeConduit().WrapErrors();
            }
            else if (UseIntentAttributes)
            {
                MatchIntentRegistry.Initialize();
            }
            TranscriptionProvider?.OnFullTranscription.AddListener(OnFinalTranscription);
            VoiceEvents.OnResponse.AddListener(HandleResponse);
        }

        private async Task InitializeConduit()
        {
            await ConduitDispatcher.Initialize(_witConfiguration.ManifestLocalPath);
            if (_witConfiguration.relaxedResolution)
            {
                if (!ConduitDispatcher.Manifest.ResolveEntities())
                {
                    VLog.E("Failed to resolve Conduit entities");
                }

                foreach (var entity in ConduitDispatcher.Manifest.CustomEntityTypes)
                {
                    _conduitParameterProvider.AddCustomType(entity.Key, entity.Value);
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            TranscriptionProvider?.OnFullTranscription.RemoveListener(OnFinalTranscription);
            VoiceEvents.OnResponse.RemoveListener(HandleResponse);
        }

        /// <summary>
        /// Activate message if transcription provider returns a final transcription
        /// </summary>
        protected virtual void OnFinalTranscription(string transcription)
        {
            if (TranscriptionProvider != null)
            {
                Activate(transcription);
            }
        }

        private VoiceSession GetVoiceSession(WitResponseNode response)
        {
            return new VoiceSession
            {
                service = this,
                response = response,
                validResponse = false
            };
        }

        protected virtual void HandleResponse(WitResponseNode response)
        {
            HandleIntents(response);
        }

        private void HandleIntents(WitResponseNode response)
        {
            var intents = response.GetIntents();
            foreach (var intent in intents)
            {
                HandleIntent(intent, response);
            }
        }

        private void HandleIntent(WitIntentData intent, WitResponseNode response)
        {
            if (UseConduit)
            {
                _conduitParameterProvider.PopulateParametersFromNode(response);
                _conduitParameterProvider.AddParameter(ParameterProvider.WitResponseNodeReservedName, response);
                ConduitDispatcher.InvokeAction(_conduitParameterProvider, intent.name,
                    _witConfiguration.relaxedResolution, intent.confidence, false);
            }
            else if (UseIntentAttributes)
            {
                var methods = MatchIntentRegistry.RegisteredMethods[intent.name];
                foreach (var method in methods)
                {
                    ExecuteRegisteredMatch(method, intent, response);
                }
            }
        }



        private void ExecuteRegisteredMatch(RegisteredMatchIntent registeredMethod,
            WitIntentData intent, WitResponseNode response)
        {
            if (intent.confidence >= registeredMethod.matchIntent.MinConfidence &&
                intent.confidence <= registeredMethod.matchIntent.MaxConfidence)
            {
                foreach (var obj in GetObjectsOfType(registeredMethod.type))
                {
                    var parameters = registeredMethod.method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        registeredMethod.method.Invoke(obj, Array.Empty<object>());
                        continue;
                    }
                    if (parameters[0].ParameterType != typeof(WitResponseNode) || parameters.Length > 2)
                    {
                        VLog.E("Match intent only supports methods with no parameters or with a WitResponseNode parameter. Enable Conduit or adjust the parameters");
                        continue;
                    }
                    if (parameters.Length == 1)
                    {
                        registeredMethod.method.Invoke(obj, new object[] {response});
                    }
                }
            }
        }
    }

    public interface IVoiceService : IVoiceEventProvider, ITelemetryEventsProvider, IVoiceActivationHandler
    {
        /// <summary>
        /// Returns true if voice service is currently active or request is transmitting
        /// </summary>
        bool IsRequestActive { get; }

        /// <summary>
        /// When set to true, the service will use platform integration.
        /// </summary>
        bool UsePlatformIntegrations { get; set; }

        /// <summary>
        /// The current running voice requests
        /// </summary>
        HashSet<VoiceServiceRequest> Requests { get; }

        /// <summary>
        /// Returns true Mic is still enabled
        /// </summary>
        bool MicActive { get; }
        /// <summary>
        /// All events used for a voice service
        /// </summary>
        new VoiceEvents VoiceEvents { get; set; }
        /// <summary>
        /// All events used for a voice service telemetry
        /// </summary>
        new TelemetryEvents TelemetryEvents { get; set; }
        /// <summary>
        /// Easy acccess for transcription
        /// </summary>
        ITranscriptionProvider TranscriptionProvider { get; set; }

        /// <summary>
        /// Whether or not this service can listen to audio
        /// </summary>
        /// <returns>True if audio can be listened to</returns>
        bool CanActivateAudio();
        /// <summary>
        /// Whether or not this service can perform requests
        /// </summary>
        /// <returns>True if a request can be sent</returns>
        bool CanSend();
    }

    public interface IVoiceActivationHandler
    {
        /// <summary>
        /// Returns true if this voice service is currently active and listening with the mic
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Send text data for NLU processing with custom request options & events.
        /// </summary>
        /// <param name="text">Text to be used for NLU processing</param>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        Task<VoiceServiceRequest> Activate(string text, WitRequestOptions requestOptions,
            VoiceServiceRequestEvents requestEvents);

        /// <summary>
        /// Activate the microphone and wait for threshold and then send data
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        VoiceServiceRequest Activate(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents);

        /// <summary>
        /// Activate the microphone and send data for NLU processing with custom request options.
        /// </summary>
        /// <param name="requestOptions">Additional options such as dynamic entities</param>
        /// <param name="requestEvents">Events specific to the request's lifecycle</param>
        VoiceServiceRequest ActivateImmediately(WitRequestOptions requestOptions,
            VoiceServiceRequestEvents requestEvents);

        /// <summary>
        /// Stop listening and submit the collected microphone data for processing.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        void DeactivateAndAbortRequest();

        /// <summary>
        /// Deactivate mic & abort a specific request
        /// </summary>
        void DeactivateAndAbortRequest(VoiceServiceRequest request);
    }
}
