/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if UNITY_EDITOR
#define DEBUG_MIC
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using Meta.Voice;
using Meta.WitAi.Attributes;
using Meta.WitAi.Events;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Lib;
using UnityEngine;
using UnityEngine.Profiling;
#if DEBUG_MIC
using System.IO;
#endif

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
        #endregion Singleton

        #region Settings
        /// <summary>
        /// If set to true, the audio buffer will always be recording.
        /// </summary>
        [Tooltip("If set to true, the audio buffer will always be recording.")]
        [SerializeField] private bool alwaysRecording;

        /// <summary>
        /// Configuration settings for the audio buffer.
        /// </summary>
        [Tooltip("Configuration settings for the audio buffer.")]
        [SerializeField] private AudioBufferConfiguration audioBufferConfiguration = new AudioBufferConfiguration();

        /// <summary>
        /// Events triggered when AudioBuffer processes and receives audio data.
        /// </summary>
        [TooltipBox("Events triggered when AudioBuffer processes and receives audio data.")]
        [SerializeField] private AudioBufferEvents events = new AudioBufferEvents();

        /// <summary>
        /// The audio buffer's encoding settings.  Mic.Encoding is re-encoded to match the desired transmission encoding
        /// </summary>
        public AudioEncoding AudioEncoding => audioBufferConfiguration.encoding;

        /// <summary>
        /// Events triggered when AudioBuffer processes and receives audio data.
        /// </summary>
        public AudioBufferEvents Events => events;

        /// <summary>
        /// The current audio input source
        /// </summary>
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
                    // Set frequency interface if implemented
                    if (_micInput is IAudioLevelRangeProvider micRange)
                    {
                        _micLevelRange = micRange;
                    }
                }
                return _micInput;
            }
        }
        // The actual mic input being used
        private IAudioInputSource _micInput;
        private IAudioLevelRangeProvider _micLevelRange;

        /// <summary>
        /// The minimum unsigned frequency handled by the microphone
        /// </summary>
        public float MicMinAudioLevel => _micLevelRange == null ? 0.5f : _micLevelRange.MinAudioLevel;

        /// <summary>
        /// The minimum unsigned frequency handled by the microphone
        /// </summary>
        public float MicMaxAudioLevel => _micLevelRange == null ? 1f : _micLevelRange.MaxAudioLevel;

        // Total sample chunks since start
        private int _totalSampleChunks;
        // The buffers
        private RingBuffer<byte> _outputBuffer;

        // The components that have requested audio input
        private HashSet<Component> _recorders = new HashSet<Component>();

        /// <summary>
        /// Returns true if an input audio source (for example Mic) is available
        /// </summary>
        public bool IsInputAvailable => MicInput != null;

        /// <summary>
        /// Returns true if a component has requested audio but
        /// </summary>
        public VoiceAudioInputState AudioState { get; private set; } = VoiceAudioInputState.Off;

        /// <summary>
        /// Current max audio level, defaults to -1 when off
        /// </summary>
        public float MicMaxLevel { get; private set; } = MIC_RESET;
        private const float MIC_RESET = -1f;

        /// <summary>
        /// Returns true if a component has registered to receive audio data and if the mic is actively capturing data
        /// that will be shared
        /// </summary>
        /// <param name="component">The source of the StartRecording</param>
        /// <returns>True if this component has called StartRecording</returns>
        public bool IsRecording(Component component) => _recorders.Contains(component);

        // Coroutine for volume update
        private Coroutine _volumeUpdate;
        #endregion Settings

        #region Lifecycle
        /// <summary>
        /// Initialize buffer
        /// </summary>
        private void Awake()
        {
            _instance = this;
            InitializeMicDataBuffer();
        }

        /// <summary>
        /// Begin watching mic input
        /// </summary>
        private void OnEnable()
        {
            // Add delegates
            MicInput.OnStartRecording += OnMicRecordSuccess;
            MicInput.OnStartRecordingFailed += OnMicRecordFailed;
            MicInput.OnStopRecording += OnMicRecordStop;
            MicInput.OnSampleReady += OnMicSampleReady;

            // Begin recording
            if (alwaysRecording) StartRecording(this);
        }

        /// <summary>
        /// Stop watching mic input
        /// </summary>
        private void OnDisable()
        {
            // Stop recording
            if (alwaysRecording) StopRecording(this);

            // Remove delegates
            MicInput.OnStartRecording -= OnMicRecordSuccess;
            MicInput.OnStartRecordingFailed -= OnMicRecordFailed;
            MicInput.OnStopRecording -= OnMicRecordStop;
            MicInput.OnSampleReady -= OnMicSampleReady;
        }

        /// <summary>
        /// Sets audio state & performs callback
        /// </summary>
        private void SetAudioState(VoiceAudioInputState newAudioState)
        {
            AudioState = newAudioState;
            if (AudioState == VoiceAudioInputState.On)
            {
                StopUpdateVolume();
                MicMaxLevel = MIC_RESET;
                _volumeUpdate = StartCoroutine(UpdateVolume());
#if DEBUG_MIC
                DebugStart();
#endif
            }
            else if (AudioState == VoiceAudioInputState.Off)
            {
#if DEBUG_MIC
                DebugStop();
#endif
                StopUpdateVolume();
            }
            Events.OnAudioStateChange?.Invoke(AudioState);
        }

        /// <summary>
        /// Adds a component to the active list of recorders. If the AudioBuffer isn't already storing mic data in the
        /// ring buffer, it will start to store data in the ring buffer.
        /// </summary>
        /// <param name="component">A component to use as a key that will keep the audio buffer actively recording</param>
        public void StartRecording(Component component)
        {
            // Ignore if contained
            if (_recorders.Contains(component))
            {
                return;
            }

            // Add component to recorder list
            _recorders.Add(component);

            // Try to activate mic audio
            if (AudioState == VoiceAudioInputState.Off || AudioState == VoiceAudioInputState.Deactivating)
            {
                // Begin activation
                _totalSampleChunks = 0;
                SetAudioState(VoiceAudioInputState.Activating);

                // Start recording
                if (!MicInput.IsRecording)
                {
                    MicInput.StartRecording(audioBufferConfiguration.sampleLengthInMs);
                }
                // Already started elsewhere, update AudioBuffer
                else
                {
                    OnMicRecordSuccess();
                }
            }
            // Already on, update component
            else if (AudioState == VoiceAudioInputState.On)
            {
                OnMicRecordStarted(component);
            }
        }

        /// <summary>
        /// Handles mic record success
        /// </summary>
        private void OnMicRecordSuccess()
        {
            // Activation success
            SetAudioState(VoiceAudioInputState.On);

            // Start each recorder
            foreach (var component in _recorders)
            {
                OnMicRecordStarted(component);
            }
        }

        /// <summary>
        /// Handles callback for individual component
        /// </summary>
        private void OnMicRecordStarted(Component component)
        {
            if (component is IVoiceEventProvider v)
            {
                v.VoiceEvents.OnMicStartedListening?.Invoke();
            }
        }

        /// <summary>
        /// Handles mic record failure
        /// </summary>
        private void OnMicRecordFailed()
        {
            // Stop all recording due to failure
            OnMicRecordStop();
        }

        /// <summary>
        /// Releases the recording state on the AudioBuffer for the given component. If no components are holding a lock
        /// on the AudioBuffer it will stop populating the ring buffer.
        /// </summary>
        /// <param name="component">The component used to start recording</param>
        public void StopRecording(Component component)
        {
            // Ignore if not contained
            if (!_recorders.Contains(component))
            {
                return;
            }

            // Try to deactivate mic audio
            if (AudioState == VoiceAudioInputState.On || AudioState == VoiceAudioInputState.Activating)
            {
                // Now deactivating
                SetAudioState(VoiceAudioInputState.Deactivating);

                // Try to stop recording
                if (MicInput.IsRecording)
                {
                    MicInput.StopRecording();
                }
                // Already stopped, update AudioBuffer
                else
                {
                    OnMicRecordStop();
                }
            }
            // Already off locally, update component
            else if (AudioState == VoiceAudioInputState.Off)
            {
                OnMicRecordStopped(component);
                _recorders.Remove(component);
            }
        }

        /// <summary>
        /// Performs a deactivation
        /// </summary>
        private void OnMicRecordStop()
        {
            // Clear recorder list
            var stoppedRecorders = _recorders;
            _recorders = new HashSet<Component>();

            // Stop each recorder
            foreach (var component in stoppedRecorders)
            {
                OnMicRecordStopped(component);
            }

            // Activation success
            SetAudioState(VoiceAudioInputState.Off);
        }

        /// <summary>
        /// Handles callback for individual component
        /// </summary>
        private void OnMicRecordStopped(Component component)
        {
            if (component is IVoiceEventProvider v)
            {
                v.VoiceEvents.OnMicStoppedListening?.Invoke();
            }
        }
        #endregion

        #region Buffer
        /// <summary>
        /// Generate mic data buffer if needed
        /// </summary>
        private void InitializeMicDataBuffer()
        {
            // Log error for non-signed encoding
            if (AudioEncoding.numChannels != 1)
            {
                VLog.E(GetType().Name, $"{AudioEncoding.numChannels} audio channels are not currently supported");
                AudioEncoding.numChannels = 1;
            }
            // Log error for non-signed encoding
            if (!string.Equals(AudioEncoding.encoding, AudioEncoding.ENCODING_SIGNED)
                && !string.Equals(AudioEncoding.encoding, AudioEncoding.ENCODING_UNSIGNED))
            {
                VLog.E(GetType().Name, $"{AudioEncoding.encoding} encoding is not currently supported");
                AudioEncoding.encoding = AudioEncoding.ENCODING_SIGNED;
            }
            // Log error for invalid bit encoding
            if (AudioEncoding.bits != AudioEncoding.BITS_BYTE && AudioEncoding.bits != AudioEncoding.BITS_SHORT
                && AudioEncoding.bits != AudioEncoding.BITS_INT && AudioEncoding.bits != AudioEncoding.BITS_LONG)
            {
                VLog.E(GetType().Name, $"{AudioEncoding.bits} bit audio encoding is not currently supported");
                AudioEncoding.bits = AudioEncoding.BITS_SHORT;
            }

            // Buffer length in ms
            var bufferLength = Mathf.Max(10f, audioBufferConfiguration.micBufferLengthInSeconds * 1000f);

            // Get output buffer
            if (_outputBuffer == null)
            {
                // Size using output encoding
                var bufferSize = AudioEncoding.numChannels * AudioEncoding.samplerate * Mathf.CeilToInt((AudioEncoding.bits / 8f) * bufferLength);
                // Generate
                _outputBuffer = new RingBuffer<byte>(bufferSize);
            }
        }

        /// <summary>
        /// Called when a new mic sample is ready to be processed as sent by the audio input source
        /// </summary>
        /// <param name="sampleCount">The number of samples to process (could be less than samples.length if multi-channel</param>
        /// <param name="samples">The raw pcm float audio samples</param>
        /// <param name="levelMax">The max volume level in this sample</param>
        private void OnMicSampleReady(int sampleCount, float[] samples, float levelMax)
            => OnAudioSampleReady(samples, 0, samples.Length);

        /// <summary>
        /// Adds all sent audio into an input buffer
        /// </summary>
        /// <param name="samples">The bytes that make up the audio input sample</param>
        /// <param name="offset">The offset in the sample array to be read from</param>
        /// <param name="length">The length of samples to be taken</param>
        private void OnAudioSampleReady(float[] samples, int offset, int length)
        {
            // Resample provided array & determine level max
            Profiler.BeginSample("Resample & Encode Audio");
            Encode(samples, offset, length, out byte[] data, out float levelMax);
            Profiler.EndSample();

            // Set max level for frame
            MicMaxLevel = Mathf.Max(levelMax, MicMaxLevel);

            // Perform received callback
            events.OnSampleReceived?.Invoke(samples, _totalSampleChunks, levelMax);

            // Create marker
            var marker = CreateMarker();
            // Push new data
            _outputBuffer.Push(data, 0, data.Length);
            #if DEBUG_MIC
            // Write to debug
            DebugWrite(data);
            #endif

            // Raw data ready
            if (null != events.OnByteDataReady)
            {
                marker.Clone().ReadIntoWriters(events.OnByteDataReady.Invoke);
            }
            // Sample ready
            events.OnSampleReady?.Invoke(marker, levelMax);

            // Increment chunk count
            _totalSampleChunks++;
        }

        /// <summary>
        /// Ensure volume callback only occurs once per frame
        /// </summary>
        private IEnumerator UpdateVolume()
        {
            float volume = MIC_RESET;
            while (true)
            {
                if (Application.isBatchMode)
                {
                    yield return null;
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                }
                if (!volume.Equals(MicMaxLevel) && !MicMaxLevel.Equals(MIC_RESET))
                {
                    volume = MicMaxLevel;
                    events.OnMicLevelChanged?.Invoke(volume);
                    MicMaxLevel = MIC_RESET;
                }
            }
        }

        /// <summary>
        /// Stop volume update coroutine
        /// </summary>
        private void StopUpdateVolume()
        {
            MicMaxLevel = -1f;
            if (_volumeUpdate != null)
            {
                StopCoroutine(_volumeUpdate);
                _volumeUpdate = null;
            }
        }

        /// <summary>
        /// Resample into a new array & determine max level
        /// </summary>
        private void Encode(float[] samples, int offset, int length, out byte[] results, out float levelMax)
        {
            // Get mic encoding
            AudioEncoding micEncoding = MicInput.AudioEncoding;
            int micChannels = micEncoding.numChannels;
            int micSampleRate = micEncoding.samplerate;
            bool micSigned = string.Equals(micEncoding.encoding, Data.AudioEncoding.ENCODING_SIGNED);

            // Get output encoding
            AudioEncoding outEncoding = AudioEncoding;
            int outSampleRate = outEncoding.samplerate;
            int bytesPerSample = Mathf.CeilToInt(outEncoding.bits / 8f);
            GetEncodingMinMax(outEncoding.bits, string.Equals(outEncoding.encoding, AudioEncoding.ENCODING_SIGNED),
                out long encodingMin, out long encodingMax);
            long encodingDif = encodingMax - encodingMin;
            bool littleEnd = outEncoding.endian == AudioEncoding.Endian.Little;
            int byteOffset = littleEnd ? 0 : bytesPerSample - 1;
            int byteMult = littleEnd ? 1 : -1;

            // Determine resize factor & total samples
            float resizeFactor = micSampleRate == outSampleRate ? 1f : (float)micSampleRate / outSampleRate;
            resizeFactor *= micChannels; // Skip all additional channels
            int totalSamples = Mathf.FloorToInt(length / resizeFactor);
            results = new byte[totalSamples * bytesPerSample];

            // Resample
            levelMax = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                // Get sample
                int micIndex = offset + Mathf.FloorToInt(i * resizeFactor);
                float sample = samples[micIndex];

                // If signed from source (-1 to 1), convert to unsigned (0 to 1)
                if (micSigned)
                {
                    sample = sample / 2f + 0.5f;
                }

                // Get largest unsigned sample
                if (sample > levelMax)
                {
                    levelMax = sample;
                }

                // Encode from unsigned long (auto clamps)
                long data = (long)(encodingMin + sample * encodingDif);
                for (int b = 0; b < bytesPerSample; b++)
                {
                    results[i * bytesPerSample + (byteOffset + b * byteMult)] = (byte)(data >> (b * 8));
                }
            }

            // Scale based on min/max audio levels
            float min = MicMinAudioLevel;
            float max = MicMaxAudioLevel;
            if ((!min.Equals(0f) || !max.Equals(1f)) && max > min)
            {
                levelMax = (levelMax - min) / (max - min);
            }

            // Clamp result 0 to 1
            levelMax = Mathf.Clamp01(levelMax);
        }
        // Encoding options
        private void GetEncodingMinMax(int bits, bool signed, out long encodingMin, out long encodingMax)
        {
            switch (bits)
            {
                // Always unsigned
                case AudioEncoding.BITS_BYTE:
                    encodingMin = byte.MinValue;
                    encodingMax = byte.MaxValue;
                    break;
                // Always signed
                case AudioEncoding.BITS_LONG:
                    encodingMin = long.MinValue;
                    encodingMax = long.MaxValue;
                    break;
                // Signed/Unsigned
                case AudioEncoding.BITS_INT:
                    encodingMin = signed ? int.MinValue : uint.MinValue;
                    encodingMax = signed ? int.MaxValue : uint.MaxValue;
                    break;
                // Signed/Unsigned
                case AudioEncoding.BITS_SHORT:
                default:
                    encodingMin = signed ? short.MinValue : ushort.MinValue;
                    encodingMax = signed ? short.MaxValue : ushort.MaxValue;
                    break;
            }
        }
        #endregion Buffer

        #region Marker
        /// <summary>
        /// Create a marker at the current audio input time.
        /// </summary>
        /// <returns>A marker representing a position in the ring buffer that can be read as long as the the ring buffer
        /// hasn't wrapped and replaced the start of this marker yet.</returns>
        public RingBuffer<byte>.Marker CreateMarker()
        {
            return _outputBuffer.CreateMarker();
        }

        /// <summary>
        /// Creates a marker with an offset
        /// </summary>
        /// <param name="offset">Number of seconds to offset the marker by</param>
        public RingBuffer<byte>.Marker CreateMarker(float offset)
        {
            var samples = (int) (AudioEncoding.numChannels * AudioEncoding.samplerate * offset);
            return _outputBuffer.CreateMarker(samples);
        }
        #endregion Marker

