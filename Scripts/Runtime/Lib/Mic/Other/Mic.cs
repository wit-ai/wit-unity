// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// Source: https://github.com/adrenak/unimic/blob/master/Assets/UniMic/Runtime/Mic.cs

#if !UNITY_WEBGL || UNITY_EDITOR
#if UNITY_EDITOR
// Simulates Android Permission Popup
#define EDITOR_PERMISSION_POPUP
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
// Disables microphones
#define DISABLE_MICROPHONES
#endif

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Meta.Voice;
using Meta.Voice.Logging;
using UnityEngine.Serialization;

namespace Meta.WitAi.Lib
{
    /// <summary>
    /// A simple mic playback class
    /// </summary>
    [LogCategory(LogCategory.Audio, LogCategory.Input)]
    public class Mic : BaseAudioClipInput
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        /// <summary>
        /// The audio clip obtained from Microphone.Start
        /// </summary>
        public override AudioClip Clip => _audioClip;
        private AudioClip _audioClip;

        /// <summary>
        /// The current clip position of the
        /// </summary>
        public override int ClipPosition => MicrophoneGetPosition(CurrentDeviceName);

        /// <summary>
        /// Always allow initial activation
        /// </summary>
        public override bool CanActivateAudio => true;

        /// <summary>
        /// Due to Microphone.Start & Microphone.End taking so long, activate on enable
        /// </summary>
        public override bool ActivateOnEnable => _activateOnEnable;
        [SerializeField] private bool _activateOnEnable = true;

        /// <summary>
        /// Searches for mics for this long following an activation request.
        /// </summary>
        [SerializeField] [Tooltip("Searches for mics for this long following an activation request.")]
        public float MicStartTimeout = 5f;
        // Seconds between mic device check
        private const float MIC_CHECK = 0.5f;

        /// <summary>
        /// Total amount of seconds included within the mic audio clip buffer
        /// </summary>
        [SerializeField] [Tooltip("Total amount of seconds included within the mic audio clip buffer")]
        public int MicBufferLength = 2;

        /// <summary>
        /// Sample rate for mic audio capture in samples per second.
        /// </summary>
        [SerializeField] [Tooltip("Sample rate for mic audio capture in samples per second.")]
        [FormerlySerializedAs("_audioClipSampleRate")]
        private int _micSampleRate = WitConstants.ENDPOINT_SPEECH_SAMPLE_RATE;

        /// <summary>
        /// Getter for audio mic sample audio capture in samples per second
        /// </summary>
        public override int AudioSampleRate => _micSampleRate;

        /// <summary>
        /// Sets the new audio sample rate if possible
        /// </summary>
        /// <param name="newSampleRate">New sample rate</param>
        public void SetAudioSampleRate(int newSampleRate)
        {
            // Cannot change if on
            if (ActivationState == VoiceAudioInputState.On)
            {
                VLog.E(GetType().Name, $"Cannot set audio sample rate while Mic is {ActivationState}");
                return;
            }

            // Apply sample rate
            _micSampleRate = newSampleRate;
        }

        #region ACTIVATION
        /// <summary>
        /// Wait for devices to exist & then start mic
        /// </summary>
        protected override IEnumerator HandleActivation()
        {
            // Attempt to wait for a mic selection
            DateTime now = DateTime.UtcNow;
            DateTime start = now;
            DateTime lastRefresh = DateTime.MinValue;
            while (string.IsNullOrEmpty(CurrentDeviceName) && (now - start).TotalSeconds < MicStartTimeout)
            {
                // Perform a refresh
                if ((now - lastRefresh).TotalSeconds > MIC_CHECK)
                {
                    // Refresh now
                    lastRefresh = now;
                    RefreshMicDevices();

                    // Use default if not provided
                    if (_devices.Count > 0 && CurrentDeviceIndex < 0)
                    {
                        CurrentDeviceIndex = 0;
                    }
                }

                // Still invalid, wait
                if (string.IsNullOrEmpty(CurrentDeviceName))
                {
                    yield return null;
                    now = DateTime.UtcNow;
                }
            }

            // Still invalid, fail
            if (string.IsNullOrEmpty(CurrentDeviceName))
            {
                VLog.W(GetType().Name, $"No mics found after {MicStartTimeout} seconds");
                SetActivationState(VoiceAudioInputState.Off);
                yield break;
            }

            // If valid, start microphone
            StartMicrophone();

            // Failed
            if (_audioClip == null)
            {
                SetActivationState(VoiceAudioInputState.Off);
            }
        }

