/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections;
using System.Collections.Generic;
using Meta.WitAi.Attributes;
using Meta.WitAi.Events;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Lib;
using UnityEngine;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// This class is responsible for managing a shared audio buffer for receiving microphone data.
    /// It is used by voice services to grab audio segments from the AudioBuffer's internal ring buffer.
    /// </summary>
    public class AudioBuffer : MonoBehaviour
    {
        #region Singleton
        private static bool _isQuitting = false;
        public void OnApplicationQuit() => _isQuitting = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SingletonInit() => _isQuitting = false;

        private static AudioBuffer _instance;
        public static AudioBuffer Instance
        {
            get
            {
                if (!_instance && Application.isPlaying && !_isQuitting)
                {
                    _instance = FindObjectOfType<AudioBuffer>();
                    if (!_instance)
                    {
                        var audioBufferObject = new GameObject("AudioBuffer");
                        _instance = audioBufferObject.AddComponent<AudioBuffer>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        [Tooltip("If set to true, the audio buffer will always be recording.")]
        [SerializeField] private bool alwaysRecording;
        
        [Tooltip("Configuration settings for the audio buffer.")]
        [SerializeField] private AudioBufferConfiguration audioBufferConfiguration = new AudioBufferConfiguration();
        
        [TooltipBox("Events triggered when AudioBuffer processes and receives audio data.")]
        [SerializeField] private AudioBufferEvents events = new AudioBufferEvents();

        /// <summary>
        /// Events triggered when AudioBuffer processes and receives audio data.
        /// </summary>
        public AudioBufferEvents Events => events;

        public IAudioInputSource MicInput
        {
            get
            {
                if (_micInput == null && Application.isPlaying)
                {
                    // Check this gameobject & it's children for audio input
                    _micInput = gameObject.GetComponentInChildren<IAudioInputSource>();
                    // Check all roots for Mic Input JIC
                    if (_micInput == null)
                    {
                        foreach (var root in gameObject.scene.GetRootGameObjects())
                        {
                            _micInput = root.GetComponentInChildren<IAudioInputSource>();
                            if (_micInput != null)
                            {
                                break;
                            }
                        }
                    }
                    // Use default mic script
                    if (_micInput == null)
                    {
                        _micInput = gameObject.AddComponent<Mic>();
                    }
                }

                return _micInput;
            }
        }
        private IAudioInputSource _micInput;
        private RingBuffer<byte> _micDataBuffer;

        private byte[] _byteDataBuffer;

        private HashSet<Component> _waitingRecorders = new HashSet<Component>();
        private HashSet<Component> _activeRecorders = new HashSet<Component>();

        /// <summary>
        /// Returns true if a component has registered to receive audio data and if the mic is actively capturing data
        /// that will be shared
        /// </summary>
        /// <param name="component">The source of the StartRecording</param>
        /// <returns>True if this component has called StartRecording</returns>
        public bool IsRecording(Component component) => _waitingRecorders.Contains(component) || _activeRecorders.Contains(component);
        
        /// <summary>
        /// Returns true if an input audio source (for example Mic) is available
        /// </summary>
        public bool IsInputAvailable => MicInput != null && MicInput.IsInputAvailable;
        
        /// <summary>
        /// Requests a check to see if an input source is available on an associated audio source. This may trigger a
        /// rescan of available mic devices and can be expensive.
        /// </summary>
        public void CheckForInput() => MicInput.CheckForInput();
        
        /// <summary>
        /// Returns the audio encoding settings set up by the audio input source.
        /// </summary>
        public AudioEncoding AudioEncoding => MicInput.AudioEncoding;

        private void Awake()
        {
            _instance = this;

            InitializeMicDataBuffer();
        }

        private void OnEnable()
        {
            MicInput.OnSampleReady += OnMicSampleReady;

            if (alwaysRecording) StartRecording(this);
        }

        // Remove mic delegates
        private void OnDisable()
        {
            MicInput.OnSampleReady -= OnMicSampleReady;

            if (alwaysRecording) StopRecording(this);
        }

        /// <summary>
        /// Called when a new mic sample is ready to be processed as sent by the audio input source
        /// </summary>
        /// <param name="sampleCount">The number of samples to process (could be less than samples.length if multi-channel</param>
        /// <param name="samples">The raw pcm float audio samples</param>
        /// <param name="levelMax">The max volume level in this sample</param>
        private void OnMicSampleReady(int sampleCount, float[] samples, float levelMax)
        {
            events.OnSampleReceived?.Invoke(samples, sampleCount, levelMax);
            events.OnMicLevelChanged?.Invoke(levelMax);
            var marker = CreateMarker();
            Convert(Mathf.Min(sampleCount, samples.Length), samples);
            if (null != events.OnByteDataReady)
            {
                marker.Clone().ReadIntoWriters(events.OnByteDataReady.Invoke);
            }
            events.OnSampleReady?.Invoke(marker, levelMax);
        }

        /// <summary>
        /// Generate mic data buffer if needed
        /// </summary>
        private void InitializeMicDataBuffer()
        {
            if (null == _micDataBuffer && audioBufferConfiguration.micBufferLengthInSeconds > 0)
            {
                var bufferSize = (int) Mathf.Ceil(2 *
                                                  audioBufferConfiguration
                                                      .micBufferLengthInSeconds * 1000 *
                                                  audioBufferConfiguration.sampleLengthInMs);
                if (bufferSize <= 0)
                {
                    bufferSize = 1024;
                }
                _micDataBuffer = new RingBuffer<byte>(bufferSize);
            }
        }

        // Resample & convert to byte[]
        private byte[] _convertBuffer = new byte[512];
        /// <summary>
        /// Resamples audio and converts it to a byte buffer
        /// </summary>
        /// <param name="sampleTotal">The total number of samples in the sample buffer</param>
        /// <param name="samples">The pcm float sample buffer</param>
        private void Convert(int sampleTotal, float[] samples)
        {
            // Increase buffer size
            int chunkTotal = sampleTotal * 2;
            if (_convertBuffer.Length < chunkTotal)
            {
                _convertBuffer = new byte[chunkTotal];
            }

            // Convert buffer data
            for (int i = 0; i < sampleTotal; i++)
            {
                short data = (short) (samples[i] * short.MaxValue);
                _convertBuffer[i * 2] = (byte)data;
                _convertBuffer[i * 2 + 1] = (byte)(data >> 8);
            }

            // Push buffer data
            _micDataBuffer.Push(_convertBuffer, 0, chunkTotal);
        }

        /// <summary>
        /// Create a marker at the current audio input time.
        /// </summary>
        /// <returns>A marker representing a position in the ring buffer that can be read as long as the the ring buffer
        /// hasn't wrapped and replaced the start of this marker yet.</returns>
        public RingBuffer<byte>.Marker CreateMarker()
        {
            return _micDataBuffer.CreateMarker();
        }

        /// <summary>
        /// Creates a marker with an offset
        /// </summary>
        /// <param name="offset">Number of seconds to offset the marker by</param>
        /// <returns></returns>
        public RingBuffer<byte>.Marker CreateMarker(float offset)
        {
            var samples = (int) (AudioEncoding.samplerate * offset);
            return _micDataBuffer.CreateMarker(samples);
        }

        /// <summary>
        /// Adds a component to the active list of recorders. If the AudioBuffer isn't already storing mic data in the
        /// ring buffer, it will start to store data in the ring buffer.
        /// </summary>
        /// <param name="component">A component to use as a key that will keep the audio buffer actively recording</param>
        public void StartRecording(Component component)
        {
            StartCoroutine(WaitForMicToStart(component));
        }

        /// <summary>
        /// Waits for the mic to start and announces it when it is ready
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        private IEnumerator WaitForMicToStart(Component component)
        {
            // Wait for mic
            _waitingRecorders.Add(component);
            yield return new WaitUntil(() => null != MicInput && MicInput.IsInputAvailable);
            if (!_waitingRecorders.Contains(component))
            {
                yield break;
            }
            _waitingRecorders.Remove(component);

            // Add component
            _activeRecorders.Add(component);
            // Start mic
            if (!MicInput.IsRecording)
            {
                MicInput.StartRecording(audioBufferConfiguration.sampleLengthInMs);
            }
            // On Start Listening
            if (component is IVoiceEventProvider v)
            {
                v.VoiceEvents.OnStartListening?.Invoke();
            }
        }

        /// <summary>
        /// Releases the recording state on the AudioBuffer for the given component. If no components are holding a lock
        /// on the AudioBuffer it will stop populating the ring buffer.
        /// </summary>
        /// <param name="component">The component used to start recording</param>
        public void StopRecording(Component component)
        {
            // Remove waiting recorder
            if (_waitingRecorders.Contains(component))
            {
                _waitingRecorders.Remove(component);
                return;
            }
            // Ignore unless active
            if (!_activeRecorders.Contains(component))
            {
                return;
            }

            // Remove active recorder
            _activeRecorders.Remove(component);
            // Stop recording if last active recorder
            if (_activeRecorders.Count == 0)
            {
                MicInput.StopRecording();
            }
            // On Stop Listening
            if (component is IVoiceEventProvider v)
            {
                v.VoiceEvents.OnStoppedListening?.Invoke();
            }
        }
    }
}
