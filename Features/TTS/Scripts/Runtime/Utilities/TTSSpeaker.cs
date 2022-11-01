/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Meta.WitAi.TTS.Data;
using UnityEngine.Serialization;

namespace Meta.WitAi.TTS.Utilities
{
    [Serializable]
    public class TTSSpeakerEvent : UnityEvent<TTSSpeaker, string> { }
    [Serializable]
    public class TTSSpeakerClipEvent : UnityEvent<AudioClip> { }
    [Serializable]
    public class TTSSpeakerEvents
    {
        [Tooltip("Called when TTS audio clip load begins")]
        public TTSSpeakerEvent OnClipLoadBegin;
        [Tooltip("Called when TTS audio clip load fails")]
        public TTSSpeakerEvent OnClipLoadFailed;
        [Tooltip("Called when TTS audio clip load successfully")]
        public TTSSpeakerEvent OnClipLoadSuccess;
        [Tooltip("Called when TTS audio clip load is cancelled")]
        public TTSSpeakerEvent OnClipLoadAbort;

        [Tooltip("Called when a new clip is queued")]
        public TTSSpeakerEvent OnQueuedSpeaking;
        [Tooltip("Called when a audio clip playback begins")]
        public TTSSpeakerEvent OnStartSpeaking;
        [Tooltip("Called when a audio clip playback completes or is cancelled")]
        public TTSSpeakerEvent OnFinishedSpeaking;
        [Tooltip("Called when a audio clip playback completes or is cancelled")]
        public TTSSpeakerEvent OnCancelledSpeaking;

        [Tooltip("Provides clip on playback ready")]
        public TTSSpeakerClipEvent OnClipPlaybackReady;
        [Tooltip("Provides clip on playback start")]
        public TTSSpeakerClipEvent OnClipPlaybackStart;
        [Tooltip("Provides clip on playback finish")]
        public TTSSpeakerClipEvent OnClipPlaybackFinished;
        [Tooltip("Provides clip on playback early cancellation")]
        public TTSSpeakerClipEvent OnClipPlaybackCancelled;
    }

    public class TTSSpeaker : MonoBehaviour
    {
        #region LIFECYCLE
        // Preset voice id
        [HideInInspector] [SerializeField] public string presetVoiceID;
        public TTSVoiceSettings VoiceSettings => _tts.GetPresetVoiceSettings(presetVoiceID);
        // Audio source
        [SerializeField] [FormerlySerializedAs("_source")]
        public AudioSource AudioSource;
        // Events
        [SerializeField] private TTSSpeakerEvents _events;
        public TTSSpeakerEvents Events => _events;

        // Loading clip list
        private List<TTSClipData> _loadingQueue = new List<TTSClipData>();
        // Accessor for loading clips
        public TTSClipData[] LoadingClips => _loadingQueue.ToArray();
        // Whether currently loading or not
        public bool IsLoading => _loadingQueue.Count > 0;

        // Playback clip list
        private List<TTSClipData> _playbackQueue = new List<TTSClipData>();
        // Accessor for speaking clips
        public TTSClipData[] PlaybackQueue => _playbackQueue.ToArray();
        // Whether currently speaking or not
        public bool IsSpeaking => _playbackQueue.Count > 0;

        // Current tts service
        private TTSService _tts;