        // Start microphone with desired device name
        private void StartMicrophone()
        {
            // Cannot start with invalid mic name
            string micName = CurrentDeviceName;
            if (string.IsNullOrEmpty(micName))
            {
                return;
            }

            // Ensure 1 second or longer
            MicBufferLength = Mathf.Max(1, MicBufferLength);

            // Start microphone
            _log.Info("Start Microphone '{0}'", micName);
            _audioClip = MicrophoneStart(micName, true, MicBufferLength, AudioSampleRate);

            // Failed to activate
            if (_audioClip == null)
            {
                VLog.W(GetType().Name, $"Microphone.Start() did not return an AudioClip\nMic Name: {micName}");
            }
        }
        #endregion ACTIVATION

        #region DEACTIVATION
        /// <summary>
        /// Stop microphone for deactivation
        /// </summary>
        protected override void HandleDeactivation()
        {
            StopMicrophone();
        }

        // Handle microphone stop
        private void StopMicrophone()
        {
            // Cannot stop with invalid mic name
            string micName = CurrentDeviceName;
            if (string.IsNullOrEmpty(micName))
            {
                return;
            }

            // Stop microphone if recording
            if (MicrophoneIsRecording(micName))
            {
                _log.Info("Stop Microphone '{0}'", micName);
                MicrophoneEnd(micName);
            }

            // Destroy clip
            if (_audioClip != null)
            {
                DestroyImmediate(_audioClip);
                _audioClip = null;
            }
        }
        #endregion MICROPHONE

        #region DEVICES
        /// <summary>
        /// List of all the available Mic devices
        /// </summary>
        public List<string> Devices
        {
            get
            {
                if (_devices == null || _devices.Count == 0)
                {
                    RefreshMicDevices();
                }
                return _devices;
            }
        }
        private List<string> _devices = new List<string>();

        /// <summary>
        /// Index of the current Mic device in m_Devices
        /// </summary>
        public int CurrentDeviceIndex { get; private set; } = -1;

        /// <summary>
        /// Gets the name of the Mic device currently in use
        /// </summary>
        public string CurrentDeviceName
        {
            get
            {
                if (_devices == null || CurrentDeviceIndex < 0 || CurrentDeviceIndex >= _devices.Count)
                    return string.Empty;
                return _devices[CurrentDeviceIndex];
            }
        }

        /// <summary>
        /// Refresh the current list of devices
        /// </summary>
        private void RefreshMicDevices()
        {
            // Get old mic name
            string oldMicName = CurrentDeviceName;

            // Clear previous list
            _devices.Clear();

            // Get new list & add if it exists
            var micNames = MicrophoneGetDevices();
            if (micNames != null)
            {
                _devices.AddRange(micNames);
            }

            // Get new device index if applicable
            CurrentDeviceIndex = _devices.IndexOf(oldMicName);
        }

        /// <summary>
        /// Changes to a Mic device for Recording
        /// </summary>
        /// <param name="index">The index of the Mic device. Refer to <see cref="Devices"/></param>
        public void ChangeMicDevice(int index)
        {
            StopMicrophone();
            CurrentDeviceIndex = index;
            StartMicrophone();
        }
        #endregion DEVICES

        #region NO_MIC_WRAPPERS
        // Wrapper methods to handle platforms where the UnityEngine.Microphone class is non-existent
        private AudioClip MicrophoneStart(string deviceName, bool loop, int lengthSeconds, int frequency)
        {
#if DISABLE_MICROPHONES
            return null;
#else
            return Microphone.Start(deviceName, loop, lengthSeconds, frequency);
#endif
        }

        private void MicrophoneEnd(string deviceName)
        {
#if !DISABLE_MICROPHONES
            Microphone.End(deviceName);
#endif
        }

        private bool MicrophoneIsRecording(string device)
        {
#if DISABLE_MICROPHONES
            return false;
#else
            return !string.IsNullOrEmpty(device) && Microphone.IsRecording(device);
#endif
        }

        private string[] MicrophoneGetDevices()
        {
#if DISABLE_MICROPHONES
            return new string[] {};
#else
            #if EDITOR_PERMISSION_POPUP
            // Simulate permission popup which returns null array for multiple frames
            if (Time.frameCount <= 5)
            {
                return null;
            }
            #endif
            return Microphone.devices;
#endif
        }

        private int MicrophoneGetPosition(string device)
        {
#if DISABLE_MICROPHONES
            // This should (probably) never happen, since the Start/Stop Recording methods will
            // silently fail under webGL.
            return 0;
#else
            return Microphone.GetPosition(device);
#endif
        }
        #endregion
    }
}
#endif
