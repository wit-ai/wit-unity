/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using Meta.WitAi.Data;
using Meta.WitAi.Interfaces;

namespace Meta.WitAi.Lib
{
    #if UNITY_EDITOR
    public class WebGlMic : MonoBehaviour, IAudioInputSource
    #else
    public class Mic : MonoBehaviour, IAudioInputSource
    #endif
    {
#pragma warning disable 0067
        public event Action OnStartRecording;
        public event Action OnStartRecordingFailed;
#pragma warning disable 0067
        public event Action<int, float[], float> OnSampleReady;
        public event Action OnStopRecording;
        public void StartRecording(int sampleLen)
        {
            VLog.E("Direct microphone use is not currently supported in WebGL.");
            OnStartRecordingFailed?.Invoke();
        }

        public void StopRecording()
        {
            OnStopRecording?.Invoke();
        }

        public bool IsRecording => false;
        public AudioEncoding AudioEncoding => new AudioEncoding();
        public bool IsInputAvailable => false;

        #region Muting
        /// <inheritdoc />
        public virtual bool IsMuted { get; private set; } = false;

#pragma warning disable 0067
        /// <inheritdoc />
        public event Action OnMicMuted;

        /// <inheritdoc />
        public event Action OnMicUnmuted;
#pragma warning disable 0067

        protected virtual void SetMuted(bool muted)
        {
            if (IsMuted != muted)
            {
                IsMuted = muted;
                if(IsMuted) OnMicMuted?.Invoke();
                else OnMicUnmuted?.Invoke();
            }
        }

        #endregion

        public void CheckForInput()
        {

        }
        private bool MicrophoneIsRecording(string device)
        {
            return false;
        }

        private string[] MicrophoneGetDevices()
        {
            VLog.E("Direct microphone use is not currently supported in WebGL.");
            return new string[] {};
        }

        private int MicrophoneGetPosition(string device)
        {
            // This should (probably) never happen, since the Start/Stop Recording methods will
            // silently fail under webGL.
            return 0;
        }

        public int AudioClipSampleRate
        {
            get => 16000;
            set
            {
               VLog.E("Cannot set sample rate on gl mic");
            }
        }
    }
}
