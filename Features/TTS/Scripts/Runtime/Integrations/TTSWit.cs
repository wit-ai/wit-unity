/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Events;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Requests;

namespace Meta.WitAi.TTS.Integrations
{
    /// <summary>
    /// Wit request specific settings
    /// </summary>
    [Serializable]
    public struct TTSWitRequestSettings
    {
        /// <summary>
        /// The configuration used for audio requests
        /// </summary>
        public WitConfiguration configuration;

        /// <summary>
        /// The desired audio type from wit
        /// </summary>
        public TTSWitAudioType audioType;

        /// <summary>
        /// Whether or not audio should be streamed from wit if possible
        /// </summary>
        public bool audioStream;

        /// <summary>
        /// Whether or not events should be requested along with audio data
        /// </summary>
        public bool useEvents;
    }

    public class TTSWit : TTSService, ITTSVoiceProvider, ITTSWebHandler, IWitConfigurationProvider
    {
        #region TTSService
        // Voice provider
        public override ITTSVoiceProvider VoiceProvider => this;
        // Request handler
        public override ITTSWebHandler WebHandler => this;
        // Runtime cache handler
        public override ITTSRuntimeCacheHandler RuntimeCacheHandler
        {
            get
            {
                if (_runtimeCache == null)
                {
                    _runtimeCache = gameObject.GetComponent<ITTSRuntimeCacheHandler>();
                    if (_runtimeCache == null)
                    {
                        _runtimeCache = gameObject.AddComponent<TTSRuntimeCache>();
                    }
                }
                return _runtimeCache;
            }
        }
        private ITTSRuntimeCacheHandler _runtimeCache;
        // Cache handler
        public override ITTSDiskCacheHandler DiskCacheHandler
        {
            get
            {
                if (_diskCache == null)
                {
                    _diskCache = gameObject.GetComponent<ITTSDiskCacheHandler>();
                }
                return _diskCache;
            }
        }
        private ITTSDiskCacheHandler _diskCache;

        // Web request events
        public TTSWebRequestEvents WebRequestEvents => Events.WebRequest;
        // Configuration provider
        public WitConfiguration Configuration => RequestSettings.configuration;

        // Returns current audio type setting for initial TTSClipData setup
        protected override AudioType GetAudioType() =>
            WitTTSVRequest.GetAudioType(RequestSettings.audioType);

        // Returns current audio stream setting for initial TTSClipData setup
        protected override bool GetShouldAudioStream(AudioType audioType) =>
            RequestSettings.audioStream && base.GetShouldAudioStream(audioType);

        // Returns true provided audio type can be decoded
        protected override bool ShouldUseEvents(AudioType audioType) =>
            RequestSettings.useEvents && base.ShouldUseEvents(audioType);

        // Get tts request prior to transmission
        private WitTTSVRequest GetTtsRequest(TTSClipData clipData) =>
            new WitTTSVRequest(RequestSettings.configuration, clipData.queryRequestId,
                clipData.textToSpeak, clipData.queryParameters,
                RequestSettings.audioType, clipData.queryStream,
                (progress) => OnRequestProgressUpdated(clipData, progress),
                () => OnRequestFirstResponse(clipData),
                clipData.useEvents, clipData.Events.AppendEvents);

        // Progress callbacks
        private void OnRequestFirstResponse(TTSClipData clipData)
        {
            if (clipData != null)
            {
                WebRequestEvents?.OnRequestFirstResponse?.Invoke(clipData);
            }
        }
        #endregion

        #region ITTSWebHandler Streams
        // Request settings
        [Header("Web Request Settings")]
        [FormerlySerializedAs("_settings")]
        public TTSWitRequestSettings RequestSettings = new TTSWitRequestSettings
        {
            audioType = TTSWitAudioType.PCM,
            audioStream = true,
        };

        // Use settings web stream events
        public TTSStreamEvents WebStreamEvents { get; set; } = new TTSStreamEvents();

        // Requests bly clip id
        private Dictionary<string, VRequest> _webStreams = new Dictionary<string, VRequest>();

