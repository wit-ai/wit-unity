/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.Voice.Audio;
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Meta.WitAi.TTS.LipSync.OvrLipSyncIntegration
{
    //-------------------------------------------------------------------------------------
    // ***** OvrLipSyncEngineContext
    //
    /// <summary>
    /// OvrLipSyncEngineContext interfaces into the Oculus phoneme recognizer.
    /// This component should be added into the scene once for each Audio Source.
    ///
    /// </summary>
    public class AudioPlayerLipSyncContext : LipSyncContextBase
    {
        [FormerlySerializedAs("audioPlayerSource")]
        [ObjectType(typeof (IAudioPlayerProvider))]
        [SerializeField] private Object _audioPlayerSource;

        /// <summary>
        /// Returns true if this component made it through enablement process.
        ///
        /// Note: we are tracking this separately because the methods for processing are called on a background thread.
        /// </summary>
        private bool _isEnabled = false;

        // * * * * * * * * * * * * *
        // Public members
        [Tooltip("Play input audio back through audio output.")]
        public bool audioLoopback = false;
        [Tooltip("Adjust the linear audio gain multiplier before processing lipsync")]
        public float gain = 1.0f;

        private IAudioPlayer _iAudioPlayer;

        public override float Time => _iAudioPlayer?.ElapsedSamples / _iAudioPlayer?.ClipStream?.SampleRate ?? 0;
        public override bool IsPlaying => _iAudioPlayer?.IsPlaying ?? false;

        protected virtual void OnEnable()
        {
            if (!_audioPlayerSource)
            {
                Debug.LogError($"No audio player source set on {name}");
                enabled = false;
                return;
            }
            _iAudioPlayer = ((IAudioPlayerProvider)_audioPlayerSource).AudioPlayer;
            if (null == _iAudioPlayer)
            {
                Debug.LogError($"No audio player provided by the audio player source on {name}");
                enabled = false;
                return;
            }
            _iAudioPlayer.OnPlaySamples += ProcessAudioSamples;
            _isEnabled = true;
        }

        private void OnDisable()
        {
            if (null != _iAudioPlayer)
            {
                _iAudioPlayer.OnPlaySamples -= ProcessAudioSamples;
            }

            _isEnabled = false;
        }

        /// <summary>
        /// Preprocess F32 PCM audio buffer
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="channels">Channels.</param>
        public void PreprocessAudioSamples(float[] data, int channels)
        {
            // Increase the gain of the input
            for (int i = 0; i < data.Length; ++i)
            {
                data[i] = data[i] * gain;
            }
        }

        /// <summary>
        /// Postprocess F32 PCM audio buffer
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="channels">Channels.</param>
        public void PostprocessAudioSamples(float[] data, int channels)
        {
            // Turn off output (so that we don't get feedback from mics too close to speakers)
            if (!audioLoopback)
            {
                for (int i = 0; i < data.Length; ++i)
                    data[i] = data[i] * 0.0f;
            }
        }

        /// <summary>
        /// Pass F32 PCM audio buffer to the lip sync module
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="channels">Channels.</param>
        public void ProcessAudioSamplesRaw(float[] data, int channels)
        {
            if (!_isEnabled) return;

            // Send data into Phoneme context for processing (if context is not 0)
            lock (this)
            {
                if (Context == 0 || OvrLipSyncEngine.IsInitialized() != OvrLipSyncEngine.Result.Success)
                {
                    return;
                }
                var frame = this.Frame;
                OvrLipSyncEngine.ProcessFrame(Context, data, frame, channels == 2);
            }
        }

        /// <summary>
        /// Pass S16 PCM audio buffer to the lip sync module
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="channels">Channels.</param>
        public void ProcessAudioSamplesRaw(short[] data, int channels)
        {
            if (!_isEnabled) return;

            // Send data into Phoneme context for processing (if context is not 0)
            lock (this)
            {
                if (Context == 0 || OvrLipSyncEngine.IsInitialized() != OvrLipSyncEngine.Result.Success)
                {
                    return;
                }
                var frame = this.Frame;
                OvrLipSyncEngine.ProcessFrame(Context, data, frame, channels == 2);
            }
        }


        /// <summary>
        /// Process F32 audio sample and pass it to the lip sync module for computation
        /// </summary>
        /// <param name="data">Data.</param>
        public void ProcessAudioSamples(float[] data)
        {
            if (!_isEnabled) return;

            // Do not process if we are not initialized, or if there is no
            // audio source attached to game object
            if (OvrLipSyncEngine.IsInitialized() != OvrLipSyncEngine.Result.Success)
            {
                return;
            }
            PreprocessAudioSamples(data, _iAudioPlayer.ClipStream.Channels);
            ProcessAudioSamplesRaw(data, _iAudioPlayer.ClipStream.Channels);
            PostprocessAudioSamples(data, _iAudioPlayer.ClipStream.Channels);
        }
    }
}
