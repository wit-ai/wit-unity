/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Meta.Voice.Audio.Decoding;

namespace Meta.WitAi
{
    /// <summary>
    /// Audio types supported by tts
    /// </summary>
    public enum TTSWitAudioType
    {
        /// <summary>
        /// Raw pcm 16 data
        /// </summary>
        PCM = 0,
        /// <summary>
        /// MP3 data format
        /// </summary>
        MPEG = 1,
        /// <summary>
        /// Wave data format
        /// </summary>
        WAV = 2,
        /// <summary>
        /// Opus data format
        /// </summary>
        OPUS = 3
    }

    /// <summary>
    /// A static script for obtaining header information related to wit requests
    /// </summary>
    public static class WitRequestSettings
    {
        #region SETUP
        // User-agent specific information
        private static string _operatingSystem;
        private static string _deviceModel;
        private static string _appIdentifier;
        private static string _unityVersion;

        /// <summary>
        /// Uri customization delegate
        /// </summary>
        public static Func<UriBuilder, UriBuilder> OnProvideCustomUri;
        /// <summary>
        /// Header customization delegate
        /// </summary>
        public static Action<Dictionary<string, string>> OnProvideCustomHeaders;
        /// <summary>
        /// User agent customization delegate
        /// </summary>
        public static Action<StringBuilder> OnProvideCustomUserAgent;

        /// <summary>
        /// Preloads all user-agent data
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            if (_operatingSystem == null) _operatingSystem = SystemInfo.operatingSystem;
            if (_deviceModel == null) _deviceModel = SystemInfo.deviceModel;
            if (_appIdentifier == null) _appIdentifier = Application.identifier;
            if (_unityVersion == null) _unityVersion = Application.unityVersion;
            if (string.IsNullOrEmpty(_localClientUserId))
            {
                _localClientUserId = PlayerPrefs.GetString(PREF_CLIENT_USER_ID);
                if (string.IsNullOrEmpty(_localClientUserId))
                {
                    _localClientUserId = System.Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(PREF_CLIENT_USER_ID, _localClientUserId);
                    PlayerPrefs.Save();
                }
            }
        }
        #if UNITY_EDITOR
        /// <summary>
        /// Add constructor to init in editor only
        /// </summary>
        static WitRequestSettings()
        {
            UnityEditor.EditorApplication.update += EditorInit;
        }
        static void EditorInit()
        {
            UnityEditor.EditorApplication.update -= EditorInit;
            Init();
        }
        #endif

        /// <summary>
        /// The default client user id sent with locally generated requests
        /// if not overriden by request options.
        /// </summary>
        public static string LocalClientUserId
        {
            get => _localClientUserId;
            set
            {
                // Ignore if same
                if (string.Equals(value, _localClientUserId))
                {
                    return;
                }
                // Otherwise, set
                _localClientUserId = value;
                // Attempt to set on main thread
                ThreadUtility.CallOnMainThread(() =>
                {
                    PlayerPrefs.SetString(PREF_CLIENT_USER_ID, _localClientUserId);
                    PlayerPrefs.Save();
                });
            }
        }
        private static string _localClientUserId;
        private const string PREF_CLIENT_USER_ID = WitConstants.HEADER_CLIENT_USER_ID;

        /// <summary>
        /// Returns a string of all bytes within an array
        /// </summary>
        internal static string GetByteString(byte[] bytes)
            => GetByteString(bytes, 0, bytes.Length);

        /// <summary>
        /// Returns a string of a substring of bytes within an array
        /// </summary>
        internal static string GetByteString(byte[] bytes, int start, int length)
            => BitConverter.ToString(bytes, start, length);
        #endregion SETUP