        // Whether TTSService is valid
        public override string GetInvalidError()
        {
            string invalidError = base.GetInvalidError();
            if (!string.IsNullOrEmpty(invalidError))
            {
                return invalidError;
            }
            if (RequestSettings.configuration == null)
            {
                return "No WitConfiguration Set";
            }
            if (string.IsNullOrEmpty(RequestSettings.configuration.GetClientAccessToken()))
            {
                return "No WitConfiguration Client Token";
            }
            return string.Empty;
        }

        /// <summary>
        /// Method for determining if there are problems that will arise
        /// with performing a web request prior to doing so
        /// </summary>
        /// <param name="clipData">The clip data to be used for the request</param>
        /// <returns>Invalid error(s).  It will be empty if there are none</returns>
        public string GetWebErrors(TTSClipData clipData) =>
            WitTTSVRequest.GetWebErrors(clipData?.textToSpeak, Configuration);

        /// <summary>
        /// Method for performing a web load request
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <param name="onStreamSetupComplete">Stream setup complete: returns clip and error if applicable</param>
        public void RequestStreamFromWeb(TTSClipData clipData)
        {
            // Stream begin
            WebStreamEvents?.OnStreamBegin?.Invoke(clipData);

            // Check if valid
            string validError = IsRequestValid(clipData, RequestSettings.configuration);
            if (!string.IsNullOrEmpty(validError))
            {
                WebStreamEvents?.OnStreamError?.Invoke(clipData, validError);
                return;
            }
            // Ignore if already performing
            if (_webStreams.ContainsKey(clipData.clipID))
            {
                CancelWebStream(clipData);
            }

            // Begin request
            WebRequestEvents?.OnRequestBegin?.Invoke(clipData);

            // Whether to stream
            DateTime startTime = DateTime.UtcNow;

            // Request tts
            WitTTSVRequest request = GetTtsRequest(clipData);
            request.RequestStream(clipData.clipStream,
                (clipStream, error) =>
                {
                    // Apply
                    _webStreams.Remove(clipData.clipID);

                    // Set new clip stream
                    clipData.clipStream = clipStream;
                    clipData.loadDuration = (float)(DateTime.UtcNow - startTime).TotalSeconds;

                    // Unloaded
                    if (clipData.loadState == TTSClipLoadState.Unloaded)
                    {
                        error = WitConstants.CANCEL_ERROR;
                        clipStream?.Unload();
                    }

                    // Error
                    if (!string.IsNullOrEmpty(error))
                    {
                        if (string.Equals(error, WitConstants.CANCEL_ERROR, StringComparison.CurrentCultureIgnoreCase))
                        {
                            WebStreamEvents?.OnStreamCancel?.Invoke(clipData);
                            WebRequestEvents?.OnRequestCancel?.Invoke(clipData);
                        }
                        else
                        {
                            WebStreamEvents?.OnStreamError?.Invoke(clipData, error);
                            WebRequestEvents?.OnRequestError?.Invoke(clipData, error);
                        }
                    }
                    // Success
                    else
                    {
                        WebStreamEvents?.OnStreamReady?.Invoke(clipData);
                        WebRequestEvents?.OnRequestReady?.Invoke(clipData);
                        if (!clipData.queryStream)
                        {
                            WebStreamEvents?.OnStreamComplete?.Invoke(clipData);
                            WebRequestEvents?.OnRequestComplete?.Invoke(clipData);
                        }
                    }
                });
            _webStreams[clipData.clipID] = request;
        }
        /// <summary>
        /// Cancel web stream
        /// </summary>
        /// <param name="clipID">Unique clip id</param>
        public bool CancelWebStream(TTSClipData clipData)
        {
            // Ignore without
            if (!_webStreams.ContainsKey(clipData.clipID))
            {
                return false;
            }

            // Get request
            VRequest request = _webStreams[clipData.clipID];
            _webStreams.Remove(clipData.clipID);

            // Destroy immediately
            request?.Cancel();
            request = null;

            // Success
            return true;
        }
        #endregion

        #region ITTSWebHandler Downloads
        // Use settings web download events
        public TTSDownloadEvents WebDownloadEvents { get; set; } = new TTSDownloadEvents();

        // Requests by clip id
        private Dictionary<string, WitVRequest> _webDownloads = new Dictionary<string, WitVRequest>();

