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
using com.facebook.witai.data;
using com.facebook.witai.interfaces;
using com.facebook.witai.lib;
using UnityEngine.Serialization;

namespace com.facebook.witai
{
    public class Wit : VoiceService, IWitRuntimeConfigProvider
    {
        [Header("Wit Configuration")]
        [FormerlySerializedAs("configuration")]
        [Tooltip("The configuration that will be used when activating wit. This includes api key.")]
        [SerializeField] private WitRuntimeConfiguration runtimeConfiguration = new WitRuntimeConfiguration();

        private float activationTime;
        private IAudioInputSource micInput;
        private float lastMinVolumeLevelTime;
        private WitRequest activeRequest;

        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

        private bool isSoundWakeActive;
        private RingBuffer<byte> micDataBuffer;
        private RingBuffer<byte>.Marker lastSampleMarker;
        private byte[] writeBuffer;
        private bool minKeepAliveWasHit;
        private bool isActive;

        private ITranscriptionProvider activeTranscriptionProvider;
        private Coroutine timeLimitCoroutine;

        // Transcription based endpointing
        private bool receivedTranscription;
        private float lastWordTime;

        #if DEBUG_SAMPLE
        private FileStream sampleFile;
        #endif

        /// <summary>
        /// Returns true if wit is currently active and listening with the mic
        /// </summary>
        public override bool Active => isActive || IsRequestActive;

        public override bool IsRequestActive => null != activeRequest && activeRequest.IsActive;

        public WitRuntimeConfiguration RuntimeConfiguration
        {
            get => runtimeConfiguration;
            set => runtimeConfiguration = value;
        }

        /// <summary>
        /// Gets/Sets a custom transcription provider. This can be used to replace any built in asr
        /// with an on device model or other provided source
        /// </summary>
        public override ITranscriptionProvider TranscriptionProvider
        {
            get => activeTranscriptionProvider;
            set
            {
                if (null != activeTranscriptionProvider)
                {
                    activeTranscriptionProvider.OnFullTranscription.RemoveListener(
                        OnFullTranscription);
                    activeTranscriptionProvider.OnPartialTranscription.RemoveListener(
                        OnPartialTranscription);
                    activeTranscriptionProvider.OnMicLevelChanged.RemoveListener(OnTranscriptionMicLevelChanged);
                    activeTranscriptionProvider.OnStartListening.RemoveListener(
                        OnStartListening);
                    activeTranscriptionProvider.OnStoppedListening.RemoveListener(
                        OnStoppedListening);
                }
                activeTranscriptionProvider = value;

                if (null != activeTranscriptionProvider)
                {
                    activeTranscriptionProvider.OnFullTranscription.AddListener(
                        OnFullTranscription);
                    activeTranscriptionProvider.OnPartialTranscription.AddListener(
                        OnPartialTranscription);
                    activeTranscriptionProvider.OnMicLevelChanged.AddListener(
                        OnTranscriptionMicLevelChanged);
                    activeTranscriptionProvider.OnStartListening.AddListener(
                        OnStartListening);
                    activeTranscriptionProvider.OnStoppedListening.AddListener(
                        OnStoppedListening);
                }
            }
        }

        public override bool MicActive => micInput.IsRecording;

        public override bool ShouldSendMicData => runtimeConfiguration.sendAudioToWit || null == activeTranscriptionProvider;

        private void Awake()
        {
            if (null == activeTranscriptionProvider &&
                runtimeConfiguration.customTranscriptionProvider)
            {
                TranscriptionProvider = runtimeConfiguration.customTranscriptionProvider;
            }

            micInput = GetComponent<IAudioInputSource>();
            if (micInput == null)
            {
                micInput = gameObject.AddComponent<Mic>();
            }
        }

        private void OnEnable()
        {
            if (!runtimeConfiguration.witConfiguration)
            {
                Debug.LogError("Wit configuration is not set on your Wit component. Requests cannot be made without a configuration. Wit will be disabled at runtime until the configuration has been set.");
                enabled = false;
                return;
            }

            #if UNITY_EDITOR
            // Make sure we have a mic input after a script recompile
            if (null == micInput)
            {
                micInput = GetComponent<IAudioInputSource>();
            }
            #endif

            micInput.OnSampleReady += OnSampleReady;
            micInput.OnStartRecording += OnStartListening;
            micInput.OnStopRecording += OnStoppedListening;
        }

        private void OnDisable()
        {
            micInput.OnSampleReady -= OnSampleReady;
            micInput.OnStartRecording -= OnStartListening;
            micInput.OnStopRecording -= OnStoppedListening;
        }