        #region URL AND HEADERS
        /// <summary>
        /// Get custom wit uri using a specific path & query parameters
        /// </summary>
        public static Uri GetUri(IWitRequestConfiguration configuration, string path, Dictionary<string, string> queryParams = null)
        {
            // Uri builder
            UriBuilder uriBuilder = new UriBuilder();

            // Append endpoint data
            IWitRequestEndpointInfo endpoint = configuration.GetEndpointInfo();
            uriBuilder.Scheme = endpoint.UriScheme;
            uriBuilder.Host = endpoint.Authority;
            uriBuilder.Port = endpoint.Port;

            // Set path
            uriBuilder.Path = path;

            // Build query
            string apiVersion = endpoint.WitApiVersion;
            uriBuilder.Query = $"v={apiVersion}";
            if (queryParams != null)
            {
                foreach (string key in queryParams.Keys)
                {
                    var value = queryParams[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = UnityWebRequest.EscapeURL(value).Replace("+", "%20");
                        uriBuilder.Query += $"&{key}={value}";
                    }
                }
            }

            // Return custom uri
            if (OnProvideCustomUri != null)
            {
                foreach (Func<UriBuilder, UriBuilder> del in OnProvideCustomUri.GetInvocationList())
                {
                    uriBuilder = del(uriBuilder);
                }
            }

            // Return uri
            return uriBuilder.Uri;
        }

        /// <summary>
        /// Obtain headers to be used with every wit service
        /// </summary>
        public static Dictionary<string, string> GetHeaders(IWitRequestConfiguration configuration, string requestId, bool useServerToken, string clientUserId = null)
        {
            // Get headers
            Dictionary<string, string> headers = new Dictionary<string, string>();

            // Set authorization
            headers[WitConstants.HEADER_AUTH] = GetAuthorizationHeader(configuration, useServerToken);

            // Use local client user id if empty
            if (string.IsNullOrEmpty(clientUserId))
            {
                clientUserId = LocalClientUserId;
            }
            headers[WitConstants.HEADER_CLIENT_USER_ID] = clientUserId;

            #if UNITY_EDITOR || !UNITY_WEBGL
            // Set request id
            headers[WitConstants.HEADER_REQUEST_ID] = string.IsNullOrEmpty(requestId) ? WitConstants.GetUniqueId() : requestId;
            // Set User-Agent
            headers[WitConstants.HEADER_USERAGENT] = GetUserAgentHeader(configuration);
            #endif

            // Allow overrides
            if (OnProvideCustomHeaders != null)
            {
                // Allow overrides
                foreach (Action<Dictionary<string, string>> del in OnProvideCustomHeaders.GetInvocationList())
                {
                    del(headers);
                }
            }

            // Return results
            return headers;
        }

        /// <summary>
        /// Obtain authorization header using provided access token
        /// </summary>
        private static string GetAuthorizationHeader(IWitRequestConfiguration configuration, bool useServerToken)
        {
            // Default to client access token
            string token = configuration.GetClientAccessToken();
            // Use server token
            if (useServerToken)
            {
                #if UNITY_EDITOR
                token = configuration.GetServerAccessToken();
                #else
                token = string.Empty;
                #endif
            }
            // Trim token
            if (!string.IsNullOrEmpty(token))
            {
                token = token.Trim();
            }
            // Use invalid token
            else
            {
                token = "XXX";
            }
            // Return with bearer
            return $"Bearer {token}";
        }

        /// <summary>
        /// Build all user agent header data using specified information
        /// </summary>
        private static string GetUserAgentHeader(IWitRequestConfiguration configuration)
        {
            // Generate user agent
            StringBuilder userAgent = new StringBuilder();

            // Append wit sdk version
            userAgent.Append($"wit-unity-{WitConstants.SDK_VERSION}");

            // Append operating system
            userAgent.Append($",\"{_operatingSystem}\"");
            // Append device model
            userAgent.Append($",\"{_deviceModel}\"");

            // Append configuration log id
            string logId = configuration.GetConfigurationId();
            if (string.IsNullOrEmpty(logId))
            {
                logId = WitConstants.HEADER_USERAGENT_CONFID_MISSING;
            }
            userAgent.Append($",{logId}");

            // Append app identifier
            userAgent.Append($",{_appIdentifier}");

            // Append editor identifier
            #if UNITY_EDITOR
            userAgent.Append(",Editor");
            #else
            userAgent.Append(",Runtime");
            #endif

            // Append unity version
            userAgent.Append($",{_unityVersion}");

            // Set custom user agent
            if (OnProvideCustomUserAgent != null)
            {
                foreach (Action<StringBuilder> del in OnProvideCustomUserAgent.GetInvocationList())
                {
                    del(userAgent);
                }
            }

            // Return user agent
            return userAgent.ToString();
        }
        #endregion URL AND HEADERS