        /// <summary>
        /// Method for performing a web load request
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <param name="downloadPath">Path to save clip</param>
        public void RequestDownloadFromWeb(TTSClipData clipData, string downloadPath)
        {
            // Begin
            WebDownloadEvents?.OnDownloadBegin?.Invoke(clipData, downloadPath);

            // Ensure valid
            string validError = IsRequestValid(clipData, RequestSettings.configuration);
            if (!string.IsNullOrEmpty(validError))
            {
                WebDownloadEvents?.OnDownloadError?.Invoke(clipData, downloadPath, validError);
                return;
            }
            // Abort if already performing
            if (_webDownloads.ContainsKey(clipData.clipID))
            {
                CancelWebDownload(clipData, downloadPath);
            }

            // Begin request
            WebRequestEvents?.OnRequestBegin?.Invoke(clipData);

            // Request tts
            WitTTSVRequest request = GetTtsRequest(clipData);
            request.RequestDownload(downloadPath,
                (success, error) =>
                {
                    _webDownloads.Remove(clipData.clipID);
                    if (!string.IsNullOrEmpty(error))
                    {
                        if (string.Equals(error, WitConstants.CANCEL_ERROR))
                        {
                            WebDownloadEvents?.OnDownloadCancel?.Invoke(clipData, downloadPath);
                            WebRequestEvents?.OnRequestCancel?.Invoke(clipData);
                        }
                        else
                        {
                            WebDownloadEvents?.OnDownloadError?.Invoke(clipData, downloadPath, error);
                            WebRequestEvents?.OnRequestError?.Invoke(clipData, error);
                        }
                    }
                    else
                    {
                        WebDownloadEvents?.OnDownloadSuccess?.Invoke(clipData, downloadPath);
                        WebRequestEvents?.OnRequestReady?.Invoke(clipData);
                    }
                    WebRequestEvents?.OnRequestComplete?.Invoke(clipData);
                });
            _webDownloads[clipData.clipID] = request;
        }
        /// <summary>
        /// Method for cancelling a running load request
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        public bool CancelWebDownload(TTSClipData clipData, string downloadPath)
        {
            // Ignore if not performing
            if (!_webDownloads.ContainsKey(clipData.clipID))
            {
                return false;
            }

            // Get request
            WitVRequest request = _webDownloads[clipData.clipID];
            _webDownloads.Remove(clipData.clipID);

            // Destroy immediately
            request?.Cancel();
            request = null;

            // Success
            return true;
        }
        #endregion

        #region ITTSVoiceProvider
        // Preset voice settings
        [Header("Voice Settings")]
        #if UNITY_2021_3_2 || UNITY_2021_3_3 || UNITY_2021_3_4 || UNITY_2021_3_5
        [NonReorderable]
        #endif
        [SerializeField] private TTSWitVoiceSettings[] _presetVoiceSettings;
        public TTSWitVoiceSettings[] PresetWitVoiceSettings => _presetVoiceSettings;

        // Cast to voice array
        public TTSVoiceSettings[] PresetVoiceSettings
        {
            get
            {
                if (_presetVoiceSettings == null)
                {
                    _presetVoiceSettings = new TTSWitVoiceSettings[] { };
                }
                return _presetVoiceSettings;
            }
        }
        // Default voice setting uses the first voice in the list
        public TTSVoiceSettings VoiceDefaultSettings => PresetVoiceSettings == null || PresetVoiceSettings.Length == 0 ? null : PresetVoiceSettings[0];

        #if UNITY_EDITOR
        // Apply settings
        public void SetVoiceSettings(TTSWitVoiceSettings[] newVoiceSettings)
        {
            _presetVoiceSettings = newVoiceSettings;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif

        // Returns an error if request is not valid
        private string IsRequestValid(TTSClipData clipData, WitConfiguration configuration)
        {
            // Invalid tts
            string invalidError = GetInvalidError();
            if (!string.IsNullOrEmpty(invalidError))
            {
                return invalidError;
            }
            // Invalid clip
            if (clipData == null)
            {
                return "No clip data provided";
            }
            // Success
            return string.Empty;
        }
        #endregion
    }
}