        // Automatically generate source if needed
        protected virtual void Awake()
        {
            if (AudioSource == null)
            {
                AudioSource = gameObject.GetComponentInChildren<AudioSource>();
                if (AudioSource == null)
                {
                    AudioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            AudioSource.playOnAwake = false;
            _tts = TTSService.Instance;
        }
        // Add listener for clip unload
        protected virtual void OnEnable()
        {
            if (_tts == null)
            {
                return;
            }
            _tts.Events.OnClipUnloaded.AddListener(OnClipUnload);
        }
        // Stop speaking & remove listener
        protected virtual void OnDisable()
        {
            Stop();
            if (_tts == null)
            {
                return;
            }
            _tts.Events.OnClipUnloaded.RemoveListener(OnClipUnload);
        }
        #endregion

        #region HELPERS
        // Format text
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
        // Get first loading clip index
        public int GetLoadingClipIndex(string clipId)
        {
            return _loadingQueue.FindIndex((clip) => clip != null && clip.HasClipId(clipId));
        }
        // Get first speaking queue index
        public int GetPlaybackQueueIndex(string clipId)
        {
            return _playbackQueue.FindIndex((clip) => clip != null && clip.HasClipId(clipId));
        }
        // Clip unloaded externally
        protected virtual void OnClipUnload(TTSClipData clipData)
        {
            // If loading, cancel load
            int index = 0;
            while (index < _loadingQueue.Count)
            {
                // Cancel all matching clips
                TTSClipData loadingClipData = _loadingQueue[index];
                if (loadingClipData == null || loadingClipData.Equals(clipData))
                {
                    OnLoadCancel(index);
                }
                // Move to next clip
                else
                {
                    index++;
                }
            }

            // If playing, abort playback
            index = 0;
            while (index < _playbackQueue.Count)
            {
                // Abort all matching clips
                TTSClipData speakingClipData = _loadingQueue[index];
                if (speakingClipData == null || speakingClipData.Equals(clipData))
                {
                    OnPlaybackCancel(index);
                }
                // Move to next clip
                else
                {
                    index++;
                }
            }
        }
        #endregion

        #region INTERACTIONS
        // Stops loading & speaking immediately
        public virtual void Stop()
        {
            StopLoading();
            StopSpeaking();
        }
        // Cancel all loading clips starting with the last added
        protected virtual void StopLoading(string ignoreClipId = null)
        {
            int start = _loadingQueue.Count - 1;
            for (int i = start; i >= 0; i--)
            {
                if (_loadingQueue[i] == null || !_loadingQueue[i].HasClipId(ignoreClipId))
                {
                    OnLoadCancel(i);
                }
            }
        }
        // Cancel all speaking clips starting with the last added
        protected virtual void StopSpeaking(string ignoreClipId = null)
        {
            int start = _playbackQueue.Count - 1;
            for (int i = start; i >= 0; i--)
            {
                if (_playbackQueue[i] == null || !_playbackQueue[i].HasClipId(ignoreClipId))
                {
                    OnPlaybackCancel(i);
                }
            }
        }
        /// <summary>
        /// Load a tts clip using the specified text & cache settings.
        /// Plays clip immediately upon load & will cancel all previously loading/spoken phrases.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public void Speak(string textToSpeak, TTSDiskCacheSettings diskCacheSettings = null) => Speak(textToSpeak, diskCacheSettings, false);
        /// <summary>
        /// Load a tts clip using the specified text & cache settings.
        /// Adds clip to speak queue and will speak once previously spoken phrases are complete
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public void SpeakQueued(string textToSpeak, TTSDiskCacheSettings diskCacheSettings = null) => Speak(textToSpeak, diskCacheSettings, true);

        /// <summary>
        /// Loads a formated phrase to be spoken
        /// Adds clip to speak queue and will speak once previously spoken phrases are complete
        /// </summary>
        /// <param name="format">Format string to be filled in with texts</param>
        public void SpeakFormat(string format, params string[] textsToSpeak) =>
            Speak(GetFormattedText(format, textsToSpeak), null, false);
        /// <summary>
        /// Loads a formated phrase to be spoken
        /// Adds clip to speak queue and will speak once previously spoken phrases are complete
        /// </summary>
        /// <param name="format">Format string to be filled in with texts</param>
        public void SpeakFormatQueued(string format, params string[] textsToSpeak) =>
            Speak(GetFormattedText(format, textsToSpeak), null, true);

        /// <summary>
        /// Speak and wait for load/playback completion
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakAsync(string textToSpeak, TTSDiskCacheSettings diskCacheSettings = null)
        {
            Stop();
            yield return SpeakQueuedAsync(new string[] {textToSpeak}, diskCacheSettings);
        }
        /// <summary>
        /// Speak and wait for load/playback completion
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        public IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSDiskCacheSettings diskCacheSettings = null)
        {
            // Speak each queued
            foreach (var textToSpeak in textsToSpeak)
            {
                SpeakQueued(textToSpeak, diskCacheSettings);
            }
            // Wait while loading/speaking
            yield return new WaitWhile(() => IsLoading || IsSpeaking);
        }

        /// <summary>
        /// Loads a tts clip & handles playback
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="diskCacheSettings">Specific tts load caching settings</param>
        /// <param name="addToQueue">Whether or not this phrase should be enqueued into the speak queue</param>
        protected virtual void Speak(string textToSpeak, TTSDiskCacheSettings diskCacheSettings, bool addToQueue)
        {
            // Ensure voice settings exist
            TTSVoiceSettings voiceSettings = VoiceSettings;
            if (voiceSettings == null)
            {
                VLog.E($"No voice found with preset id: {presetVoiceID}");
                return;
            }
            // Log if empty text
            if (string.IsNullOrEmpty(textToSpeak))
            {
                VLog.E("No text to speak provided");
                return;
            }

            // Get new clip if possible
            string newClipID = _tts.GetClipID(textToSpeak, voiceSettings);
            TTSClipData newClip = _tts.GetRuntimeCachedClip(newClipID);

            // Stop previous loading queue
            if (!addToQueue)
            {
                StopLoading(newClipID);
            }

            // Begin playback
            if (newClip != null && newClip.loadState == TTSClipLoadState.Loaded)
            {
                // Stop speaking
                if (!addToQueue)
                {
                    StopSpeaking();
                }

                // Begin playback
                OnPlaybackReady(newClip);
            }
            // Begin load/add load completion callback
            else
            {
                OnLoadBegin(textToSpeak, newClipID, voiceSettings, diskCacheSettings, addToQueue);
            }
        }
        #endregion

        #region LOAD
        // Begin a load
        protected virtual void OnLoadBegin(string textToSpeak, string clipID, TTSVoiceSettings voiceSettings, TTSDiskCacheSettings diskCacheSettings, bool addToQueue)
        {
            // Load begin
            Events?.OnClipLoadBegin?.Invoke(this, textToSpeak);

            // Perform load request
            VLog.D($"Load Begin\nText: {textToSpeak}");
            TTSClipData newClip = _tts.Load(textToSpeak, clipID, voiceSettings, diskCacheSettings, (clipData, error) => OnClipLoadComplete(clipData, error, addToQueue));
            _loadingQueue.Add(newClip);
        }
        // Load complete
        protected virtual void OnClipLoadComplete(TTSClipData clipData, string error, bool addToQueue)
        {
            // Invalid clip, ignore
            int index = GetLoadingClipIndex(clipData?.clipID);
            if (index == -1)
            {
                return;
            }

            // Remove from loading queue
            _loadingQueue.RemoveAt(index);

            // Load error
            if (!string.IsNullOrEmpty(error))
            {
                VLog.E($"Load Failed\nText: {clipData?.textToSpeak}\n{error}");
                Events?.OnClipLoadFailed?.Invoke(this, clipData.textToSpeak);
                return;
            }
            // No clip failure
            if (clipData.clip == null)
            {
                VLog.E($"Load Failed\nText: {clipData?.textToSpeak}\nNo clip returned");
                Events?.OnClipLoadFailed?.Invoke(this, clipData.textToSpeak);
                return;
            }

            // Load success event
            VLog.D($"Load Success\nText: {clipData?.textToSpeak}");
            Events?.OnClipLoadSuccess?.Invoke(this, clipData.textToSpeak);

            // Stop speaking
            if (!addToQueue)
            {
                StopSpeaking();
            }

            // Play clip
            OnPlaybackReady(clipData);
        }
        #endregion

        #region UNLOAD
        // Cancel load
        protected virtual void OnLoadCancel(int index)
        {
            // Invalid clip, ignore
            if (index < 0 || index >= _loadingQueue.Count)
            {
                return;
            }

            // Get clip data
            TTSClipData clipData = _loadingQueue[index];

            // Remove from loading queue
            _loadingQueue.RemoveAt(index);

            // Abort event
            VLog.D($"Load Cancelled\nText: {clipData?.textToSpeak}");
            Events?.OnClipLoadAbort?.Invoke(this, clipData.textToSpeak);
        }
        #endregion

        #region PLAY
        // Wait for playback completion
        private Coroutine _waitForCompletion;

        // Playback ready
        protected virtual void OnPlaybackReady(TTSClipData clipData)
        {
            // If clip missing
            if (clipData == null || clipData.clip == null)
            {
                VLog.E("Clip destroyed prior to playback");
                return;
            }

            // Add to queue
            _playbackQueue.Add(clipData);

            // Playback ready
            VLog.D($"Playback Queued\nText: {clipData.textToSpeak}");
            Events?.OnQueuedSpeaking?.Invoke(this, clipData.textToSpeak);
            Events?.OnClipPlaybackReady?.Invoke(clipData.clip);

            // Begin playback if first
            if (_playbackQueue.Count == 1)
            {
                OnPlaybackBegin();
            }
        }
        // Play next
        protected virtual void OnPlaybackBegin()
        {
            // Get next clip
            TTSClipData nextClip = _playbackQueue[0];
            while (nextClip == null || nextClip.clip == null)
            {
                // Unload if null
                OnPlaybackCancel(0);
                // Get next
                if (_playbackQueue.Count > 0)
                {
                    nextClip = _playbackQueue[0];
                }
                // Exit
                else
                {
                    return;
                }
            }

            // Started speaking
            VLog.D($"Playback Begin\nText: {nextClip.textToSpeak}");
            AudioSource.PlayOneShot(nextClip.clip);

            // Callback events
            Events?.OnStartSpeaking?.Invoke(this, nextClip.textToSpeak);
            Events?.OnClipPlaybackStart?.Invoke(nextClip.clip);

            // Wait for completion
            if (_waitForCompletion != null)
            {
                StopCoroutine(_waitForCompletion);
                _waitForCompletion = null;
            }
            _waitForCompletion = StartCoroutine(WaitForCompletion(nextClip.clip.length));
        }
        // Wait for clip completion
        protected virtual IEnumerator WaitForCompletion(float duration)
        {
            yield return new WaitForSeconds(duration);
            OnPlaybackComplete();
        }
        // Completed playback
        protected virtual void OnPlaybackComplete()
        {
            // Invalid
            if (_playbackQueue.Count == 0)
            {
                return;
            }

            // Get first clip
            TTSClipData clipData = _playbackQueue[0];
            VLog.D($"Playback Complete\nText: {clipData?.textToSpeak}");

            // Remove from queue
            _playbackQueue.RemoveAt(0);

            // Abort event
            if (clipData != null)
            {
                Events?.OnFinishedSpeaking?.Invoke(this, clipData.textToSpeak);
                if (clipData.clip != null)
                {
                    Events?.OnClipPlaybackFinished?.Invoke(clipData.clip);
                }
            }

            // Play next
            if (_playbackQueue.Count > 0)
            {
                OnPlaybackBegin();
            }
        }
        // Cancel playback
        protected virtual void OnPlaybackCancel(int index)
        {
            // Invalid clip, ignore
            if (index < 0 || index >= _playbackQueue.Count)
            {
                return;
            }

            // Get clip data
            TTSClipData clipData = _playbackQueue[index];
            VLog.D($"Playback Cancelled\nText: {clipData?.textToSpeak}");

            // Remove from queue
            _playbackQueue.RemoveAt(index);

            // Abort event
            if (clipData != null)
            {
                Events?.OnCancelledSpeaking?.Invoke(this, clipData.textToSpeak);
                if (clipData.clip != null)
                {
                    Events?.OnClipPlaybackCancelled?.Invoke(clipData.clip);
                }
            }

            // Stop waiting/playback
            if (index == 0)
            {
                // Stop playback handler
                if (_waitForCompletion != null)
                {
                    StopCoroutine(_waitForCompletion);
                    _waitForCompletion = null;
                }
                // Stop audio source playback
                if (AudioSource.isPlaying)
                {
                    AudioSource.Stop();
                }
            }
        }
        #endregion
    }
}
