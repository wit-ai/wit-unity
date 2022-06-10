/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.Conduit;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Events;
using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi
{
    public abstract class VoiceService : MonoBehaviour, IVoiceService, IInstanceResolver
    {
        /// <summary>
        /// When set to true, Conduit will be used. Otherwise, the legacy dispatching will be used.
        /// </summary>
        private bool UseConduit => (_witConfiguration!= null) && _witConfiguration.useConduit;

        /// <summary>
        /// The wit configuration.
        /// </summary>
        private WitConfiguration _witConfiguration;

        private readonly IParameterProvider conduitParameterProvider = new WitConduitParameterProvider();

        [Tooltip("Events that will fire before, during and after an activation")] [SerializeField]
        public VoiceEvents events = new VoiceEvents();

        /// <summary>
        /// Returns true if this voice service is currently active and listening with the mic
        /// </summary>
        public abstract bool Active { get; }

        /// <summary>
        /// The Conduit-based dispatcher that dispatches incoming invocations based on a manifest.
        /// </summary>
        internal IConduitDispatcher ConduitDispatcher { get; set; }

        /// <summary>
        /// Returns true if the service is actively communicating with Wit.ai during an Activation. The mic may or may not still be active while this is true.
        /// </summary>
        public abstract bool IsRequestActive { get; }

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

        /// <summary>
        /// Returns true if the audio input should be read in an activation
        /// </summary>
        protected abstract bool ShouldSendMicData { get; }

        /// <summary>
        /// Constructs a <see cref="VoiceService"/>
        /// </summary>
        protected VoiceService()
        {
            var conduitDispatcherFactory = new ConduitDispatcherFactory(this, this.conduitParameterProvider);
            ConduitDispatcher = conduitDispatcherFactory.GetDispatcher();
        }

        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        public abstract void Activate();

        /// <summary>
        /// Activate the microphone and send data for NLU processing. Includes optional additional request parameters like dynamic entities and maximum results.
        /// </summary>
        /// <param name="requestOptions"></param>
        public abstract void Activate(WitRequestOptions requestOptions);

        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        public abstract void ActivateImmediately();

        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.  Includes optional additional request parameters like dynamic entities and maximum results.
        /// </summary>
        public abstract void ActivateImmediately(WitRequestOptions requestOptions);

        /// <summary>
        /// Stop listening and submit any remaining buffered microphone data for processing.
        /// </summary>
        public abstract void Deactivate();

        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        public abstract void DeactivateAndAbortRequest();

        /// <summary>
        /// Send text data for NLU processing. Results will return the same way a voice based activation would.
        /// </summary>
        /// <param name="text"></param>
        public abstract void Activate(string text);

        /// <summary>
        /// Send text data for NLU processing with custom request options.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="requestOptions"></param>
        public abstract void Activate(string text, WitRequestOptions requestOptions);

        /// <summary>
        /// Returns objects of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Objects of the specified type.</returns>
        public IEnumerable<object> GetObjectsOfType(Type type)
        {
            return FindObjectsOfType(type);
        }

        protected virtual void Awake()
        {
            var witConfigProvider = this.GetComponent<IWitRuntimeConfigProvider>();
            _witConfiguration = witConfigProvider?.RuntimeConfiguration?.witConfiguration;

            if (!UseConduit)
            {
                MatchIntentRegistry.Initialize();
            }
        }

        protected virtual void OnEnable()
        {
            if (UseConduit)
            {
                ConduitDispatcher.Initialize(_witConfiguration.manifestLocalPath);
            }

            AddEvents();
        }

        protected void AddEvents()
        {
            VoiceEvents.OnPartialResponse.AddListener(OnPartialResponse);
            VoiceEvents.OnResponse.AddListener(OnResponse);
        }

        protected virtual void OnDisable()
        {
            VoiceEvents.OnPartialResponse.RemoveListener(OnPartialResponse);
            VoiceEvents.OnResponse.RemoveListener(OnResponse);
        }

        protected virtual void OnPartialResponse(WitResponseNode response)
        {
            HandleIntents(response, false);
        }

        protected virtual void OnResponse(WitResponseNode response)
        {
            HandleIntents(response, true);
        }

        private void HandleIntents(WitResponseNode response, bool isFinal)
        {
            var intents = response.GetIntents();
            foreach (var intent in intents)
            {
                HandleIntent(intent, response, isFinal);
            }
        }

        private void HandleIntent(WitIntentData intent, WitResponseNode response, bool isFinal)
        {
            if (UseConduit)
            {
                var parameters = new Dictionary<string, object>();

                foreach (var entity in response.AsObject["entities"].Childs)
                {
                    var parameterName = entity[0]["role"].Value;
                    var parameterValue = entity[0]["value"].Value;
                    parameters.Add(parameterName, parameterValue);

                    Debug.Log($"{parameterName} = {parameterValue}");
                }

                parameters.Add(WitConduitParameterProvider.WitResponseNodeReservedName, response);

                if (!ConduitDispatcher.InvokeAction(intent.name, parameters))
                {
                    Debug.Log($"Failed to dispatch intent {intent.name}");
                }
            }
            else
            {
                var methods = MatchIntentRegistry.RegisteredMethods[intent.name];
                foreach (var method in methods)
                {
                    ExecuteRegisteredMatch(method, intent, response, isFinal);
                }
            }
        }

        private void ExecuteRegisteredMatch(RegisteredMatchIntent registeredMethod,
            WitIntentData intent, WitResponseNode response, bool isFinal)
        {
            if (intent.confidence >= registeredMethod.matchIntent.MinConfidence &&
                intent.confidence <= registeredMethod.matchIntent.MaxConfidence &&
                (isFinal || registeredMethod.matchIntent.AllowPartial))
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
                        Debug.LogError("Match intent only supports methods with no parameters or with a WitResponseNode parameter. Enable Conduit or adjust the parameters");
                        continue;
                    }

                    if (parameters.Length == 2)
                    {
                        if (parameters[1].ParameterType != typeof(bool))
                        {
                            Debug.LogError("Match intent only supports methods with no parameters or with a WitResponseNode parameter. Enable Conduit or adjust the parameters");
                            continue;
                        }

                        registeredMethod.method.Invoke(obj, new object[] {response, isFinal});
                    }
                    else if (parameters.Length == 1)
                    {
                        registeredMethod.method.Invoke(obj, new object[] {response});
                    }
                }
            }
        }
    }

    public interface IVoiceService : IVoiceEventProvider
    {
        /// <summary>
        /// Returns true if this voice service is currently active and listening with the mic
        /// </summary>
        bool Active { get; }

        bool IsRequestActive { get; }

        bool MicActive { get; }

        new VoiceEvents VoiceEvents { get; set; }

        ITranscriptionProvider TranscriptionProvider { get; set; }

        /// <summary>
        /// Activate the microphone and send data for NLU processing.
        /// </summary>
        void Activate();

        /// <summary>
        /// Activate the microphone and send data for NLU processing with custom request options.
        /// </summary>
        /// <param name="requestOptions"></param>
        void Activate(WitRequestOptions requestOptions);

        void ActivateImmediately();
        void ActivateImmediately(WitRequestOptions requestOptions);

        /// <summary>
        /// Stop listening and submit the collected microphone data for processing.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        void DeactivateAndAbortRequest();

        /// <summary>
        /// Send text data for NLU processing
        /// </summary>
        /// <param name="text"></param>
        void Activate(string transcription);

        /// <summary>
        /// Send text data for NLU processing with custom request options.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="requestOptions"></param>
        void Activate(string text, WitRequestOptions requestOptions);

    }
}
