/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice;
using Meta.Voice.Net.WebSockets;
using Meta.Voice.Net.WebSockets.Requests;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Options that adjust how audio requests should be handled.
    /// </summary>
    public enum WitAudioRequestOption
    {
        /// <summary>
        /// Used when not an audio request
        /// </summary>
        None,

        /// <summary>
        /// An option that provides NLP responses
        /// </summary>
        Speech,

        /// <summary>
        /// An option that provides a single full transcription response
        /// </summary>
        Transcribe,

        /// <summary>
        /// An option that provides a multiple full transcription response
        /// </summary>
        Dictation
    }

    /// <summary>
    /// A WitSocketRequest implementation using web sockets
    /// </summary>
    [Serializable]
    public class WitSocketRequest : VoiceServiceRequest, IAudioUploadHandler
    {
        /// <summary>
        /// The configuration to be used for the request
        /// </summary>
        public WitConfiguration Configuration { get; private set; }
        /// <summary>
        /// The script used to transmit data
        /// </summary>
        public WitWebSocketAdapter WebSocketAdapter { get; private set; }
        /// <summary>
        /// The audio buffer used for audio based requests
        /// </summary>
        public AudioBuffer AudioInput { get; private set; }
        /// <summary>
        /// Endpoint to be used
        /// </summary>
        public string Endpoint { get; set; }
        /// <summary>
        /// Whether request is used for transcribing only
        /// </summary>
        public WitAudioRequestOption AudioRequestOption { get; private set; }
        /// <summary>
        /// Audio encoding used for audio requests
        /// </summary>
        public AudioEncoding AudioEncoding { get; set; }

        /// <summary>
        /// Whether or not the audio stream is ready
        /// </summary>
        public bool IsInputStreamReady { get; private set; }
        /// <summary>
        /// Callback when socket connection is ready to send data
        /// </summary>
        public Action OnInputStreamReady { get; set; }

        /// <summary>
        /// Web socket client decodes responses prior to raw response callback
        /// for request id lookup.
        /// </summary>
        protected override bool DecodeRawResponses => false;

        /// <summary>
        /// Web socket request being performed
        /// </summary>
        public WitWebSocketMessageRequest WebSocketRequest { get; private set; }

        // Lock to ensure initialize callbacks are not performed until all fields are setup
        private bool _initialized = false;

        /// <summary>
        /// Internal constructor for WitSocketRequest class.  Use static constructors for generation.
        /// </summary>
        private WitSocketRequest(NLPRequestInputType inputType, WitRequestOptions options = null,
            VoiceServiceRequestEvents events = null
        ) : base(NLPRequestInputType.Text, options, events) {}

        /// <summary>
        /// Destructor removes web socket request references
        /// </summary>
        ~WitSocketRequest()
        {
            SetWebSocketRequest(null);
        }

        /// <summary>
        /// Return web socket message request initialized with required data
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client.</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client.</param>
        /// <param name="options">Optional parameter for web request options.</param>
        /// <param name="events">Optional parameter for request event callbacks.</param>
        /// <returns></returns>
        public static WitSocketRequest GetMessageRequest(WitConfiguration configuration,
            WitWebSocketAdapter webSocketAdapter, WitRequestOptions options = null, VoiceServiceRequestEvents events = null)
        {
            var request = new WitSocketRequest(NLPRequestInputType.Text, options, events);
            request.Init(configuration.GetEndpointInfo().Message, WitAudioRequestOption.None,
                configuration, webSocketAdapter, null);
            return request;
        }

        /// <summary>
        /// Return web socket speech request initialized with required data to generate an audio request.
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client.</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client.</param>
        /// <param name="audioBuffer">Audio input buffer used to obtain audio data</param>
        /// <param name="options">Optional parameter for web request options.</param>
        /// <param name="events">Optional parameter for request event callbacks.</param>
        /// <returns></returns>
        public static WitSocketRequest GetSpeechRequest(WitConfiguration configuration,
            WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer,
            WitRequestOptions options = null, VoiceServiceRequestEvents events = null)
        {
            var request = new WitSocketRequest(NLPRequestInputType.Audio, options, events);
            request.Init(configuration.GetEndpointInfo().Speech, WitAudioRequestOption.Speech,
                configuration, webSocketAdapter, audioBuffer);
            return request;
        }

        /// <summary>
        /// Return web socket request initialized on an external client but handled locally.
        /// </summary>
        /// <param name="webSocketRequest">The externally generated web socket request.</param>
        /// <param name="configuration">Configuration to be used when authenticating web socket client.</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client.</param>
        /// <param name="audioBuffer">Audio input buffer used to obtain audio data</param>
        /// <param name="options">Optional parameter for web request options.</param>
        /// <param name="events">Optional parameter for request event callbacks.</param>
        public static WitSocketRequest GetExternalRequest(WitWebSocketMessageRequest webSocketRequest,
            WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter,
            WitRequestOptions options = null, VoiceServiceRequestEvents events = null)
        {
            var request = new WitSocketRequest(NLPRequestInputType.Text, options, events);
            request.SetWebSocketRequest(webSocketRequest);
            request.Init(webSocketRequest.Endpoint, WitAudioRequestOption.None,
                configuration, webSocketAdapter, null);
            return request;
        }

        /// <summary>
        /// Return web socket transcribe request initialized with required data to generate a short audio request.
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client.</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client.</param>
        /// <param name="audioBuffer">Audio input buffer used to obtain audio data</param>
        /// <param name="options">Optional parameter for web request options.</param>
        /// <param name="events">Optional parameter for request event callbacks.</param>
        public static WitSocketRequest GetTranscribeRequest(WitConfiguration configuration,
            WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer,
            WitRequestOptions options = null, VoiceServiceRequestEvents events = null)
        {
            var request = new WitSocketRequest(NLPRequestInputType.Audio, options, events);
            request.Init(WitConstants.WIT_SOCKET_TRANSCRIBE_KEY, WitAudioRequestOption.Transcribe,
                configuration, webSocketAdapter, audioBuffer);
            return request;
        }

        /// <summary>
        /// Return web socket dictation request initialized with required data to generate a long form audio request.
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client.</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client.</param>
        /// <param name="audioBuffer">Audio input buffer used to obtain audio data</param>
        /// <param name="options">Optional parameter for web request options.</param>
        /// <param name="events">Optional parameter for request event callbacks.</param>
        public static WitSocketRequest GetDictationRequest(WitConfiguration configuration,
            WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer,
            WitRequestOptions options = null, VoiceServiceRequestEvents events = null)
        {
            var request = new WitSocketRequest(NLPRequestInputType.Audio, options, events);
            request.Init(WitConstants.WIT_SOCKET_TRANSCRIBE_KEY, WitAudioRequestOption.Dictation,
                configuration, webSocketAdapter, audioBuffer);
            return request;
        }

        /// <summary>
        /// Applies all constructor variables and sets init state
        /// </summary>
        private void Init(string endpoint, WitAudioRequestOption audioOption, WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer)
        {
            Endpoint = endpoint;
            AudioRequestOption = audioOption;
            Configuration = configuration;
            WebSocketAdapter = webSocketAdapter;
            AudioInput = audioBuffer;
            Options.InputType = audioOption == WitAudioRequestOption.None
                ? NLPRequestInputType.Text
                : NLPRequestInputType.Audio;
            _initialized = true;
            SetState(VoiceRequestState.Initialized);
        }

        /// <summary>
        /// Ignore state changes unless setup
        /// </summary>
        protected override void SetState(VoiceRequestState newState)
        {
            if (_initialized)
            {
                base.SetState(newState);
            }
        }

        /// <summary>
        /// Get send error options
        /// </summary>
        protected override string GetSendError()
        {
            if (Configuration == null)
            {
                return "Cannot send request without a valid configuration.";
            }
            if (AudioInput == null && Options.InputType == NLPRequestInputType.Audio)
            {
                return "No audio input provided";
            }
            return base.GetSendError();
        }

        /// <summary>
        /// Begins uploading data
        /// </summary>
        /// <param name="onSendComplete">Callback that handles send completion</param>
        protected override void HandleSend()
        {
            // Generate text request
            if (Options.InputType == NLPRequestInputType.Text)
            {
                Options.QueryParams[WitConstants.ENDPOINT_MESSAGE_PARAM] = Options.Text;
                var request = new WitWebSocketMessageRequest(Endpoint, Options.QueryParams, Options.RequestId);
                SetWebSocketRequest(request);
            }
            // Generate audio request
            else if (Options.InputType == NLPRequestInputType.Audio)
            {
                Options.QueryParams[WitConstants.WIT_SOCKET_CONTENT_KEY] = AudioEncoding.ToString();
                var request = CreateAudioWebSocketRequest();
                if (request != null)
                {
                    SetWebSocketRequest(request);
                }
            }

            // Request generation failed
            if (WebSocketRequest == null || WebSocketAdapter == null)
            {
                return;
            }

            // Send request
            WebSocketAdapter.SendRequest(WebSocketRequest);
        }

        /// <summary>
        /// Generates an audio request depending on the audio request option type
        /// </summary>
        private WitWebSocketMessageRequest CreateAudioWebSocketRequest()
        {
            switch (AudioRequestOption)
            {
                case WitAudioRequestOption.Speech:
                    return new WitWebSocketSpeechRequest(Endpoint, Options.QueryParams, Options.RequestId);
                case WitAudioRequestOption.Transcribe:
                    return new WitWebSocketTranscribeRequest(Endpoint, Options.QueryParams, false, Options.RequestId);
                case WitAudioRequestOption.Dictation:
                    return new WitWebSocketTranscribeRequest(Endpoint, Options.QueryParams, true, Options.RequestId);
            }
            return null;
        }

        /// <summary>
        /// Sets web socket request & applies all callback delegates
        /// </summary>
        private void SetWebSocketRequest(WitWebSocketMessageRequest request)
        {
            if (WebSocketRequest != null)
            {
                WebSocketRequest.OnRawResponse -= ReturnRawResponse;
                WebSocketRequest.OnFirstResponse -= ReturnInputReady;
                WebSocketRequest.OnDecodedResponse -= ReturnDecodedResponse;
                WebSocketRequest.OnComplete -= ReturnSuccessOrError;
            }
            WebSocketRequest = request;
            if (WebSocketRequest != null)
            {
                WebSocketRequest.OnRawResponse += ReturnRawResponse;
                WebSocketRequest.OnFirstResponse += ReturnInputReady;
                WebSocketRequest.OnDecodedResponse += ReturnDecodedResponse;
                WebSocketRequest.OnComplete += ReturnSuccessOrError;
            }
        }

        /// <summary>
        /// Called when websocket request performs on raw response callback.
        /// Calls OnRawResponse event
        /// </summary>
        private void ReturnRawResponse(string rawResponse)
        {
            HandleRawResponse(rawResponse, false);
        }

        /// <summary>
        /// Callback when the first response was received for this request.
        /// If an audio response, call OnInputStreamReady
        /// </summary>
        private void ReturnInputReady(IWitWebSocketRequest request)
        {
            if (request is WitWebSocketSpeechRequest speechRequest && speechRequest.IsReadyForInput)
            {
                IsInputStreamReady = true;
                OnInputStreamReady?.Invoke();
            }
        }

        /// <summary>
        /// Callback when a response node was successfully decoded
        /// </summary>
        private void ReturnDecodedResponse(WitResponseNode responseNode)
        {
            ThreadUtility.CallOnMainThread(() => ApplyResponseData(responseNode, false));
        }

        /// <summary>
        /// Callback handler for web socket request completion
        /// </summary>
        /// <param name="request"></param>
        private void ReturnSuccessOrError(IWitWebSocketRequest request)
        {
            // Ignore if already complete
            if (!IsActive)
            {
                return;
            }

            // Success
            if (string.IsNullOrEmpty(request.Error))
            {
                ApplyResponseData(ResponseData, true);
            }
            // Error
            else
            {
                if (!int.TryParse(request.Code, out var errorCode))
                {
                    errorCode = WitConstants.ERROR_CODE_GENERAL;
                }
                HandleFailure(errorCode, request.Error);
            }
        }

        /// <summary>
        /// Handle cancellation
        /// </summary>
        protected override void HandleCancel()
        {
            if (WebSocketRequest != null)
            {
                WebSocketRequest.Cancel();
            }
        }

        #region AUDIO
        /// <summary>
        /// Error returned if audio cannot be activated
        /// </summary>
        protected override string GetActivateAudioError()
        {
            if (Options.InputType != NLPRequestInputType.Audio)
            {
                return string.Empty;
            }
            if (AudioInput == null)
            {
                return "No audio input provided";
            }
            return string.Empty;
        }
        /// <summary>
        /// Activates audio and calls activated callback once complete
        /// </summary>
        protected override void HandleAudioActivation()
        {
            SetAudioInputState(VoiceAudioInputState.On);
        }
        /// <summary>
        /// Public method for sending binary audio data
        /// </summary>
        /// <param name="buffer">The buffer used for uploading data</param>
        /// <param name="offset">The starting offset of the buffer selection</param>
        /// <param name="length">The length of the buffer to be used</param>
        public void Write(byte[] buffer, int offset, int length)
        {
            if (!IsListening)
            {
                return;
            }
            if (WebSocketRequest is WitWebSocketSpeechRequest speechRequest)
            {
                speechRequest.SendAudioData(buffer, offset, length);
            }
        }
        /// <summary>
        /// Deactivates audio asap and calls deactivated callback once complete
        /// </summary>
        protected override void HandleAudioDeactivation()
        {
            if (WebSocketRequest is WitWebSocketSpeechRequest speechRequest)
            {
                speechRequest.CloseAudioStream();
            }
            SetAudioInputState(VoiceAudioInputState.Off);
        }
        #endregion AUDIO
    }
}
