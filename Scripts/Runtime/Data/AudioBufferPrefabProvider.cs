/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// This class is responsible for managing a shared audio buffer for receiving microphone data.
    /// It is used by voice services to grab audio segments from the AudioBuffer's internal ring buffer.
    /// </summary>
    public class AudioBufferPrefabProvider : MonoBehaviour, IAudioBufferProvider
    {
        [SerializeField]
        private AudioBuffer _audioBufferPrefab;

        /// <summary>
        /// Set to audio buffer provider ASAP
        /// </summary>
        private void Awake()
        {
            AudioBuffer.AudioBufferProvider = this;
        }

        /// <summary>
        /// Generate an audio buffer using the prefab
        /// </summary>
        public AudioBuffer InstantiateAudioBuffer()
        {
            if (_audioBufferPrefab == null)
            {
                return null;
            }
            var instance = Instantiate(_audioBufferPrefab.gameObject, null, true);
            instance.name = _audioBufferPrefab.gameObject.name;
            return instance.GetComponent<AudioBuffer>();
        }
    }
}
