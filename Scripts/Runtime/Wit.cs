/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data;
using Facebook.WitAi.Events;
using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Lib;
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

        private IAudioInputSource _micInput;
        private WitRequestOptions _currentRequestOptions;
        private float _lastMinVolumeLevelTime;
        private WitRequest _activeRequest;
        private WitRequest _transmitRequest;

        private bool _isSoundWakeActive;
        private RingBuffer<byte> _micDataBuffer;
        private RingBuffer<byte>.Marker _lastSampleMarker;
        private byte[] _writeBuffer;
        private bool _minKeepAliveWasHit;
        private bool _isActive;
        private byte[] _byteDataBuffer;

        private ITranscriptionProvider _activeTranscriptionProvider;
        private Coroutine _timeLimitCoroutine;

        // Transcription based endpointing
        private bool _receivedTranscription;
        private float _lastWordTime;

        #region Interfaces
        private IWitByteDataReadyHandler[] _dataReadyHandlers;
        private IWitByteDataSentHandler[] _dataSentHandlers;
        private Coroutine _micInitCoroutine;

        #endregion

#if DEBUG_SAMPLE
        private FileStream sampleFile;
#endif

        /// <summary>
        /// Returns true if wit is currently active and listening with the mic
        /// </summary>
        public override bool Active => _isActive || IsRequestActive;

        public override bool IsRequestActive => null != _activeRequest && _activeRequest.IsActive;

        public WitRuntimeConfiguration RuntimeConfiguration
        {
            get => _runtimeConfiguration;
            set
            {
                _runtimeConfiguration = value;

                InitializeConfig();
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

        public override bool MicActive => null != _micInput && _micInput.IsRecording;

        protected override bool ShouldSendMicData => _runtimeConfiguration.sendAudioToWit ||
                                                  null == _activeTranscriptionProvider;

        #region LIFECYCLE
        // Find transcription provider & Mic
        private void Awake()
        {
            if (null == _activeTranscriptionProvider &&
                _runtimeConfiguration.customTranscriptionProvider)
            {
                TranscriptionProvider = _runtimeConfiguration.customTranscriptionProvider;
            }

            _micInput = GetComponent<IAudioInputSource>();
            if (_micInput == null)
            {
                _micInput = gameObject.AddComponent<Mic>();
            }

            _dataReadyHandlers = GetComponents<IWitByteDataReadyHandler>();
            _dataSentHandlers = GetComponents<IWitByteDataSentHandler>();
        }
        // Add mic delegates
        private void OnEnable()
        {
#if UNITY_EDITOR
            // Make sure we have a mic input after a script recompile
            if (null == _micInput)
            {
                _micInput = GetComponent<IAudioInputSource>();
            }
#endif

            _micInput.OnSampleReady += OnMicSampleReady;
            _micInput.OnStartRecording += OnMicStartListening;
            _micInput.OnStopRecording += OnMicStoppedListening;

            InitializeConfig();
        }
        // If always recording, begin now
        private void InitializeConfig()
        {
            if (_runtimeConfiguration.alwaysRecord)
            {
                StartRecording();
            }
        }
        // Remove mic delegates
        private void OnDisable()
        {
            _micInput.OnSampleReady -= OnMicSampleReady;
            _micInput.OnStartRecording -= OnMicStartListening;
            _micInput.OnStopRecording -= OnMicStoppedListening;
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
            if (_isActive) return;
            StopRecording();

            if (!_micInput.IsRecording && ShouldSendMicData)
            {
                _minKeepAliveWasHit = false;
                _isSoundWakeActive = true;

#if DEBUG_SAMPLE
                var file = Application.dataPath + "/test.pcm";
                sampleFile = File.Open(file, FileMode.Create);
                Debug.Log("Writing recording to file: " + file);
#endif

                StartRecording();
            }

            if (!_isActive)
            {
                _activeTranscriptionProvider?.Activate();
                _isActive = true;

                _lastMinVolumeLevelTime = float.PositiveInfinity;
                _currentRequestOptions = requestOptions;
            }
        }
        public override void ActivateImmediately()
        {
            ActivateImmediately(new WitRequestOptions());
        }
        public override void ActivateImmediately(WitRequestOptions requestOptions)
        {
            // Make sure we aren't checking activation time until
            // the mic starts recording. If we're already recording for a live
            // recording, we just triggered an activation so we will reset the
            // last minvolumetime to ensure a minimum time from activation time
            _lastMinVolumeLevelTime = float.PositiveInfinity;
            _lastWordTime = float.PositiveInfinity;
            _receivedTranscription = false;

            if (ShouldSendMicData)
            {
                _activeRequest = RuntimeConfiguration.witConfiguration.SpeechRequest(requestOptions);
                _activeRequest.audioEncoding = _micInput.AudioEncoding;
                _activeRequest.onPartialTranscription = OnPartialTranscription;
                _activeRequest.onFullTranscription = OnFullTranscription;
                _activeRequest.onInputStreamReady = r => OnWitReadyForData();
                _activeRequest.onResponse = HandleResult;
                events.OnRequestCreated?.Invoke(_activeRequest);
                _activeRequest.Request();
                _timeLimitCoroutine = StartCoroutine(DeactivateDueToTimeLimit());
            }

            if (!_isActive)
            {
                if (_runtimeConfiguration.alwaysRecord && null != _micDataBuffer)
                {
                    _lastSampleMarker = _micDataBuffer.CreateMarker();
                }
                _activeTranscriptionProvider?.Activate();
                _isActive = true;
            }
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

            if (_micInput.IsRecording && !_runtimeConfiguration.alwaysRecord)
            {
                _micInput.StopRecording();
                _lastSampleMarker = null;

#if DEBUG_SAMPLE
                sampleFile.Close();
#endif
            }
        }
        // When wit is ready, start recording
        private void OnWitReadyForData()
        {
            _lastMinVolumeLevelTime = Time.time;
            if (!_micInput.IsRecording && _micInput.IsInputAvailable)
            {
                _micInput.StartRecording(_runtimeConfiguration.sampleLengthInMs);
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
            if (!_micInput.IsInputAvailable)
            {
                _micInitCoroutine = StartCoroutine(WaitForMic());
                events.OnError.Invoke("Input Error", "No input source was available. Cannot activate for voice input.");
            }
            // Begin recording
            else
            {
                _micInput.StartRecording(_runtimeConfiguration.sampleLengthInMs);
                InitializeMicDataBuffer();
            }
        }
        // Wait until mic is available
        private IEnumerator WaitForMic()
        {
            yield return new WaitUntil(() => _micInput.IsInputAvailable);
            _micInitCoroutine = null;
            StartRecording();
        }
        // Generate mic data buffer if needed
        private void InitializeMicDataBuffer()
        {
            if (null == _micDataBuffer && _runtimeConfiguration.micBufferLengthInSeconds > 0)
            {
                _micDataBuffer = new RingBuffer<byte>((int) Mathf.Ceil(2 * _runtimeConfiguration.micBufferLengthInSeconds * 1000 * _runtimeConfiguration.sampleLengthInMs));
                _lastSampleMarker = _micDataBuffer.CreateMarker();
            }
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
        // Callback for mic sample ready
        private void OnMicSampleReady(int sampleCount, float[] sample, float levelMax)
        {
            if (null == TranscriptionProvider || !TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(levelMax);
            }

            if (null != _micDataBuffer)
            {
                if (_isSoundWakeActive && levelMax > _runtimeConfiguration.soundWakeThreshold)
                {
                    _lastSampleMarker = _micDataBuffer.CreateMarker(
                        (int) (-_runtimeConfiguration.micBufferLengthInSeconds * 1000 *
                               _runtimeConfiguration.sampleLengthInMs));
                }

                byte[] data = Convert(sample);
                _micDataBuffer.Push(data, 0, data.Length);
                if (data.Length > 0)
                {
                    events.OnByteDataReady?.Invoke(data, 0, data.Length);
                    for(int i = 0; null != _dataReadyHandlers && i < _dataReadyHandlers.Length; i++)
                    {
                        _dataReadyHandlers[i].OnWitDataReady(data, 0, data.Length);
                    }
                }
#if DEBUG_SAMPLE
                    sampleFile.Write(data, 0, data.Length);
#endif
            }

            if (IsRequestActive && _activeRequest.IsRequestStreamActive)
            {
                if (null != _micDataBuffer && _micDataBuffer.Capacity > 0)
                {
                    if (null == _writeBuffer)
                    {
                        _writeBuffer = new byte[sample.Length * 2];
                    }

                    // Flush the marker buffer to catch up
                    int read;
                    while ((read = _lastSampleMarker.Read(_writeBuffer, 0, _writeBuffer.Length, true)) > 0)
                    {
                        _activeRequest.Write(_writeBuffer, 0, read);
                        events.OnByteDataSent?.Invoke(_writeBuffer, 0, read);
                        for (int i = 0; null != _dataSentHandlers && i < _dataSentHandlers.Length; i++)
                        {
                            _dataSentHandlers[i].OnWitDataSent(_writeBuffer, 0, read);
                        }
                    }
                }
                else
                {
                    byte[] sampleBytes = Convert(sample);
                    _activeRequest.Write(sampleBytes, 0, sampleBytes.Length);
                }


                if (_receivedTranscription)
                {
                    if (Time.time - _lastWordTime >
                        _runtimeConfiguration.minTranscriptionKeepAliveTimeInSeconds)
                    {
                        Debug.Log("Deactivated due to inactivity. No new words detected.");
                        DeactivateRequest();
                        events.OnStoppedListeningDueToInactivity?.Invoke();
                    }
                }
                else if (Time.time - _lastMinVolumeLevelTime >
                         _runtimeConfiguration.minKeepAliveTimeInSeconds)
                {
                    Debug.Log("Deactivated input due to inactivity.");
                    DeactivateRequest();
                    events.OnStoppedListeningDueToInactivity?.Invoke();
                }
            }
            else if (_isSoundWakeActive && levelMax > _runtimeConfiguration.soundWakeThreshold)
            {
                events.OnMinimumWakeThresholdHit?.Invoke();
                _isSoundWakeActive = false;
                ActivateImmediately(_currentRequestOptions);
            }
        }
        // Convert
        private byte[] Convert(float[] samples)
        {
            var sampleCount = samples.Length;

            if (null == _byteDataBuffer || _byteDataBuffer.Length != sampleCount)
            {
                _byteDataBuffer = new byte[sampleCount * 2];
            }

            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < sampleCount; i++)
            {
                short data = (short) (samples[i] * rescaleFactor);
                _byteDataBuffer[i * 2] = (byte) data;
                _byteDataBuffer[i * 2 + 1] = (byte) (data >> 8);
            }

            return _byteDataBuffer;
        }
        // Mic level change
        private void OnMicLevelChanged(float level)
        {
            if (level > _runtimeConfiguration.minKeepAliveVolume)
            {
                _lastMinVolumeLevelTime = Time.time;
                _minKeepAliveWasHit = true;
            }

            events.OnMicLevelChanged?.Invoke(level);
        }
        #endregion

        #region DEACTIVATION
        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public override void Deactivate()
        {
            DeactivateRequest(false, true);
        }
        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        public override void DeactivateAndAbortRequest()
        {
            DeactivateRequest(true, true);
        }
        // Stop listening if time expires
        private IEnumerator DeactivateDueToTimeLimit()
        {
            yield return new WaitForSeconds(_runtimeConfiguration.maxRecordingTime);
            DeactivateRequest();
            events.OnStoppedListeningDueToTimeout?.Invoke();
        }
        private void DeactivateRequest(bool abort = false, bool checkRecord = false)
        {
            // Check if recording for callback
            var recording = _micInput.IsRecording;

            // Stop timeout coroutine
            if (null != _timeLimitCoroutine)
            {
                StopCoroutine(_timeLimitCoroutine);
                _timeLimitCoroutine = null;
            }

            // Stop recording
            StopRecording();
            _micDataBuffer?.Clear();
            _writeBuffer = null;

            // Deactivate transcription provider
            _activeTranscriptionProvider?.Deactivate();

            // Deactivate request
            if (IsRequestActive)
            {
                if (abort)
                {
                    _activeRequest.AbortRequest();
                }
                else
                {
                    _activeRequest.CloseRequestStream();
                }
                if (_minKeepAliveWasHit)
                {
                    events.OnMicDataSent?.Invoke();
                }
            }

            // Disable below event
            _minKeepAliveWasHit = false;

            // No longer active
            _isActive = false;

            // Stop listening event
            if (checkRecord && recording)
            {
                events.OnStoppedListeningDueToDeactivation?.Invoke();
            }
        }
        #endregion

        #region TRANSCRIPTION
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
            SendTranscription(text, requestOptions);
        }
        private void OnPartialTranscription(string transcription)
        {
            _receivedTranscription = true;
            _lastWordTime = Time.time;
            events.OnPartialTranscription.Invoke(transcription);
        }
        private void OnFullTranscription(string transcription)
        {
            DeactivateRequest();
            events.OnFullTranscription?.Invoke(transcription);
            if (_runtimeConfiguration.customTranscriptionProvider)
            {
                SendTranscription(transcription, new WitRequestOptions());
            }
        }
        private void SendTranscription(string transcription, WitRequestOptions requestOptions)
        {
            // Ensure not already active
            if (Active)
            {
                return;
            }
            _isActive = true;

            // Generate new request
            _activeRequest = RuntimeConfiguration.witConfiguration.MessageRequest(transcription, requestOptions);
            _activeRequest.onResponse = HandleResult;
            events.OnRequestCreated?.Invoke(_activeRequest);
            _activeRequest.Request();
        }
        private void OnTranscriptionMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(level);
            }
        }
        #endregion

        #region RESPONSE
        /// <summary>
        /// Main thread call to handle result callbacks
        /// </summary>
        private void HandleResult(WitRequest request)
        {
            _isActive = false;
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
            else
            {
                DeactivateRequest();
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

            events?.OnRequestCompleted?.Invoke();

            _activeRequest = null;
        }
        #endregion
    }

    public interface IWitRuntimeConfigProvider
    {
        WitRuntimeConfiguration RuntimeConfiguration { get; }
    }
}
