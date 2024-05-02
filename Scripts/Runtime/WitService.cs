/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Meta.Voice;
using Meta.Voice.Net.PubSub;
using Meta.Voice.Net.WebSockets;
using Meta.Voice.Net.WebSockets.Requests;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Events;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Requests;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Meta.WitAi
{
    public class WitService : MonoBehaviour, IVoiceEventProvider, IVoiceActivationHandler, ITelemetryEventsProvider, IWitRuntimeConfigProvider, IWitConfigurationProvider
    {
        private float _lastMinVolumeLevelTime;

        /// <summary>
        /// Script used for publishing and subscribing to topics
        /// </summary>
        private WitWebSocketAdapter _webSocketAdapter;
        /// <summary>
        /// Access pub sub
        /// </summary>
        public IPubSubAdapter PubSub
        {
            get
            {
                SetupWebSockets();
                return _webSocketAdapter;
            }
        }

        // Request options
        private VoiceServiceRequest _recordingRequest;

        private bool _isSoundWakeActive;
        private RingBuffer<byte>.Marker _lastSampleMarker;
        private bool _minKeepAliveWasHit;
        private bool _isActive;
        private long _minSampleByteCount = 1024 * 10;

        private IVoiceEventProvider _voiceEventProvider;
        private ITelemetryEventsProvider _telemetryEventsProvider;
        private IWitRuntimeConfigProvider _runtimeConfigProvider;
        private ITranscriptionProvider _activeTranscriptionProvider;
        private Coroutine _timeLimitCoroutine;

        // Transcription based endpointing
        private bool _receivedTranscription;
        private float _lastWordTime;

        // Parallel Requests
        private HashSet<VoiceServiceRequest> _transmitRequests = new HashSet<VoiceServiceRequest>();
        private Coroutine _queueHandler;

        // Wit configuration provider
        public WitConfiguration Configuration => RuntimeConfiguration?.witConfiguration;

        #region Interfaces
        private IWitByteDataReadyHandler[] _dataReadyHandlers;
        private IWitByteDataSentHandler[] _dataSentHandlers;
        private IDynamicEntitiesProvider[] _dynamicEntityProviders;
        private float _time;

        #endregion

        /// <summary>
        /// Returns true if wit is currently active and listening with the mic
        /// </summary>
        public bool Active => _isActive || IsRequestActive;

        /// <summary>
        /// Active if recording, transmitting, or queued up
        /// </summary>
        public bool IsRequestActive
        {
            get
            {
                if (null != _recordingRequest && _recordingRequest.IsActive)
                {
                    return true;
                }
                return false;
            }
        }

        public IVoiceEventProvider VoiceEventProvider
        {
            get => _voiceEventProvider;
            set => _voiceEventProvider = value;
        }

        public ITelemetryEventsProvider TelemetryEventsProvider
        {
            get => _telemetryEventsProvider;
            set => _telemetryEventsProvider = value;
        }

        public IWitRuntimeConfigProvider ConfigurationProvider
        {
            get => _runtimeConfigProvider;
            set => _runtimeConfigProvider = value;
        }

        public WitRuntimeConfiguration RuntimeConfiguration =>
            _runtimeConfigProvider?.RuntimeConfiguration;

        public VoiceEvents VoiceEvents => _voiceEventProvider.VoiceEvents;

        public TelemetryEvents TelemetryEvents => _telemetryEventsProvider.TelemetryEvents;

        /// <summary>
        /// Gets/Sets a custom transcription provider. This can be used to replace any built in asr
        /// with an on device model or other provided source
        /// </summary>
        public ITranscriptionProvider TranscriptionProvider
        {
            get => _activeTranscriptionProvider;
            set
            {
                if (null != _activeTranscriptionProvider)
                {
                    _activeTranscriptionProvider.OnPartialTranscription.RemoveListener(
                        OnPartialTranscription);
                    _activeTranscriptionProvider.OnMicLevelChanged.RemoveListener(
                        OnTranscriptionMicLevelChanged);
                }

                _activeTranscriptionProvider = value;

                if (null != _activeTranscriptionProvider)
                {
                    _activeTranscriptionProvider.OnPartialTranscription.AddListener(
                        OnPartialTranscription);
                    _activeTranscriptionProvider.OnMicLevelChanged.AddListener(
                        OnTranscriptionMicLevelChanged);
                }
            }
        }

        /// <summary>
        /// Generic voice service request provider
        /// </summary>
        public IVoiceServiceRequestProvider RequestProvider { get; set; }

        public bool MicActive => _buffer.IsRecording(this);

        protected bool ShouldSendMicData => RuntimeConfiguration.sendAudioToWit ||
                                                  null == _activeTranscriptionProvider;

        /// <summary>
        /// Check configuration, client access token & app id
        /// </summary>
        public virtual bool IsConfigurationValid()
        {
            return RuntimeConfiguration.witConfiguration != null &&
                   !string.IsNullOrEmpty(RuntimeConfiguration.witConfiguration.GetClientAccessToken());
        }

        /// <summary>
        /// Get text input request based on settings
        /// </summary>
        private VoiceServiceRequest GetTextRequest(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents)
        {
            var newOptions = WitRequestFactory.GetSetupOptions(requestOptions, _dynamicEntityProviders);
            var newEvents = requestEvents ?? new VoiceServiceRequestEvents();
            requestOptions.InputType = NLPRequestInputType.Text;
            var config = Configuration;
            if (config != null && config.RequestType == WitRequestType.WebSocket)
            {
                SetupWebSockets();
            }
            if (RequestProvider != null)
            {
                var request = RequestProvider.CreateRequest(RuntimeConfiguration, requestOptions, newEvents);
                if (request != null)
                {
                    return request;
                }
            }
            if (config != null && config.RequestType == WitRequestType.WebSocket)
            {
                return WitSocketRequest.GetMessageRequest(config, _webSocketAdapter, newOptions, newEvents);
            }
            return config.CreateMessageRequest(requestOptions, newEvents, _dynamicEntityProviders);
        }

        /// <summary>
        /// Get audio input request based on settings
        /// </summary>
        private VoiceServiceRequest GetAudioRequest(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents)
        {
            var newOptions = WitRequestFactory.GetSetupOptions(requestOptions, _dynamicEntityProviders);
            var newEvents = requestEvents ?? new VoiceServiceRequestEvents();
            requestOptions.InputType = NLPRequestInputType.Audio;
            var config = Configuration;
            if (config != null && config.RequestType == WitRequestType.WebSocket)
            {
                SetupWebSockets();
            }
            if (RequestProvider != null)
            {
                var request = RequestProvider.CreateRequest(RuntimeConfiguration, newOptions, newEvents);
                if (request != null)
                {
                    return request;
                }
            }
            if (config != null && config.RequestType == WitRequestType.WebSocket)
            {
                return WitSocketRequest.GetSpeechRequest(config, _webSocketAdapter, _buffer, newOptions, newEvents);
            }
            return config.CreateSpeechRequest(newOptions, newEvents, _dynamicEntityProviders);
        }

        #region LIFECYCLE
        // Find transcription provider & Mic
        protected void Awake()
        {
            _dataReadyHandlers = GetComponents<IWitByteDataReadyHandler>();
            _dataSentHandlers = GetComponents<IWitByteDataSentHandler>();
            _runtimeConfigProvider = GetComponent<IWitRuntimeConfigProvider>();
        }
        // Add mic delegates
        protected void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _runtimeConfigProvider = GetComponent<IWitRuntimeConfigProvider>();
            _voiceEventProvider = GetComponent<IVoiceEventProvider>();

            if (null == _activeTranscriptionProvider && null != RuntimeConfiguration &&
                RuntimeConfiguration.customTranscriptionProvider)
            {
                TranscriptionProvider = RuntimeConfiguration.customTranscriptionProvider;
            }
            if (RuntimeConfiguration != null)
            {
                RuntimeConfiguration.OnConfigurationUpdated += RefreshConfigurationSettings;
            }

            SetMicDelegates(true);
            SetupWebSockets();
            if (_webSocketAdapter != null)
            {
                _webSocketAdapter.OnRequestGenerated += HandleWebSocketRequestGeneration;
            }
            _dynamicEntityProviders = GetComponents<IDynamicEntitiesProvider>();
        }
        // Remove mic delegates
        protected void OnDisable()
        {
            if (RuntimeConfiguration != null)
            {
                RuntimeConfiguration.OnConfigurationUpdated -= RefreshConfigurationSettings;
            }
            if (_webSocketAdapter != null)
            {
                _webSocketAdapter.OnRequestGenerated -= HandleWebSocketRequestGeneration;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SetMicDelegates(false);
        }
        /// <summary>
        /// Method called whenever the OnConfigurationUpdated action is invoked to re-init
        /// all runtime configuration based setup.
        /// </summary>
        protected virtual void RefreshConfigurationSettings()
        {
            SetupWebSockets();
        }
        // On scene refresh
        protected virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetMicDelegates(true);
        }
        // Toggle audio events
        private AudioBuffer _buffer;
        private bool _bufferDelegates = false;
        protected void SetMicDelegates(bool add)
        {
            // Obtain buffer
            if (_buffer == null)
            {
                _buffer = AudioBuffer.Instance;
                _bufferDelegates = false;
            }
            // Get events if possible
            AudioBufferEvents e = _buffer?.Events;
            if (e == null)
            {
                return;
            }
            // Already set
            if (_bufferDelegates == add)
            {
                return;
            }
            // Set delegates
            _bufferDelegates = add;

            // Add delegates
            if (add)
            {
                e.OnAudioStateChange += OnAudioBufferStateChange;
                e.OnMicLevelChanged.AddListener(OnMicLevelChanged);
                e.OnByteDataReady.AddListener(OnByteDataReady);
                e.OnSampleReady += OnMicSampleReady;
            }
            // Remove delegates
            else
            {
                e.OnAudioStateChange -= OnAudioBufferStateChange;
                e.OnMicLevelChanged.RemoveListener(OnMicLevelChanged);
                e.OnByteDataReady.RemoveListener(OnByteDataReady);
                e.OnSampleReady -= OnMicSampleReady;
            }
        }
        #endregion

        #region WEB SOCKETS
        /// <summary>
        /// Setup web socket adapter
        /// </summary>
        private void SetupWebSockets()
        {
            // Get/Add web socket adapter if not yet set
            if (!_webSocketAdapter)
            {
                _webSocketAdapter = gameObject.GetOrAddComponent<WitWebSocketAdapter>();
            }

            // Apply client provider and topic id if applicable
            var config = Configuration;
            bool useWebSockets = config != null && config.RequestType == WitRequestType.WebSocket;
            _webSocketAdapter.SetClientProvider(useWebSockets ? config : null);
            _webSocketAdapter.SetTopicId(useWebSockets ? RuntimeConfiguration.pubSubTopicId : null);
        }

        /// <summary>
        /// Handle web socket request if possible
        /// </summary>
        public void HandleWebSocketRequestGeneration(IWitWebSocketRequest webSocketRequest)
        {
            // Ignore if not message request
            var messageRequest = webSocketRequest as WitWebSocketMessageRequest;
            if (messageRequest == null)
            {
                return;
            }
            // Ignore if already wrapped by a voice service request
            if (IsWebSocketRequestWrapped(webSocketRequest))
            {
                return;
            }
            // Wrap web socket request
            var options = new WitRequestOptions(webSocketRequest.RequestId);
            var voiceRequest = WitSocketRequest.GetExternalRequest(messageRequest, RuntimeConfiguration.witConfiguration, _webSocketAdapter, options);
            SetupRequest(voiceRequest);
        }
        /// <summary>
        /// Returns true if the web socket request is wrapped by a referenced VoiceServiceRequest
        /// </summary>
        private bool IsWebSocketRequestWrapped(IWitWebSocketRequest webSocketRequest)
        {
            // True if recording audio request tracks this web socket request
            if (IsWebSocketRequestWrapped(_recordingRequest, webSocketRequest))
            {
                return true;
            }
            // True if any transmitting requests track this web socket request
            return _transmitRequests.FirstOrDefault((tRequest) => IsWebSocketRequestWrapped(tRequest, webSocketRequest)) != null;
        }
        /// <summary>
        /// Returns true if the voice service request wraps the specified web socket request
        /// </summary>
        private bool IsWebSocketRequestWrapped(VoiceServiceRequest voiceServiceRequest,
            IWitWebSocketRequest webSocketRequest)
        {
            return voiceServiceRequest is WitSocketRequest vsSocketRequest &&
                   vsSocketRequest.WebSocketRequest == webSocketRequest;
        }
        #endregion WEB SOCKETS

        #region ACTIVATION
        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public void Activate() => Activate(new WitRequestOptions());
        public void Activate(WitRequestOptions requestOptions) => Activate(requestOptions, new VoiceServiceRequestEvents());
        public VoiceServiceRequest Activate(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents)
        {
            // Not valid
            if (!IsConfigurationValid())
            {
                VLog.E($"Your AppVoiceExperience \"{gameObject.name}\" does not have a wit config assigned. Understanding Viewer activations will not trigger in game events..");
                return null;
            }
            // Already recording
            if (_isActive)
            {
                return null;
            }

            // Stop recording
            StopRecording();

            // Setup options
            if (requestOptions == null)
            {
                requestOptions = new WitRequestOptions();
            }

            // Now active
            _isActive = true;
            _lastSampleMarker = _buffer.CreateMarker(ConfigurationProvider.RuntimeConfiguration.preferredActivationOffset);
            _lastMinVolumeLevelTime = float.PositiveInfinity;
            _lastWordTime = float.PositiveInfinity;
            _receivedTranscription = false;

            // Generate request
            var request = GetAudioRequest(requestOptions, requestEvents);
            SetupRequest(request);

            // Start recording if possible
            if (ShouldSendMicData)
            {
                if (!_buffer.IsRecording(this))
                {
                    _minKeepAliveWasHit = false;
                    _isSoundWakeActive = true;
                    StartRecording();
                }
                else
                {
                    request.ActivateAudio();
                }
            }

            // Activate transcription provider
            _activeTranscriptionProvider?.Activate();

            // Return the generated request
            return _recordingRequest;
        }
        /// <summary>
        /// Activate the microphone and immediately send data to Wit for NLU processing.
        /// </summary>
        public void ActivateImmediately() => ActivateImmediately(new WitRequestOptions());
        public void ActivateImmediately(WitRequestOptions requestOptions) => ActivateImmediately(requestOptions, new VoiceServiceRequestEvents());
        public VoiceServiceRequest ActivateImmediately(WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents)
        {
            // Activate mic & generate request if possible
            var request = Activate(requestOptions, requestEvents);
            if (request == null)
            {
                return null;
            }

            // Send recording request
            SendRecordingRequest();

            // Start marker
            _lastSampleMarker = _buffer.CreateMarker(ConfigurationProvider
                .RuntimeConfiguration.preferredActivationOffset);

            // Return the request
            return request;
        }
        /// <summary>
        /// Sends recording request if possible
        /// </summary>
        protected virtual void SendRecordingRequest()
        {
            if (_recordingRequest == null || _recordingRequest.State != VoiceRequestState.Initialized)
            {
                return;
            }

            // Sound wake active
            _isSoundWakeActive = false;

            // Execute request
            if (ShouldSendMicData)
            {
                ExecuteRequest(_recordingRequest);
            }
        }
        /// <summary>
        /// Setup recording request
        /// </summary>
        /// <param name="recordingRequest"></param>
        protected void SetupRequest(VoiceServiceRequest newRequest)
        {
            // Setup audio recording request
            if (newRequest.Options.InputType == NLPRequestInputType.Audio)
            {
                // Only allow one at a time
                if (_recordingRequest == newRequest)
                {
                    return;
                }

                // Apply recording request
                _recordingRequest = newRequest;

                // Setup audio upload settings & callback
                if (_recordingRequest is IAudioUploadHandler audioUploader)
                {
                    audioUploader.AudioEncoding = _buffer.AudioEncoding;
                    audioUploader.OnInputStreamReady = OnWitReadyForData;
                }
                // TODO: Fix audio duration tracker to work with multiple request types (T184166691)
                if (_recordingRequest is WitRequest wr)
                {
                    wr.audioDurationTracker = new AudioDurationTracker(_recordingRequest.Options?.RequestId,
                        wr.AudioEncoding);
                }

                // Only used while recording
                _recordingRequest.Events.OnPartialTranscription.AddListener(OnPartialTranscription);
            }
            // Add to text request to transmit list
            else
            {
                _transmitRequests.Add(newRequest);
            }

            // Add additional events
            newRequest.Events.OnCancel.AddListener(HandleResult);
            newRequest.Events.OnFailed.AddListener(HandleResult);
            newRequest.Events.OnSuccess.AddListener(HandleResult);
            newRequest.Events.OnComplete.AddListener(HandleComplete);

            // Consider initialized
            VoiceEvents.OnRequestInitialized?.Invoke(newRequest);
        }
        /// <summary>
        /// Execute a wit request immediately
        /// </summary>
        /// <param name="recordingRequest"></param>
        public void ExecuteRequest(VoiceServiceRequest newRequest)
        {
            if (newRequest == null || newRequest.State != VoiceRequestState.Initialized)
            {
                return;
            }
            SetupRequest(newRequest);
            #pragma warning disable CS0618
            _timeLimitCoroutine = StartCoroutine(DeactivateDueToTimeLimit());
            newRequest.Send();
        }
        #endregion

        #region TEXT REQUESTS
        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public void Activate(string text) => Activate(text, new WitRequestOptions());
        public void Activate(string text, WitRequestOptions requestOptions) => Activate(text, requestOptions, new VoiceServiceRequestEvents());
        public VoiceServiceRequest Activate(string text, WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents)
        {
            // Not valid
            if (!IsConfigurationValid())
            {
                VLog.E($"Your AppVoiceExperience \"{gameObject.name}\" does not have a wit config assigned. Understanding Viewer activations will not trigger in game events..");
                return null;
            }

            // Handle option setup
            if (requestOptions == null)
            {
                requestOptions = new WitRequestOptions();
            }
            // Set request option text
            requestOptions.Text = text;

            // Generate request
            var request = GetTextRequest(requestOptions, requestEvents);
            SetupRequest(request);

            // Send & return
            request.Send();
            return request;
        }
        #endregion TEXT REQUESTS

        #region RECORDING
        // Stop any recording
        private void StopRecording()
        {
            if (!_buffer.IsRecording(this)) return;
            _buffer.StopRecording(this);
        }
        // When wit is ready, start recording
        private void OnWitReadyForData()
        {
            _lastMinVolumeLevelTime = _time;
            if (!_buffer.IsRecording(this))
            {
                StartRecording();
            }
        }
        // Handle begin recording
        private void StartRecording()
        {
            // Wait for input and then try again
            if (!_buffer.IsInputAvailable)
            {
                VoiceEvents.OnError.Invoke("Input Error", "No input source was available. Cannot activate for voice input.");
                return;
            }
            // Already recording
            if (_buffer.IsRecording(this))
            {
                return;
            }

            // Start recording
            _buffer.StartRecording(this);
        }
        // Callback from audio buffer if mic started successfully
        private void OnAudioBufferStateChange(VoiceAudioInputState audioInputState)
        {
            if (_recordingRequest != null)
            {
                // Success
                if (_buffer.IsRecording(this) && _recordingRequest.AudioInputState == VoiceAudioInputState.Off)
                {
                    _recordingRequest.ActivateAudio();
                }
                // Deactivate
                else if (!_buffer.IsRecording(this))
                {
                    // Deactivate
                    if (_recordingRequest.AudioInputState == VoiceAudioInputState.On
                        || _recordingRequest.AudioInputState == VoiceAudioInputState.Activating)
                    {
                        _recordingRequest.DeactivateAudio();
                    }
                    // Failed to start
                    else if (_recordingRequest.AudioInputState == VoiceAudioInputState.Off &&
                             _recordingRequest.State == VoiceRequestState.Initialized)
                    {
                        _recordingRequest.Cancel("Failed to start audio input");
                    }
                }
            }
        }
        // Callback for mic byte data ready
        private void OnByteDataReady(byte[] buffer, int offset, int length)
        {
            VoiceEvents?.OnByteDataReady.Invoke(buffer, offset, length);

            for (int i = 0; null != _dataReadyHandlers && i < _dataReadyHandlers.Length; i++)
            {
                _dataReadyHandlers[i].OnWitDataReady(buffer, offset, length);
            }
        }
        // Callback for mic sample data ready
        private void OnMicSampleReady(RingBuffer<byte>.Marker marker, float levelMax)
        {
            if (null == _lastSampleMarker || _recordingRequest == null) return;

            if (_minSampleByteCount > _lastSampleMarker.RingBuffer.Capacity)
            {
                _minSampleByteCount = _lastSampleMarker.RingBuffer.Capacity;
            }

            if (_recordingRequest.State == VoiceRequestState.Transmitting && IsInputStreamReady() && _lastSampleMarker.AvailableByteCount >= _minSampleByteCount)
            {
                // Flush the marker since the last read and send it to Wit
                _lastSampleMarker.ReadIntoWriters(
                    WriteAudio,
                    (buffer, offset, length) => VoiceEvents?.OnByteDataSent?.Invoke(buffer, offset, length),
                    (buffer, offset, length) =>
                    {
                        for (int i = 0; i < _dataSentHandlers.Length; i++)
                        {
                            _dataSentHandlers[i]?.OnWitDataSent(buffer, offset, length);
                        }
                    });

                if (_receivedTranscription)
                {
                    float elapsed = _time - _lastWordTime;
                    if (elapsed >
                        RuntimeConfiguration.minTranscriptionKeepAliveTimeInSeconds)
                    {
                        VLog.D($"Deactivated due to inactivity. No new words detected in {elapsed:0.00} seconds.");
                        DeactivateRequest(VoiceEvents?.OnStoppedListeningDueToInactivity);
                    }
                }
                else
                {
                    float elapsed = _time - _lastMinVolumeLevelTime;
                    if (elapsed >
                        RuntimeConfiguration.minKeepAliveTimeInSeconds)
                    {
                        VLog.D($"Deactivated due to inactivity. No sound detected in {elapsed:0.00} seconds.");
                        DeactivateRequest(VoiceEvents?.OnStoppedListeningDueToInactivity);
                    }
                }
            }
            else if (_isSoundWakeActive && levelMax > RuntimeConfiguration.soundWakeThreshold)
            {
                VoiceEvents?.OnMinimumWakeThresholdHit?.Invoke();
                SendRecordingRequest();
                _lastSampleMarker.Offset(RuntimeConfiguration.sampleLengthInMs * -2);
            }
        }
        // Whether or not the current recording stream is ready for audio data
        private bool IsInputStreamReady()
        {
            if (_recordingRequest is IAudioUploadHandler wr)
            {
                return wr.IsInputStreamReady;
            }
            return false;
        }
        // Write audio from audio buffer to the specified request
        private void WriteAudio(byte[] buffer, int offset, int length)
        {
            if (_recordingRequest is IDataUploadHandler uploadHandler)
            {
                uploadHandler.Write(buffer, offset, length);
            }
        }
        // Time tracking for multi-threaded callbacks
        private void Update()
        {
            _time = Time.time;
        }
        // Mic level change
        private void OnMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel) return;

            if (level > RuntimeConfiguration.minKeepAliveVolume)
            {
                _lastMinVolumeLevelTime = _time;
                _minKeepAliveWasHit = true;
            }
            VoiceEvents?.OnMicLevelChanged?.Invoke(level);
        }
        // Mic level changed in transcription
        private void OnTranscriptionMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(level);
            }
        }

        /// <summary>
        /// Finalizes audio duration tracker if possible
        /// </summary>
        private void FinalizeAudioDurationTracker()
        {
            // Ignore without recording request
            if (null == _recordingRequest)
            {
                return;
            }

            // Get audio duration tracker if possible
            // TODO: Fix audio duration tracker to work with multiple request types (T184166691)
            AudioDurationTracker audioDurationTracker = null;
            if (_recordingRequest is WitRequest witRequest)
            {
                audioDurationTracker = witRequest.audioDurationTracker;
            }
            if (audioDurationTracker == null)
            {
                return;
            }

            string requestId = _recordingRequest.Options?.RequestId;
            if (!string.Equals(requestId, audioDurationTracker.GetRequestId()))
            {
                VLog.W($"Mismatch in request IDs when finalizing AudioDurationTracker. " +
                       $"Expected {requestId} but got {audioDurationTracker.GetRequestId()}");
                return;
            }
            audioDurationTracker.FinalizeAudio();
            TelemetryEvents.OnAudioTrackerFinished?.Invoke(audioDurationTracker.GetFinalizeTimeStamp(), audioDurationTracker.GetAudioDuration());
        }
        #endregion

        #region DEACTIVATION
        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public void Deactivate()
        {
            DeactivateRequest(_buffer.IsRecording(this) ? VoiceEvents?.OnStoppedListeningDueToDeactivation : null, false);
        }

        /// <summary>
        /// Stop listening and cancel a specific report
        /// </summary>
        public void DeactivateAndAbortRequest(VoiceServiceRequest request)
        {
            if (request != null)
            {
                VoiceEvents?.OnAborting?.Invoke();
                request.Cancel();
            }
        }
        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        public void DeactivateAndAbortRequest()
        {
            DeactivateRequest(_buffer.IsRecording(this) ? VoiceEvents?.OnStoppedListeningDueToDeactivation : null, true);
        }
        // Stop listening if time expires
        private IEnumerator DeactivateDueToTimeLimit()
        {
            yield return new WaitForSeconds(RuntimeConfiguration.maxRecordingTime);
            if (IsRequestActive)
            {
                VLog.D($"Deactivated input due to timeout.\nMax Record Time: {RuntimeConfiguration.maxRecordingTime}");
                DeactivateRequest(VoiceEvents?.OnStoppedListeningDueToTimeout, false);
            }
        }
        private void DeactivateRequest(UnityEvent onComplete = null, bool abort = false)
        {
            // Aborting
            if (abort)
            {
                VoiceEvents?.OnAborting?.Invoke();
            }

            // Stop timeout coroutine
            if (null != _timeLimitCoroutine)
            {
                StopCoroutine(_timeLimitCoroutine);
                _timeLimitCoroutine = null;
            }

            // No longer active
            _isActive = false;

            // Stop recording
            StopRecording();
            FinalizeAudioDurationTracker();

            // Deactivate transcription provider
            _activeTranscriptionProvider?.Deactivate();

            // Deactivate recording request
            var previousRequest = _recordingRequest;
            _recordingRequest = null;
            DeactivateWitRequest(previousRequest, abort);

            // Abort transmitting requests
            if (abort)
            {
                HashSet<VoiceServiceRequest> requests = _transmitRequests;
                _transmitRequests = new HashSet<VoiceServiceRequest>();
                foreach (var request in requests)
                {
                    DeactivateWitRequest(request, true);
                }
            }
            // Transmit recording request
            else if (previousRequest != null && previousRequest.IsActive && _minKeepAliveWasHit)
            {
                _transmitRequests.Add(previousRequest);
                VoiceEvents?.OnMicDataSent?.Invoke();
            }
            // Disable below event
            _minKeepAliveWasHit = false;

            // Perform on complete event
            onComplete?.Invoke();
        }
        // Deactivate wit request
        private void DeactivateWitRequest(VoiceServiceRequest request, bool abort)
        {
            if (request == null)
            {
                return;
            }
            if (abort)
            {
                request.Cancel("Request was aborted by user.");
            }
            else if (request.IsAudioInputActivated)
            {
                request.DeactivateAudio();
            }
        }
        #endregion

        #region RESPONSE
        // Tracks transcription
        private void OnPartialTranscription(string transcription)
        {
            _receivedTranscription = true;
            _lastWordTime = _time;
        }

        // If result is obtained before transcription
        private void HandleResult(VoiceServiceRequest request)
        {
            if (request == _recordingRequest)
            {
                DeactivateRequest(null, false);
            }
        }

        // Removes service events
        private void HandleComplete(VoiceServiceRequest request)
        {
            // Remove all event listeners
            if (request.InputType == NLPRequestInputType.Audio)
            {
                request.Events.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            }
            request.Events.OnCancel.RemoveListener(HandleResult);
            request.Events.OnFailed.RemoveListener(HandleResult);
            request.Events.OnSuccess.RemoveListener(HandleResult);
            request.Events.OnComplete.RemoveListener(HandleComplete);

            // Remove from transmit list, missing if aborted
            if ( _transmitRequests.Contains(request))
            {
                _transmitRequests.Remove(request);
            }
        }
        #endregion
    }

    public interface IWitRuntimeConfigProvider
    {
        WitRuntimeConfiguration RuntimeConfiguration { get; }
    }

    public interface IWitRuntimeConfigSetter
    {
        WitRuntimeConfiguration RuntimeConfiguration { set; }
    }

    public interface IVoiceEventProvider
    {
        VoiceEvents VoiceEvents { get; }
    }

    public interface ITelemetryEventsProvider
    {
        TelemetryEvents TelemetryEvents { get; }
    }
}
