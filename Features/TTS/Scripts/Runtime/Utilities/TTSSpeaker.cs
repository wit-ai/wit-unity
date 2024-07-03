/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.WitAi.Json;
using Meta.WitAi.Speech;
using UnityEngine;
using UnityEngine.Serialization;
using Meta.Voice.Audio;
using Meta.Voice.Logging;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Integrations;
using Meta.WitAi.TTS.Interfaces;

namespace Meta.WitAi.TTS.Utilities
{
    [LogCategory(Voice.Logging.LogCategory.TextToSpeech)]
    public class TTSSpeaker : MonoBehaviour, ISpeechEventProvider, ISpeaker, ITTSEventPlayer, ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.TextToSpeech);

        [Header("Event Settings")]
        [Tooltip("All speaker load and playback events")]
        [SerializeField] private TTSSpeakerEvents _events = new TTSSpeakerEvents();
        public TTSSpeakerEvents Events => _events;
        public VoiceSpeechEvents SpeechEvents => _events;

        [Header("Text Settings")]
        [Tooltip("Text that is added to the front of any Speech() request")]
        [TextArea] [FormerlySerializedAs("prependedText")]
        public string PrependedText;

        [Tooltip("Text that is added to the end of any Speech() text")]
        [TextArea] [FormerlySerializedAs("appendedText")]
        public string AppendedText;

        [Header("Load Settings")]
        [Tooltip("Optional TTSService reference to be used for text-to-speech loading.  If missing, it will check the component.  If that is also missing then it will use the current singleton")]
        [SerializeField] private TTSService _ttsService;
        public TTSService TTSService
        {
            get
            {
                if (!_ttsService)
                {
                    _ttsService = GetComponent<TTSService>();
                    if (!_ttsService)
                    {
                        _ttsService = TTSService.Instance;
                    }
                }
                return _ttsService;
            }
        }

        [Tooltip("Preset voice setting id of TTSService voice settings")]
        [HideInInspector] [SerializeField] private string presetVoiceID;

        [Tooltip("Custom wit specific voice settings used if the preset is null or empty")]
        [HideInInspector] [SerializeField] public TTSWitVoiceSettings customWitVoiceSettings;

        [SerializeField] private bool verboseLogging;

        public string VoiceID
        {
            get => presetVoiceID;
            set => presetVoiceID = value;
        }

        // Override voice settings
        private TTSVoiceSettings _overrideVoiceSettings;

        /// <summary>
        /// The voice settings to be used for this TTSSpeaker
        /// </summary>
        public TTSVoiceSettings VoiceSettings
        {
            get
            {
                // Use override if exists & runtime
                if (_isPlaying && _overrideVoiceSettings != null)
                {
                    return _overrideVoiceSettings;
                }
                // Uses preset settings if id is not null & can be found
                var settings = string.IsNullOrEmpty(presetVoiceID) ? null : TTSService.GetPresetVoiceSettings(presetVoiceID);
                if (settings != null)
                {
                    return settings;
                }
                // Otherwise use custom settings
                return customWitVoiceSettings;
            }
        }

        /// <summary>
        /// Whether a clip is currently playing for this speaker
        /// </summary>
        public bool IsSpeaking => SpeakingClip != null;
        /// <summary>
        /// The data for the currently playing clip
        /// </summary>
        public TTSClipData SpeakingClip => _speakingRequest?.ClipData;

        /// <summary>
        /// Whether there are any clips in the loading queue
        /// </summary>
        public bool IsLoading => _queuedRequests.Count > 0;
        /// <summary>
        /// Whether any queued clips are still not ready for playback
        /// </summary>
        public bool IsPreparing
        {
            get
            {
                foreach (var request in _queuedRequests)
                {
                    if (request.ClipData != null && request.ClipData.loadState == TTSClipLoadState.Preparing)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        // Loading clip queue
        public List<TTSClipData> QueuedClips
        {
            get
            {
                List<TTSClipData> clips = new List<TTSClipData>();
                foreach (var request in _queuedRequests)
                {
                    clips.Add(request.ClipData);
                }
                return clips;
            }
        }

        /// <summary>
        /// Whether the speaker currently has currently speaking clip or a playback queue
        /// </summary>
        public bool IsActive => IsSpeaking || IsLoading;

        // Total elapsed seconds of the playing audio clip.  Used for elapsed sample calculation if needed.
        private float _elapsedPlayTime;

        // Current clip to be played
        private TTSSpeakerRequestData _speakingRequest;
        // Full clip data list
        private ConcurrentQueue<TTSSpeakerRequestData> _queuedRequests = new ConcurrentQueue<TTSSpeakerRequestData>();
        private class TTSSpeakerRequestData
        {
            public TTSClipData ClipData;
            public Action<TTSClipData> OnReady;
            public bool IsReady;
            public DateTime StartTime;
            public bool StopPlaybackOnLoad;
            public TTSSpeakerClipEvents PlaybackEvents;
            public TaskCompletionSource<bool> PlaybackCompletion;
        }

        // Check if queued
        private bool _hasQueue = false;
        private bool _willHaveQueue = false;

        // Text processors
        private ISpeakerTextPreprocessor[] _textPreprocessors;
        private ISpeakerTextPostprocessor[] _textPostprocessors;

        /// <summary>
        /// The script used to perform audio playback of IAudioClipStreams.
        /// 1. Gets IAudioPlayer component if applied to this speaker
        /// 2. If no IAudioPlayer component is found, the TTSService's audio system
        /// will be used to generate an audio player.
        /// 3. If still not found, adds a UnityAudioPlayer.
        /// </summary>
        public IAudioPlayer AudioPlayer
        {
            get
            {
                if (_audioPlayer == null)
                {
                    _audioPlayer = gameObject.GetComponent<IAudioPlayer>();
                    if (_audioPlayer == null)
                    {
                        _audioPlayer = TTSService?.AudioSystem?.GetAudioPlayer(gameObject);
                        if (_audioPlayer == null)
                        {
                            _audioPlayer = gameObject.AddComponent<UnityAudioPlayer>();
                        }
                    }
                }
                return _audioPlayer;
            }
        }
        private IAudioPlayer _audioPlayer;

        // Unity audio source if used by the unity player
        public AudioSource AudioSource
        {
            get
            {
                if (AudioPlayer is IAudioSourceProvider uap)
                {
                    return uap.AudioSource;
                }
                return null;
            }
        }

        #region LIFECYCLE
        // Automatically generate source if needed
        protected virtual void Start()
        {
            // Initialize audio
            AudioPlayer.Init();
        }
        // Stop
        protected virtual void OnDestroy()
        {
            Stop();
            _speakingRequest = null;
            _queuedRequests.Clear();
        }
        // Add listener for clip unload
        protected virtual void OnEnable()
        {
            _isPlaying = Application.isPlaying;
            // Get preprocessors
            if (_textPreprocessors == null)
            {
                _textPreprocessors = GetComponents<ISpeakerTextPreprocessor>();
            }
            // Get postprocessors
            if (_textPostprocessors == null)
            {
                _textPostprocessors = GetComponents<ISpeakerTextPostprocessor>();
            }
            // Fix prepend text to ensure it has a space
            if (!string.IsNullOrEmpty(PrependedText) && PrependedText.Length > 0 && !PrependedText.EndsWith(" "))
            {
                PrependedText = PrependedText + " ";
            }
            // Fix append text to ensure it is spaced correctly
            if (!string.IsNullOrEmpty(AppendedText) && AppendedText.Length > 0 && !AppendedText.StartsWith(" "))
            {
                AppendedText = " " + AppendedText;
            }
            if (TTSService)
            {
                TTSService.Events.OnClipUnloaded.AddListener(HandleClipUnload);
            }
        }
        // Stop speaking & remove listener
        protected virtual void OnDisable()
        {
            Stop();
            if (TTSService)
            {
                TTSService.Events.OnClipUnloaded.RemoveListener(HandleClipUnload);
            }
        }
        // Clip unloaded externally
        protected virtual void HandleClipUnload(TTSClipData clipData)
        {
            Stop(clipData, true);
        }
        // Check queue
        private TTSSpeakerRequestData GetQueuedRequest(TTSClipData clipData)
        {
            if (_queuedRequests != null)
            {
                foreach (var requestData in _queuedRequests)
                {
                    if (string.Equals(clipData?.clipID, requestData.ClipData?.clipID))
                    {
                        return requestData;
                    }
                }
            }
            return null;
        }
        // Check queue
        private bool QueueContainsClip(TTSClipData clipData)
        {
            TTSSpeakerRequestData requestData = GetQueuedRequest(clipData);
            return requestData?.ClipData != null;
        }
        // Refresh queue
        private void RefreshQueueEvents()
        {
            bool newHasQueueStatus = IsActive || _willHaveQueue;
            if (_hasQueue != newHasQueueStatus)
            {
                _hasQueue = newHasQueueStatus;
                if (_hasQueue)
                {
                    RaiseEvents(RaiseOnPlaybackQueueBegin);
                }
                else
                {
                    RaiseEvents(RaiseOnPlaybackQueueComplete);
                }
            }
        }
        // Check if clip request is active
        private bool IsClipRequestActive(TTSSpeakerRequestData requestData)
        {
            return IsClipRequestLoading(requestData) || IsClipRequestSpeaking(requestData);
        }
        // Check if clip request is active
        private bool IsClipRequestLoading(TTSSpeakerRequestData requestData)
        {
            return _queuedRequests.Contains(requestData);
        }
        // Check if clip request is active
        private bool IsClipRequestSpeaking(TTSSpeakerRequestData requestData)
        {
            return _speakingRequest != null && _speakingRequest.Equals(requestData);
        }
        #endregion

        #region Text
        /// <summary>
        /// Gets final text following prepending/appending and any special formatting
        /// </summary>
        /// <param name="textToSpeak">The base text to be spoken</param>
        /// <returns>Returns an array of split texts to be spoken</returns>
        public List<string> GetFinalText(string textToSpeak)
        {
            // Get results
            List<string> phrases = new List<string>();
            phrases.Add(textToSpeak);

            // Pre-processor
            if (_textPreprocessors != null)
            {
                foreach (var preprocessor in _textPreprocessors)
                {
                    preprocessor.OnPreprocessTTS(this, phrases);
                }
            }

            // Add prepend and appended text to each item
            if (!string.IsNullOrEmpty(PrependedText)
                || !string.IsNullOrEmpty(AppendedText))
            {
                for (int i = 0; i < phrases.Count; i++)
                {
                    if (string.IsNullOrEmpty(phrases[i].Trim())) continue;
                    string phrase = phrases[i];
                    phrase = $"{PrependedText}{phrase}{AppendedText}".Trim();
                    phrases[i] = phrase;
                }
            }

            // Post-processors
            if (_textPostprocessors != null)
            {
                foreach (var postprocessor in _textPostprocessors)
                {
                    postprocessor.OnPostprocessTTS(this, phrases);
                }
            }

            // Return success
            return phrases;
        }
        /// <summary>
        /// Obtain final text list from format and text list
        /// </summary>
        /// <param name="format">The format to be used</param>
        /// <param name="textsToSpeak">The array of strings to be inserted into the format</param>
        /// <returns>Returns a list of formatted texts</returns>
        public List<string> GetFinalTextFormatted(string format, params string[] textsToSpeak)
            => GetFinalText(GetFormattedText(format, textsToSpeak));

        /// <summary>
        /// Formats text using an initial format string parameter and additional text items to
        /// be inserted into the format
        /// </summary>
        /// <param name="format">The format to be used</param>
        /// <param name="textsToSpeak">The array of strings to be inserted into the format</param>
        /// <returns>A formatted text string</returns>
        public string GetFormattedText(string format, params string[] textsToSpeak)
        {
            if (textsToSpeak != null && !string.IsNullOrEmpty(format))
            {
                object[] objects = new object[textsToSpeak.Length];
                textsToSpeak.CopyTo(objects, 0);
                return string.Format(format, objects);
            }
            return null;
        }
        #endregion Text

        #region REQUESTS
        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events.
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public void Speak(string textToSpeak, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
            => _ = Load(textToSpeak, null, diskCacheSettings, playbackEvents, true);

        /// <summary>
        /// Load a tts clip using the specified text and playback events.  Cancels all previous clips
        /// when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public void Speak(string textToSpeak, TTSSpeakerClipEvents playbackEvents) =>
            Speak(textToSpeak, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings.  Cancels all previous clips
        /// when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public void Speak(string textToSpeak, TTSDiskCacheSettings diskCacheSettings) =>
            Speak(textToSpeak, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        public void Speak(string textToSpeak) =>
            Speak(textToSpeak, null, null);

        /// <summary>
        /// Loads a formated phrase to be spoken.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="format">Format string to be filled in with texts</param>
        /// <param name="textsToSpeak">Texts to be inserted into the formatter</param>
        public void SpeakFormat(string format, params string[] textsToSpeak) =>
            Speak(GetFormattedText(format, textsToSpeak), null, null);

        #region Speak Coroutine
        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakAsync(string textToSpeak, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
        {
            yield return ThreadUtility.CoroutineAwait(()
                => _ = Load(textToSpeak, null, diskCacheSettings, playbackEvents, true));
        }

        /// <summary>
        /// Load a tts clip using the specified text and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakAsync(string textToSpeak, TTSSpeakerClipEvents playbackEvents)
        {
            yield return SpeakAsync(textToSpeak, null, playbackEvents);
        }

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakAsync(string textToSpeak, TTSDiskCacheSettings diskCacheSettings)
        {
            yield return SpeakAsync(textToSpeak, diskCacheSettings, null);
        }

        /// <summary>
        /// Load a tts clip using the specified text and then waits for the file to load and play.
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        public IEnumerator SpeakAsync(string textToSpeak)
        {
            yield return SpeakAsync(textToSpeak, null, null);
        }
        #endregion

        #region Speak async/await
        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakTask(string textToSpeak, TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents)
            => Load(textToSpeak, null, diskCacheSettings, playbackEvents, true);

        /// <summary>
        /// Load a tts clip using the specified text and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakTask(string textToSpeak, TTSSpeakerClipEvents playbackEvents)
            => SpeakTask(textToSpeak, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public Task SpeakTask(string textToSpeak, TTSDiskCacheSettings diskCacheSettings)
            => SpeakTask(textToSpeak, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text and then waits for the file to load and play.
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        public Task SpeakTask(string textToSpeak)
            => SpeakTask(textToSpeak, null, null);

        /// <summary>
        /// Load a tts clip using the specified response node and then waits for the file to load and play.
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public Task SpeakTask(WitResponseNode responseNode,
            TTSSpeakerClipEvents playbackEvents)
            => Load(responseNode, null, playbackEvents, true);
        #endregion

        #region Speak Queued Sync
        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public void SpeakQueued(string textToSpeak, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
            => _ = Load(textToSpeak, null, diskCacheSettings, playbackEvents, false);

        /// <summary>
        /// Load a tts clip using the specified text and playback events.  Adds clip to playback queue and will
        /// speak once queue has completed all playback.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public void SpeakQueued(string textToSpeak, TTSSpeakerClipEvents playbackEvents) =>
            SpeakQueued(textToSpeak, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings events.  Adds clip
        /// to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public void SpeakQueued(string textToSpeak, TTSDiskCacheSettings diskCacheSettings) =>
            SpeakQueued(textToSpeak, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text.  Adds clip to playback queue and will speak
        /// once queue has completed all playback.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        public void SpeakQueued(string textToSpeak) =>
            SpeakQueued(textToSpeak, null, null);

        /// <summary>
        /// Loads a formated phrase to be spoken.  Adds clip to playback queue and will speak
        /// once queue has completed all playback.
        /// </summary>
        /// <param name="format">Format string to be filled in with texts</param>
        /// <param name="textsToSpeak">Texts to be inserted into the formatter</param>
        public void SpeakFormatQueued(string format, params string[] textsToSpeak) =>
            SpeakQueued(GetFormattedText(format, textsToSpeak), null, null);
        #endregion

        #region Speak Queued Coroutine
        /// <summary>
        /// Load a tts clip using the specified text phrases, disk cache settings and playback events and then
        /// waits for the files to load and play.  Adds clip to playback queue and will speak once queue has
        /// completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
        {
            yield return ThreadUtility.CoroutineAwait(
                () => Load(textsToSpeak, null, diskCacheSettings, playbackEvents, false));
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSSpeakerClipEvents playbackEvents)
        {
            yield return SpeakQueuedAsync(textsToSpeak, null, playbackEvents);
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and disk cache settings and then waits for the files to
        /// load and play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSDiskCacheSettings diskCacheSettings)
        {
            yield return SpeakQueuedAsync(textsToSpeak, diskCacheSettings, null);
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and then waits for the files to load and play.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak)
        {
            yield return SpeakQueuedAsync(textsToSpeak, null, null);
        }
        #endregion

        #region Speak Queued Async/Await
        /// <summary>
        /// Load a tts clip using the specified text phrases, disk cache settings and playback events and then
        /// waits for the files to load and play.  Adds clip to playback queue and will speak once queue has
        /// completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakQueuedTask(string[] textsToSpeak, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
            => Load(textsToSpeak, null, diskCacheSettings, playbackEvents, false);

        /// <summary>
        /// Load a tts clip using the specified text phrases and playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakQueuedTask(string[] textsToSpeak, TTSSpeakerClipEvents playbackEvents)
            => SpeakQueuedTask(textsToSpeak, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases and disk cache settings and then waits for the files to
        /// load and play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public Task SpeakQueuedTask(string[] textsToSpeak, TTSDiskCacheSettings diskCacheSettings)
            => SpeakQueuedTask(textsToSpeak, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text phrases and then waits for the files to load and play.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        public Task SpeakQueuedTask(string[] textsToSpeak)
            => SpeakQueuedTask(textsToSpeak, null, null);
        #endregion
        #endregion

        #region Voice
        /// <summary>
        /// Set a voice override for future requests
        /// </summary>
        /// <param name="overrideVoiceSettings">The settings to be applied to upcoming requests</param>
        public void SetVoiceOverride(TTSVoiceSettings overrideVoiceSettings)
        {
            _overrideVoiceSettings = overrideVoiceSettings;
        }

        /// <summary>
        /// Clears the current voice override
        /// </summary>
        public void ClearVoiceOverride() => SetVoiceOverride(null);

        /// <summary>
        /// Load a tts clip using the specified response node, disk cache settings and playback events.
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool Speak(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents)
        {
            // Decode text to speak and voice settings
            if (!TTSService.DecodeTts(responseNode, out var textToSpeak, out var voiceSettings))
            {
                return false;
            }
            _ = Load(textToSpeak, voiceSettings, diskCacheSettings, playbackEvents, true);
            return true;
        }

        /// <summary>
        /// Load a tts clip using the specified response node and disk cache settings
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool Speak(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings) =>
            Speak(responseNode, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified response node and playback events
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool Speak(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
            => Speak(responseNode, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified response node and playback events
        /// Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool Speak(WitResponseNode responseNode)
            => Speak(responseNode, null, null);

        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakAsync(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
        {
            // Decode text to speak and voice settings
            if (!TTSService.DecodeTts(responseNode, out var textToSpeak, out var voiceSettings))
            {
                yield break;
            }

            // Wait while loading/speaking
            yield return ThreadUtility.CoroutineAwait(
                () => Load(textToSpeak, voiceSettings, diskCacheSettings, playbackEvents, true));
        }

        /// <summary>
        /// Load a tts clip using the specified text and playback events and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakAsync(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
        {
            yield return SpeakAsync(responseNode, null, playbackEvents);
        }

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings and then waits
        /// for the file to load and play.  Cancels all previous clips when loaded and then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakAsync(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings)
        {
            yield return SpeakAsync(responseNode, diskCacheSettings, null);
        }

        /// <summary>
        /// Load a tts clip using the specified text, disk cache settings and playback events.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool SpeakQueued(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents)
        {
            // Decode text to speak and voice settings
            if (!TTSService.DecodeTts(responseNode, out var textToSpeak, out var voiceSettings))
            {
                return false;
            }
            // Speak queued
            _ = Load(textToSpeak, voiceSettings, diskCacheSettings, playbackEvents, false);
            return true;
        }

        /// <summary>
        /// Load a tts clip using the specified text and playback events.  Adds clip to playback queue and will
        /// speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool SpeakQueued(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents) =>
            SpeakQueued(responseNode, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text and disk cache settings events.  Adds clip
        /// to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool SpeakQueued(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings) =>
            SpeakQueued(responseNode, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text.  Adds clip to playback queue and will speak
        /// once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        public bool SpeakQueued(WitResponseNode responseNode) =>
            SpeakQueued(responseNode, null, null);

        /// <summary>
        /// Load a tts clip using the specified text phrases, disk cache settings and playback events and then
        /// waits for the files to load and play.  Adds clip to playback queue and will speak once queue has
        /// completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakQueuedAsync(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings, TTSSpeakerClipEvents playbackEvents)
        {
            // Decode text to speak and voice settings
            if (!TTSService.DecodeTts(responseNode, out var textToSpeak, out var voiceSettings))
            {
                yield break;
            }
            // Wait while loading/speaking
            yield return ThreadUtility.CoroutineAwait(
                () => Load(textToSpeak, voiceSettings, diskCacheSettings, playbackEvents, false));
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public IEnumerator SpeakQueuedAsync(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
        {
            yield return SpeakQueuedAsync(responseNode, null, playbackEvents);
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and disk cache settings and then waits for the files to
        /// load and play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakQueuedAsync(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings)
        {
            yield return SpeakQueuedAsync(responseNode, diskCacheSettings, null);
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases and then waits for the files to load and play.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        public IEnumerator SpeakQueuedAsync(WitResponseNode responseNode)
        {
            yield return SpeakQueuedAsync(responseNode, null, null);
        }

        /// <summary>
        /// Load a tts clip using the specified text phrases, disk cache settings and playback events and then
        /// waits for the files to load and play.  Adds clip to playback queue and will speak once queue has
        /// completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakQueuedTask(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents)
            => Load(responseNode, diskCacheSettings, playbackEvents, false);

        /// <summary>
        /// Load a tts clip using the specified text phrases and playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        public Task SpeakQueuedTask(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents)
            => SpeakQueuedTask(responseNode, null, playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases and disk cache settings and then waits for the files to
        /// load and play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public Task SpeakQueuedTask(WitResponseNode responseNode, TTSDiskCacheSettings diskCacheSettings)
            => SpeakQueuedTask(responseNode, diskCacheSettings, null);

        /// <summary>
        /// Load a tts clip using the specified text phrases and then waits for the files to load and play.
        /// Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        public Task SpeakQueuedTask(WitResponseNode responseNode)
            => SpeakQueuedTask(responseNode, null, null);
        #endregion Voice

        #region Stop
        /// <summary>
        /// Stop load and playback of a specific clip
        /// </summary>
        /// <param name="textToSpeak">Stop a specific text phrase</param>
        /// <param name="allInstances">Whether to remove the first instance of this clip or all instances</param>
        public virtual void Stop(string textToSpeak, bool allInstances = false)
        {
            // Found speaking clip
            if (string.Equals(SpeakingClip?.textToSpeak, textToSpeak))
            {
                Stop(SpeakingClip, allInstances);
                return;
            }

            // Find all clips that match and stop them
            foreach (var clipData in QueuedClips)
            {
                if (string.Equals(clipData?.textToSpeak, textToSpeak))
                {
                    Stop(clipData, allInstances);
                    if (!allInstances)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Stop load and playback of a specific clip
        /// </summary>
        /// <param name="clipData">The clip to be stopped and removed from the queue</param>
        /// <param name="allInstances">Whether to remove the first instance of this clip or all instances</param>
        public virtual void Stop(TTSClipData clipData, bool allInstances = false)
        {
            // Check if speaking
            bool isSpeakingClip = SpeakingClip != null && clipData.Equals(SpeakingClip);

            // Cancel queue
            if (!isSpeakingClip || allInstances)
            {
                // Unload all instances
                if (allInstances)
                {
                    if (QueueContainsClip(clipData))
                    {
                        HandleUnload(clipData, string.Empty);
                    }
                }
                // Unload a single request
                else
                {
                    HandleUnload(GetQueuedRequest(clipData), string.Empty);
                }
            }

            // Cancel playback
            if (isSpeakingClip)
            {
                StopSpeaking();
            }
        }

        /// <summary>
        /// Abort loading of all items in the load queue
        /// </summary>
        public virtual void StopLoading()
        {
            // Ignore if not loading
            if (!IsLoading)
            {
                return;
            }

            // Cancel each clip from loading
            while (_queuedRequests.TryDequeue(out var request))
            {
                RaiseEvents(RaiseOnLoadAborted, request);
            }

            // Refresh in queue check
            RefreshQueueEvents();
        }

        /// <summary>
        /// Stop playback of currently played audio clip
        /// </summary>
        public virtual void StopSpeaking()
        {
            // Cannot stop speaking when not currently speaking
            if (!IsSpeaking)
            {
                return;
            }

            // Cancel playback
            HandlePlaybackComplete(true);
        }

        /// <summary>
        /// Stops loading and playback immediately
        /// </summary>
        public virtual void Stop()
        {
            StopLoading();
            StopSpeaking();
        }
        #endregion Stop

        #region Load
        /// <summary>
        /// Decode a response node into text to be spoken or a specific voice setting
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="textToSpeak">The text to be spoken output</param>
        /// <param name="voiceSettings">The output for voice settings</param>
        /// <returns>True if decode was successful</returns>
        private bool DecodeTts(WitResponseNode responseNode,
            out string textToSpeak,
            out TTSVoiceSettings voiceSettings)
            => TTSService.DecodeTts(responseNode, out textToSpeak, out voiceSettings);

        /// <summary>
        /// A method that generates and enqueues a request
        /// </summary>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <param name="clearQueue">If true, queue is cleared prior to load.  Otherwise, clip is queued as expected.</param>
        /// <returns>Generated tts clip request tracker</returns>
        private TTSSpeakerRequestData CreateRequest(TTSSpeakerClipEvents playbackEvents, bool clearQueue)
        {
            TTSSpeakerRequestData requestData = new TTSSpeakerRequestData();
            requestData.OnReady = (clip) => TryPlayLoadedClip(requestData);
            requestData.IsReady = false;
            requestData.StartTime = DateTime.UtcNow;
            requestData.PlaybackCompletion = new TaskCompletionSource<bool>();
            requestData.PlaybackEvents = playbackEvents ?? new TTSSpeakerClipEvents();
            requestData.StopPlaybackOnLoad = clearQueue;
            _queuedRequests.Enqueue(requestData);
            return requestData;
        }

        /// <summary>
        /// Loads tts clips and handles playback from a single text input
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <param name="clearQueue">If true, queue is cleared prior to load.  Otherwise, clip is queued as expected.</param>
        /// <returns>Returns load errors if applicable</returns>
        private async Task<string> Load(WitResponseNode responseNode,
            TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents,
            bool clearQueue)
        {
            // Decode voice settings async
            string textToSpeak = null;
            TTSVoiceSettings voiceSettings = null;
            await ThreadUtility.BackgroundAsync(Logger, () =>
            {
                DecodeTts(responseNode, out textToSpeak, out voiceSettings);
                return Task.FromResult(true);
            });

            // Perform speech with custom voice settings
            return await Load(textToSpeak, voiceSettings, diskCacheSettings, playbackEvents, clearQueue);
        }

        /// <summary>
        /// Loads tts clips and handles playback from a single text input
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="voiceSettings">The voice settings to be used for the request</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <param name="clearQueue">If true, queue is cleared prior to load.  Otherwise, clip is queued as expected.</param>
        /// <returns>Returns load errors if applicable</returns>
        private async Task<string> Load(string textToSpeak,
            TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents,
            bool clearQueue) =>
            await Load(new [] { textToSpeak }, voiceSettings, diskCacheSettings, playbackEvents, clearQueue);

        /// <summary>
        /// Loads one or more tts clips, plays them and returns when complete
        /// </summary>
        /// <param name="textsToSpeak">The texts to be spoken</param>
        /// <param name="voiceSettings">The voice settings to be used for the request</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <param name="clearQueue">If true, queue is cleared prior to load.  Otherwise, clip is queued as expected.</param>
        /// <returns>Returns load errors if applicable</returns>
        private async Task<string> Load(string[] textsToSpeak,
            TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings,
            TTSSpeakerClipEvents playbackEvents,
            bool clearQueue)
        {
            // Ensure voice settings exist
            voiceSettings ??= VoiceSettings;
            if (voiceSettings == null)
            {
                var error = "No voice provided";
                Logger.Error("{0}\nPreset: {1}", error, presetVoiceID);
                return error;
            }

            // Get final text phrases to be spoken
            List<string> phrases = new List<string>();
            foreach (var textToSpeak in textsToSpeak)
            {
                var newPhrases = GetFinalText(textToSpeak);
                if (newPhrases != null && newPhrases.Count > 0)
                {
                    phrases.AddRange(newPhrases);
                }
            }
            if (phrases == null || phrases.Count == 0)
            {
                var error = "No phrases provided";
                Logger.Error(error);
                return error;
            }

            // Cancel previous loading queue
            if (clearQueue)
            {
                _willHaveQueue = true;
                StopLoading();
                _willHaveQueue = false;
            }

            // Iterate voices
            var requests = new TTSSpeakerRequestData[phrases.Count];
            var tasks = new Task[phrases.Count];
            for (int i = 0; i < phrases.Count; i++)
            {
                // Generate request data
                var requestData = CreateRequest(playbackEvents, clearQueue);

                // Track requests and playback completion
                requests[i] = requestData;
                tasks[i] = requestData.PlaybackCompletion.Task;

                // Get & set clip data
                var clipData = TTSService.GetClipData(phrases[i], voiceSettings, diskCacheSettings);
                requestData.ClipData = clipData;
                RaiseEvents(RaiseOnBegin, requestData);
                RaiseEvents(RaiseOnLoadBegin, requestData);
                RefreshQueueEvents();

                // Load clip async
                _ = LoadClip(requestData);
                clearQueue = false;
            }

            // Await all tasks
            await Task.WhenAll(tasks);

            // Add errors
            var errors = string.Empty;
            for (int i = 0; i < requests.Length; i++)
            {
                var clipData = requests[i]?.ClipData;
                var tts = clipData?.textToSpeak;
                var loadError = clipData?.LoadError;
                if (!string.IsNullOrEmpty(loadError)
                    && !string.Equals(WitConstants.CANCEL_ERROR, loadError))
                {
                    errors += $"\nLoad Error: {loadError}\n\tText: {tts}";
                }
            }
            if (!string.IsNullOrEmpty(errors))
            {
                errors = "TTSSpeaker Load Errors" + errors;
            }

            // Return errors if applicable
            return errors;
        }

        /// <summary>
        /// Loads audio async and returns once complete
        /// </summary>
        private async Task LoadClip(TTSSpeakerRequestData requestData)
        {
            // Load
            var errors = await TTSService.LoadAsync(requestData.ClipData, requestData.OnReady);

            // Call errors if needed
            FinalizeLoadedClip(requestData, errors);
        }

        /// <summary>
        /// When clip is ready for playback
        /// </summary>
        private void TryPlayLoadedClip(TTSSpeakerRequestData requestData)
        {
            // Ignore if already called
            if (requestData.IsReady)
            {
                return;
            }

            // Playback is ready
            requestData.IsReady = true;
            RaiseEvents(RaiseOnPlaybackReady, requestData);

            // Stop previously spoken clip and play next
            if (requestData.StopPlaybackOnLoad && IsSpeaking)
            {
                StopSpeaking();
            }
            // Attempt to play next in queue
            else
            {
                RefreshPlayback();
            }
        }
        /// <summary>
        /// On clip load completion, check for additional errors
        /// </summary>
        private void FinalizeLoadedClip(TTSSpeakerRequestData requestData, string error)
        {
            // Check for other errors
            if (string.IsNullOrEmpty(error)
                && (requestData.ClipData == null
                    || !string.IsNullOrEmpty(requestData.ClipData.textToSpeak)))
            {
                if (requestData.ClipData == null)
                {
                    error = "No TTSClipData found";
                }
                else if (requestData.ClipData.clipStream == null)
                {
                    error = "No AudioClip found";
                }
                else if (requestData.ClipData.loadState == TTSClipLoadState.Error)
                {
                    error = "Error without message";
                }
                else if (requestData.ClipData.loadState == TTSClipLoadState.Unloaded)
                {
                    error = WitConstants.CANCEL_ERROR;
                }
            }
            // Unload request
            if (!string.IsNullOrEmpty(error))
            {
                HandleUnload(requestData, error);
                return;
            }

            // If ready has not yet called, do so now
            if (!requestData.IsReady)
            {
                TryPlayLoadedClip(requestData);
            }
        }
        #endregion Load

        #region Playback
        // Wait for playback completion
        private Coroutine _waitForCompletion;
        private bool _isPlaying;

        /// <summary>
        /// Refreshes playback queue to play next available clip if possible
        /// </summary>
        private void RefreshPlayback()
        {
            // Ignore if currently playing or nothing in uque
            if (SpeakingClip != null ||  _queuedRequests == null || _queuedRequests.Count == 0 || _audioPlayer == null)
            {
                return;
            }
            // No audio source
            string errors = AudioPlayer.GetPlaybackErrors();
            if (!string.IsNullOrEmpty(errors))
            {
                Logger.Error($"Refresh Playback Failed\nError: {errors}");
                return;
            }
            // Check if queued request is done
            if (!_queuedRequests.TryPeek(out var requestData)
                || requestData.ClipData == null
                || requestData.ClipData.loadState != TTSClipLoadState.Loaded)
            {
                Logger.Verbose("Refresh Playback Too Soon");
                return;
            }

            // Dequeue, set request and call delegates
            _queuedRequests.TryDequeue(out requestData);
            _speakingRequest = requestData;
            RaiseEvents(RaiseOnPlaybackBegin, _speakingRequest);

            // Add playback event callbacks
            if (_speakingRequest.ClipData.Events != null)
            {
                _speakingRequest.ClipData.Events.OnEventsUpdated += RaisePlaybackEventsUpdated;
                RaisePlaybackEventsUpdated(requestData.ClipData.Events);
            }

            // Resume prior to playback
            if (requestData.StopPlaybackOnLoad && IsPaused)
            {
                Resume();
            }

            // If we're sending an empty string we're really just potentially queuing an event so we can trigger it
            // between audio clips. Trigger start/stop events.
            if (string.IsNullOrEmpty(requestData.ClipData.textToSpeak))
            {
                HandlePlaybackComplete(false);
                return;
            }

            // Somehow clip unloaded
            if (requestData.ClipData.clipStream == null)
            {
                HandlePlaybackComplete(true);
                return;
            }

            // Start audio player speaking
            _ = ThreadUtility.CallOnMainThread(
                () => AudioPlayer.Play(_speakingRequest.ClipData.clipStream, 0));

            // TODO: Move async
            // Wait for completion
            if (_waitForCompletion != null)
            {
                StopCoroutine(_waitForCompletion);
                _waitForCompletion = null;
            }
            _waitForCompletion = StartCoroutine(WaitForPlaybackComplete());
        }
        // Wait for clip completion
        private IEnumerator WaitForPlaybackComplete()
        {
            // Use delta time to wait for completion
            int sample = -1;
            _elapsedPlayTime = 0f;
            while (!IsPlaybackComplete())
            {
                // Wait a frame
                yield return new WaitForEndOfFrame();

                // Append delta time for elapsed play time
                if (!IsPaused)
                {
                    _elapsedPlayTime += Time.deltaTime;
                }

                // Fix audio source, paused/resumed externally
                bool playerPaused = !AudioPlayer.IsPlaying;
                if (IsPaused != playerPaused)
                {
                    if (IsPaused)
                    {
                        AudioPlayer.Pause();
                    }
                    else
                    {
                        AudioPlayer.Resume();
                    }
                }

                // Update current sample if needed
                var newSample = ElapsedSamples;
                if (sample != newSample)
                {
                    sample = newSample;
                    RaisePlaybackSampleUpdated(sample);
                }
            }

            // Playback completed
            HandlePlaybackComplete(false);
        }
        // Check for playback completion
        protected virtual bool IsPlaybackComplete()
        {
            // No longer playing if audio player stopped
            if (!AudioPlayer.IsPlaying && !IsPaused)
            {
                return true;
            }
            // No longer playing if clip stream is missing
            if (AudioPlayer?.ClipStream == null)
            {
                return true;
            }
            // Complete if stream is complete (total samples are set) and current sample is final
            return AudioPlayer.ClipStream.IsComplete && ElapsedSamples >= TotalSamples;
        }
        // Completed playback
        protected virtual void HandlePlaybackComplete(bool stopped)
        {
            // Stop playback handler
            if (_waitForCompletion != null)
            {
                StopCoroutine(_waitForCompletion);
                _waitForCompletion = null;
            }

            // Keep last request data
            TTSSpeakerRequestData lastRequestData = _speakingRequest;
            // Clear speaking request
            _speakingRequest = null;

            // Remove playback event callbacks
            if (lastRequestData.ClipData.Events != null)
            {
                lastRequestData.ClipData.Events.OnEventsUpdated -= RaisePlaybackEventsUpdated;
            }
            RaisePlaybackSampleUpdated(0);
            RaisePlaybackEventsUpdated(null);

            // Stop audio source playback
            _ = ThreadUtility.CallOnMainThread(() => AudioPlayer.Stop());

            // Stopped
            if (stopped)
            {
                RaiseEvents(RaiseOnPlaybackCancelled, lastRequestData, "Playback stopped manually");
            }
            // Clip unloaded
            else if (lastRequestData.ClipData.loadState == TTSClipLoadState.Unloaded)
            {
                RaiseEvents(RaiseOnPlaybackCancelled, lastRequestData, "TTSClipData was unloaded");
            }
            // Clip destroyed
            else if (lastRequestData.ClipData.clipStream == null)
            {
                RaiseEvents(RaiseOnPlaybackCancelled, lastRequestData, "AudioClip no longer exists");
            }
            // Success
            else
            {
                RaiseEvents(RaiseOnPlaybackComplete, lastRequestData);
            }

            // Refresh in queue check
            RefreshQueueEvents();

            // Attempt to play next in queue if all playback was not just stopped
            RefreshPlayback();
        }

        /// <summary>
        /// Whether playback is currently paused or not
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Pause any current or future loaded audio playback
        /// </summary>
        public void Pause() => SetPause(true);

        /// <summary>
        /// Resume playback for current and future audio clips
        /// </summary>
        public void Resume() => SetPause(false);

        /// <summary>
        /// Call before sending speech data to warm up TTS system
        /// </summary>
        public void PrepareToSpeak()
        {
            // Not currently needed
        }

        /// <summary>
        /// Call at the start of a larger text block to indicate many queued requests will be coming
        /// </summary>
        public void StartTextBlock()
        {
            // Not currently needed
        }

        /// <summary>
        /// Call at the end of a larger text block to indicate a block of text is complete.
        /// </summary>
        public void EndTextBlock()
        {
            // Not currently needed
        }

        // Set's the current pause state
        protected virtual void SetPause(bool toPaused)
        {
            // Ignore if same
            if (IsPaused == toPaused)
            {
                return;
            }

            // Apply
            IsPaused = toPaused;
            Log($"Speak Audio {(IsPaused ? "Paused" : "Resumed")}");

            // Adjust if speaking
            if (IsSpeaking)
            {
                if (IsPaused)
                {
                    AudioPlayer.Pause();
                }
                else if (!IsPaused)
                {
                    AudioPlayer.Resume();
                }
            }
        }
        #endregion Playback

        #region Unload
        // Handles unload of all requests using a specific clip
        private void HandleUnload(TTSClipData clipData, string error)
        {
            HandleUnload((checkRequest) => !string.Equals(checkRequest.ClipData.clipID, clipData?.clipID), error);
        }
        // Handles unload of specific request
        private void HandleUnload(TTSSpeakerRequestData requestData, string error)
        {
            if (!_queuedRequests.Contains(requestData))
            {
                return;
            }
            if (_queuedRequests.TryPeek(out var discard)
                && requestData.Equals(discard))
            {
                _queuedRequests.TryDequeue(out discard);
                RefreshQueueEvents();
                return;
            }
            HandleUnload((checkRequest) => !checkRequest.Equals(requestData), error);
        }
        // Handles unload of requests with specified should keep lookup
        private void HandleUnload(Func<TTSSpeakerRequestData, bool> shouldKeep, string error)
        {
            // Ignore if destroyed
            if (_queuedRequests == null)
            {
                return;
            }

            // Otherwise create discard queue
            ConcurrentQueue<TTSSpeakerRequestData> discard = _queuedRequests;
            _queuedRequests = new ConcurrentQueue<TTSSpeakerRequestData>();

            // Iterate all items
            while (discard.Count > 0)
            {
                // Dequeue from discard
                if (!discard.TryDequeue(out var check))
                {
                    continue;
                }
                // Clip data missing
                if (check.ClipData == null)
                {
                    RaiseEvents(RaiseOnLoadFailed, check, "TTSClipData missing");
                }
                // Do not keep
                else if (shouldKeep != null && !shouldKeep(check))
                {
                    // Cancelled
                    if (string.IsNullOrEmpty(error) || string.Equals(error, WitConstants.CANCEL_ERROR))
                    {
                        RaiseEvents(RaiseOnLoadAborted, check);
                    }
                    // Failure
                    else
                    {
                        RaiseEvents(RaiseOnLoadFailed, check, error);
                    }
                }
                // Keep all others
                else
                {
                    _queuedRequests.Enqueue(check);
                }
            }

            // Refresh in queue check
            RefreshQueueEvents();
        }
        // Unloads
        private void UnloadClip(TTSSpeakerRequestData requestData, string error)
        {
            // Playback cancelled
            if (requestData.Equals(_speakingRequest))
            {
                RaiseEvents(RaiseOnPlaybackCancelled, requestData, error);
            }
            // Load Cancelled
            else if (string.IsNullOrEmpty(error) || string.Equals(error, WitConstants.CANCEL_ERROR))
            {
                RaiseEvents(RaiseOnLoadAborted, requestData);
            }
            // Load Failure
            else
            {
                RaiseEvents(RaiseOnLoadFailed, requestData, error);
            }
        }
        #endregion Unload

        #region Logging
        private void Log(string format, params object[] parameters)
        {
            if (verboseLogging) Logger.Verbose(format, parameters);
        }
        private void Error(string format, params object[] parameters)
            => Logger.Warning(format, parameters);

        private void LogRequest(string comment, TTSSpeakerRequestData requestData, string error = null)
        {
            if (!verboseLogging && string.IsNullOrEmpty(error))
            {
                return;
            }
            const string format = "{0}\n{1}\nElapsed: {2:0.00} seconds\nAudio Player Type: {3}";
            if (!string.IsNullOrEmpty(error))
            {
                Error(format + "\nError: {4}",
                    comment,
                    requestData.ClipData,
                    (DateTime.UtcNow - requestData.StartTime).TotalSeconds,
                    _audioPlayer?.GetType().Name ?? "Null",
                    error);
            }
            else
            {
                Log(format,
                    comment,
                    requestData.ClipData,
                    (DateTime.UtcNow - requestData.StartTime).TotalSeconds,
                    _audioPlayer?.GetType().Name ?? "Null");
            }
        }
        #endregion Logging

        #region Callbacks
        // Call events via main thread
        private void RaiseEvents(Action events)
        {
            _ = ThreadUtility.CallOnMainThread(events.Invoke);
        }
        private void RaiseEvents<T>(Action<T> events, T parameter)
        {
            RaiseEvents(() => events.Invoke(parameter));
        }
        private void RaiseEvents<T1, T2>(Action<T1, T2> events, T1 parameter1, T2 parameter2)
        {
            RaiseEvents(() => events.Invoke(parameter1, parameter2));
        }
        // Perform start of playback queue
        protected virtual void RaiseOnPlaybackQueueBegin()
        {
            Log("Playback Queue Begin");
            Events?.OnPlaybackQueueBegin?.Invoke();
        }
        // Perform end of playback queue
        protected virtual void RaiseOnPlaybackQueueComplete()
        {
            Log("Playback Queue Complete");
            Events?.OnPlaybackQueueComplete?.Invoke();
        }
        // Initial callback as soon as the audio clip speak request is generated
        private void RaiseOnBegin(TTSSpeakerRequestData requestData)
        {
            LogRequest("Speak Begin", requestData);
            Events?.OnInit?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnInit?.Invoke(this, requestData.ClipData);
        }
        // Perform load begin events
        private void RaiseOnLoadBegin(TTSSpeakerRequestData requestData)
        {
            LogRequest("Load Begin", requestData);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataQueued?.Invoke(requestData.ClipData);
            Events?.OnClipDataLoadBegin?.Invoke(requestData.ClipData);
            Events?.OnClipLoadBegin?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            Events?.OnLoadBegin?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnLoadBegin?.Invoke(this, requestData.ClipData);
        }
        // Perform load begin abort events
        private void RaiseOnLoadAborted(TTSSpeakerRequestData requestData)
        {
            LogRequest("Load Aborted", requestData);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataLoadAbort?.Invoke(requestData.ClipData);
            Events?.OnClipLoadAbort?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            Events?.OnLoadAbort?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnLoadAbort?.Invoke(this, requestData.ClipData);

            // Complete
            RaiseOnComplete(requestData);
        }
        // Perform load failed events
        private void RaiseOnLoadFailed(TTSSpeakerRequestData requestData, string error)
        {
            if (string.Equals(error, WitConstants.CANCEL_ERROR))
            {
                RaiseOnLoadAborted(requestData);
                return;
            }

            LogRequest($"Load Failed", requestData, error);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataLoadFailed?.Invoke(requestData.ClipData);
            Events?.OnClipLoadFailed?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            Events?.OnLoadFailed?.Invoke(this, requestData.ClipData, error);
            requestData.PlaybackEvents?.OnLoadFailed?.Invoke(this, requestData.ClipData, error);

            // Complete
            RaiseOnComplete(requestData);
        }
        // Perform events for playback being ready
        private void RaiseOnPlaybackReady(TTSSpeakerRequestData requestData)
        {
            LogRequest("Playback Ready", requestData);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataLoadSuccess?.Invoke(requestData.ClipData);
            Events?.OnClipLoadSuccess?.Invoke(this, requestData.ClipData?.textToSpeak);
            Events?.OnClipDataPlaybackReady?.Invoke(requestData.ClipData);
#pragma warning restore CS0618

            // Speaker clip events
            Events?.OnLoadSuccess?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnLoadSuccess?.Invoke(this, requestData.ClipData);

            // Speaker playback events
            Events?.OnAudioClipPlaybackReady?.Invoke(requestData.ClipData?.clip);
            requestData.PlaybackEvents?.OnAudioClipPlaybackReady?.Invoke(requestData.ClipData?.clip);

            // Speaker clip events
            requestData.ClipData?.onPlaybackQueued?.Invoke(requestData.ClipData);
            Events?.OnPlaybackReady?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnPlaybackReady?.Invoke(this, requestData.ClipData);
        }
        // Perform events for playback start
        private void RaiseOnPlaybackBegin(TTSSpeakerRequestData requestData)
        {
            LogRequest("Playback Begin", requestData);

            // Speaker playback events
            Events?.OnTextPlaybackStart?.Invoke(requestData.ClipData?.textToSpeak);
            requestData.PlaybackEvents?.OnTextPlaybackStart?.Invoke(requestData.ClipData?.textToSpeak);
            Events?.OnAudioClipPlaybackStart?.Invoke(requestData.ClipData?.clip);
            requestData.PlaybackEvents?.OnAudioClipPlaybackStart?.Invoke(requestData.ClipData?.clip);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataPlaybackStart?.Invoke(requestData.ClipData);
            Events?.OnStartSpeaking?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            requestData.ClipData?.onPlaybackBegin?.Invoke(requestData.ClipData);
            Events?.OnPlaybackStart?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnPlaybackStart?.Invoke(this, requestData.ClipData);
        }
        // Perform events for playback cancelation
        private void RaiseOnPlaybackCancelled(TTSSpeakerRequestData requestData, string reason)
        {
            LogRequest($"Playback Cancelled\nReason: {reason}", requestData);

            // Speaker playback events
            Events?.OnTextPlaybackCancelled?.Invoke(requestData.ClipData?.textToSpeak);
            requestData.PlaybackEvents?.OnTextPlaybackCancelled?.Invoke(requestData.ClipData?.textToSpeak);
            Events?.OnAudioClipPlaybackCancelled?.Invoke(requestData.ClipData?.clip);
            requestData.PlaybackEvents?.OnAudioClipPlaybackCancelled?.Invoke(requestData.ClipData?.clip);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataPlaybackCancelled?.Invoke(requestData.ClipData);
            Events?.OnCancelledSpeaking?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            requestData.ClipData?.onPlaybackComplete?.Invoke(requestData.ClipData);
            Events?.OnPlaybackCancelled?.Invoke(this, requestData.ClipData, reason);
            requestData.PlaybackEvents?.OnPlaybackCancelled?.Invoke(this, requestData.ClipData, reason);

            // Complete
            RaiseOnComplete(requestData);
        }
        // Perform events for playback completion
        private void RaiseOnPlaybackComplete(TTSSpeakerRequestData requestData)
        {
            LogRequest("Playback Complete", requestData);

            // Speaker playback events
            Events?.OnTextPlaybackFinished?.Invoke(requestData.ClipData?.textToSpeak);
            requestData.PlaybackEvents?.OnTextPlaybackFinished?.Invoke(requestData.ClipData?.textToSpeak);
            Events?.OnAudioClipPlaybackFinished?.Invoke(requestData.ClipData?.clip);
            requestData.PlaybackEvents?.OnAudioClipPlaybackFinished?.Invoke(requestData.ClipData?.clip);

            // Deprecated speaker events
#pragma warning disable CS0618
            Events?.OnClipDataPlaybackFinished?.Invoke(requestData.ClipData);
            Events?.OnFinishedSpeaking?.Invoke(this, requestData.ClipData?.textToSpeak);
#pragma warning restore CS0618

            // Speaker clip events
            requestData.ClipData?.onPlaybackComplete?.Invoke(requestData.ClipData);
            Events?.OnPlaybackComplete?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnPlaybackComplete?.Invoke(this, requestData.ClipData);

            // Complete
            RaiseOnComplete(requestData);
        }
        // Final call for a 'Speak' request that is called following a load failure, load abort, playback cancellation or playback completion
        private void RaiseOnComplete(TTSSpeakerRequestData requestData)
        {
            LogRequest("Speak Complete", requestData);
            Events?.OnComplete?.Invoke(this, requestData.ClipData);
            requestData.PlaybackEvents?.OnComplete?.Invoke(this, requestData.ClipData);
            requestData.PlaybackCompletion?.TrySetResult(true);
        }
        #endregion Callbacks

        #region ITTSEventPlayer
        /// <summary>
        /// The current amount of elapsed samples of the playing audio data if applicable
        /// </summary>
        public int ElapsedSamples
        {
            get
            {
                // Not speaking
                if (!IsSpeaking || _audioPlayer?.ClipStream == null)
                {
                    return 0;
                }
                // Ensure elapsed samples can be determined
                if (_audioPlayer.CanSetElapsedSamples)
                {
                    return _audioPlayer.ElapsedSamples;
                }
                // Otherwise use elapsed time
                return Mathf.FloorToInt(_elapsedPlayTime * _audioPlayer.ClipStream.Channels * _audioPlayer.ClipStream.SampleRate);
            }
        }

        /// <summary>
        /// The total samples available for the current tts events
        /// </summary>
        public int TotalSamples => IsSpeaking && SpeakingClip?.clipStream != null ? SpeakingClip.clipStream.TotalSamples : 0;

        /// <summary>
        /// The callback following the change of the current sample
        /// </summary>
        public TTSEventSampleDelegate OnSampleUpdated { get; set; }

        /// <summary>
        /// Updates callback sample
        /// </summary>
        protected virtual void RaisePlaybackSampleUpdated(int sample)
        {
            OnSampleUpdated?.Invoke(sample);
        }

        /// <summary>
        /// The current tts events available
        /// </summary>
        public TTSEventContainer CurrentEvents => SpeakingClip?.Events;

        /// <summary>
        /// The callback following a tts event update
        /// </summary>
        public TTSEventContainerDelegate OnEventsUpdated { get; set; }

        /// <summary>
        /// Updates event callback
        /// </summary>
        protected virtual void RaisePlaybackEventsUpdated(TTSEventContainer events)
        {
            OnEventsUpdated?.Invoke(events);
        }
        #endregion
    }
}