#if DEBUG_MIC
        /// <summary>
        /// Whether to generate pcm files for each audio interaction
        /// </summary>
        [Header("Debugging")]
        [SerializeField] private bool _debugOutput = false;

        /// <summary>
        /// Editor directory from project root to
        /// </summary>
        [SerializeField] private string _debugFileDirectory = "Logs";

        /// <summary>
        /// Debug path from project root
        /// </summary>
        [SerializeField] private string _debugFileName = "AudioBuffer";

        // The file stream being used
        private FileStream _fileStream;

        // On start, debug
        private void DebugStart()
        {
            if (!_debugOutput)
            {
                return;
            }

            // The directory to be used
            string directory = Application.dataPath.Replace("Assets", "");
            directory += _debugFileDirectory + "/";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Start with directory & file name
            string path = directory + _debugFileName;

            // Append settings
            path += $"_{Mathf.FloorToInt(AudioEncoding.samplerate / 1000f)}k";
            bool signed = string.Equals(AudioEncoding.encoding, AudioEncoding.ENCODING_SIGNED);
            path += $"_{(signed ? "s" : "u")}{AudioEncoding.bits}bit";
            path += AudioEncoding.endian == AudioEncoding.Endian.Little ? "" : "_BigEnd";

            // Append datetime
            DateTime now = DateTime.UtcNow;
            path += $"_{now.Year:0000}{now.Month:00}{now.Day:00}";
            path += $"_{now.Hour:00}{now.Minute:00}{now.Second:00}";

            // Append ext
            path += ".pcm";

            // Create file stream
            VLog.D(GetType().Name, $"Start Writing to AudioBuffer Debug File\nPath: {path}");
            _fileStream = File.Open(path, FileMode.Create);
        }

        // Write to file
        private void DebugWrite(byte[] bytes)
        {
            if (_fileStream == null)
            {
                return;
            }
            _fileStream.Write(bytes, 0, bytes.Length);
        }

        // Stop mic debug
        private void DebugStop()
        {
            if (_fileStream == null)
            {
                return;
            }
            VLog.D(GetType().Name, "Stop Writing to AudioBuffer Debug File");
            _fileStream.Close();
            _fileStream.Dispose();
            _fileStream = null;
        }
#endif
    }
}
