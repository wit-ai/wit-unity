/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data;
using Facebook.WitAi.Events;
using Facebook.WitAi.Interfaces;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Facebook.WitAi
{
    public class Wit : VoiceService, IWitRuntimeConfigProvider
    {
        [FormerlySerializedAs("runtimeConfiguration")]
        [Header("Wit Configuration")]
        [FormerlySerializedAs("configuration")]
        [Tooltip("The configuration that will be used when activating wit. This includes api key.")]
        [SerializeField]
        private WitRuntimeConfiguration _runtimeConfiguration = new WitRuntimeConfiguration();

        private WitRequestOptions _currentRequestOptions;
        private float _lastMinVolumeLevelTime;
        private WitRequest _recordingRequest;

        private bool _isSoundWakeActive;
        private RingBuffer<byte>.Marker _lastSampleMarker;
        private bool _minKeepAliveWasHit;
        private bool _isActive;
        private long _minSampleByteCount = 1024;

        private ITranscriptionProvider _activeTranscriptionProvider;
        private Coroutine _timeLimitCoroutine;

        // Transcription based endpointing
        private bool _receivedTranscription;
        private float _lastWordTime;

        // Parallel Requests
        private HashSet<WitRequest> _transmitRequests = new HashSet<WitRequest>();
        private HashSet<WitRequest> _queuedRequests = new HashSet<WitRequest>();
        private Coroutine _queueHandler;

        #region Interfaces
        private IWitByteDataReadyHandler[] _dataReadyHandlers;
        private IWitByteDataSentHandler[] _dataSentHandlers;
        private Coroutine _micInitCoroutine;
        private IDynamicEntitiesProvider[] _dynamicEntityProviders;

        #endregion

#if DEBUG_SAMPLE
        private FileStream sampleFile;
#endif

        /// <summary>
        /// Returns true if wit is currently active and listening with the mic
        /// </summary>
        public override bool Active => _isActive || IsRequestActive;

        public override bool IsRequestActive => null != _recordingRequest && _recordingRequest.IsActive;

        public WitRuntimeConfiguration RuntimeConfiguration
        {
            get => _runtimeConfiguration;
            set
            {
                _runtimeConfiguration = value;
            }
        }

        /// <summary>
        /// Gets/Sets a custom transcription provider. This can be used to replace any built in asr
        /// with an on device model or other provided source
        /// </summary>
        public override ITranscriptionProvider TranscriptionProvider
        {
            get => _activeTranscriptionProvider;
            set
            {
                if (null != _activeTranscriptionProvider)
                {
                    _activeTranscriptionProvider.OnFullTranscription.RemoveListener(
                        OnFullTranscription);
                    _activeTranscriptionProvider.OnPartialTranscription.RemoveListener(
                        OnPartialTranscription);
                    _activeTranscriptionProvider.OnMicLevelChanged.RemoveListener(
                        OnTranscriptionMicLevelChanged);
                    _activeTranscriptionProvider.OnStartListening.RemoveListener(
                        OnMicStartListening);
                    _activeTranscriptionProvider.OnStoppedListening.RemoveListener(
                        OnMicStoppedListening);
                }

                _activeTranscriptionProvider = value;

                if (null != _activeTranscriptionProvider)
                {
                    _activeTranscriptionProvider.OnFullTranscription.AddListener(
                        OnFullTranscription);
                    _activeTranscriptionProvider.OnPartialTranscription.AddListener(
                        OnPartialTranscription);
                    _activeTranscriptionProvider.OnMicLevelChanged.AddListener(
                        OnTranscriptionMicLevelChanged);
                    _activeTranscriptionProvider.OnStartListening.AddListener(
                        OnMicStartListening);
                    _activeTranscriptionProvider.OnStoppedListening.AddListener(
                        OnMicStoppedListening);
                }
            }
        }

        public override bool MicActive => AudioBuffer.Instance.IsRecording(this);

        protected override bool ShouldSendMicData => _runtimeConfiguration.sendAudioToWit ||
                                                  null == _activeTranscriptionProvider;

        #region LIFECYCLE
        // Find transcription provider & Mic
        protected override void Awake()
        {
            base.Awake();
            if (null == _activeTranscriptionProvider &&
                _runtimeConfiguration.customTranscriptionProvider)
            {
                TranscriptionProvider = _runtimeConfiguration.customTranscriptionProvider;
            }

            _dataReadyHandlers = GetComponents<IWitByteDataReadyHandler>();
            _dataSentHandlers = GetComponents<IWitByteDataSentHandler>();
        }
        // Add mic delegates
        protected override void OnEnable()
        {
            base.OnEnable();

            AudioBuffer.Instance.Events.OnMicLevelChanged.AddListener(OnMicLevelChanged);
            AudioBuffer.Instance.Events.OnByteDataReady.AddListener(OnByteDataReady);
            AudioBuffer.Instance.Events.OnSampleReady += OnMicSampleReady;

            _dynamicEntityProviders = GetComponents<IDynamicEntitiesProvider>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AudioBuffer.Instance.Events.OnMicLevelChanged.RemoveListener(OnMicLevelChanged);
            AudioBuffer.Instance.Events.OnByteDataReady.RemoveListener(OnByteDataReady);
            AudioBuffer.Instance.Events.OnSampleReady -= OnMicSampleReady;
        }
        #endregion

        #region ACTIVATION
        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public override void Activate()
        {
            Activate(new WitRequestOptions());
        }
        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public override void Activate(WitRequestOptions requestOptions)
        {
            if (!IsConfigurationValid())
            {
                Debug.LogError("Cannot activate without valid Wit Configuration.");
                return;
            }
            if (_isActive) return;
            StopRecording();
            _lastSampleMarker = AudioBuffer.Instance.CreateMarker();

            if (!AudioBuffer.Instance.IsRecording(this) && ShouldSendMicData)
            {
                _minKeepAliveWasHit = false;
                _isSoundWakeActive = true;

                StartRecording();
            }

            _activeTranscriptionProvider?.Activate();
            _isActive = true;

            _lastMinVolumeLevelTime = float.PositiveInfinity;
            _currentRequestOptions = requestOptions;
        }
        public override void ActivateImmediately()
        {
            ActivateImmediately(new WitRequestOptions());
        }
        public override void ActivateImmediately(WitRequestOptions requestOptions)
        {
            if (!IsConfigurationValid())
            {
                Debug.LogError("Cannot activate without valid Wit Configuration.");
                return;
            }
            // Make sure we aren't checking activation time until
            // the mic starts recording. If we're already recording for a live
            // recording, we just triggered an activation so we will reset the
            // last minvolumetime to ensure a minimum time from activation time
            _lastMinVolumeLevelTime = float.PositiveInfinity;
            _lastWordTime = float.PositiveInfinity;
            _receivedTranscription = false;

            if (ShouldSendMicData)
            {
                _recordingRequest = RuntimeConfiguration.witConfiguration.SpeechRequest(requestOptions, _dynamicEntityProviders);
                _recordingRequest.audioEncoding = AudioBuffer.Instance.AudioEncoding;
                _recordingRequest.onPartialTranscription = OnPartialTranscription;
                _recordingRequest.onFullTranscription = OnFullTranscription;
                _recordingRequest.onInputStreamReady = r => OnWitReadyForData();
                _recordingRequest.onResponse += HandleResult;
                events.OnRequestCreated?.Invoke(_recordingRequest);
                _recordingRequest.Request();
                _timeLimitCoroutine = StartCoroutine(DeactivateDueToTimeLimit());
            }

            if (!_isActive)
            {
                _activeTranscriptionProvider?.Activate();
                _isActive = true;
            }

#if DEBUG_SAMPLE
            if (null == sampleFile)
            {
                var file = Application.dataPath + "/test.pcm";
                sampleFile = File.Open(file, FileMode.Create);
                Debug.Log("Writing recording to file: " + file);
            }
#endif
            _lastSampleMarker = AudioBuffer.Instance.CreateMarker();
        }
        /// <summary>
        /// Send text data to Wit.ai for NLU processing
        /// </summary>
        /// <param name="text">Text to be processed</param>
        public override void Activate(string text)
        {
            Activate(text, new WitRequestOptions());
        }
        /// <summary>
        /// Send text data to Wit.ai for NLU processing
        /// </summary>
        /// <param name="text">Text to be processed</param>
        /// <param name="requestOptions">Additional options</param>
        public override void Activate(string text, WitRequestOptions requestOptions)
        {
            if (!IsConfigurationValid())
            {
                Debug.LogError("Cannot activate without valid Wit Configuration.");
                return;
            }
            SendTranscription(text, requestOptions);
        }
        /// <summary>
        /// Check configuration, client access token & app id
        /// </summary>
        public virtual bool IsConfigurationValid()
        {
            return _runtimeConfiguration.witConfiguration != null &&
                   !string.IsNullOrEmpty(_runtimeConfiguration.witConfiguration.clientAccessToken);
        }
        #endregion

        #region RECORDING
        // Stop any recording
        private void StopRecording()
        {
            if (null != _micInitCoroutine)
            {
                StopCoroutine(_micInitCoroutine);
                _micInitCoroutine = null;
            }

            AudioBuffer.Instance.StopRecording(this);

#if DEBUG_SAMPLE
            if (null != sampleFile)
            {
                Debug.Log($"Wrote test samples to {Application.dataPath}/test.pcm");
                sampleFile?.Close();
                sampleFile = null;
            }
#endif
        }
        // When wit is ready, start recording
        private void OnWitReadyForData()
        {
            _lastMinVolumeLevelTime = Time.time;
            if (!AudioBuffer.Instance.IsRecording(this))
            {
                StartRecording();
            }
        }
        // Handle begin recording
        private void StartRecording()
        {
            // Stop any init coroutine
            if (null != _micInitCoroutine)
            {
                StopCoroutine(_micInitCoroutine);
                _micInitCoroutine = null;
            }

            // Wait for input and then try again
            if (!AudioBuffer.Instance.IsInputAvailable)
            {
                _micInitCoroutine = StartCoroutine(WaitForMic());
                events.OnError.Invoke("Input Error", "No input source was available. Cannot activate for voice input.");
            }
            // Begin recording
            else
            {
                AudioBuffer.Instance.StartRecording(this);;
            }
        }
        // Wait until mic is available
        private IEnumerator WaitForMic()
        {
            yield return new WaitUntil(() => AudioBuffer.Instance.IsInputAvailable);
            _micInitCoroutine = null;
            StartRecording();
        }
        // Callback for mic start
        private void OnMicStartListening()
        {
            events?.OnStartListening?.Invoke();
        }
        // Callback for mic end
        private void OnMicStoppedListening()
        {
            events?.OnStoppedListening?.Invoke();
        }

        private void OnByteDataReady(byte[] buffer, int offset, int length)
        {
            events?.OnByteDataReady.Invoke(buffer, offset, length);

            for (int i = 0; null != _dataReadyHandlers && i < _dataReadyHandlers.Length; i++)
            {
                _dataReadyHandlers[i].OnWitDataReady(buffer, offset, length);
            }
        }

        // Callback for mic sample ready
        private void OnMicSampleReady(RingBuffer<byte>.Marker marker, float levelMax)
        {
            if (null != _lastSampleMarker && IsRequestActive && _recordingRequest.IsRequestStreamActive && _lastSampleMarker.AvailableByteCount >= _minSampleByteCount)
            {
                int read = 0;
                // Flush the marker since the last read and send it to Wit
                _lastSampleMarker.ReadIntoWriters(
                    (buffer, offset, length) =>
                    {
                        _recordingRequest.Write(buffer, offset, length);
                        #if DEBUG_SAMPLE
                        sampleFile?.Write(buffer, offset, length);
                        #endif
                    },
                    (buffer, offset, length) => events.OnByteDataSent?.Invoke(buffer, offset, length),
                    (buffer, offset, length) =>
                    {
                        for (int i = 0; i < _dataSentHandlers.Length; i++)
                        {
                            _dataSentHandlers[i]?.OnWitDataSent(buffer, offset, length);
                        }
                    });

                if (_receivedTranscription)
                {
                    if (Time.time - _lastWordTime >
                        _runtimeConfiguration.minTranscriptionKeepAliveTimeInSeconds)
                    {
                        Debug.Log("Deactivated due to inactivity. No new words detected.");
                        DeactivateRequest(events.OnStoppedListeningDueToInactivity);
                    }
                }
                else if (Time.time - _lastMinVolumeLevelTime >
                         _runtimeConfiguration.minKeepAliveTimeInSeconds)
                {
                    Debug.Log("Deactivated input due to inactivity.");
                    DeactivateRequest(events.OnStoppedListeningDueToInactivity);
                }
            }
            else if (_isSoundWakeActive && levelMax > _runtimeConfiguration.soundWakeThreshold)
            {
                events.OnMinimumWakeThresholdHit?.Invoke();
                _isSoundWakeActive = false;
                ActivateImmediately(_currentRequestOptions);
                _lastSampleMarker.Offset(_runtimeConfiguration.sampleLengthInMs * -2);
            }
        }
        // Mic level change
        private void OnMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel) return;

            if (level > _runtimeConfiguration.minKeepAliveVolume)
            {
                _lastMinVolumeLevelTime = Time.time;
                _minKeepAliveWasHit = true;
            }
            events.OnMicLevelChanged?.Invoke(level);
        }
        // Mic level changed in transcription
        private void OnTranscriptionMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(level);
            }
        }
        #endregion

        #region DEACTIVATION
        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public override void Deactivate()
        {
            DeactivateRequest(AudioBuffer.Instance.IsRecording(this) ? events.OnStoppedListeningDueToDeactivation : null, false);
        }
        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        public override void DeactivateAndAbortRequest()
        {
            events.OnAborting.Invoke();
            DeactivateRequest(AudioBuffer.Instance.IsRecording(this) ? events.OnStoppedListeningDueToDeactivation : null, true);
        }
        // Stop listening if time expires
        private IEnumerator DeactivateDueToTimeLimit()
        {
            yield return new WaitForSeconds(_runtimeConfiguration.maxRecordingTime);
            if (IsRequestActive)
            {
                Debug.Log($"Deactivated input due to timeout.\nMax Record Time: {_runtimeConfiguration.maxRecordingTime}");
                DeactivateRequest(events.OnStoppedListeningDueToTimeout, false);
            }
        }
        private void DeactivateRequest(UnityEvent onComplete = null, bool abort = false)
        {
            // Stop timeout coroutine
            if (null != _timeLimitCoroutine)
            {
                StopCoroutine(_timeLimitCoroutine);
                _timeLimitCoroutine = null;
            }

            // Stop recording
            StopRecording();

            // Deactivate transcription provider
            _activeTranscriptionProvider?.Deactivate();

            // Deactivate recording request
            bool isRecordingRequestActive = IsRequestActive;
            DeactivateWitRequest(_recordingRequest, abort);

            // Abort transmitting requests
            if (abort)
            {
                AbortQueue();
                foreach (var request in _transmitRequests)
                {
                    DeactivateWitRequest(request, true);
                }
                _transmitRequests.Clear();
            }
            // Transmit recording request
            else if (isRecordingRequestActive && _minKeepAliveWasHit)
            {
                _transmitRequests.Add(_recordingRequest);
                _recordingRequest = null;
                events.OnMicDataSent?.Invoke();
            }

            // Disable below event
            _minKeepAliveWasHit = false;

            // No longer active
            _isActive = false;

            // Perform on complete event
            onComplete?.Invoke();
        }
        // Deactivate wit request
        private void DeactivateWitRequest(WitRequest request, bool abort)
        {
            if (request != null && request.IsActive)
            {
                if (abort)
                {
                    request.AbortRequest();
                }
                else
                {
                    request.CloseRequestStream();
                }
            }
        }
        #endregion

        #region TRANSCRIPTION
        private void OnPartialTranscription(string transcription)
        {
            // Clear record data
            _receivedTranscription = true;
            _lastWordTime = Time.time;
            // Delegate
            events.OnPartialTranscription.Invoke(transcription);
        }
        private void OnFullTranscription(string transcription)
        {
            // End existing request
            DeactivateRequest(null);
            // Delegate
            events.OnFullTranscription?.Invoke(transcription);
            // Send transcription
            if (_runtimeConfiguration.customTranscriptionProvider)
            {
                SendTranscription(transcription, new WitRequestOptions());
            }
        }
        private void SendTranscription(string transcription, WitRequestOptions requestOptions)
        {
            // Create request & add response delegate
            WitRequest request = RuntimeConfiguration.witConfiguration.MessageRequest(transcription, requestOptions, _dynamicEntityProviders);
            request.onResponse += HandleResult;

            // Call on create delegate
            events.OnRequestCreated?.Invoke(request);

            // Add to queue
            AddToQueue(request);
        }
        #endregion

        #region QUEUE
        // Add request to wait queue
        private void AddToQueue(WitRequest request)
        {
            // In editor or disabled, do not queue
            if (!Application.isPlaying || _runtimeConfiguration.maxConcurrentRequests <= 0)
            {
                _transmitRequests.Add(request);
                request.Request();
                return;
            }

            // Add to queue
            _queuedRequests.Add(request);

            // If not running, begin
            if (_queueHandler == null)
            {
                _queueHandler = StartCoroutine(PerformDequeue());
            }
        }
        // Abort request
        private void AbortQueue()
        {
            if (_queueHandler != null)
            {
                StopCoroutine(_queueHandler);
                _queueHandler = null;
            }
            foreach (var request in _queuedRequests)
            {
                DeactivateWitRequest(request, true);
            }
            _queuedRequests.Clear();
        }
        // Coroutine used to send transcriptions when possible
        private IEnumerator PerformDequeue()
        {
            // Perform until no requests remain
            while (_queuedRequests.Count > 0)
            {
                // Wait a frame to space out requests
                yield return new WaitForEndOfFrame();

                // If space, dequeue & request
                if (_transmitRequests.Count < _runtimeConfiguration.maxConcurrentRequests)
                {
                    // Dequeue
                    WitRequest request = _queuedRequests.First();
                    _queuedRequests.Remove(request);

                    // Transmit
                    _transmitRequests.Add(request);
                    request.Request();
                }
            }

            // Kill coroutine
            _queueHandler = null;
        }
        #endregion

        #region RESPONSE
        /// <summary>
        /// Main thread call to handle result callbacks
        /// </summary>
        private void HandleResult(WitRequest request)
        {
            // If result is obtained before transcription
            if (request == _recordingRequest)
            {
                DeactivateRequest(null, false);
            }

            // Handle success
            if (request.StatusCode == (int) HttpStatusCode.OK)
            {
                if (null != request.ResponseData)
                {
                    events?.OnResponse?.Invoke(request.ResponseData);
                }
                else
                {
                    events?.OnError?.Invoke("No Data", "No data was returned from the server.");
                }
            }
            // Handle failure
            else
            {
                if (request.StatusCode != WitRequest.ERROR_CODE_ABORTED)
                {
                    events?.OnError?.Invoke("HTTP Error " + request.StatusCode,
                        request.StatusDescription);
                }
                else
                {
                    events?.OnAborted?.Invoke();
                }
            }
            // Remove from transmit list, missing if aborted
            if ( _transmitRequests.Contains(request))
            {
                _transmitRequests.Remove(request);
            }

            // Complete delegate
            events?.OnRequestCompleted?.Invoke();
        }
        #endregion
    }

    public interface IWitRuntimeConfigProvider
    {
        WitRuntimeConfiguration RuntimeConfiguration { get; }
    }
}
