/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Audio;
using Meta.Voice.Net.WebSockets;
using Meta.Voice.Net.WebSockets.Requests;
using UnityEngine;
using UnityEngine.Serialization;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Requests;

namespace Meta.WitAi.TTS.Integrations
{
    public class TTSWit : TTSService, ITTSVoiceProvider, ITTSWebHandler, IWitConfigurationProvider, IWitConfigurationSetter, ILogSource
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

        /// <summary>
        /// Attempt to instantiate web socket adapter and setup audio system
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshWebSocketSettings();
            RefreshAudioSystemSettings();
            if (AudioSystem != null)
            {
                AudioSystem.PreloadClipStreams(RequestSettings.audioStreamPreloadCount);
            }
        }

        /// <summary>
        /// Refreshes client provider on web socket settings
        /// </summary>
        protected virtual void RefreshWebSocketSettings()
        {
            _webSocketAdapter = GetOrCreateInterface<WitWebSocketAdapter, WitWebSocketAdapter>(_webSocketAdapter);
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

        #region ITTSWebHandler
        // Request settings
        [Header("Web Request Settings")]
        [FormerlySerializedAs("_settings")]
        public TTSWitRequestSettings RequestSettings = new TTSWitRequestSettings
        {
            audioType = WitConstants.TTS_TYPE_DEFAULT,
            audioReadyDuration = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH,
            audioMaxDuration = WitConstants.ENDPOINT_TTS_DEFAULT_MAX_LENGTH,
            audioStreamPreloadCount = WitConstants.ENDPOINT_TTS_DEFAULT_PRELOAD,
            audioStream = true,
            useEvents = true
        };

        // Http requests by unique clip id key
        private ConcurrentDictionary<string, WitTTSVRequest> _httpRequests = new ConcurrentDictionary<string, WitTTSVRequest>();
        // Web socket requests by unique clip id key
        private ConcurrentDictionary<string, WitWebSocketTtsRequest> _webSocketRequests = new ConcurrentDictionary<string, WitWebSocketTtsRequest>();

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
        public string GetWebErrors(TTSClipData clipData)
        {
            var invalidErrors = GetInvalidError();
            if (!string.IsNullOrEmpty(invalidErrors))
            {
                return invalidErrors;
            }
            var webErrors = WitRequestSettings.GetTtsErrors(clipData?.textToSpeak, Configuration);
            if (!string.IsNullOrEmpty(webErrors))
            {
                return webErrors;
            }
            return string.Empty;
        }

        /// <summary>
        /// Method for creating a new TTSClipData
        /// </summary>
        /// <param name="clipId">Unique clip identifier</param>
        /// <param name="textToSpeak">Text to be spoken</param>
        /// <param name="voiceSettings">Settings for how the clip should sound during playback.</param>
        /// <param name="diskCacheSettings">If and how this clip should be cached.</param>
        public TTSClipData CreateClipData(string clipId,
            string textToSpeak,
            TTSVoiceSettings voiceSettings,
            TTSDiskCacheSettings diskCacheSettings)
            => new TTSClipData()
            {
                clipID = clipId,
                textToSpeak = textToSpeak,
                voiceSettings = voiceSettings,
                diskCacheSettings = diskCacheSettings,
                loadState = TTSClipLoadState.Unloaded,
                loadProgress = 0f,
                queryParameters = voiceSettings?.EncodedValues,
                clipStream = string.IsNullOrEmpty(textToSpeak) ? null : CreateClipStream(),
                extension = WitRequestSettings.GetAudioExtension(RequestSettings.audioType, RequestSettings.useEvents),
                queryStream = RequestSettings.audioStream,
                useEvents = RequestSettings.useEvents
            };

        // Generate a new audio clip stream
        private IAudioClipStream CreateClipStream()
        {
            // Default
            if (AudioSystem == null)
            {
                return new RawAudioClipStream(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE, RequestSettings.audioReadyDuration);
            }
            // Get audio clip via audio system
            RefreshAudioSystemSettings();
            return AudioSystem.GetAudioClipStream();
        }

        /// <summary>
        /// Set audio clip settings
        /// </summary>
        private void RefreshAudioSystemSettings()
        {
            if (AudioSystem == null)
            {
                return;
            }
            AudioSystem.ClipSettings = new AudioClipSettings()
            {
                Channels = WitConstants.ENDPOINT_TTS_CHANNELS,
                SampleRate = WitConstants.ENDPOINT_TTS_SAMPLE_RATE,
                ReadyDuration = RequestSettings.audioReadyDuration,
                MaxDuration = RequestSettings.audioMaxDuration
            };
        }

        // Get tts request prior to transmission
        private WitTTSVRequest CreateHttpRequest(TTSClipData clipData)
        {
            var request = new WitTTSVRequest(Configuration, clipData.queryRequestId);
            request.TextToSpeak = clipData.textToSpeak;
            request.TtsParameters = clipData.queryParameters;
            request.FileType = RequestSettings.audioType;
            request.Stream = clipData.queryStream;
            request.UseEvents = clipData.useEvents;
            _httpRequests[clipData.clipID] = request;
            return request;
        }

        // Generate tts web socket request and handle responses
        private WitWebSocketTtsRequest CreateWebSocketRequest(TTSClipData clipData, string downloadPath)
        {
            var request = new WitWebSocketTtsRequest(clipData.queryRequestId,
                clipData.textToSpeak,
                clipData.queryParameters,
                RequestSettings.audioType,
                clipData.useEvents,
                downloadPath);
            request.OnSamplesReceived = clipData.clipStream.AddSamples;
            request.OnEventsReceived = clipData.Events.AddEvents;
            _webSocketRequests[clipData.clipID] = request;
            return request;
        }

        /// <summary>
        /// Decode a response node into text to be spoken or a specific voice setting
        /// Example Data:
        /// {
        ///    "q": "Text to be spoken"
        ///    "voice": "Charlie
        /// }
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken and voice settings</param>
        /// <param name="textToSpeak">The text to be spoken output</param>
        /// <param name="voiceSettings">The output for voice settings</param>
        /// <returns>True if decode was successful</returns>
        public bool DecodeTtsFromJson(WitResponseNode responseNode, out string textToSpeak, out TTSVoiceSettings voiceSettings)
        {
            if (TTSWitVoiceSettings.CanDecode(responseNode))
            {
                TTSWitVoiceSettings witVoice = JsonConvert.DeserializeObject<TTSWitVoiceSettings>(responseNode, null, true);
                if (witVoice != null)
                {
                    voiceSettings = witVoice;
                    textToSpeak = responseNode[WitConstants.ENDPOINT_TTS_PARAM];
                    return true;
                }
            }
            textToSpeak = null;
            voiceSettings = null;
            return false;
        }

        /// <summary>
        /// Method for streaming audio from a back-end service.
        /// </summary>
        /// <param name="clipData">Information about the clip being requested.</param>
        /// <param name="onReady">Callback on request is ready for playback.</param>
        public async Task<string> RequestStreamFromWeb(TTSClipData clipData,
            Action<TTSClipData> onReady)
        {
            // Cancel previous if already requested
            CancelRequests(clipData);

            // Clip stream must exist
            if (clipData.clipStream == null)
            {
                return "Cannot load without a clip stream";
            }

            // Set on ready callback
            DateTime startTime = DateTime.UtcNow;
            clipData.clipStream.OnStreamReady += (stream) =>
            {
                clipData.readyDuration = (float)(DateTime.UtcNow - startTime).TotalSeconds;
                onReady?.Invoke(clipData);
            };

            // Request web socket request
            string error;
            if (Configuration != null
                && Configuration.RequestType == WitRequestType.WebSocket
                && _webSocketAdapter)
            {
                error = await RequestStreamFromWebSocket(clipData);
            }
            // Perform http request
            else
            {
                error = await RequestStreamViaHttp(clipData);
            }

            // Set complete duration
            clipData.completeDuration = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            // No samples added
            if (string.IsNullOrEmpty(error) &&
                (clipData?.clipStream == null || clipData.clipStream.AddedSamples == 0))
            {
                error = "No audio samples added during stream";
            }
            // Set expected samples
            if (string.IsNullOrEmpty(error))
            {
                clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
            }

            // Return errors
            return error;
        }

        /// <summary>
        /// Generates an web socket client request using specified tts clip data
        /// </summary>
        private async Task<string> RequestStreamFromWebSocket(TTSClipData clipData)
        {
            // Generate tts request and store it in request
            var wsRequest = CreateWebSocketRequest(clipData, null);

            // Set all web socket request callbacks
            // TODO: T192757334 Update to async once added in WebSockets
            var completion = new TaskCompletionSource<bool>();
            wsRequest.OnComplete = (r) =>
            {
                completion.SetResult(true);
            };

            // Send request and await completion
            RefreshWebSocketSettings();
            _webSocketAdapter.SendRequest(wsRequest);
            await completion.Task;

            // Return any error
            return wsRequest.Error;
        }

        /// <summary>
        /// Generates an http request using specified tts clip data
        /// </summary>
        private Task<string> RequestStreamViaHttp(TTSClipData clipData)
        {
            var clipId = clipData.clipID;
            var request = CreateHttpRequest(clipData);
            _httpRequests[clipId] = request;
            return ThreadUtility.BackgroundAsync(Logger, async () =>
            {
                var results = await request.RequestStream(clipData.clipStream.AddSamples, clipData.Events.AddEvents);
                _httpRequests.TryRemove(clipId, out var discard);
                return results.Error;
            });
        }

        /// <summary>
        /// Method for performing a web download request
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <param name="diskPath">The specific disk path the file should be downloaded to</param>
        public Task<string> RequestDownloadFromWeb(TTSClipData clipData,
            string diskPath)
        {
            // Cancel previous if already requested
            CancelRequests(clipData);

            // Request web socket request
            if (Configuration != null
                && Configuration.RequestType == WitRequestType.WebSocket
                && _webSocketAdapter)
            {
                return RequestDownloadFromWebSocket(clipData, diskPath);
            }

            // Perform http request
            return RequestDownloadViaHttp(clipData, diskPath);
        }

        /// <summary>
        /// Generates an web socket client request using specified tts clip data
        /// </summary>
        private async Task<string> RequestDownloadFromWebSocket(TTSClipData clipData,
            string diskPath)
        {
            // Generate tts request and store it in request
            var wsRequest = CreateWebSocketRequest(clipData, diskPath);

            // Set all web socket request callbacks
            var completion = new TaskCompletionSource<bool>();
            wsRequest.OnComplete = (r) =>
            {
                completion.SetResult(true);
            };

            // Send request and await completion
            RefreshWebSocketSettings();
            _webSocketAdapter.SendRequest(wsRequest);
            await completion.Task;

            // Return any error
            return wsRequest.Error;
        }

        /// <summary>
        /// Generates an http request using specified tts clip data
        /// </summary>
        private Task<string> RequestDownloadViaHttp(TTSClipData clipData,
            string diskPath)
        {
            var clipId = clipData.clipID;
            var request = CreateHttpRequest(clipData);
            _httpRequests[clipId] = request;
            return ThreadUtility.BackgroundAsync(Logger, async () =>
            {
                var results = await request.RequestDownload(diskPath);
                _httpRequests.TryRemove(clipId, out var discard);
                return results.Error;
            });
        }

        /// <summary>
        /// Checks if file exists on disk
        /// </summary>
        public async Task<string> IsDownloadedToDisk(string diskPath)
        {
            string error = null;
            await ThreadUtility.BackgroundAsync(Logger, async () =>
            {
                var request = new VRequest();
                var results = await request.RequestFileExists(diskPath);
                error = results.Error;
                if (string.IsNullOrEmpty(error) && !results.Value)
                {
                    error = "File Not Found";
                }
            });
            return error;
        }

        /// <summary>
        /// Streams from disk
        /// </summary>
        public async Task<string> RequestStreamFromDisk(TTSClipData clipData, string diskPath, Action<TTSClipData> onReady)
        {
            // Cancel previous if already requested
            CancelRequests(clipData);

            // Clip stream must exist
            if (clipData.clipStream == null)
            {
                return "Cannot load without a clip stream";
            }

            // Set on ready callback
            DateTime startTime = DateTime.UtcNow;
            clipData.clipStream.OnStreamReady += (stream) =>
            {
                clipData.readyDuration = (float)(DateTime.UtcNow - startTime).TotalSeconds;
                onReady?.Invoke(clipData);
            };

            // Perform unity web request
            var error = await RequestStreamFromDiskViaVRequest(clipData, diskPath);

            // Set complete duration
            clipData.completeDuration = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            // No samples added
            if (string.IsNullOrEmpty(error) &&
                (clipData?.clipStream == null || clipData.clipStream.AddedSamples == 0))
            {
                error = "No audio samples added during stream";
            }
            // Set expected samples
            if (string.IsNullOrEmpty(error))
            {
                clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
            }

            // Return errors
            return error;
        }

        /// <summary>
        /// Generates a VRequest using specified tts clip data
        /// </summary>
        private Task<string> RequestStreamFromDiskViaVRequest(TTSClipData clipData,
            string diskPath)
        {
            var clipId = clipData.clipID;
            var request = CreateHttpRequest(clipData);
            _httpRequests[clipId] = request;
            return ThreadUtility.BackgroundAsync(Logger, async () =>
            {
                var results = await request.RequestStreamFromDisk(diskPath,
                    clipData.clipStream.AddSamples,
                    clipData.Events.AddEvents);
                _httpRequests.TryRemove(clipId, out var discard);
                return results.Error;
            });
        }

        /// <summary>
        /// Cancels any running requests
        /// </summary>
        public bool CancelRequests(TTSClipData clipData)
        {
            // Cancel http v request if found
            if (_httpRequests.TryGetValue(clipData.clipID, out var vRequest))
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
        #endregion ITTSWebHandler

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
