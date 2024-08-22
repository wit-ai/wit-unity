/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Audio;
using Meta.Voice.Logging;
using Meta.WitAi.Attributes;
using Meta.WitAi.Json;
using UnityEngine;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Events;
using Meta.WitAi.TTS.Integrations;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Utilities;

namespace Meta.WitAi.TTS
{
    /// <summary>
    /// Abstract script for loading and returning text-to-speech clip streams.
    /// </summary>
    [LogCategory(LogCategory.TextToSpeech)]
    public abstract class TTSService : MonoBehaviour, ILogSource
    {
        // Logging
        [SerializeField] private bool verboseLogging;
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.TextToSpeech);

        #region SETUP
        /// <summary>
        /// Static singleton to be used when interacting with a single TTSService
        /// </summary>
        public static TTSService Instance
        {
            get
            {
                _instance ??= GameObjectSearchUtility.FindSceneObject<TTSService>();
                return _instance;
            }
        }
        private static TTSService _instance;

        /// <summary>
        /// Audio system to be used for obtaining audio clip streams
        /// </summary>
        public IAudioSystem AudioSystem
        {
            get => _audioSystem as IAudioSystem;
            set => _audioSystem = SetInterface(value);
        }
        [Header("TTS Modules")]
        [Tooltip("Audio system to be used for obtaining audio clip streams.")]
        [SerializeField] [ObjectType(typeof(IAudioSystem))]
        private UnityEngine.Object _audioSystem;

        /// <summary>
        /// Runtime cache that assists with the temporary storage of audio clips
        /// </summary>
        public ITTSRuntimeCacheHandler RuntimeCacheHandler
        {
            get => _runtimeCacheHandler as ITTSRuntimeCacheHandler;
            set => _runtimeCacheHandler = SetInterface(value);
        }
        [Tooltip("Runtime cache that assists with the temporary storage of audio clips.")]
        [SerializeField] [ObjectType(typeof(ITTSRuntimeCacheHandler))]
        private UnityEngine.Object _runtimeCacheHandler;

        /// <summary>
        /// Disk cache that assists with the backup and retrieval of audio clips saved to disk.
        /// </summary>
        public ITTSDiskCacheHandler DiskCacheHandler
        {
            get => _diskCacheHandler as ITTSDiskCacheHandler;
            set => _diskCacheHandler = SetInterface(value);
        }
        [Tooltip("Disk cache that assists with the backup and retrieval of audio clips saved to disk.")]
        [SerializeField] [ObjectType(typeof(ITTSDiskCacheHandler))]
        private UnityEngine.Object _diskCacheHandler;

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

        // Handles TTS events
        public TTSServiceEvents Events => _events;
        [Header("Event Settings")]
        [SerializeField] private TTSServiceEvents _events = new TTSServiceEvents();

        // Current thread safe active state
        private bool _isActive;
        // Thread safe listener state
        private bool _hasListeners = false;

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

        // Set instance
        protected virtual void Awake()
        {
            _instance = this;
#if UNITY_EDITOR
            // Ensure listeners are reset if added in editor
            SetListeners(false);
#endif
        }
        // Call event
        protected virtual void Start()
        {
            OnServiceStart?.Invoke(this);
        }
        // Log if invalid
        protected virtual void OnEnable()
        {
            _isActive = true;
            SetListeners(true);
            string validError = GetInvalidError();
            if (!string.IsNullOrEmpty(validError))
            {
                Log(validError, null, VLoggerVerbosity.Warning);
            }
        }
        // Remove delegates
        protected virtual void OnDisable()
        {
            _isActive = false;
            SetListeners(false);
        }

        /// <summary>
        /// Sets listeners to add or remove
        /// </summary>
        protected virtual void SetListeners(bool add)
        {
            // Ignore if already set
            if (_hasListeners == add)
            {
                return;
            }
            _hasListeners = add;

            // Setup all modules
            if (add)
            {
                AudioSystem = GetOrCreateInterface<IAudioSystem, UnityAudioSystem>(AudioSystem);
                RuntimeCacheHandler = GetOrCreateInterface<ITTSRuntimeCacheHandler, TTSRuntimeLRUCache>(RuntimeCacheHandler);
                DiskCacheHandler = GetInterface(DiskCacheHandler);
            }

            // Setup runtime handler callbacks
            if (RuntimeCacheHandler != null)
            {
                if (add)
                {
                    RuntimeCacheHandler.OnClipAdded += OnRuntimeClipAdded;
                    RuntimeCacheHandler.OnClipRemoved += OnRuntimeClipRemoved;
                }
                else
                {
                    RuntimeCacheHandler.OnClipAdded -= OnRuntimeClipAdded;
                    RuntimeCacheHandler.OnClipRemoved -= OnRuntimeClipRemoved;
                }
            }
        }