        private void OnSampleReady(int sampleCount, float[] sample, float levelMax)
        {
            if (null == TranscriptionProvider || !TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(levelMax);
            }

            if (null != micDataBuffer)
            {
                if (isSoundWakeActive && levelMax > runtimeConfiguration.soundWakeThreshold)
                {
                    lastSampleMarker = micDataBuffer.CreateMarker();
                }

                if (null != lastSampleMarker)
                {
                    byte[] data = Convert(sample);
                    micDataBuffer.Push(data, 0, data.Length);
                    #if DEBUG_SAMPLE
                    sampleFile.Write(data, 0, data.Length);
                    #endif
                }
            }

            if (IsRequestActive && activeRequest.IsRequestStreamActive)
            {
                if (null != micDataBuffer && micDataBuffer.Capacity > 0)
                {
                    if (null == writeBuffer)
                    {
                        writeBuffer = new byte[sample.Length * 2];
                    }

                    // Flush the marker buffer to catch up
                    int read;
                    while ((read = lastSampleMarker.Read(writeBuffer, 0, writeBuffer.Length)) > 0)
                    {
                        activeRequest.Write(writeBuffer, 0, read);
                    }
                }
                else
                {
                    byte[] sampleBytes = Convert(sample);
                    activeRequest.Write(sampleBytes, 0, sampleBytes.Length);
                }


                if (receivedTranscription)
                {
                    if(Time.time - lastWordTime > runtimeConfiguration.minTranscriptionKeepAliveTimeInSeconds)
                    {
                        Debug.Log("Deactivated due to inactivity. No new words detected.");
                        DeactivateRequest();
                        events.OnStoppedListeningDueToInactivity?.Invoke();
                    }
                }
                else if (Time.time - lastMinVolumeLevelTime > runtimeConfiguration.minKeepAliveTimeInSeconds)
                {
                    Debug.Log("Deactivated input due to inactivity.");
                    DeactivateRequest();
                    events.OnStoppedListeningDueToInactivity?.Invoke();
                }
            }
            else if(!isSoundWakeActive)
            {
                DeactivateRequest();
            }
            else if (isSoundWakeActive && levelMax > runtimeConfiguration.soundWakeThreshold)
            {
                events.OnMinimumWakeThresholdHit?.Invoke();
                isSoundWakeActive = false;
                ActivateImmediately();
            }
        }

        private void OnFullTranscription(string transcription)
        {
            DeactivateRequest();
            events.OnFullTranscription?.Invoke(transcription);
            if (runtimeConfiguration.customTranscriptionProvider)
            {
                SendTranscription(transcription);
            }
        }

        private void OnPartialTranscription(string transcription)
        {
            receivedTranscription = true;
            lastWordTime = Time.time;
            events.OnPartialTranscription.Invoke(transcription);
        }

        private void OnTranscriptionMicLevelChanged(float level)
        {
            if (null != TranscriptionProvider && TranscriptionProvider.OverrideMicLevel)
            {
                OnMicLevelChanged(level);
            }
        }

        private void OnMicLevelChanged(float level)
        {
            if (level > runtimeConfiguration.minKeepAliveVolume)
            {
                lastMinVolumeLevelTime = Time.time;
                minKeepAliveWasHit = true;
            }
            events.OnMicLevelChanged?.Invoke(level);
        }

        private void OnStoppedListening()
        {
            events?.OnStoppedListening?.Invoke();
        }


        private void OnStartListening()
        {
            events?.OnStartListening?.Invoke();
        }

        private void Update()
        {
            if (updateQueue.Count > 0)
            {
                if (updateQueue.TryDequeue(out var result)) result.Invoke();
            }
        }

        private IEnumerator DeactivateDueToTimeLimit()
        {
            yield return new WaitForSeconds(runtimeConfiguration.maxRecordingTime);
            Debug.Log("Deactivated due to time limit.");
            DeactivateRequest();
            events.OnStoppedListeningDueToTimeout?.Invoke();
            timeLimitCoroutine = null;
        }

        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public override void Activate()
        {
            if (!micInput.IsRecording && ShouldSendMicData)
            {
                if (null == micDataBuffer && runtimeConfiguration.micBufferLengthInSeconds > 0)
                {
                    micDataBuffer = new RingBuffer<byte>((int) Mathf.Ceil( 2 *
                        runtimeConfiguration.micBufferLengthInSeconds * 1000 *
                        runtimeConfiguration.sampleLengthInMs));
                }

                minKeepAliveWasHit = false;

                #if DEBUG_SAMPLE
                var file = Application.dataPath + "/test.pcm";
                sampleFile = File.Open(file, FileMode.Create);
                Debug.Log("Writing recording to file: " + file);
                #endif

                micInput.StartRecording(sampleLen: runtimeConfiguration.sampleLengthInMs);
                isSoundWakeActive = true;
            }

            if (!isActive)
            {
                activeTranscriptionProvider?.Activate();
                isActive = true;

                lastMinVolumeLevelTime = float.PositiveInfinity;
            }
        }

