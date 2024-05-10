/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.Voice.Net.WebSockets;
using Meta.Voice.Net.WebSockets.Requests;
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
        [FormerlySerializedAs("configuration")]
        [SerializeField] internal WitConfiguration _configuration;

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

    public class TTSWit : TTSService, ITTSVoiceProvider, ITTSWebHandler, IWitConfigurationProvider, IWitConfigurationSetter
    {
        #region TTSService
        /// <summary>
        /// The voice provider used for preset voice settings.  Uses TTSWit with TTSWitVoiceSettings
        /// </summary>
        public override ITTSVoiceProvider VoiceProvider => this;

        /// <summary>
        /// This script provides web request handling
        /// </summary>
        public override ITTSWebHandler WebHandler => this;

        /// <summary>
        /// Generates a runtime cache if one is not found
        /// </summary>
        public override ITTSRuntimeCacheHandler RuntimeCacheHandler
        {
            get
            {
                if (_runtimeCache == null)
                {
                    _runtimeCache = gameObject.GetComponent<ITTSRuntimeCacheHandler>();
                    if (_runtimeCache == null)
                    {
                        _runtimeCache = gameObject.AddComponent<TTSRuntimeLRUCache>();
                    }
                }
                return _runtimeCache;
            }
        }
        private ITTSRuntimeCacheHandler _runtimeCache;

        /// <summary>
        /// Uses the local disk cache if found
        /// </summary>
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

        /// <summary>
        /// The configuration to be updated
        /// </summary>
        public WitConfiguration Configuration
        {
            get => RequestSettings._configuration;
            set
            {
                RequestSettings._configuration = value;
                OnConfigurationUpdated?.Invoke(RequestSettings._configuration);
                RefreshWebSocketSettings();
            }
        }
        /// <summary>
        /// Callback following configuration change
        /// </summary>
        public event Action<WitConfiguration> OnConfigurationUpdated;

        /// <summary>
        /// The current web socket adapter used to perform web socket requests
        /// </summary>
        private WitWebSocketAdapter _webSocketAdapter;

        // Returns current audio type setting for initial TTSClipData setup
        protected override AudioType GetAudioType() =>
            WitConstants.GetUnityAudioType(RequestSettings.audioType);

        // Returns current audio stream setting for initial TTSClipData setup
        protected override bool GetShouldAudioStream(AudioType audioType) =>
            RequestSettings.audioStream && base.GetShouldAudioStream(audioType);

        // Returns true provided audio type can be decoded
        protected override bool ShouldUseEvents(AudioType audioType) =>
            RequestSettings.useEvents && base.ShouldUseEvents(audioType);

        // Get tts request prior to transmission
        private WitTTSVRequest GetHttpRequest(TTSClipData clipData)
        {
            return new WitTTSVRequest(Configuration, clipData.queryRequestId,
                clipData.textToSpeak, clipData.queryParameters,
                RequestSettings.audioType, clipData.queryStream, clipData.useEvents,
                (progress) => RaiseRequestProgressUpdated(clipData, progress),
                () => RaiseRequestFirstResponse(clipData));
        }

        // Generate tts web socket request and handle responses
        private WitWebSocketTtsRequest GetWebSocketRequest(TTSClipData clipData, string downloadPath = null)
        {
            var request = new WitWebSocketTtsRequest(clipData.textToSpeak, clipData.queryParameters,
                RequestSettings.audioType, clipData.useEvents, downloadPath);
            request.OnEventsReceived = clipData.Events.AppendJsonEvents;
            request.OnSamplesReceived = clipData.clipStream.AddSamples;
            request.OnFirstResponse = (r) => RaiseRequestFirstResponse(clipData);
            _webSocketRequests[clipData.clipID] = request;
            return request;
        }

        // Performs OnRequestFirstResponse callback
        private void RaiseRequestFirstResponse(TTSClipData clipData)
        {
            if (clipData != null)
            {
                WebRequestEvents?.OnRequestFirstResponse?.Invoke(clipData);
            }
        }

        /// <summary>
        /// Attempt to instantiate web socket adapter
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshWebSocketSettings();
        }

        /// <summary>
        /// Refreshes client provider on web socket settings
        /// </summary>
        protected virtual void RefreshWebSocketSettings()
        {
            if (!_webSocketAdapter)
            {
                _webSocketAdapter = GetComponent<WitWebSocketAdapter>() ?? gameObject.AddComponent<WitWebSocketAdapter>();
            }
            var config = Configuration;
            _webSocketAdapter.SetClientProvider(config != null && config.RequestType == WitRequestType.WebSocket ? config : null);
        }

        /// <summary>
        /// Ensures web socket adapter disconnects from the client
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            if (_webSocketAdapter)
            {
                _webSocketAdapter.SetClientProvider(null);
            }
        }
        #endregion

        #region ITTSWebHandler Streams
        // Request settings
        [Header("Web Request Settings")]
        [FormerlySerializedAs("_settings")]
        public TTSWitRequestSettings RequestSettings = new TTSWitRequestSettings
        {
            audioType = TTSWitAudioType.MPEG,
            audioStream = true,
            useEvents = true
        };

        // Use settings web stream events
        public TTSStreamEvents WebStreamEvents { get; set; } = new TTSStreamEvents();

        // Requests bly clip id
        private Dictionary<string, VRequest> _webStreams = new Dictionary<string, VRequest>();
        // Web socket requests
        private Dictionary<string, WitWebSocketTtsRequest> _webSocketRequests = new Dictionary<string, WitWebSocketTtsRequest>();

        // Whether TTSService is valid
        public override string GetInvalidError()
        {
            string invalidError = base.GetInvalidError();
            if (!string.IsNullOrEmpty(invalidError))
            {
                return invalidError;
            }
            if (Configuration == null)
            {
                return "No WitConfiguration Set";
            }
            if (string.IsNullOrEmpty(Configuration.GetClientAccessToken()))
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
        public void RequestStreamFromWeb(TTSClipData clipData)
        {
            // Stream begin
            WebStreamEvents?.OnStreamBegin?.Invoke(clipData);

            // Check if valid
            string validError = IsRequestValid(clipData, Configuration);
            if (!string.IsNullOrEmpty(validError))
            {
                WebStreamEvents?.OnStreamError?.Invoke(clipData, validError);
                return;
            }
            // Cancel previous if already requested
            if (_webStreams.ContainsKey(clipData.clipID))
            {
                CancelWebStream(clipData);
            }
            if (_webSocketRequests.ContainsKey(clipData.clipID))
            {
                CancelWebStream(clipData);
            }

            // Begin request
            WebRequestEvents?.OnRequestBegin?.Invoke(clipData);

            // Request tts via web socket
            if (Configuration != null
                && Configuration.RequestType == WitRequestType.WebSocket
                && _webSocketAdapter)
            {
                RequestStreamFromWebSocket(clipData);
                return;
            }

            // Perform http request
            RequestStreamViaHttp(clipData);
        }

        /// <summary>
        /// Generates an web socket client request using specified tts clip data
        /// </summary>
        private void RequestStreamFromWebSocket(TTSClipData clipData)
        {
            // Generate tts request and store it in request
            var wsRequest = GetWebSocketRequest(clipData);
            _webSocketRequests[clipData.clipID] = wsRequest;

            // Add handlers for clip stream ready & complete
            DateTime startTime = DateTime.UtcNow;
            clipData.clipStream.OnStreamReady = (clipStream) => HandleWebStreamReady(clipData, startTime, null);
            clipData.clipStream.OnStreamComplete = (clipStream) => RaiseWebStreamCompletionCallbacks(clipData, startTime, null);

            // Set all web socket request callbacks
            wsRequest.OnEventsReceived = clipData.Events.AppendJsonEvents;
            wsRequest.OnSamplesReceived = clipData.clipStream.AddSamples;
            wsRequest.OnFirstResponse = (r) => RaiseRequestFirstResponse(clipData);
            wsRequest.OnComplete = (r) =>
            {
                if (string.IsNullOrEmpty(r.Error))
                {
                    clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
                }
                else
                {
                    RaiseWebStreamCompletionCallbacks(clipData, startTime, r.Error);
                }
            };

            // Get client and send request asap
            RefreshWebSocketSettings();
            _webSocketAdapter.SendRequest(wsRequest);
        }

        /// <summary>
        /// Generates an http request using specified tts clip data
        /// </summary>
        private void RequestStreamViaHttp(TTSClipData clipData)
        {
            // Generate request & store it
            var request = GetHttpRequest(clipData);
            _webStreams[clipData.clipID] = request;

            // Add handlers for clip stream ready & complete
            DateTime startTime = DateTime.UtcNow;
            clipData.clipStream.OnStreamReady = (clipStream) => HandleWebStreamReady(clipData, startTime, null);
            clipData.clipStream.OnStreamComplete = (clipStream) => RaiseWebStreamCompletionCallbacks(clipData, startTime, null);

            // Perform stream
            request.RequestStream(clipData.clipStream.AddSamples, clipData.Events.AppendJson,
                (success, error) =>
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
                    }
                    else
                    {
                        RaiseWebStreamCompletionCallbacks(clipData, startTime, error);
                    }
                });
        }

        /// <summary>
        /// Cancel web stream
        /// </summary>
        /// <param name="clipID">Unique clip id</param>
        public bool CancelWebStream(TTSClipData clipData)
        {
            // Cancel http v request if found
            if (_webStreams.TryGetValue(clipData.clipID, out var vRequest))
            {
                vRequest?.Cancel();
                return true;
            }
            // Cancel web socket request if found
            if (_webSocketRequests.TryGetValue(clipData.clipID, out var wsRequest))
            {
                wsRequest?.Cancel();
                return true;
            }
            // None found
            return false;
        }

        /// <summary>
        /// Handles stream clip is ready for playback
        /// </summary>
        private void HandleWebStreamReady(TTSClipData clipData, DateTime start, string error)
        {
            // Set ready duration
            clipData.loadDuration = (float)(DateTime.UtcNow - start).TotalSeconds;

            // Assume complete if errors are present
            if (!string.IsNullOrEmpty(error) || clipData.loadState == TTSClipLoadState.Unloaded)
            {
                RaiseWebStreamCompletionCallbacks(clipData, start, error);
                return;
            }

            // Perform on ready callbacks
            WebStreamEvents?.OnStreamReady?.Invoke(clipData);
            WebRequestEvents?.OnRequestReady?.Invoke(clipData);
        }

        /// <summary>
        /// Handle web stream completion with desired callbacks
        /// </summary>
        private void RaiseWebStreamCompletionCallbacks(TTSClipData clipData, DateTime start, string error)
        {
            // Remove web stream request and if none found, ignore
            if (!RemoveWebStreamRequest(clipData))
            {
                return;
            }

            // Completion
            clipData.completeDuration = (float)(DateTime.UtcNow - start).TotalSeconds;

            // Unload clip stream
            if (clipData.loadState == TTSClipLoadState.Unloaded)
            {
                error = WitConstants.CANCEL_ERROR;
                clipData.clipStream?.Unload();
            }

            // Cancelled
            if (string.Equals(error, WitConstants.CANCEL_ERROR, StringComparison.CurrentCultureIgnoreCase))
            {
                WebStreamEvents?.OnStreamCancel?.Invoke(clipData);
                WebRequestEvents?.OnRequestCancel?.Invoke(clipData);
            }
            // Error
            else if (!string.IsNullOrEmpty(error))
            {
                WebStreamEvents?.OnStreamError?.Invoke(clipData, error);
                WebRequestEvents?.OnRequestError?.Invoke(clipData, error);
            }
            // Success
            else
            {
                // If expected samples was never set, assign now
                if (clipData.clipStream.ExpectedSamples == 0)
                {
                    clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
                }

                // Set complete
                WebStreamEvents?.OnStreamComplete?.Invoke(clipData);
                WebRequestEvents?.OnRequestComplete?.Invoke(clipData);
            }
        }

        // Remove web stream request if possible and return true if success
        private bool RemoveWebStreamRequest(TTSClipData clipData)
        {
            // Remove from web stream dictionary
            if (_webStreams.ContainsKey(clipData.clipID))
            {
                _webStreams.Remove(clipData.clipID);
                return true;
            }
            // Remove from web socket dictionary
            if (_webSocketRequests.ContainsKey(clipData.clipID))
            {
                _webSocketRequests.Remove(clipData.clipID);
                return true;
            }
            return false;
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
            string validError = IsRequestValid(clipData, Configuration);
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
            WitTTSVRequest request = GetHttpRequest(clipData);
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
