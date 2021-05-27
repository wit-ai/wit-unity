/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;
using System;
using System.Collections.Concurrent;
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

        [Tooltip("Events that will fire before, during and after an activation")]
        [SerializeField] public WitEvents events = new WitEvents();

        private float activationTime;
        private Mic micInput;
        private float lastMinVolumeLevelTime;
        private WitRequest activeRequest;

        private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

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
            }

            events?.OnMicLevelChanged?.Invoke(levelMax);
            if (null != activeRequest && activeRequest.IsActive)
            {
                byte[] sampleBytes = Convert(sample);
                activeRequest.Write(sampleBytes, 0, sampleBytes.Length);
            }
            else
            {
                Deactivate();
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
                    Deactivate();
                }
                else if (Time.time - activationTime >= maxRecordingTime)
                {
                    Debug.Log("Deactivated due to time limit.");
                    Deactivate();
                }
            }
        }

        /// <summary>
        /// Activate the microphone and send data to Wit for NLU processing.
        /// </summary>
        public WitRequest Activate()
        {
            if (Active) return null;

            // Make sure we aren't checking activation time until
            // the mic starts recording.
            activationTime = float.PositiveInfinity;
            lastMinVolumeLevelTime = float.PositiveInfinity;

            activeRequest = Configuration.SpeechRequest();
            activeRequest.onInputStreamReady = (r) => updateQueue.Enqueue(StartMic);
            activeRequest.onResponse = QueueResult;
            activeRequest.Request();
            return activeRequest;
        }

        private void StartMic()
        {
            activationTime = Time.time;
            lastMinVolumeLevelTime = Time.time;
            micInput.StartRecording(WitRequest.samplerate);
        }

        /// <summary>
        /// Stop listening and submit the collected microphone data to wit for processing.
        /// </summary>
        public void Deactivate()
        {
            if (!Active) return;
            micInput.StopRecording();
            activeRequest.CloseRequestStream();
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
        public WitRequest Activate(string transcription)
        {
            activeRequest = Configuration.MessageRequest(transcription);
            activeRequest.onResponse = QueueResult;
            activeRequest.Request();
            return activeRequest;
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