        #region TTS
        /// <summary>
        /// Method for determining if there are problems that will arise
        /// with performing a tts web request prior to doing so
        /// </summary>
        public static string GetTtsErrors(string textToSpeak, IWitRequestConfiguration configuration)
        {
            // Invalid text
            if (string.IsNullOrEmpty(textToSpeak))
            {
                return WitConstants.ENDPOINT_TTS_NO_TEXT;
            }
            // Check configuration & configuration token
            if (configuration == null)
            {
                return WitConstants.ERROR_NO_CONFIG;
            }
            if (string.IsNullOrEmpty(configuration.GetClientAccessToken()))
            {
                return WitConstants.ERROR_NO_CONFIG_TOKEN;
            }
            // Should be good
            return string.Empty;
        }

        /// <summary>
        /// Method for determining if stream is supported
        /// </summary>
        public static bool CanStreamAudio(TTSWitAudioType witAudioType)
            => witAudioType != TTSWitAudioType.WAV;

        /// <summary>
        /// Method for obtaining audio Mime string for TTSWitAudioType
        /// </summary>
        public static string GetAudioMimeType(TTSWitAudioType witAudioType)
        {
            switch (witAudioType)
            {
                case TTSWitAudioType.PCM:
                    return "audio/raw";
                case TTSWitAudioType.OPUS:
                    return "audio/opus-demo";
                case TTSWitAudioType.MPEG:
                case TTSWitAudioType.WAV:
                default:
                    return $"audio/{witAudioType.ToString().ToLower()}";
            }
        }

        /// <summary>
        /// Method for obtaining audio Mime string for TTSWitAudioType
        /// </summary>
        public static string GetAudioExtension(TTSWitAudioType witAudioType, bool includeEvents)
        {
            string ext;
            switch (witAudioType)
            {
                case TTSWitAudioType.MPEG:
                    ext = ".mp3";
                    break;
                case TTSWitAudioType.PCM:
                    ext = ".raw";
                    break;
                case TTSWitAudioType.OPUS:
                    ext = ".opusd"; // Add d due to custom wit encoding
                    break;
                case TTSWitAudioType.WAV:
                default:
                    ext = $".{witAudioType.ToString().ToLower()}";
                    break;
            }
            if (includeEvents)
            {
                ext += WitConstants.ENDPOINT_TTS_EVENT_EXTENSION;
            }
            return ext;
        }

        /// <summary>
        /// Instantiate an audio decoder based on the wit audio type that allows for decoding directly from wit.
        /// </summary>
        /// <param name="witAudioType">The audio type supported by wit</param>
        public static IAudioDecoder GetTtsAudioDecoder(TTSWitAudioType witAudioType)
        {
            switch (witAudioType)
            {
                case TTSWitAudioType.PCM:
                    return new AudioDecoderPcm(AudioDecoderPcmType.Int16);
                case TTSWitAudioType.MPEG:
                    return new AudioDecoderMp3();
                case TTSWitAudioType.WAV:
                    return new AudioDecoderWav();
                case TTSWitAudioType.OPUS:
                    return new AudioDecoderOpus(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE);
            }
            throw new ArgumentException($"{witAudioType} audio decoder not supported");
        }

        /// <summary>
        /// Instantiate an audio decoder based on the wit audio type that allows for decoding directly from wit.
        /// </summary>
        /// <param name="witAudioType">The audio type supported by wit</param>
        /// <param name="onEventsDecoded">If this delegate is provided then the feed will be decoded
        /// for audio event data as well.</param>
        public static IAudioDecoder GetTtsAudioDecoder(TTSWitAudioType witAudioType,
            AudioJsonDecodeDelegate onEventsDecoded)
        {
            var audioDecoder = GetTtsAudioDecoder(witAudioType);
            if (audioDecoder != null && onEventsDecoded != null)
            {
                return new AudioDecoderJson(audioDecoder, onEventsDecoded);
            }
            return audioDecoder;
        }
        #endregion TTS
    }
}
