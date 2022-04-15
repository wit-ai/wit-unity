using System;
using System.Collections.Generic;
using Facebook.WitAi.Events;
using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi.Data
{
    public class AudioBuffer : MonoBehaviour
    {
        #region Singleton
        private static AudioBuffer _instance;
        public static AudioBuffer Instance
        {
            get
            {
                if (!_instance) _instance = FindObjectOfType<AudioBuffer>();
                if (!_instance)
                {
                    var audioBufferObject = new GameObject("AudioBuffer");
                    _instance = audioBufferObject.AddComponent<AudioBuffer>();
                }

                return _instance;
            }
        }
        #endregion

        [SerializeField] private AudioBufferConfiguration audioBufferConfiguration = new AudioBufferConfiguration();
        [SerializeField] private AudioBufferEvents events = new AudioBufferEvents();

        public AudioBufferEvents Events => events;

        private IAudioInputSource _micInput;
        private RingBuffer<byte> _micDataBuffer;

        private byte[] _byteDataBuffer;

        private HashSet<IVoiceService> _activeRecorders = new HashSet<IVoiceService>();

        public bool IsRecording(IVoiceService component) => _activeRecorders.Contains(component);
        public bool IsInputAvailable => _micInput.IsInputAvailable;
        public AudioEncoding AudioEncoding => _micInput.AudioEncoding;

        private void Awake()
        {
            _micInput = GetComponent<IAudioInputSource>();
            if (_micInput == null)
            {
                _micInput = gameObject.AddComponent<Mic>();
            }

            InitializeMicDataBuffer();
        }

        private void OnEnable()
        {
            _instance = this;
#if UNITY_EDITOR
            // Make sure we have a mic input after a script recompile
            if (null == _micInput)
            {
                _micInput = GetComponent<IAudioInputSource>();
            }
#endif

            _micInput.OnSampleReady += OnMicSampleReady;
        }

        // Remove mic delegates
        private void OnDisable()
        {
            _micInput.OnSampleReady -= OnMicSampleReady;
        }

        // Callback for mic sample ready
        private void OnMicSampleReady(int sampleCount, float[] sample, float levelMax)
        {
            events.OnMicLevelChanged.Invoke(levelMax);

            var marker = CreateMarker();
            Convert(sample);
            if (null != events.OnByteDataReady)
            {
                marker.Clone().ReadIntoWriters(events.OnByteDataReady.Invoke);
            }
            events.OnSampleReady?.Invoke(marker, levelMax);
        }

        // Generate mic data buffer if needed
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

        // Convert
        private void Convert(float[] samples)
        {
            var sampleCount = samples.Length;
            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < sampleCount; i++)
            {
                short data = (short) (samples[i] * rescaleFactor);
                _micDataBuffer.Push((byte) data);
                _micDataBuffer.Push((byte) (data >> 8));
            }
        }

        public RingBuffer<byte>.Marker CreateMarker()
        {
            return _micDataBuffer.CreateMarker();
        }

        public void StartRecording(IVoiceService component)
        {
            _activeRecorders.Add(component);
            if (!_micInput.IsRecording)
            {
                _micInput.StartRecording(audioBufferConfiguration.sampleLengthInMs);
            }

            component.VoiceEvents.OnStartListening?.Invoke();
        }

        public void StopRecording(IVoiceService component)
        {
            _activeRecorders.Remove(component);
            if (_activeRecorders.Count == 0)
            {
                _micInput.StopRecording();
            }

            component.VoiceEvents.OnStoppedListening?.Invoke();
        }
    }
}
