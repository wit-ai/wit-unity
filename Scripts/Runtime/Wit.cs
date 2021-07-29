/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using com.facebook.witai.data;
using com.facebook.witai.events;
using com.facebook.witai.lib;

namespace com.facebook.witai
{
    public class Wit : MonoBehaviour
    {
        [Tooltip("The configuration that will be used when activating wit. This includes api key.")]
        [SerializeField] private WitConfiguration configuration;

        [Header("Keepalive")]
        [Tooltip("The minimum volume from the mic needed to keep the activation alive")]
        [SerializeField] private float minKeepAliveVolume = .01f;
        [Tooltip("The amount of time an activation will be kept open after volume is under the keep alive threshold")]
        [SerializeField] private float minKeepAliveTime = 2f;
        [Tooltip("The maximum amount of time the mic will stay active")]
        [Range(0, 10f)]
        [SerializeField] private float maxRecordingTime = 10;

        [Header("Sound Activation")]
        [SerializeField] private float soundWakeThreshold = .01f;
        [Range(10, 500)]
        [SerializeField] private int sampleLengthInMs = 10;
        [SerializeField] private float micBufferLengthInSeconds = 1;

        [Tooltip("Events that will fire before, during and after an activation")]
        [SerializeField] public WitEvents events = new WitEvents();

        private float activationTime;
        private Mic micInput;
        private float lastMinVolumeLevelTime;
        private WitRequest activeRequest;

        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

        private bool isSoundWakeActive;
        private RingBuffer<byte> micDataBuffer;
        private RingBuffer<byte>.Marker lastSampleMarker;
        private byte[] writeBuffer;
        private bool minKeepAliveWasHit;
        #if DEBUG_SAMPLE
        private FileStream sampleFile;
        #endif

        /// <summary>
        /// Returns true if wit is currently active and listening with the mic
        /// </summary>
        public bool Active => null != activeRequest && activeRequest.IsActive;

        /// <summary>
        /// Gets/Sets the wit configuration to be used for activations.
        /// </summary>
        public WitConfiguration Configuration
        {
            get => configuration;
            set => configuration = value;
        }

        public bool MicActive => micInput.IsRecording;

        private void Awake()
        {
            micInput = gameObject.AddComponent<Mic>();
        }

        private void OnEnable()
        {
            micInput.OnSampleReady += OnSampleReady;
            micInput.OnStartRecording += () => events?.OnStartListening?.Invoke();
            micInput.OnStopRecording += () => events?.OnStoppedListening?.Invoke();
        }

        private void OnDisable()
        {
            micInput.OnSampleReady -= OnSampleReady;
        }

        private void OnSampleReady(int sampleCount, float[] sample, float levelMax)
        {
            if (levelMax > minKeepAliveVolume)
            {
                lastMinVolumeLevelTime = Time.time;
                minKeepAliveWasHit = true;
            }

            events?.OnMicLevelChanged?.Invoke(levelMax);

            if (null != micDataBuffer)
            {
                if (isSoundWakeActive && levelMax > soundWakeThreshold)
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

            if (null != activeRequest && activeRequest.IsRequestStreamActive)
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
            }
            else if(!isSoundWakeActive)
            {
                DeactivateRequest();
            }
            else if (isSoundWakeActive && levelMax > soundWakeThreshold)
            {
                isSoundWakeActive = false;
                ActivateImmediately();
            }
        }


        private void Update()
        {
            if (updateQueue.Count > 0)
            {
                if (updateQueue.TryDequeue(out var result)) result.Invoke();
            }

            if (Active && micInput.IsRecording)
            {
                if (Time.time - lastMinVolumeLevelTime >= minKeepAliveTime)
                {
                    Debug.Log("Deactivated input due to inactivity.");
                    DeactivateRequest();
                    events.OnStoppedListeningDueToInactivity?.Invoke();
                }
                else if (Time.time - activationTime >= maxRecordingTime)
                {
                    Debug.Log("Deactivated due to time limit.");
                    DeactivateRequest();
                    events.OnStoppedListeningDueToTimeout?.Invoke();
                }
            }
        }

        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public void Activate()
        {
            if (!micInput.IsRecording)
            {
                if (null == micDataBuffer && micBufferLengthInSeconds > 0)
                {
                    micDataBuffer = new RingBuffer<byte>((int) Mathf.Ceil( 2 * micBufferLengthInSeconds * 1000 * sampleLengthInMs));
                }

                minKeepAliveWasHit = false;

                #if DEBUG_SAMPLE
                var file = Application.dataPath + "/test.pcm";
                sampleFile = File.Open(file, FileMode.Create);
                Debug.Log("Writing recording to file: " + file);
                #endif

                micInput.StartRecording(WitRequest.samplerate, sampleLen: sampleLengthInMs);
                isSoundWakeActive = true;
            }
        }

        public void ActivateImmediately()
        {
            // Make sure we aren't checking activation time until
            // the mic starts recording.
            activationTime = float.PositiveInfinity;
            lastMinVolumeLevelTime = float.PositiveInfinity;

            activeRequest = Configuration.SpeechRequest();
            activeRequest.onInputStreamReady = (r) => updateQueue.Enqueue(OnWitReadyForData);
            activeRequest.onResponse = QueueResult;
            events.OnRequestCreated?.Invoke(activeRequest);
            activeRequest.Request();
        }

        private void OnWitReadyForData()
        {
            activationTime = Time.time;
            lastMinVolumeLevelTime = Time.time;
            if (!micInput.IsRecording)
            {
                micInput.StartRecording(WitRequest.samplerate, sampleLen: sampleLengthInMs);
            }
        }

        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public void Deactivate()
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

            if (Active)
            {
                activeRequest.CloseRequestStream();
                if (minKeepAliveWasHit)
                {
                    events.OnMicDataSent?.Invoke();
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
        public void Activate(string transcription)
        {
            if (Active) return;

            activeRequest = Configuration.MessageRequest(transcription);
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
}
