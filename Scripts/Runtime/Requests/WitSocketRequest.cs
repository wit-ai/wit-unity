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

namespace Meta.WitAi.Requests
{
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
        /// Web socket decodes responses automatically
        /// </summary>
        protected override bool DecodeRawResponses => false;

        /// <summary>
        /// Web socket request being performed
        /// </summary>
        public WitWebSocketMessageRequest WebSocketRequest { get; private set; }

        // Lock to ensure initialize callbacks are not performed until all fields are setup
        private bool _initialized = false;
        // Web socket request performing voice service request
        private WitWebSocketMessageRequest _request;

        /// <summary>
        /// Constructor for audio requests using an audio buffer
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client</param>
        /// <param name="audioBuffer">Audio input buffer used to obtain audio data</param>
        /// <param name="options">Options used for request parameters</param>
        /// <param name="events">Event callbacks used for this request</param>
        public WitSocketRequest(WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer, WitRequestOptions options = null,
            VoiceServiceRequestEvents events = null) : base(NLPRequestInputType.Audio, options, events)
        {
            Init(configuration, webSocketAdapter, audioBuffer);
        }

        /// <summary>
        /// Constructor for a text request
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client</param>
        /// <param name="options">Options used for request parameters</param>
        /// <param name="events">Event callbacks used for this request</param>
        public WitSocketRequest(WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter, WitRequestOptions options = null,
            VoiceServiceRequestEvents events = null) : base(NLPRequestInputType.Text, options, events)
        {
            Init(configuration, webSocketAdapter, null);
        }

        /// <summary>
        /// Constructor for json response turned into message response
        /// </summary>
        /// <param name="configuration">Configuration to be used when authenticating web socket client</param>
        /// <param name="webSocketAdapter">Adapter used to communicate with web socket client</param>
        /// <param name="jsonData">Initial response from server</param>
        public WitSocketRequest(WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter,
            WitWebSocketMessageRequest webSocketRequest,
            WitRequestOptions options, VoiceServiceRequestEvents events = null
            ) : base(NLPRequestInputType.Text, options, events)
        {
            SetWebSocketRequest(webSocketRequest);
            Init(configuration, webSocketAdapter, null);
        }

        /// <summary>
        /// Applies all constructor variables and sets init state
        /// </summary>
        private void Init(WitConfiguration configuration, WitWebSocketAdapter webSocketAdapter, AudioBuffer audioBuffer)
        {
            Configuration = configuration;
            WebSocketAdapter = webSocketAdapter;
            AudioInput = audioBuffer;
            Endpoint = InputType == NLPRequestInputType.Audio
                ? Endpoint = configuration.GetEndpointInfo().Speech
                : Endpoint = configuration.GetEndpointInfo().Message;
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
                var request = new WitWebSocketSpeechRequest(Endpoint, Options.QueryParams, Options.RequestId);
                SetWebSocketRequest(request);
            }

            // Request generation failed
            if (WebSocketRequest == null)
            {
                return;
            }

            // Send request
            WebSocketAdapter.SendRequest(WebSocketRequest);
        }

        /// <summary>
        /// Sets web socket request & applies all callback delegates
        /// </summary>
        private void SetWebSocketRequest(WitWebSocketMessageRequest request)
        {
            WebSocketRequest = request;
            WebSocketRequest.OnFirstResponse += HandleSocketFirstResponse;
            WebSocketRequest.OnDecodedResponse += HandleSocketDecodedResponse;
            WebSocketRequest.OnComplete += HandleSocketRequestCompletion;
        }

        /// <summary>
        /// Callback when a response was received for this request.
        /// If an audio response, call OnInputStreamReady
        /// </summary>
        private void HandleSocketFirstResponse(IWitWebSocketRequest request)
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
        private void HandleSocketDecodedResponse(WitResponseNode responseNode)
        {
            ThreadUtility.CallOnMainThread(() => ApplyResponseData(responseNode, false));
        }

        /// <summary>
        /// Callback handler for web socket request completion
        /// </summary>
        /// <param name="request"></param>
        private void HandleSocketRequestCompletion(IWitWebSocketRequest request)
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
            SetAudioInputState(VoiceAudioInputState.Off);
        }
        #endregion AUDIO
    }
}
