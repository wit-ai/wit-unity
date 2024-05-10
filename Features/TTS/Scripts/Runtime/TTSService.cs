/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using Meta.Voice.Audio;
using Meta.Voice.Logging;
using Meta.WitAi.Requests;
using UnityEngine;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Events;
using Meta.WitAi.TTS.Interfaces;

namespace Meta.WitAi.TTS
{
    [LogCategory(LogCategory.TextToSpeech)]
    public abstract class TTSService : MonoBehaviour
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        #region SETUP
        // Accessor
        public static TTSService Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Get all services
                    TTSService[] services = Resources.FindObjectsOfTypeAll<TTSService>();
                    if (services != null)
                    {
                        // Set as first instance that isn't a prefab
                        _instance = Array.Find(services, (o) => o.gameObject.scene.rootCount != 0);
                    }
                }
                return _instance;
            }
        }
        private static TTSService _instance;

        /// <summary>
        /// Audio system to be used for streaming & playback
        /// </summary>
        public IAudioSystem AudioSystem
        {
            get
            {
                if (_audioSystem == null && Application.isPlaying)
                {
                    // Search on TTS Service
                    _audioSystem = gameObject.GetComponent<IAudioSystem>();
                    if (_audioSystem == null)
                    {
                        // Add default unity audio system if not found
                        _audioSystem = gameObject.AddComponent<UnityAudioSystem>();
                    }
                }
                return _audioSystem;
            }
            set => _audioSystem = value;
        }
        private IAudioSystem _audioSystem;

        // Handles TTS runtime cache
        public abstract ITTSRuntimeCacheHandler RuntimeCacheHandler { get; }
        // Handles TTS cache requests
        public abstract ITTSDiskCacheHandler DiskCacheHandler { get; }
        // Handles TTS web requests
        public abstract ITTSWebHandler WebHandler { get; }
        // Handles TTS voice presets
        public abstract ITTSVoiceProvider VoiceProvider { get; }

        /// <summary>
        /// Static event called whenever any TTSService.Awake is called
        /// </summary>
        public static event Action<TTSService> OnServiceStart;
        /// <summary>
        /// Static event called whenever any TTSService.OnDestroy is called
        /// </summary>
        public static event Action<TTSService> OnServiceDestroy;

        /// <summary>
        /// Returns error if invalid
        /// </summary>
        public virtual string GetInvalidError()
        {
            if (WebHandler == null)
            {
                return "Web Handler Missing";
            }
            if (VoiceProvider == null)
            {
                return "Voice Provider Missing";
            }
            return string.Empty;
        }

        // Handles TTS events
        public TTSServiceEvents Events => _events;
        [Header("Event Settings")]
        [SerializeField] private TTSServiceEvents _events = new TTSServiceEvents();

        // Set instance
        protected virtual void Awake()
        {
            _instance = this;
            _delegates = false;
        }
        // Call event
        protected virtual void Start()
        {
            OnServiceStart?.Invoke(this);
        }
        // Log if invalid
        protected virtual void OnEnable()
        {
            string validError = GetInvalidError();
            if (!string.IsNullOrEmpty(validError))
            {
                Log(validError, null, VLoggerVerbosity.Warning);
            }
        }
        // Remove delegates
        protected virtual void OnDisable()
        {
            RemoveDelegates();
        }
        // Add delegates
        private bool _delegates = false;
        protected virtual void AddDelegates()
        {
            // Ignore if already added
            if (_delegates)
            {
                return;
            }
            _delegates = true;

            if (RuntimeCacheHandler != null)
            {
                RuntimeCacheHandler.OnClipAdded.AddListener(OnRuntimeClipAdded);
                RuntimeCacheHandler.OnClipRemoved.AddListener(OnRuntimeClipRemoved);
            }
            if (DiskCacheHandler != null)
            {
                DiskCacheHandler.DiskStreamEvents.OnStreamBegin.AddListener(OnDiskStreamBegin);
                DiskCacheHandler.DiskStreamEvents.OnStreamCancel.AddListener(OnDiskStreamCancel);
                DiskCacheHandler.DiskStreamEvents.OnStreamReady.AddListener(OnDiskStreamReady);
                DiskCacheHandler.DiskStreamEvents.OnStreamError.AddListener(OnDiskStreamError);
            }
            if (WebHandler != null)
            {
                WebHandler.WebStreamEvents.OnStreamBegin.AddListener(OnWebStreamBegin);
                WebHandler.WebStreamEvents.OnStreamCancel.AddListener(OnWebStreamCancel);
                WebHandler.WebStreamEvents.OnStreamReady.AddListener(OnWebStreamReady);
                WebHandler.WebStreamEvents.OnStreamError.AddListener(OnWebStreamError);
                WebHandler.WebDownloadEvents.OnDownloadBegin.AddListener(OnWebDownloadBegin);
                WebHandler.WebDownloadEvents.OnDownloadCancel.AddListener(OnWebDownloadCancel);
                WebHandler.WebDownloadEvents.OnDownloadSuccess.AddListener(OnWebDownloadSuccess);
                WebHandler.WebDownloadEvents.OnDownloadError.AddListener(OnWebDownloadError);
            }
        }
        // Remove delegates
        protected virtual void RemoveDelegates()
        {
            // Ignore if not yet added
            if (!_delegates)
            {
                return;
            }
            _delegates = false;

            if (RuntimeCacheHandler != null)
            {
                RuntimeCacheHandler.OnClipAdded.RemoveListener(OnRuntimeClipAdded);
                RuntimeCacheHandler.OnClipRemoved.RemoveListener(OnRuntimeClipRemoved);
            }
            if (DiskCacheHandler != null)
            {
                DiskCacheHandler.DiskStreamEvents.OnStreamBegin.RemoveListener(OnDiskStreamBegin);
                DiskCacheHandler.DiskStreamEvents.OnStreamCancel.RemoveListener(OnDiskStreamCancel);
                DiskCacheHandler.DiskStreamEvents.OnStreamReady.RemoveListener(OnDiskStreamReady);
                DiskCacheHandler.DiskStreamEvents.OnStreamError.RemoveListener(OnDiskStreamError);
            }
            if (WebHandler != null)
            {
                WebHandler.WebStreamEvents.OnStreamBegin.RemoveListener(OnWebStreamBegin);
                WebHandler.WebStreamEvents.OnStreamCancel.RemoveListener(OnWebStreamCancel);
                WebHandler.WebStreamEvents.OnStreamReady.RemoveListener(OnWebStreamReady);
                WebHandler.WebStreamEvents.OnStreamError.RemoveListener(OnWebStreamError);
                WebHandler.WebDownloadEvents.OnDownloadBegin.RemoveListener(OnWebDownloadBegin);
                WebHandler.WebDownloadEvents.OnDownloadCancel.RemoveListener(OnWebDownloadCancel);
                WebHandler.WebDownloadEvents.OnDownloadSuccess.RemoveListener(OnWebDownloadSuccess);
                WebHandler.WebDownloadEvents.OnDownloadError.RemoveListener(OnWebDownloadError);
            }
        }
        // Remove instance
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            UnloadAll();
            OnServiceDestroy?.Invoke(this);
        }

        /// <summary>
        /// Get clip log data
        /// </summary>
        private void Log(string logMessage, TTSClipData clipData = null, VLoggerVerbosity logLevel = VLoggerVerbosity.Info)
        {
            _log.Log(_log.CorrelationID, logLevel, logMessage + (clipData == null ? "" : "\n" + clipData));
        }
        #endregion

        #region HELPERS
        // Frequently used keys
        private const string CLIP_ID_DELIM = "|";
        private readonly SHA256 CLIP_HASH = SHA256.Create();

        /// <summary>
        /// Gets the text to be spoken after applying all relevant voice settings.
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken by a particular voice</param>
        /// <param name="voiceSettings">Voice settings to be used</param>
        /// <returns>Returns a the final text to be spoken</returns>
        public string GetFinalText(string textToSpeak, TTSVoiceSettings voiceSettings)
        {
            StringBuilder result = new StringBuilder();
            AppendFinalText(result, textToSpeak, voiceSettings);
            return result.ToString();
        }
        // Finalize text using a string builder
        protected virtual void AppendFinalText(StringBuilder builder, string textToSpeak, TTSVoiceSettings voiceSettings)
        {
            if (!string.IsNullOrEmpty(voiceSettings?.PrependedText))
            {
                builder.Append(voiceSettings.PrependedText);
            }
            builder.Append(textToSpeak);
            if (!string.IsNullOrEmpty(voiceSettings?.AppendedText))
            {
                builder.Append(voiceSettings.AppendedText);
            }
        }

        /// <summary>
        /// Obtain unique id for clip data
        /// </summary>
        public virtual string GetClipID(string textToSpeak, TTSVoiceSettings voiceSettings)
        {
            // Get a text string for a unique id
            StringBuilder uniqueId = new StringBuilder();
            // Add all data items
            if (voiceSettings == null && VoiceProvider != null)
            {
                voiceSettings = VoiceProvider.VoiceDefaultSettings;
            }
            if (voiceSettings != null)
            {
                Dictionary<string, string> data = voiceSettings.Encode();
                foreach (var key in data.Keys)
                {
                    string keyClean = data[key].Replace(CLIP_ID_DELIM, "");
                    uniqueId.Append(keyClean);
                    uniqueId.Append(CLIP_ID_DELIM);
                }
            }
            // Finally, add unique id
            AppendFinalText(uniqueId, textToSpeak, voiceSettings);
            // Return id
            return GetSha256Hash(CLIP_HASH, uniqueId.ToString().ToLower());
        }

        private string GetSha256Hash(SHA256 shaHash, string input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = shaHash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        /// <summary>
        /// Creates new clip data or returns existing cached clip
        /// </summary>
        /// <param name="textToSpeak">Text to speak</param>
        /// <param name="clipID">Unique clip id</param>
        /// <param name="voiceSettings">Voice settings</param>
        /// <param name="diskCacheSettings">Disk Cache settings</param>
        /// <returns>Clip data structure</returns>
        protected virtual TTSClipData CreateClipData(string textToSpeak, string clipID, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings)
        {
            // Use default voice settings if none are set
            if (voiceSettings == null && VoiceProvider != null)
            {
                voiceSettings = VoiceProvider.VoiceDefaultSettings;
            }
            // Use default disk cache settings if none are set
            if (diskCacheSettings == null && DiskCacheHandler != null)
            {
                diskCacheSettings = DiskCacheHandler.DiskCacheDefaultSettings;
            }
            // Determine clip id if empty
            if (string.IsNullOrEmpty(clipID))
            {
                clipID = GetClipID(textToSpeak, voiceSettings);
            }

            // Get clip from runtime cache if applicable
            TTSClipData clipData = GetRuntimeCachedClip(clipID);
            if (clipData != null)
            {
                return clipData;
            }

            // Generate new clip data
            AudioType audioType = GetAudioType();
            clipData = new TTSClipData()
            {
                clipID = clipID,
                audioType = audioType,
                textToSpeak = GetFinalText(textToSpeak, voiceSettings),
                voiceSettings = voiceSettings,
                diskCacheSettings = diskCacheSettings,
                loadState = TTSClipLoadState.Unloaded,
                loadProgress = 0f,
                loadDuration = 0f,
                queryParameters = voiceSettings?.Encode(),
                queryStream = GetShouldAudioStream(audioType),
                clipStream = CreateClipStream(),
                useEvents = ShouldUseEvents(audioType)
            };

            // Null text is assumed loaded
            if (string.IsNullOrEmpty(clipData.textToSpeak))
            {
                clipData.loadState = TTSClipLoadState.Loaded;
            }

            // Return generated clip
            return clipData;
        }
        // Generate a new audio clip stream
        protected virtual IAudioClipStream CreateClipStream()
        {
            // Default
            if (AudioSystem == null)
            {
                return new RawAudioClipStream(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE);
            }
            // Get audio clip via audio system
            return AudioSystem.GetAudioClipStream(WitConstants.ENDPOINT_TTS_CHANNELS,
                WitConstants.ENDPOINT_TTS_SAMPLE_RATE);
        }

        // Returns current audio type setting for initial TTSClipData setup
        protected virtual AudioType GetAudioType() => AudioType.WAV;

        // Returns current audio stream setting for initial TTSClipData setup
        protected virtual bool GetShouldAudioStream(AudioType audioType) =>
            VRequest.CanStreamAudio(audioType);

        // Returns true provided audio type can be decoded
        protected virtual bool ShouldUseEvents(AudioType audioType) =>
            VRequest.CanDecodeAudio(audioType);

        // Set clip state
        protected virtual void SetClipLoadState(TTSClipData clipData, TTSClipLoadState loadState)
        {
            clipData.loadState = loadState;
            clipData.onStateChange?.Invoke(clipData, clipData.loadState);
        }
        #endregion

        #region LOAD
        // TTS Request options
        public TTSClipData Load(string textToSpeak, Action<TTSClipData, string> onStreamReady = null) => Load(textToSpeak, null, null, null, onStreamReady);
        public TTSClipData Load(string textToSpeak, string presetVoiceId, Action<TTSClipData, string> onStreamReady = null) => Load(textToSpeak, null, GetPresetVoiceSettings(presetVoiceId), null, onStreamReady);
        public TTSClipData Load(string textToSpeak, string presetVoiceId, TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string> onStreamReady = null) => Load(textToSpeak, null, GetPresetVoiceSettings(presetVoiceId), diskCacheSettings, onStreamReady);
        public TTSClipData Load(string textToSpeak, TTSVoiceSettings voiceSettings, TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string> onStreamReady = null) => Load(textToSpeak, null, voiceSettings, diskCacheSettings, onStreamReady);

        /// <summary>
        /// Perform a request for a TTS audio clip
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="clipID">Unique clip id</param>
        /// <param name="voiceSettings">Custom voice settings</param>
        /// <param name="diskCacheSettings">Custom cache settings</param>
        /// <returns>Generated TTS clip data</returns>
        public virtual TTSClipData Load(string textToSpeak, string clipID, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string> onStreamReady)
        {
            // Add delegates if needed
            AddDelegates();

            // Get clip data
            TTSClipData clipData = CreateClipData(textToSpeak, clipID, voiceSettings, diskCacheSettings);
            if (clipData == null)
            {
                Log("No clip provided", null, VLoggerVerbosity.Error);
                onStreamReady?.Invoke(clipData, "No clip provided");
                return null;
            }
            if (!gameObject.activeSelf)
            {
                Log("Cannot load clip while inactive", null, VLoggerVerbosity.Error);
                onStreamReady?.Invoke(clipData, "TTSService inactive");
                return null;
            }

            // From Runtime Cache
            if (clipData.loadState != TTSClipLoadState.Unloaded)
            {
                // Add callback
                if (onStreamReady != null)
                {
                    // Call once ready
                    if (clipData.loadState == TTSClipLoadState.Preparing)
                    {
                        clipData.onPlaybackReady += (e) => onStreamReady(clipData, e);
                    }
                    // Call after return
                    else
                    {
                        StartCoroutine(CallAfterAMoment(() =>
                        {
                            onStreamReady(clipData,
                                clipData.loadState == TTSClipLoadState.Loaded ? string.Empty : "Error");
                        }));
                    }
                }

                // Return clip
                return clipData;
            }

            // Add to runtime cache if possible
            if (RuntimeCacheHandler != null)
            {
                if (!RuntimeCacheHandler.AddClip(clipData))
                {
                    // Add callback
                    if (onStreamReady != null)
                    {
                        // Call once ready
                        if (clipData.loadState == TTSClipLoadState.Preparing)
                        {
                            clipData.onPlaybackReady += (e) => onStreamReady(clipData, e);
                        }
                        // Call after return
                        else
                        {
                            StartCoroutine(CallAfterAMoment(() => onStreamReady(clipData,
                                clipData.loadState == TTSClipLoadState.Loaded ? string.Empty : "Error")));
                        }
                    }

                    // Return clip
                    return clipData;
                }
            }
            // Load begin
            else
            {
                OnLoadBegin(clipData);
            }

            // Add on ready delegate
            clipData.onPlaybackReady += (error) => onStreamReady?.Invoke(clipData, error);

            // Wait a moment and load
            StartCoroutine(CallAfterAMoment(() =>
            {
                // If should cache to disk, attempt to do so
                if (ShouldCacheToDisk(clipData))
                {
                    PerformDownloadAndStream(clipData);
                }
                // Simply stream from the web
                else
                {
                    PerformStreamFromWeb(clipData);
                }
            }));

            // Return data
            return clipData;
        }
        // Perform download & stream following error checks
        private void PerformDownloadAndStream(TTSClipData clipDataParam)
        {
            // Download if possible
            DownloadToDiskCache(clipDataParam, (clipDataResult, downloadPath, error) =>
            {
                // Not in cache & cannot download, stream from web
                if (string.Equals(error, WitConstants.ERROR_TTS_CACHE_DOWNLOAD))
                {
                    PerformStreamFromWeb(clipDataResult);
                }
                // Download failed, throw error
                else if (!string.IsNullOrEmpty(error))
                {
                    OnDiskStreamBegin(clipDataResult);
                    OnDiskStreamError(clipDataResult, error);
                }
                // Stream from disk
                else
                {
                    PerformStreamFromDisk(clipDataResult);
                }
            });
        }
        // Perform stream from web following error check
        private void PerformStreamFromWeb(TTSClipData clipData)
        {
            // Stream was canceled before starting
            if (clipData.loadState != TTSClipLoadState.Preparing)
            {
                OnWebStreamBegin(clipData);
                OnWebStreamCancel(clipData);
                return;
            }

            // Check for web errors
            string webErrors = WebHandler.GetWebErrors(clipData);
            if (!string.IsNullOrEmpty(webErrors))
            {
                OnWebStreamBegin(clipData);
                OnWebStreamError(clipData, webErrors);
                return;
            }

            // Stream
            WebHandler?.RequestStreamFromWeb(clipData);
        }
        // Perform stream from disk following cancel check
        private void PerformStreamFromDisk(TTSClipData clipData)
        {
            // Stream was canceled while downloading
            if (clipData.loadState != TTSClipLoadState.Preparing)
            {
                OnDiskStreamBegin(clipData);
                OnDiskStreamCancel(clipData);
                return;
            }

            // Stream from Cache
            DiskCacheHandler?.StreamFromDiskCache(clipData, RaiseRequestProgressUpdated);
        }
        // Wait a moment
        private IEnumerator CallAfterAMoment(Action call)
        {
            if (Application.isPlaying && !Application.isBatchMode)
            {
                yield return new WaitForEndOfFrame();
            }
            else
            {
                yield return null;
            }
            call();
        }

        // On progress update, apply to clip
        protected virtual void RaiseRequestProgressUpdated(TTSClipData clipData, float newProgress)
        {
            if (clipData != null)
            {
                clipData.loadProgress = newProgress;
            }
        }

        // Load begin
        private void OnLoadBegin(TTSClipData clipData, bool download = false)
        {
            // Now preparing
            SetClipLoadState(clipData, TTSClipLoadState.Preparing);

            // Begin load
            Log($"{(download ? "Download" : "Load")} Clip", clipData);
            Events?.OnClipCreated?.Invoke(clipData);
        }
        // Handle begin of disk cache streaming
        private void OnDiskStreamBegin(TTSClipData clipData) => OnStreamBegin(clipData, true);
        private void OnWebStreamBegin(TTSClipData clipData) => OnStreamBegin(clipData, false);
        private void OnStreamBegin(TTSClipData clipData, bool fromDisk)
        {
            // Set delegates for clip stream update/completion
            if (clipData.clipStream != null)
            {
                clipData.clipStream.OnStreamUpdated = (cs) => OnStreamUpdated(clipData, cs, fromDisk);
                clipData.clipStream.OnStreamComplete = (cs) => OnStreamComplete(clipData, cs, fromDisk);
            }

            // Callback delegate
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Begin", clipData);
            Events?.Stream?.OnStreamBegin?.Invoke(clipData);
        }
        // Handle cancel of disk cache streaming
        private void OnDiskStreamCancel(TTSClipData clipData) => OnStreamCancel(clipData, true);
        private void OnWebStreamCancel(TTSClipData clipData) => OnStreamCancel(clipData, false);
        private void OnStreamCancel(TTSClipData clipData, bool fromDisk)
        {
            // Ignore unless preparing
            bool unloaded = clipData.loadState == TTSClipLoadState.Unloaded;

            // Handled as an error
            SetClipLoadState(clipData, TTSClipLoadState.Error);

            // Invoke
            clipData.onPlaybackReady?.Invoke(WitConstants.CANCEL_ERROR);
            clipData.onPlaybackReady = null;

            // Callback delegate
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Canceled", clipData);
            Events?.Stream?.OnStreamCancel?.Invoke(clipData);

            // Unload clip
            if (!unloaded)
            {
                Unload(clipData);
            }
            // Set back to unloaded
            else
            {
                SetClipLoadState(clipData, TTSClipLoadState.Unloaded);
            }
        }
        // Handle disk cache streaming error
        private void OnDiskStreamError(TTSClipData clipData, string error) => OnStreamError(clipData, error, true);
        private void OnWebStreamError(TTSClipData clipData, string error) => OnStreamError(clipData, error, false);
        private void OnStreamError(TTSClipData clipData, string error, bool fromDisk)
        {
            // Cancelled
            if (error.Equals(WitConstants.CANCEL_ERROR))
            {
                OnStreamCancel(clipData, fromDisk);
                return;
            }

            // Error
            SetClipLoadState(clipData, TTSClipLoadState.Error);

            // Invoke playback is ready
            clipData.onPlaybackReady?.Invoke(error);
            clipData.onPlaybackReady = null;

            // Stream error
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Error\nError: {error}", clipData, VLoggerVerbosity.Error);
            Events?.Stream?.OnStreamError?.Invoke(clipData, error);

            // Unload clip
            Unload(clipData);
        }
        // Handle successful completion of disk cache streaming
        private void OnDiskStreamReady(TTSClipData clipData) => OnStreamReady(clipData, true);
        private void OnWebStreamReady(TTSClipData clipData) => OnStreamReady(clipData, false);
        private void OnStreamReady(TTSClipData clipData, bool fromDisk)
        {
            // Refresh cache for file size
            if (RuntimeCacheHandler != null)
            {
                // Stop forcing an unload if runtime cache update fails
                RuntimeCacheHandler.OnClipRemoved.RemoveListener(OnRuntimeClipRemoved);
                bool failed = !RuntimeCacheHandler.AddClip(clipData);
                RuntimeCacheHandler.OnClipRemoved.AddListener(OnRuntimeClipRemoved);

                // Handle fail directly
                if (failed)
                {
                    OnStreamError(clipData, "Removed from runtime cache due to file size", fromDisk);
                    OnRuntimeClipRemoved(clipData);
                    return;
                }
            }

            // Set delegates again since the stream may have changed during setup
            if (clipData.clipStream != null)
            {
                clipData.clipStream.OnStreamUpdated = (cs) => OnStreamUpdated(clipData, cs, fromDisk);
                clipData.clipStream.OnStreamComplete = (cs) => OnStreamComplete(clipData, cs, fromDisk);
            }

            // Set clip stream state
            SetClipLoadState(clipData, TTSClipLoadState.Loaded);
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Ready", clipData);

            // Invoke playback is ready
            clipData.onPlaybackReady?.Invoke(string.Empty);
            clipData.onPlaybackReady = null;

            // Callback delegate
            Events?.Stream?.OnStreamReady?.Invoke(clipData);
        }
        // Stream clip update
        private void OnStreamUpdated(TTSClipData clipData, IAudioClipStream clipStream, bool fromDisk)
        {
            // Ignore invalid
            if (clipStream == null || clipData == null || clipStream != clipData.clipStream)
            {
                return;
            }

            // Log & call event
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Updated", clipData);
            Events?.Stream?.OnStreamClipUpdate?.Invoke(clipData);
        }
        // Stream complete
        private void OnStreamComplete(TTSClipData clipData, IAudioClipStream clipStream, bool fromDisk)
        {
            // Ignore invalid
            if (clipStream == null || clipData == null || clipStream != clipData.clipStream)
            {
                return;
            }

            // Log & call event
            Log($"{(fromDisk ? "Disk" : "Web")} Stream Complete", clipData);
            Events?.Stream?.OnStreamComplete?.Invoke(clipData);

            // Web request completion
            if (!fromDisk)
            {
                Events?.WebRequest?.OnRequestComplete.Invoke(clipData);
            }
        }
        #endregion

        #region UNLOAD
        /// <summary>
        /// Unload all audio clips from the runtime cache
        /// </summary>
        public void UnloadAll()
        {
            // Failed
            TTSClipData[] clips = RuntimeCacheHandler?.GetClips();
            if (clips == null)
            {
                return;
            }

            // Copy array
            HashSet<TTSClipData> remaining = new HashSet<TTSClipData>(clips);

            // Unload all clips
            foreach (var clip in remaining)
            {
                Unload(clip);
            }
        }
        /// <summary>
        /// Force a runtime cache unload
        /// </summary>
        public void Unload(TTSClipData clipData)
        {
            if (RuntimeCacheHandler != null)
            {
                RuntimeCacheHandler.RemoveClip(clipData.clipID);
            }
            else
            {
                OnUnloadBegin(clipData);
            }
        }
        /// <summary>
        /// Perform clip unload
        /// </summary>
        /// <param name="clipID"></param>
        protected virtual void OnUnloadBegin(TTSClipData clipData)
        {
            // Abort if currently preparing
            if (clipData.loadState == TTSClipLoadState.Preparing)
            {
                // Cancel web stream
                WebHandler?.CancelWebStream(clipData);
                // Cancel web download to cache
                WebHandler?.CancelWebDownload(clipData, GetDiskCachePath(clipData.textToSpeak, clipData.clipID, clipData.voiceSettings, clipData.diskCacheSettings));
                // Cancel disk cache stream
                DiskCacheHandler?.CancelDiskCacheStream(clipData);
            }

            // Unloads clip stream
            clipData.clipStream?.Unload();
            clipData.clipStream = null;

            // Clip is now unloaded
            SetClipLoadState(clipData, TTSClipLoadState.Unloaded);

            // Unload
            Log($"Unload Clip", clipData);
            Events?.OnClipUnloaded?.Invoke(clipData);
        }
        #endregion

        #region RUNTIME CACHE
        /// <summary>
        /// Obtain a clip from the runtime cache, if applicable
        /// </summary>
        public TTSClipData GetRuntimeCachedClip(string clipID) => RuntimeCacheHandler?.GetClip(clipID);
        /// <summary>
        /// Obtain all clips from the runtime cache, if applicable
        /// </summary>
        public TTSClipData[] GetAllRuntimeCachedClips() => RuntimeCacheHandler?.GetClips();

        /// <summary>
        /// Called when runtime cache adds a clip
        /// </summary>
        /// <param name="clipData"></param>
        protected virtual void OnRuntimeClipAdded(TTSClipData clipData) => OnLoadBegin(clipData);

        /// <summary>
        /// Called when runtime cache unloads a clip
        /// </summary>
        /// <param name="clipData">Clip to be unloaded</param>
        protected virtual void OnRuntimeClipRemoved(TTSClipData clipData) => OnUnloadBegin(clipData);
        #endregion

        #region DISK CACHE
        /// <summary>
        /// Whether a specific clip should be cached
        /// </summary>
        /// <param name="clipData">Clip data</param>
        /// <returns>True if should be cached</returns>
        public bool ShouldCacheToDisk(TTSClipData clipData) =>
            DiskCacheHandler != null && DiskCacheHandler.ShouldCacheToDisk(clipData);

        /// <summary>
        /// Get disk cache
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="clipID">Unique clip id</param>
        /// <param name="voiceSettings">Custom voice settings</param>
        /// <param name="diskCacheSettings">Custom disk cache settings</param>
        /// <returns></returns>
        public string GetDiskCachePath(string textToSpeak, string clipID, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings) =>
            DiskCacheHandler?.GetDiskCachePath(CreateClipData(textToSpeak, clipID, voiceSettings, diskCacheSettings));

        // Download options
        public TTSClipData DownloadToDiskCache(string textToSpeak,
            Action<TTSClipData, string, string> onDownloadComplete = null) =>
            DownloadToDiskCache(textToSpeak, null, null, null, onDownloadComplete);
        public TTSClipData DownloadToDiskCache(string textToSpeak, string presetVoiceId,
            Action<TTSClipData, string, string> onDownloadComplete = null) => DownloadToDiskCache(textToSpeak, null,
            GetPresetVoiceSettings(presetVoiceId), null, onDownloadComplete);
        public TTSClipData DownloadToDiskCache(string textToSpeak, string presetVoiceId,
            TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string, string> onDownloadComplete = null) =>
            DownloadToDiskCache(textToSpeak, null, GetPresetVoiceSettings(presetVoiceId), diskCacheSettings,
                onDownloadComplete);
        public TTSClipData DownloadToDiskCache(string textToSpeak, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string, string> onDownloadComplete = null) =>
            DownloadToDiskCache(textToSpeak, null, voiceSettings, diskCacheSettings, onDownloadComplete);

        /// <summary>
        /// Perform a download for a TTS audio clip
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="clipID">Unique clip id</param>
        /// <param name="voiceSettings">Custom voice settings</param>
        /// <param name="diskCacheSettings">Custom disk cache settings</param>
        /// <param name="onDownloadComplete">Callback when file has finished downloading</param>
        /// <returns>Generated TTS clip data</returns>
        public TTSClipData DownloadToDiskCache(string textToSpeak, string clipID, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings, Action<TTSClipData, string, string> onDownloadComplete = null)
        {
            // Add delegates if needed
            AddDelegates();

            // Generate clip & perform load callback
            TTSClipData clipData = CreateClipData(textToSpeak, clipID, voiceSettings, diskCacheSettings);
            OnLoadBegin(clipData, true);

            // Handle download
            DownloadToDiskCache(clipData, onDownloadComplete);

            // Return clip data for tracking
            return clipData;
        }

        // Performs download to disk cache
        protected virtual void DownloadToDiskCache(TTSClipData clipDataParam, Action<TTSClipData, string, string> onDownloadComplete)
        {
            // Check if cached to disk & log
            string downloadPath = DiskCacheHandler.GetDiskCachePath(clipDataParam);
            DiskCacheHandler.CheckCachedToDisk(clipDataParam, (clipDataResult, found) =>
            {
                // Cache checked
                Log($"Disk Cache {(found ? "Found" : "Missing")}\nPath: {downloadPath}", clipDataResult);

                // Already downloaded, return successful
                if (found)
                {
                    onDownloadComplete?.Invoke(clipDataResult, downloadPath, string.Empty);
                    return;
                }

                // Preload selected but not in disk cache, return an error
                if (Application.isPlaying && clipDataResult.diskCacheSettings.DiskCacheLocation == TTSDiskCacheLocation.Preload)
                {
                    OnWebDownloadBegin(clipDataResult, downloadPath);
                    OnWebDownloadError(clipDataResult, downloadPath, WitConstants.ERROR_TTS_CACHE_DOWNLOAD);
                    onDownloadComplete?.Invoke(clipDataResult, downloadPath, WitConstants.ERROR_TTS_CACHE_DOWNLOAD);
                    return;
                }

                // Cancelled while checking cache
                if (clipDataResult.loadState != TTSClipLoadState.Preparing)
                {
                    OnWebDownloadBegin(clipDataResult, downloadPath);
                    OnWebDownloadCancel(clipDataResult, downloadPath);
                    onDownloadComplete?.Invoke(clipDataResult, downloadPath, WitConstants.CANCEL_ERROR);
                    return;
                }

                // Check for web issues
                string webErrors = WebHandler.GetWebErrors(clipDataResult);
                if (!string.IsNullOrEmpty(webErrors))
                {
                    OnWebDownloadBegin(clipDataResult, downloadPath);
                    OnWebDownloadError(clipDataResult, downloadPath, webErrors);
                    onDownloadComplete?.Invoke(clipDataResult, downloadPath, webErrors);
                    return;
                }

                // Add download completion callback
                clipDataResult.onDownloadComplete += (error) => onDownloadComplete?.Invoke(clipDataResult, downloadPath, error);

                // Download to cache
                WebHandler.RequestDownloadFromWeb(clipDataResult, downloadPath);
            });
        }
        // On web download begin
        private void OnWebDownloadBegin(TTSClipData clipData, string downloadPath)
        {
            Log($"Download Clip - Begin\nPath: {downloadPath}", clipData);
            Events?.Download?.OnDownloadBegin?.Invoke(clipData, downloadPath);
        }
        // On web download complete
        private void OnWebDownloadSuccess(TTSClipData clipData, string downloadPath)
        {
            // Invoke clip callback & clear
            clipData.onDownloadComplete?.Invoke(string.Empty);
            clipData.onDownloadComplete = null;

            // Log
            Log($"Download Clip - Success\nPath: {downloadPath}", clipData);
            Events?.Download?.OnDownloadSuccess?.Invoke(clipData, downloadPath);
        }
        // On web download complete
        private void OnWebDownloadCancel(TTSClipData clipData, string downloadPath)
        {
            // Invoke clip callback & clear
            clipData.onDownloadComplete?.Invoke(WitConstants.CANCEL_ERROR);
            clipData.onDownloadComplete = null;

            // Log
            Log($"Download Clip - Canceled\nPath: {downloadPath}", clipData);
            Events?.Download?.OnDownloadCancel?.Invoke(clipData, downloadPath);
        }
        // On web download complete
        private void OnWebDownloadError(TTSClipData clipData, string downloadPath, string error)
        {
            // Cancelled
            if (error.Equals(WitConstants.CANCEL_ERROR))
            {
                OnWebDownloadCancel(clipData, downloadPath);
                return;
            }

            // Invoke clip callback & clear
            clipData.onDownloadComplete?.Invoke(error);
            clipData.onDownloadComplete = null;

            // Log
            Log($"Download Clip - Failed\nPath: {downloadPath}\nError: {error}", clipData, VLoggerVerbosity.Error);
            Events?.Download?.OnDownloadError?.Invoke(clipData, downloadPath, error);
        }
        #endregion

        #region VOICES
        /// <summary>
        /// Return all preset voice settings
        /// </summary>
        /// <returns></returns>
        public TTSVoiceSettings[] GetAllPresetVoiceSettings() => VoiceProvider?.PresetVoiceSettings;

        /// <summary>
        /// Return preset voice settings for a specific id
        /// </summary>
        /// <param name="presetVoiceId"></param>
        /// <returns></returns>
        public TTSVoiceSettings GetPresetVoiceSettings(string presetVoiceId)
        {
            if (VoiceProvider == null || VoiceProvider.PresetVoiceSettings == null)
            {
                return null;
            }
            return Array.Find(VoiceProvider.PresetVoiceSettings, (v) => string.Equals(v.SettingsId, presetVoiceId, StringComparison.CurrentCultureIgnoreCase));
        }
        #endregion
    }
}
