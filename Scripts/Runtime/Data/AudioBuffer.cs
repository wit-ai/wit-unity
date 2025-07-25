﻿/*
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
using Meta.Voice.Logging;
using Meta.Voice.TelemetryUtilities;
using Meta.WitAi.Attributes;
using Meta.WitAi.Events;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Lib;
using Meta.WitAi.Utilities;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if DEBUG_MIC
using System.IO;
#endif

namespace Meta.WitAi.Data
{
    /// <summary>
    /// This class is responsible for managing a shared audio buffer for receiving microphone data.
    /// It is used by voice services to grab audio segments from the AudioBuffer's internal ring buffer.
    /// </summary>
    [LogCategory(LogCategory.Audio, LogCategory.Input)]
    public class AudioBuffer : MonoBehaviour
    {
        /// <inheritdoc/>
        public static IVLogger _log { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Input);

        private const string DEFAULT_OBJECT_NAME = nameof(AudioBuffer);

        #region Singleton
        private static bool _isQuitting = false;

        /// <summary>
        /// Determines if the AudioBuffer will attempt to automatically instantiate a mic object if it can't find one
        /// when the AudioBuffer object is first enabled.
        /// </summary>
        public static bool instantiateMic = true;
        public void OnApplicationQuit() => _isQuitting = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SingletonInit() => _isQuitting = false;

        private static AudioBuffer _instance;
        public static AudioBuffer Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindAnyObjectByType<AudioBuffer>();
                    if (!_instance && CanInstantiate())
                    {
                        if (AudioBufferProvider != null)
                        {
                            _log.Verbose("No {0} found, creating using provider {1}.", DEFAULT_OBJECT_NAME, AudioBufferProvider.GetType().Name);
                            _instance = AudioBufferProvider.InstantiateAudioBuffer();
                        }
                        if (!_instance)
                        {
                            _log.Verbose("No {0} found, creating using {0}.", DEFAULT_OBJECT_NAME);
                            var audioBufferObject = new GameObject(DEFAULT_OBJECT_NAME);
                            _instance = audioBufferObject.AddComponent<AudioBuffer>();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Check if an instance currently exists, for situations where it wouldn't be
        /// appropriate to create a new one.
        /// </summary>
        public static bool HasInstance => (bool)_instance;

        /// <summary>
        /// A script that will instantiate an audio buffer if needed
        /// </summary>
        public static IAudioBufferProvider AudioBufferProvider;

        /// <summary>
        /// Whether or not a new buffer should be instantiated
        /// </summary>
        private static bool CanInstantiate() => !_isQuitting && Application.isPlaying;
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
        public virtual IAudioInputSource MicInput
        {
            get => _micInput as IAudioInputSource;
            set => SetInputSource(value);
        }
        // The actual mic input being used
        [ObjectType(typeof(IAudioInputSource))]
        [SerializeField] private Object _micInput;
        private IAudioLevelRangeProvider _micLevelRange;
        private bool _active;
        private Mic _instantiatedMic;

        // Attempt to find input source
        private IAudioInputSource FindOrCreateInputSource()
        {
            // Check this gameobject & it's children for audio input
            var result = gameObject.GetComponentInChildren<IAudioInputSource>(true);
            if (result != null)
            {
                return result;
            }
            // Check all root gameobjects for Mic input
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                result = root.GetComponentInChildren<IAudioInputSource>(true);
                if (result != null)
                {
                    return result;
                }
            }
            // If can instantiate, do so
            if (instantiateMic && CanInstantiate())
            {
                _log.Verbose("No input assigned or found, {0} will use Unity Mic Input.", DEFAULT_OBJECT_NAME);
                _instantiatedMic = gameObject.AddComponent<Mic>();
                result = _instantiatedMic;
            }
            // Returns the result
            return result;
        }

        // Set input source if possible
        private void SetInputSource(IAudioInputSource newInput)
        {
            // Ignore if same as old
            if (MicInput == newInput)
            {
                return;
            }

            if (_instantiatedMic && !Equals(_instantiatedMic, newInput))
            {
                _log.Verbose($"Replacing default mic.");
                Destroy(_instantiatedMic);
                _instantiatedMic = null;
            }

            // Stop previous recording
            bool wasRecording = _recorders.Contains(this);
            if (wasRecording) StopRecording(this);

            // Remove previous delegates
            if (_active) SetInputDelegates(false);

            // Apply mic input
            if (newInput is UnityEngine.Object newObj)
            {
                _micInput = newObj;
                _log.Verbose("{0} set input of type: {1}", DEFAULT_OBJECT_NAME, newInput.GetType().Name);
            }
            // Log warning if null
            else if (newInput == null)
            {
                _log.Warning("{0} setting MicInput to null instead of {1}",
                  DEFAULT_OBJECT_NAME,
                    nameof(IAudioInputSource));
            }
            // Log error if not UnityEngine.Object
            else
            {
                _log.Error("{0} cannot set MicInput of type '{1}' since it does not inherit from {2}",
                  DEFAULT_OBJECT_NAME,
                    newInput.GetType().Name,
                    nameof(UnityEngine.Object));
            }
            // Set frequency interface if implemented
            if (_micInput is IAudioLevelRangeProvider micRange)
            {
                _micLevelRange = micRange;
            }

            // Set new delegates
            if (_active) SetInputDelegates(true);

            // Start new recording
            if (wasRecording) StartRecording(this);
        }

        /// <summary>
        /// Applies all required methods for input
        /// </summary>
        private void SetInputDelegates(bool add)
        {
            var mic = MicInput;
            if (mic == null)
            {
                return;
            }
            if (add)
            {
                mic.OnStartRecording += OnMicRecordSuccess;
                mic.OnStartRecordingFailed += OnMicRecordFailed;
                mic.OnStopRecording += OnMicRecordStop;
                mic.OnSampleReady += OnMicSampleReady;
            }
            else
            {
                mic.OnStartRecording -= OnMicRecordSuccess;
                mic.OnStartRecordingFailed -= OnMicRecordFailed;
                mic.OnStopRecording -= OnMicRecordStop;
                mic.OnSampleReady -= OnMicSampleReady;
            }
        }

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
        public virtual bool IsInputAvailable => MicInput != null;

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

        // Silence gap skipping settings
        public float SecondsToSkipSilence
        {
            get => _secondsBeforeSkip;
            set
            {
                _secondsBeforeSkip = value;
                _samplesBeforeSkip = Mathf.RoundToInt(AudioEncoding.samplerate * _secondsBeforeSkip);
                if (_samplesBeforeSkip <= 0) {
                    _skipping = false;
                }
            }
        }

        private float _secondsBeforeSkip;
        private int _samplesBeforeSkip;
        private bool _skipping;
        private int _silentSamplesHit;

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
        /// Remove instance reference
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Begin watching mic input
        /// </summary>
        private void OnEnable()
        {
            if (_instance && _instance != this)
            {
                _log.Error("Multiple {0} detected. This can lead to extra memory use and unexpected results. Duplicate was found on {1}",
                  DEFAULT_OBJECT_NAME, name);
            }

            if (name != DEFAULT_OBJECT_NAME)
            {
                _log.Verbose("{0} active on {1}", DEFAULT_OBJECT_NAME, name);
            }

            // Attempt to find mic input if needed
            if (MicInput == null)
            {
                MicInput = FindOrCreateInputSource();
            }

            // Add delegates
            _active = true;
            SetInputDelegates(true);

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
            _active = false;
            SetInputDelegates(false);
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


        // Coroutine used for ensuring a single callback
        private Coroutine _sampleReadyCoroutine;
        // Last unsent sample marker
        private RingBuffer<byte>.Marker _sampleReadyMarker;
        // Float used to return max level across multiple encodes
        private float _sampleReadyMaxLevel;

        /// <summary>
        /// Adds all sent audio into an input buffer
        /// </summary>
        /// <param name="samples">The bytes that make up the audio input sample</param>
        /// <param name="offset">The offset in the sample array to be read from</param>
        /// <param name="length">The length of samples to be taken</param>
        private void OnAudioSampleReady(float[] samples, int offset, int length)
        {
            // Begin a coroutine for OnSampleReady callback
            if (_sampleReadyCoroutine == null)
            {
                _sampleReadyCoroutine = StartCoroutine(WaitForSampleReady());
            }
            // Generate a new marker if previous has been called
            if (_sampleReadyMarker == null)
            {
                _sampleReadyMarker = CreateMarker();
                _sampleReadyMaxLevel = float.MinValue;
            }

            // Resample on main thread to use sample buffer while it still includes audio data
            Profiler.BeginSample("AudioBuffer - Resample, Encode and Push");
            float levelMax = EncodeAndPush(samples, offset, length, !_skipping);
            UpdateSilenceSkipping(samples, offset, length, levelMax);
            Profiler.EndSample();

            // Set max level for frame and perform sample received callback
            MicMaxLevel = Mathf.Max(levelMax, MicMaxLevel);
            events.OnSampleReceived?.Invoke(samples, _totalSampleChunks, levelMax);

            // Increment chunk count
            _totalSampleChunks++;

            // Otherwise, set if higher
            if (levelMax > _sampleReadyMaxLevel)
            {
                _sampleReadyMaxLevel = levelMax;
            }
        }

        /// <summary>
        /// Waits a single frame and then returns the marker
        /// </summary>
        private IEnumerator WaitForSampleReady()
        {
            // Continue while audio state exists
            while (AudioState == VoiceAudioInputState.On)
            {
                // Wait a single frame
                if (Application.isPlaying && !Application.isBatchMode)
                {
                    yield return new WaitForEndOfFrame();
                }
                // In editor or batch script yield return asap
                else
                {
                    yield return null;
                }

                // Perform on sample change callback
                if (_sampleReadyMarker != null)
                {
                    var marker = _sampleReadyMarker;
                    _sampleReadyMarker = null;
                    CallSampleReady(marker);
                }
            }

            // Complete
            _sampleReadyCoroutine = null;
        }

        /// <summary>
        /// Perform callbacks for sample encode completion
        /// </summary>
        private void CallSampleReady(RingBuffer<byte>.Marker marker)
        {
            Profiler.BeginSample("AudioBuffer - OnSampleReady Callbacks");
            // Invoke byte data ready callback
            if (events.OnByteDataReady != null)
            {
                marker.Clone().ReadIntoWriters(events.OnByteDataReady.Invoke);
            }
            // Invoke sample ready callback
            events.OnSampleReady?.Invoke(marker, _sampleReadyMaxLevel);
            Profiler.EndSample();
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

            // Note: self null check for when this gets called during app stop
            if (_volumeUpdate != null && !this.IsDestroyedOrNull())
            {
                StopCoroutine(_volumeUpdate);
                _volumeUpdate = null;
            }
        }

        /// <summary>
        /// Resample and encode into bytes that are passed into a setByte method.
        /// </summary>
        /// <returns>Returns the max level of the provided samples</returns>
        private float EncodeAndPush(float[] samples, int offset, int length, bool push)
        {
            // Attempt to calculate sample rate if not determined
            if (MicInput.AudioEncoding.samplerate <= 0
                || (MicInput is IAudioVariableSampleRate check
                    && check.NeedsSampleRateCalculation))
            {
                // Update sample rate if possible
                UpdateSampleRate(length);
                if (MicInput.AudioEncoding.samplerate <= 0)
                {
                    return 0;
                }
            }

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

            // Determine resize factor & total samples
            float resizeFactor = micSampleRate == outSampleRate ? 1f : (float)micSampleRate / outSampleRate;
            resizeFactor *= micChannels; // Skip all additional channels
            int totalSamples = (int)(length / resizeFactor);

            // Resample
            float levelMax = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                // Get sample
                var micIndex = offset +  (int)(i * resizeFactor);
                var sample = samples[micIndex];

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

                // If we're skipping silence, then the sample will initially not be pushed,
                // then will be pushed as a second pass once it qualifies
                if (!push)
                {
                    continue;
                }

                // Encode from unsigned long (auto clamps)
                var data = (long)(encodingMin + sample * encodingDif);
                for (int b = 0; b < bytesPerSample; b++)
                {
                    var outByte = (byte)(data >> (b * 8));
                    _outputBuffer.Push(outByte);
#if DEBUG_MIC
                    // Editor only
                    DebugWrite(outByte);
#endif
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
            return Mathf.Clamp01(levelMax);
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

        /// <summary>
        /// If silence skipping is enabled (the SecondsToSkipSilence property), then update
        /// the state of that based on the levelMax returned from EncodeAndPush
        /// </summary>
        private void UpdateSilenceSkipping(float[] samples, int offset, int length, float levelMax)
        {
            if (_samplesBeforeSkip <= 0)
            {
                return;
            }

            if (_skipping && levelMax > Mathf.Epsilon)
            {
                _skipping = false;
                EncodeAndPush(samples, offset, length, true);
            }
            else if (!_skipping && levelMax <= Mathf.Epsilon)
            {
                _silentSamplesHit += length;
                if (_silentSamplesHit >= _samplesBeforeSkip)
                {
                    _silentSamplesHit = 0;
                    _skipping = true;
                }
            }
            else if (!_skipping)
            {
                _silentSamplesHit = 0;
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

        #region Variable Sample Rate
        // Last sample time tracked by ticks
        private long _lastSampleTime;
        // First sample time of current calculation by ticks
        private long _startSampleTime;
        // The currently measured sample total
        private long _measureSampleTotal;
        // The current measurement index
        private int _measuredSampleRateCount;
        // The various measured sample rates
        private readonly double[] _measuredSampleRates = new double[MEASURE_AVERAGE_COUNT];

        // Timeout if no samples after interval (0.05 seconds)
        private const int TIMEOUT_TICKS = 500_000;
        // Perform calculation after interval (0.25 seconds)
        private const int MEASURE_TICKS = 2_500_000;
        // Total measurements to average out (5 seconds)
        private const int MEASURE_AVERAGE_COUNT = 20;
        // Sample rate options
        private static readonly int[] ALLOWED_SAMPLE_RATES = new []
        {
            8000,
            11025,
            16000,
            22050,
            32000,
            44100,
            48000,
            88200,
            96000,
            176400,
            192000
        };

        /// <summary>
        /// Calculates sample rate using the current length
        /// </summary>
        private void UpdateSampleRate(int sampleLength)
        {
            // Ignore invalid sample length
            if (sampleLength <= 0)
            {
                return;
            }

            // Check if calculation restart is needed
            var newSampleTime = DateTimeOffset.Now.Ticks;
            var deltaSampleTime = newSampleTime - _lastSampleTime;
            _lastSampleTime = newSampleTime;
            if (deltaSampleTime > TIMEOUT_TICKS || _startSampleTime == 0)
            {
                _startSampleTime = newSampleTime;
                _measureSampleTotal = 0;
                return;
            }

            // Append sample length
            int channels = MicInput.AudioEncoding.numChannels;
            _measureSampleTotal += Mathf.FloorToInt((float)sampleLength / channels);

            // Ignore until ready to calculate
            var elapsedTicks = newSampleTime - _startSampleTime;
            if (elapsedTicks < MEASURE_TICKS)
            {
                return;
            }

            // Perform calculation
            var elapsedSeconds = elapsedTicks / 10_000_000d;
            var samplesPerSecond = _measureSampleTotal / elapsedSeconds;

            // Add to array and average out
            var index = _measuredSampleRateCount % MEASURE_AVERAGE_COUNT;
            _measuredSampleRates[index] = samplesPerSecond;
            _measuredSampleRateCount++;
            if (_measuredSampleRateCount == MEASURE_AVERAGE_COUNT * 2) _measuredSampleRateCount -= MEASURE_AVERAGE_COUNT;
            var averageSampleRate = GetAverageSampleRate(_measuredSampleRates, _measuredSampleRateCount);

            // Determine closest sample rate using averaged value
            var closestSampleRate = GetClosestSampleRate(averageSampleRate);
            if (MicInput.AudioEncoding.samplerate != closestSampleRate)
            {
                MicInput.AudioEncoding.samplerate = closestSampleRate;
                _log.Info("Input SampleRate Set: {0}\nElapsed: {1:0.000} seconds\nAverage Samples per Second: {2}",
                    closestSampleRate, elapsedSeconds, averageSampleRate);
            }

            // Restart calculation
            _startSampleTime = newSampleTime;
            _measureSampleTotal = 0;
        }

        /// <summary>
        /// Return average sample rate
        /// </summary>
        private static double GetAverageSampleRate(double[] sampleRates, int sampleRateCount)
        {
            // Ignore if invalid total
            var count = Mathf.Min(sampleRateCount, sampleRates.Length);
            if (count <= 0)
            {
                return 0d;
            }
            // Iterate each sample
            var result = 0d;
            for (int i = 0; i < count; i++)
            {
                result += sampleRates[i];
            }
            // Return average
            return result / count;
        }

        /// <summary>
        /// Obtains the closest sample rate using the samples per second
        /// </summary>
        private static int GetClosestSampleRate(double samplesPerSecond)
        {
            // Iterate sample rates
            var result = 0;
            var diff = int.MaxValue;
            var samplesPerSecondInt = (int)Math.Round(samplesPerSecond);
            for (int i = 0; i < ALLOWED_SAMPLE_RATES.Length; i++)
            {
                // Determine difference between sample rates
                var sampleRate = ALLOWED_SAMPLE_RATES[i];
                var check = Mathf.Abs(sampleRate - samplesPerSecondInt);
                // Closer, replace
                if (check < diff)
                {
                    result = sampleRate;
                    diff = check;
                }
                // More, return previous
                else
                {
                    return result;
                }
            }
            // Return result
            return result;
        }
        #endregion Dynamic Sample Rate

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
            _log.Debug($"Start Writing to AudioBuffer Debug File\nPath: {path}");
            _fileStream = File.Open(path, FileMode.Create);
        }

        // Write to file
        private void DebugWrite(byte outByte)
        {
            if (_fileStream == null)
            {
                return;
            }
            _fileStream.WriteByte(outByte);
        }

        // Stop mic debug
        private void DebugStop()
        {
            if (_fileStream == null)
            {
                return;
            }
            _log.Debug(GetType().Name, "Stop Writing to AudioBuffer Debug File");
            _fileStream.Close();
            _fileStream.Dispose();
            _fileStream = null;
        }
#endif
    }
}
