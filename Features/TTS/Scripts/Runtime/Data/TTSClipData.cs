/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meta.Voice.Audio;
using UnityEngine;

namespace Meta.WitAi.TTS.Data
{
    // Various request load states
    public enum TTSClipLoadState
    {
        Unloaded,
        Preparing,
        Loaded,
        Error
    }

    [Serializable]
    public class TTSClipData
    {
        // Text to be spoken
        public string textToSpeak;
        // Unique identifier
        public string clipID;
        // Audio type extension
        [Obsolete("Use extension directly.")]
        public AudioType audioType;
        // Voice settings for request
        public TTSVoiceSettings voiceSettings;
        // Cache settings for request
        public TTSDiskCacheSettings diskCacheSettings;

        /// <summary>
        /// Unique request id used for tracking & logging
        /// </summary>
        public string queryRequestId { get; } = WitConstants.GetUniqueId();
        // Whether service should stream audio or just provide all at once
        public bool queryStream;
        // Request data
        public Dictionary<string, string> queryParameters;

        // Clip stream
        public IAudioClipStream clipStream
        {
            get => _clipStream;
            set
            {
                // Unload previous clip stream
                IAudioClipStream v = value;
                if (_clipStream != null && _clipStream != v)
                {
                    clipStream.OnStreamReady = null;
                    clipStream.OnStreamUpdated = null;
                    clipStream.OnStreamComplete = null;
                    _clipStream.Unload();
                }
                // Apply new clip stream
                _clipStream = v;
            }
        }
        private IAudioClipStream _clipStream;
        public AudioClip clip
        {
            get
            {
                if (clipStream is IAudioClipProvider uacs)
                {
                    return uacs.Clip;
                }
                return null;
            }
        }
        // Clip load state
        [NonSerialized] public TTSClipLoadState loadState;
        // Clip load progress
        [NonSerialized] public float loadProgress;

        /// <summary>
        /// Amount of time from request begin to ready callback in seconds
        /// </summary>
        [NonSerialized] public float readyDuration;
        /// <summary>
        /// Amount of time from request begin to complete callback in seconds
        /// </summary>
        [NonSerialized] public float completeDuration;

        // On clip state change
        public Action<TTSClipData, TTSClipLoadState> onStateChange;

        /// <summary>
        /// Whether or not this tts clip data is requesting event data
        /// with tts stream.
        /// </summary>
        public bool useEvents;
        /// <summary>
        /// The currently set tts events
        /// </summary>
        public TTSEventContainer Events { get; } = new TTSEventContainer();

        /// <summary>
        /// The file extension to be used for this specific file type.  Includes the period.
        /// </summary>
        public string extension;

        /// <summary>
        /// Any error that occurs during the load process
        /// </summary>
        public string LoadError { get; set; }
        /// <summary>
        /// Task that returns when ready for playback
        /// </summary>
        public TaskCompletionSource<bool> LoadReady { get; } = new TaskCompletionSource<bool>();
        /// <summary>
        /// Task that returns when complete
        /// </summary>
        public TaskCompletionSource<bool> LoadCompletion { get; } = new TaskCompletionSource<bool>();

        /// <summary>
        /// A callback when clip stream is ready
        /// Returns an error if there was an issue
        /// </summary>
        public Action<TTSClipData> onPlaybackReady;
        /// <summary>
        /// A callback when clip has downloaded successfully
        /// Returns an error if there was an issue
        /// </summary>
        public Action<string> onDownloadComplete;

        /// <summary>
        /// Called when a script has requested load and playback of this clip
        /// </summary>
        public Action<TTSClipData> onRequestBegin;
        /// <summary>
        /// Called when a script has completed load and playback of this clip
        /// </summary>
        public Action<TTSClipData> onRequestComplete;

        /// <summary>
        /// Called when a script has queued playback with this clip
        /// </summary>
        public Action<TTSClipData> onPlaybackQueued;
        /// <summary>
        /// Called when a script has began playback with this clip
        /// </summary>
        public Action<TTSClipData> onPlaybackBegin;
        /// <summary>
        /// Called when a script has completed playback with this clip
        /// </summary>
        public Action<TTSClipData> onPlaybackComplete;

        /// <summary>
        /// Compare clips if possible
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is TTSClipData other)
            {
                return Equals(other);
            }
            return false;
        }
        /// <summary>
        /// Compare clip ids
        /// </summary>
        public bool Equals(TTSClipData other)
        {
            return HasClipId(other?.clipID);
        }
        /// <summary>
        /// Compare clip ids
        /// </summary>
        public bool HasClipId(string clipId)
        {
            return string.Equals(clipID, clipId, StringComparison.CurrentCultureIgnoreCase);
        }
        /// <summary>
        /// Get hash code
        /// </summary>
        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 31 + clipID.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Obtains all clip data via formatting
        /// </summary>
        public override string ToString()
        {
            return string.Format("Text: {0}\nVoice: {1}\nClip Id: {2}\nType: {3}\nStream: {4}\nEvents: {5}\nAudio Length: {6:0.00} seconds",
                textToSpeak,
                voiceSettings?.SettingsId ?? "Null",
                clipID,
                extension,
                queryStream,
                Events?.Events?.Count() ?? 0,
                clipStream?.Length ?? 0
            );
        }
    }
}