        /// <summary>
        /// Obtains a script implementing a specific interface on this game object
        /// </summary>
        protected TInterface GetInterface<TInterface>(TInterface current)
        {
            // Already set
            if (current is UnityEngine.Object obj && obj)
            {
                return current;
            }
            // Get interface and cast if possible
            return gameObject.GetComponent<TInterface>();
        }

        /// <summary>
        /// Obtains a script implementing an interface or generates one if not found.
        /// </summary>
        protected TInterface GetOrCreateInterface<TInterface, TDefault>(TInterface current)
            where TDefault : MonoBehaviour, TInterface
        {
            // Get module and return if successful
            var result = GetInterface(current);
            if (result is UnityEngine.Object obj && obj)
            {
                return result;
            }
            // Adds a component of the default type
            return gameObject.AddComponent<TDefault>();
        }

        /// <summary>
        /// Safely sets an interface to a unity object
        /// </summary>
        private UnityEngine.Object SetInterface<TInterface>(TInterface newValue)
        {
            if (newValue is UnityEngine.Object cacheObject)
            {
                return cacheObject;
            }
            if (newValue != null)
            {
                Logger.Error("Set {0} Failed\nCannot set {1} to a UnityEngine.Object property", typeof(TInterface).Name, newValue.GetType().Name);
            }
            return null;
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
        private void Log(string logMessage, TTSClipData clipData = null, VLoggerVerbosity logLevel = VLoggerVerbosity.Verbose)
        {
            Logger.Log(Logger.CorrelationID, logLevel, "{0}\n{1}", logMessage, (clipData == null ? "" : clipData));
        }

        private void LogState(TTSClipData clipData, string message, bool fromDisk, string error = null)
        {
            const string log = "{0} {1}\nText: {2}\nVoice: {3}\nReady: {4:0.00} seconds\nRequest Id: {5}";
            if (!string.IsNullOrEmpty(error))
            {
                Logger.Error(log + "\nError: {6}",
                    fromDisk ? "Disk" : "Web",
                    message,
                    clipData?.textToSpeak ?? "Null",
                    clipData?.voiceSettings?.SettingsId ?? "Null",
                    clipData?.readyDuration ?? 0f,
                    clipData?.queryRequestId ?? "Null",
                    error);
            }
            else if (verboseLogging)
            {
                Logger.Verbose(log,
                    fromDisk ? "Disk" : "Web",
                    message,
                    clipData?.textToSpeak ?? "Null",
                    clipData?.voiceSettings?.SettingsId ?? "Null",
                    clipData?.readyDuration ?? 0f,
                    clipData?.queryRequestId ?? "Null");
            }
        }
        #endregion

        #region HELPERS
        /// <summary>
        /// Gets the text to be spoken after applying all relevant voice settings.
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken by a particular voice</param>
        /// <param name="voiceSettings">Voice settings to be used</param>
        /// <returns>Returns a the final text to be spoken</returns>
        public virtual string GetFinalText(string textToSpeak, TTSVoiceSettings voiceSettings)
        {
            // If no final determination is needed, return as is
            voiceSettings ??= VoiceProvider?.VoiceDefaultSettings;
            if (voiceSettings == null
                || string.IsNullOrEmpty(textToSpeak)
                || (string.IsNullOrEmpty(voiceSettings.PrependedText)
                    && string.IsNullOrEmpty(voiceSettings.AppendedText)))
            {
                return textToSpeak;
            }
            // Prepend and append
            return $"{voiceSettings.PrependedText}{textToSpeak}{voiceSettings.AppendedText}";
        }

        /// <summary>
        /// Obtain unique id for clip data using provided text
        /// </summary>
        public string GetClipID(string textToSpeak, TTSVoiceSettings voiceSettings)
        {
            var formattedText = GetFinalText(textToSpeak, voiceSettings);
            return GetClipIDWithFinalText(formattedText, voiceSettings);
        }

        /// <summary>
        /// Obtain unique id for clip data with text that has already been passed through GetFinalText
        /// </summary>
        protected virtual string GetClipIDWithFinalText(string formattedText, TTSVoiceSettings voiceSettings)
        {
            // Use empty
            if (string.IsNullOrEmpty(formattedText))
            {
                return WitConstants.TTS_EMPTY_ID;
            }

            // Use hash code for text
            var result = formattedText;

            // Prepend voice id if preset voice list is used
            if (VoiceProvider?.PresetVoiceSettings != null
                && VoiceProvider.PresetVoiceSettings.Length > 0)
            {
                voiceSettings ??= VoiceProvider?.VoiceDefaultSettings;
                if (voiceSettings != null)
                {
                    result = $"{result}|{voiceSettings.UniqueId}";
                }
            }

            // Use hash code with disk cache handler
            if (DiskCacheHandler != null)
            {
                int hashcode = result.GetHashCode();
                result = $"tts_{(hashcode < 0 ? "n" : "p")}{Mathf.Abs(hashcode)}";
            }

            // Return string
            return result;
        }

        /// <summary>
        /// Creates new clip data or returns existing cached clip
        /// </summary>
        /// <param name="textToSpeak">Text to speak</param>
        /// <param name="voiceSettings">Voice settings</param>
        /// <param name="diskCacheSettings">Disk Cache settings</param>
        /// <returns>Clip data structure</returns>
        public TTSClipData GetClipData(string textToSpeak, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings)
        {
            // Add listeners if not set to true, otherwise ignored.
            SetListeners(true);

            // Use default voice settings if none are set
            voiceSettings ??= VoiceProvider?.VoiceDefaultSettings;
            // Use default disk cache settings if none are set
            diskCacheSettings ??= DiskCacheHandler?.DiskCacheDefaultSettings;

            // Obtain final text and final clip id
            var finalText = GetFinalText(textToSpeak, voiceSettings);
            var finalClipId = GetClipIDWithFinalText(finalText, voiceSettings);

            // Get clip from runtime cache if applicable
            TTSClipData clipData = GetRuntimeCachedClip(finalClipId);
            if (clipData != null && string.Equals(clipData.clipID, finalClipId))
            {
                return clipData;
            }

            // Generate new clip data
            clipData = WebHandler.CreateClipData(finalClipId, finalText, voiceSettings, diskCacheSettings);

            // Return generated clip
            return clipData;
        }
        // Set clip state
        protected virtual void SetClipLoadState(TTSClipData clipData, TTSClipLoadState loadState)
        {
            clipData.loadState = loadState;
            RaiseEvents(() =>
            {
                clipData.onStateChange?.Invoke(clipData, clipData.loadState);
            });
        }
        #endregion

        #region LOAD
        /// <summary>
        /// Decode a response node into text to be spoken or a specific voice setting
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="textToSpeak">The text to be spoken output</param>
        /// <param name="voiceSettings">The output for voice settings</param>
        /// <returns>True if decode was successful</returns>
        public bool DecodeTts(WitResponseNode responseNode,
            out string textToSpeak,
            out TTSVoiceSettings voiceSettings)
            => WebHandler.DecodeTtsFromJson(responseNode, out textToSpeak, out voiceSettings);

        /// <summary>
        /// Loads a text-to-speech clip using a preset voice id, specified settings and return methods
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in audio</param>
        /// <param name="presetVoiceId">Specific id that corresponds to a preset voice setting id</param>
        /// <param name="diskCacheSettings">Custom disk cache options</param>
        /// <param name="onStreamReady">Callback for stream ready to perform playback</param>
        /// <param name="onStreamComplete">Callback for stream complete</param>
        /// <returns>Returns the generated tts clip</returns>
        public TTSClipData Load(string textToSpeak,
            string presetVoiceId = null,
            TTSDiskCacheSettings diskCacheSettings = null,
            Action<TTSClipData> onStreamReady = null,
            Action<TTSClipData, string> onStreamComplete = null)
            => Load(textToSpeak, GetPresetVoiceSettings(presetVoiceId), diskCacheSettings, onStreamReady, onStreamComplete);

        /// <summary>
        /// Loads a text-to-speech clip using specified settings and return methods
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in audio</param>
        /// <param name="voiceSettings">Custom voice options</param>
        /// <param name="diskCacheSettings">Custom disk cache options</param>
        /// <param name="onStreamReady">Callback for stream ready to perform playback</param>
        /// <param name="onStreamComplete">Callback for stream complete</param>
        /// <returns>Returns the generated tts clip</returns>
        public TTSClipData Load(string textToSpeak,
            TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings = null,
            Action<TTSClipData> onStreamReady = null,
            Action<TTSClipData, string> onStreamComplete = null)
        {
            var clip = GetClipData(textToSpeak, voiceSettings, diskCacheSettings);
            _ = LoadAsync(clip, onStreamReady, onStreamComplete);
            return clip;
        }

        /// <summary>
        /// Loads a text-to-speech clip using specified settings and return methods
        /// </summary>
        /// <param name="clipData">The clip that will be loaded if needed</param>
        /// <param name="onStreamReady">Callback for stream ready to perform playback</param>
        /// <param name="onStreamComplete">Callback for stream complete</param>
        /// <returns>Returns an error</returns>
        public async Task<string> LoadAsync(TTSClipData clipData,
            Action<TTSClipData> onStreamReady = null,
            Action<TTSClipData, string> onStreamComplete = null)
        {
            // Error without clip data
            if (clipData == null)
            {
                var error = "No clip provided";
                Log(error, null, VLoggerVerbosity.Error);
                onStreamComplete?.Invoke(null, error);
                return error;
            }
            // Error if inactive
            if (!_isActive)
            {
                var error = "Cannot load clip while inactive";
                Log(error, null, VLoggerVerbosity.Error);
                onStreamComplete?.Invoke(clipData, error);
                return error;
            }

            // Keep track if recently generated
            bool unloaded = clipData.loadState == TTSClipLoadState.Unloaded;

            // Attempt to add to runtime cache
            if (RuntimeCacheHandler != null)
            {
                if (!RuntimeCacheHandler.AddClip(clipData))
                {
                    var error = "Runtime cache refused to load";
                    Log(error, clipData, VLoggerVerbosity.Error);
                    onStreamComplete?.Invoke(clipData, error);
                    return error;
                }
            }
            // Otherwise begin 'preparing'
            else
            {
                RaiseLoadBegin(clipData);
            }

            // Add on ready delegate
            if (onStreamReady != null)
            {
                clipData.onPlaybackReady += onStreamReady.Invoke;
            }

            // Perform load
            if (unloaded)
            {
                // If empty, consider complete
                if (string.IsNullOrEmpty(clipData.textToSpeak))
                {
                    RaiseWebStreamBegin(clipData);
                    RaiseWebStreamReady(clipData);
                    RaiseWebStreamComplete(clipData);
                    onStreamComplete?.Invoke(clipData, string.Empty);
                    return string.Empty;
                }
                // If should cache to disk, attempt to do so
                if (ShouldCacheToDisk(clipData))
                {
                    var error = await PerformDownloadAndStream(clipData);
                    onStreamComplete?.Invoke(clipData, error);
                    return error;
                }

                // Simply stream from the web
                var streamError = await PerformStreamFromWeb(clipData);
                onStreamComplete?.Invoke(clipData, streamError);
                return streamError;
            }

            // Await completion task
            if (clipData.loadState == TTSClipLoadState.Preparing)
            {
                await clipData.LoadCompletion.Task;
            }
            // Otherwise call ready directly
            else if (clipData.loadState == TTSClipLoadState.Loaded)
            {
                onStreamReady?.Invoke(clipData);
            }

            // Return clip and load error if applicable
            onStreamComplete?.Invoke(clipData, clipData.LoadError);
            return clipData.LoadError;
        }

        /// <summary>
        /// Downloads and streams a specific clip
        /// </summary>
        /// <param name="clipData">The clip that will be loaded if needed</param>
        /// <returns>Returns an error</returns>
        private async Task<string> PerformDownloadAndStream(TTSClipData clipData)
        {
            // Download if possible
            var error = await DownloadAsync(clipData);

            // Not in cache & cannot download, stream from web
            if (string.Equals(error, WitConstants.ERROR_TTS_CACHE_DOWNLOAD))
            {
                return await PerformStreamFromWeb(clipData);
            }

            // Download failed, throw disk stream errors
            if (!string.IsNullOrEmpty(error))
            {
                RaiseDiskStreamBegin(clipData);
                RaiseDiskStreamError(clipData, error);
                return error;
            }

            // Stream from disk
            return await PerformStreamFromDisk(clipData);
        }

        /// <summary>
        /// Streams a specific clip from the web
        /// </summary>
        /// <param name="clipData">The clip that will be loaded if possible</param>
        /// <returns>Returns an error if applicable</returns>
        private async Task<string> PerformStreamFromWeb(TTSClipData clipData)
        {
            // Begin stream
            RaiseWebStreamBegin(clipData);

            // Stream was canceled before starting
            if (clipData.loadState != TTSClipLoadState.Preparing)
            {
                RaiseWebStreamCancel(clipData);
                return clipData.LoadError;
            }
            // Check for web errors
            string webErrors = WebHandler == null ? "No web handler found" : WebHandler.GetWebErrors(clipData);
            if (!string.IsNullOrEmpty(webErrors))
            {
                RaiseWebStreamError(clipData, webErrors);
                return clipData.LoadError;
            }

            // Perform stream
            var error = await WebHandler.RequestStreamFromWeb(clipData, RaiseWebStreamReady);

            // Throw error
            if (!string.IsNullOrEmpty(error))
            {
                RaiseWebStreamError(clipData, error);
                return clipData.LoadError;
            }

            // Success
            RaiseWebStreamComplete(clipData);
            return clipData.LoadError;
        }

        /// <summary>
        /// Streams a specific clip from disk if possible
        /// </summary>
        /// <param name="clipData">The clip that will be loaded if possible</param>
        /// <returns>Returns an error if applicable</returns>
        private async Task<string> PerformStreamFromDisk(TTSClipData clipData)
        {
            // Begin stream
            RaiseDiskStreamBegin(clipData);

            // Stream was canceled before starting
            if (clipData.loadState != TTSClipLoadState.Preparing)
            {
                RaiseDiskStreamCancel(clipData);
                return clipData.LoadError;
            }

            // Perform stream
            string downloadPath = DiskCacheHandler.GetDiskCachePath(clipData);
            var error = await WebHandler.RequestStreamFromDisk(clipData, downloadPath, RaiseDiskStreamReady);

            // Throw error
            if (!string.IsNullOrEmpty(error))
            {
                RaiseDiskStreamError(clipData, error);
                return clipData.LoadError;
            }

            // Success
            RaiseDiskStreamComplete(clipData);
            return clipData.LoadError;
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
                RaiseUnloadComplete(clipData);
            }
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
        protected virtual void OnRuntimeClipAdded(TTSClipData clipData) => RaiseLoadBegin(clipData);

        /// <summary>
        /// Called when runtime cache unloads a clip
        /// </summary>
        /// <param name="clipData">Clip to be unloaded</param>
        protected virtual void OnRuntimeClipRemoved(TTSClipData clipData) => RaiseUnloadComplete(clipData);
        #endregion

        #region DISK CACHE
        /// <summary>
        /// Whether a specific clip should be cached
        /// </summary>
        /// <param name="clipData">Clip data</param>
        /// <returns>True if should be cached</returns>
        public bool ShouldCacheToDisk(TTSClipData clipData) =>
            DiskCacheHandler != null && DiskCacheHandler.ShouldCacheToDisk(clipData) && !string.IsNullOrEmpty(clipData.textToSpeak);

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
            DiskCacheHandler?.GetDiskCachePath(GetClipData(textToSpeak, voiceSettings, diskCacheSettings));

        /// <summary>
        /// Perform a download for a TTS audio clip
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="presetVoiceId">Specific voice id</param>
        /// <param name="diskCacheSettings">Custom disk cache settings</param>
        /// <param name="onDownloadComplete">Callback when file has finished downloading</param>
        /// <returns>Generated TTS clip data</returns>
        public TTSClipData DownloadToDiskCache(string textToSpeak, string presetVoiceId,
            TTSDiskCacheSettings diskCacheSettings = null, Action<TTSClipData, string, string> onDownloadComplete = null) =>
            DownloadToDiskCache(textToSpeak, GetPresetVoiceSettings(presetVoiceId), diskCacheSettings,
                onDownloadComplete);

        /// <summary>
        /// Perform a download for a TTS audio clip
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="voiceSettings">Custom voice settings</param>
        /// <param name="diskCacheSettings">Custom disk cache settings</param>
        /// <param name="onDownloadComplete">Callback when file has finished
        /// downloading with success or error</param>
        /// <returns>Generated TTS clip data</returns>
        public TTSClipData DownloadToDiskCache(string textToSpeak, TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings = null, Action<TTSClipData, string, string> onDownloadComplete = null)
        {
            var clipData = GetClipData(textToSpeak, voiceSettings, diskCacheSettings);
            _ = DownloadAsync(clipData, onDownloadComplete);
            return clipData;
        }

        /// <summary>
        /// Perform a download for a TTS audio clip
        /// </summary>
        /// <param name="textToSpeak">Text to be spoken in clip</param>
        /// <param name="voiceSettings">Custom voice settings</param>
        /// <param name="diskCacheSettings">Custom disk cache settings</param>
        /// <returns>Any errors that occured during the download process</returns>
        public async Task<string> DownloadAsync(string textToSpeak,
            TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings)
        {
            var clipData = GetClipData(textToSpeak, voiceSettings, diskCacheSettings);
            return await DownloadAsync(clipData);
        }

        /// <summary>
        /// Perform a download for a TTS audio clip
        /// </summary>
        /// <param name="clipData">Clip data to be used for download</param>
        /// <param name="onDownloadComplete">Callback when file has finished
        /// downloading with success or error</param>
        /// <returns>Any errors that occured during the download process</returns>
        private async Task<string> DownloadAsync(TTSClipData clipData,
            Action<TTSClipData, string, string> onDownloadComplete = null)
        {
            // Ensure disk cache is found if needed
            SetListeners(true);

            // Throw error without clip data
            if (clipData == null)
            {
                var error = "Cannot download with null clip data";
                onDownloadComplete?.Invoke(clipData, null, error);
                return error;
            }
            // Throw error without disk cache handler
            if (DiskCacheHandler == null)
            {
                var error = "Cannot download without disk cache handler";
                onDownloadComplete?.Invoke(clipData, null, error);
                return error;
            }

            // Get download path
            var downloadPath = DiskCacheHandler.GetDiskCachePath(clipData);

            // Check if download
            var shouldDownload = await ShouldDownload(clipData, downloadPath);
            if (!shouldDownload.Item1)
            {
                // Network or service setup errors should throw download begin/error
                if (!string.IsNullOrEmpty(shouldDownload.Item2))
                {
                    RaiseDownloadBegin(clipData, downloadPath);
                    RaiseDownloadError(clipData, downloadPath, shouldDownload.Item2);
                }
                onDownloadComplete?.Invoke(clipData, downloadPath, shouldDownload.Item2);
                return shouldDownload.Item2;
            }

            // Download to cache
            RaiseDownloadBegin(clipData, downloadPath);
            var downloadErrors = await WebHandler.RequestDownloadFromWeb(clipData, downloadPath);
            if (string.Equals(clipData.LoadError, WitConstants.CANCEL_ERROR))
            {
                RaiseDownloadCancel(clipData, downloadPath);
            }
            else if (!string.IsNullOrEmpty(downloadErrors))
            {
                RaiseDownloadError(clipData, downloadPath, downloadErrors);
            }
            else
            {
                RaiseDownloadSuccess(clipData, downloadPath);
            }
            onDownloadComplete?.Invoke(clipData, downloadPath, downloadErrors);
            return downloadErrors;
        }

        /// <summary>
        /// Performs a lookup on a downloaded file and returns any errors that occur.
        /// </summary>
        private async Task<Tuple<bool, string>> ShouldDownload(TTSClipData clipData, string downloadPath)
        {
            // Empty clip should be considered downloaded
            if (string.IsNullOrEmpty(clipData.textToSpeak))
            {
                return new Tuple<bool, string>(false, string.Empty);
            }

            // Empty if currently on disk
            var checkError = await WebHandler.IsDownloadedToDisk(downloadPath);
            // Already downloaded
            if (string.IsNullOrEmpty(checkError))
            {
                return new Tuple<bool, string>(false, string.Empty);
            }
            // Cancelled
            if (string.Equals(clipData.LoadError, WitConstants.CANCEL_ERROR))
            {
                return new Tuple<bool, string>(false, WitConstants.CANCEL_ERROR);
            }
            // Preload selected but not in disk cache, return a specific error
            if (Application.isPlaying
                && clipData.diskCacheSettings.DiskCacheLocation == TTSDiskCacheLocation.Preload)
            {
                return new Tuple<bool, string>(false, WitConstants.ERROR_TTS_CACHE_DOWNLOAD);
            }
            // Check for web errors
            var webErrors = WebHandler.GetWebErrors(clipData);
            if (!string.IsNullOrEmpty(webErrors))
            {
                return new Tuple<bool, string>(false, webErrors);
            }
            // Download
            return new Tuple<bool, string>(true, checkError);
        }
        #endregion

        #region VOICES
        /// <summary>
        /// Return all preset voice settings
        /// </summary>
        public TTSVoiceSettings[] GetAllPresetVoiceSettings() => VoiceProvider?.PresetVoiceSettings;

        /// <summary>
        /// Return preset voice settings for a specific id
        /// </summary>
        public TTSVoiceSettings GetPresetVoiceSettings(string presetVoiceId)
        {
            if (VoiceProvider == null || VoiceProvider.PresetVoiceSettings == null)
            {
                return null;
            }
            return Array.Find(VoiceProvider.PresetVoiceSettings, (v) => string.Equals(v.SettingsId, presetVoiceId, StringComparison.CurrentCultureIgnoreCase));
        }
        #endregion

        #region CALLBACKS
        // Load begin
        private void RaiseLoadBegin(TTSClipData clipData, bool download = false)
        {
            SetClipLoadState(clipData, TTSClipLoadState.Preparing);
            RaiseEvents(() =>
            {
                if (verboseLogging) Logger.Verbose("Clip Loading\nText: {0}", clipData.textToSpeak);
                Events?.OnClipCreated?.Invoke(clipData);
            });
        }
        // Load complete
        private void RaiseUnloadComplete(TTSClipData clipData, bool download = false)
        {
            // Cancel any requests currently in progress
            WebHandler?.CancelRequests(clipData);

            // Unloads clip stream
            clipData.clipStream?.Unload();
            clipData.clipStream = null;
            if (clipData.loadState == TTSClipLoadState.Preparing)
            {
                clipData.LoadError = WitConstants.CANCEL_ERROR;
            }
            if (clipData.loadState != TTSClipLoadState.Error)
            {
                SetClipLoadState(clipData, TTSClipLoadState.Unloaded);
            }

            RaiseEvents(() =>
            {
                if (verboseLogging) Logger.Verbose("Clip Unloaded\nText: {0}", clipData.textToSpeak);
                Events?.OnClipUnloaded?.Invoke(clipData);
            });
        }
        // Handle begin of disk cache streaming
        private void RaiseDiskStreamBegin(TTSClipData clipData) => RaiseStreamBegin(clipData, true);
        private void RaiseWebStreamBegin(TTSClipData clipData) => RaiseStreamBegin(clipData, false);
        private void RaiseStreamBegin(TTSClipData clipData, bool fromDisk)
        {
            RaiseEvents(() =>
            {
                LogState(clipData, "Stream Begin", fromDisk);
                Events?.Stream?.OnStreamBegin?.Invoke(clipData);
            });
        }
        // Log and call all events related to stream error for playback
        private void RaiseDiskStreamError(TTSClipData clipData, string error) => RaiseStreamError(clipData, error, true);
        private void RaiseWebStreamError(TTSClipData clipData, string error) => RaiseStreamError(clipData, error, false);
        private void RaiseStreamError(TTSClipData clipData, string error, bool fromDisk)
        {
            // Cancelled
            if (error.Equals(WitConstants.CANCEL_ERROR))
            {
                RaiseStreamCancel(clipData, fromDisk);
                return;
            }
            clipData.LoadError = error;
            SetClipLoadState(clipData, TTSClipLoadState.Error);
            RaiseEvents(() =>
            {
                // Log and call errors
                LogState(clipData, "Stream Error", fromDisk, error);
                Events?.Stream?.OnStreamError?.Invoke(clipData, error);
                if (!clipData.LoadReady.Task.IsCompleted)
                {
                    clipData.LoadReady.SetResult(false);
                }

                // Stream complete
                RaiseStreamComplete(clipData, fromDisk);
            });
        }
        // Log and call all events related to stream cancellation
        private void RaiseDiskStreamCancel(TTSClipData clipData) => RaiseStreamCancel(clipData, true);
        private void RaiseWebStreamCancel(TTSClipData clipData) => RaiseStreamCancel(clipData, false);
        private void RaiseStreamCancel(TTSClipData clipData, bool fromDisk)
        {
            clipData.LoadError = WitConstants.CANCEL_ERROR;
            SetClipLoadState(clipData, TTSClipLoadState.Error);
            RaiseEvents(() =>
            {
                // Log and call ready
                LogState(clipData, "Stream Cancelled", fromDisk);
                Events?.Stream?.OnStreamCancel?.Invoke(clipData);
                if (!clipData.LoadReady.Task.IsCompleted)
                {
                    clipData.LoadReady.SetResult(false);
                }

                // Stream complete
                RaiseStreamComplete(clipData, fromDisk);
            });
        }
        // Log and call all events related to stream ready for playback
        private void RaiseDiskStreamReady(TTSClipData clipData) => RaiseStreamReady(clipData, true);
        private void RaiseWebStreamReady(TTSClipData clipData) => RaiseStreamReady(clipData, false);
        private void RaiseStreamReady(TTSClipData clipData, bool fromDisk)
        {
            // TODO: Move to pre-ready callback
            // Refresh cache for file size
            if (RuntimeCacheHandler != null)
            {
                // Stop forcing an unload if runtime cache update fails
                RuntimeCacheHandler.OnClipRemoved -= OnRuntimeClipRemoved;
                bool failed = !RuntimeCacheHandler.AddClip(clipData);
                RuntimeCacheHandler.OnClipRemoved += OnRuntimeClipRemoved;

                // Handle fail directly
                if (failed)
                {
                    RaiseStreamError(clipData, "Removed from runtime cache due to file size", fromDisk);
                    OnRuntimeClipRemoved(clipData);
                    return;
                }
            }
            SetClipLoadState(clipData, TTSClipLoadState.Loaded);
            RaiseEvents(() =>
            {
                LogState(clipData, "Stream Ready", fromDisk);
                clipData.onPlaybackReady?.Invoke(clipData);
                clipData.onPlaybackReady = null;
                Events?.Stream?.OnStreamReady?.Invoke(clipData);
                if (!clipData.LoadReady.Task.IsCompleted)
                {
                    clipData.LoadReady.SetResult(true);
                }
            });
        }
        // Log and call all events related to stream load completion
        private void RaiseDiskStreamComplete(TTSClipData clipData) => RaiseStreamComplete(clipData, true);
        private void RaiseWebStreamComplete(TTSClipData clipData) => RaiseStreamComplete(clipData, false);
        private void RaiseStreamComplete(TTSClipData clipData, bool fromDisk)
        {
            RaiseEvents(() =>
            {
                // Log and raise events
                LogState(clipData, "Stream Complete", fromDisk);
                Events?.Stream?.OnStreamComplete?.Invoke(clipData);
                if (!fromDisk)
                {
                    Events?.WebRequest?.OnRequestComplete.Invoke(clipData);
                }
                if (!clipData.LoadCompletion.Task.IsCompleted)
                {
                    clipData.LoadCompletion.SetResult(true);
                }

                // Unload any failures/cancellations
                if (clipData.loadState == TTSClipLoadState.Error)
                {
                    Unload(clipData);
                }
            });
        }
        // On web download begin
        private void RaiseDownloadBegin(TTSClipData clipData, string downloadPath)
        {
            RaiseEvents(() =>
            {
                LogState(clipData, "Download Begin", true);
                Events?.Download?.OnDownloadBegin?.Invoke(clipData, downloadPath);
            });
        }
        // On web download complete
        private void RaiseDownloadSuccess(TTSClipData clipData, string downloadPath)
        {
            RaiseEvents(() =>
            {
                LogState(clipData, "Download Success", true);
                clipData.onDownloadComplete?.Invoke(string.Empty);
                clipData.onDownloadComplete = null;
                Events?.Download?.OnDownloadSuccess?.Invoke(clipData, downloadPath);
            });
        }
        // On web download complete
        private void RaiseDownloadCancel(TTSClipData clipData, string downloadPath)
        {
            RaiseEvents(() =>
            {
                LogState(clipData, "Download Cancelled", true);
                clipData.onDownloadComplete?.Invoke(WitConstants.CANCEL_ERROR);
                clipData.onDownloadComplete = null;
                Events?.Download?.OnDownloadCancel?.Invoke(clipData, downloadPath);
            });
        }
        // On web download complete
        private void RaiseDownloadError(TTSClipData clipData, string downloadPath, string error)
        {
            // Cancelled
            if (error.Equals(WitConstants.CANCEL_ERROR))
            {
                RaiseDownloadCancel(clipData, downloadPath);
                return;
            }
            RaiseEvents(() =>
            {
                LogState(clipData, "Download Failed", true, error);
                clipData.onDownloadComplete?.Invoke(error);
                clipData.onDownloadComplete = null;
                Events?.Download?.OnDownloadError?.Invoke(clipData, downloadPath, error);
            });
        }
        // Calls all events on the main thread
        private void RaiseEvents(Action events)
        {
            _ = ThreadUtility.CallOnMainThread(events);
        }
        #endregion CALLBACKS
    }
}