        public override void ActivateImmediately()
        {
            // Make sure we aren't checking activation time until
            // the mic starts recording. If we're already recording for a live
            // recording, we just triggered an activation so we will reset the
            // last minvolumetime to ensure a minimum time from activation time
            activationTime = float.PositiveInfinity;
            lastMinVolumeLevelTime = float.PositiveInfinity;
            lastWordTime = float.PositiveInfinity;
            receivedTranscription = false;

            if (ShouldSendMicData)
            {
                activeRequest = RuntimeConfiguration.witConfiguration.SpeechRequest();
                activeRequest.audioEncoding = micInput.AudioEncoding;
                activeRequest.onPartialTranscription =
                    s => updateQueue.Enqueue(() => OnPartialTranscription(s));
                activeRequest.onFullTranscription =
                    s => updateQueue.Enqueue(() => OnFullTranscription(s));
                activeRequest.onInputStreamReady = (r) => updateQueue.Enqueue(OnWitReadyForData);
                activeRequest.onResponse = QueueResult;
                events.OnRequestCreated?.Invoke(activeRequest);
                activeRequest.Request();
                timeLimitCoroutine = StartCoroutine(DeactivateDueToTimeLimit());
            }

            if (!isActive)
            {
                activeTranscriptionProvider?.Activate();
                isActive = true;
            }
        }

        private void OnWitReadyForData()
        {
            activationTime = Time.time;
            lastMinVolumeLevelTime = Time.time;
            if (!micInput.IsRecording)
            {
                micInput.StartRecording(runtimeConfiguration.sampleLengthInMs);
            }
        }

        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public override void Deactivate()
        {
            var recording = micInput.IsRecording;
            DeactivateRequest();

            if (recording)
            {
                events.OnStoppedListeningDueToDeactivation?.Invoke();
            }
        }

        private void DeactivateRequest()
        {
            if (null != timeLimitCoroutine)
            {
                StopCoroutine(timeLimitCoroutine);
                timeLimitCoroutine = null;
            }

            if (micInput.IsRecording)
            {
                micInput.StopRecording();

                #if DEBUG_SAMPLE
                sampleFile.Close();
                #endif
            }
            if (null != micDataBuffer) micDataBuffer.Clear();
            writeBuffer = null;
            lastSampleMarker = null;
            minKeepAliveWasHit = false;

            activeTranscriptionProvider?.Deactivate();

            if (isActive)
            {
                if (IsRequestActive)
                {

                    activeRequest.CloseRequestStream();
                    if (minKeepAliveWasHit)
                    {
                        events.OnMicDataSent?.Invoke();
                    }
                }
                else
                {
                    isActive = false;
                }
            }
        }

        static byte[] Convert(float[] samples)
        {
            var sampleCount = samples.Length;

            Int16[] intData = new Int16[sampleCount];
            //converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

            Byte[] bytesData = new Byte[sampleCount * 2];
            //bytesData array is twice the size of
            //dataSource array because a float converted in Int16 is 2 bytes.

            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < sampleCount; i++)
            {
                intData[i] = (short) (samples[i] * rescaleFactor);
                Byte[] byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            return bytesData;
        }

        /// <summary>
        /// Send text data to Wit.ai for NLU processing
        /// </summary>
        /// <param name="transcription"></param>
        public override void Activate(string transcription)
        {
            if (Active) return;

            SendTranscription(transcription);
        }

        private void SendTranscription(string transcription)
        {
            isActive = true;
            activeRequest = RuntimeConfiguration.witConfiguration.MessageRequest(transcription);
            activeRequest.onResponse = QueueResult;
            events.OnRequestCreated?.Invoke(activeRequest);
            activeRequest.Request();
        }

        /// <summary>
        /// Enqueues a result to be handled on the main thread in Wit's next Update call
        /// </summary>
        /// <param name="request"></param>
        private void QueueResult(WitRequest request)
        {
            updateQueue.Enqueue(() => HandleResult(request));
        }

        /// <summary>
        /// Main thread call to handle result callbacks
        /// </summary>
        /// <param name="request"></param>
        private void HandleResult(WitRequest request)
        {
            isActive = false;
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
                events?.OnError?.Invoke("HTTP Error " + request.StatusCode,
                    "There was an error requesting data from the server.");
            }

            activeRequest = null;
        }
    }

    public interface IWitRuntimeConfigProvider
    {
        WitRuntimeConfiguration RuntimeConfiguration { get; }
    }
}
