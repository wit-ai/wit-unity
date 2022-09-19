/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Meta.WitAi.Json;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi
{
    /// <summary>
    /// Endpoint overrides
    /// </summary>
    [Serializable]
    public struct WitRequestEndpointOverride
    {
        public string uriScheme;
        public string authority;
        public string witApiVersion;
        public int port;
    }

    /// <summary>
    /// Configuration interface
    /// </summary>
    public interface IWitRequestConfiguration
    {
        string GetConfigurationId();
        string GetApplicationId();
        WitAppInfo GetApplicationInfo();
        WitRequestEndpointOverride GetEndpointOverrides();
        string GetClientAccessToken();
        #if UNITY_EDITOR
        void SetClientAccessToken(string newToken);
        string GetServerAccessToken();
        void SetApplicationInfo(WitAppInfo appInfo);
        #endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// A simple configuration for initial setup
    /// </summary>
    public class WitServerRequestConfiguration : IWitRequestConfiguration
    {
        private string _clientToken;
        private string _serverToken;
        public WitServerRequestConfiguration(string serverToken)
        {
            _serverToken = serverToken;
        }
        public string GetConfigurationId() => null;
        public string GetApplicationId() => null;
        public WitAppInfo GetApplicationInfo() => new WitAppInfo();
        public void SetApplicationInfo(WitAppInfo newInfo) {}
        public WitRequestEndpointOverride GetEndpointOverrides() => new WitRequestEndpointOverride();
        public string GetClientAccessToken() => _clientToken;
        public void SetClientAccessToken(string newToken) => _clientToken = newToken;
        public string GetServerAccessToken() => _serverToken;
    }
#endif

    public static class WitRequestUtility
    {
        // Wit service endpoint info
        public const string WIT_URI_SCHEME = "https";
        public const string WIT_URI_AUTHORITY = "api.wit.ai";
        public const int WIT_URI_DEFAULT_PORT = -1;
        // Headers
        public const string WIT_REQUEST_ID_KEY = "X-Wit-Client-Request-Id";
        public const string WIT_USER_AGENT_KEY = "User-Agent";

        // Wit service version info
        public const string WIT_API_VERSION = "20220728";
        public const string WIT_SDK_VERSION = "0.0.49";

        // Wit audio clip name
        public const string WIT_CLIP_NAME = "WIT_AUDIO_CLIP";

        /// <summary>
        /// Uri customization delegate
        /// </summary>
        public static event Func<UriBuilder, Uri> OnProvideCustomUri;
        /// <summary>
        /// Header customization delegate
        /// </summary>
        public static event Action<Dictionary<string, string>> OnProvideCustomHeaders;
        /// <summary>
        /// User agent customization delegate
        /// </summary>
        public static event Action<StringBuilder> OnProvideCustomUserAgent;

        #region SHARED
        /// <summary>
        /// Get custom wit uri using a specific path & query parameter
        /// </summary>
        public static Uri GetWitUri(string path, Dictionary<string, string> queryParams, IWitRequestConfiguration configuration)
        {
            // Uri builder
            UriBuilder uriBuilder = new UriBuilder();

            // Append endpoint data
            WitRequestEndpointOverride endpoint = configuration.GetEndpointOverrides();
            uriBuilder.Scheme = string.IsNullOrEmpty(endpoint.uriScheme) ? WIT_URI_SCHEME : endpoint.uriScheme;
            uriBuilder.Host = string.IsNullOrEmpty(endpoint.authority) ? WIT_URI_AUTHORITY : endpoint.authority;
            uriBuilder.Port = endpoint.port <= 0 ?  WIT_URI_DEFAULT_PORT : endpoint.port;
            string apiVersion = string.IsNullOrEmpty(endpoint.witApiVersion) ? WIT_API_VERSION : endpoint.witApiVersion;

            // Set path
            uriBuilder.Path = path;

            // Build query
            uriBuilder.Query = $"v={apiVersion}";
            if (queryParams != null)
            {
                foreach (string key in queryParams.Keys)
                {
                    uriBuilder.Query += $"&{key}={queryParams[key]}";
                }
            }

            // Return custom uri
            if (OnProvideCustomUri != null)
            {
                return OnProvideCustomUri(uriBuilder);
            }

            // Return uri
            return uriBuilder.Uri;
        }
        /// <summary>
        /// Obtain headers to be used with every wit service
        /// </summary>
        public static Dictionary<string, string> GetWitHeaders(IWitRequestConfiguration configuration, bool useServerToken)
        {
            // Get headers
            Dictionary<string, string> headers = new Dictionary<string, string>();

            // Set request id
            headers[WIT_REQUEST_ID_KEY] = Guid.NewGuid().ToString();
            // Set User-Agent
            headers[WIT_USER_AGENT_KEY] = GetUserAgentHeader(configuration);
            // Set authorization
            headers["Authorization"] = GetAuthorizationHeader(configuration, useServerToken);
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
        /// <param name="accessToken">Client or server access token</param>
        /// <returns></returns>
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
        // Build and return user agent header
        private static string _operatingSystem;
        private static string _deviceModel;
        private static string _appIdentifier;
        private static string _unityVersion;
        private static string GetUserAgentHeader(IWitRequestConfiguration configuration)
        {
            // Generate user agent
            StringBuilder userAgent = new StringBuilder();

            // Append wit sdk version
            userAgent.Append($"wit-unity-{WIT_SDK_VERSION}");

            // Append operating system
            if (_operatingSystem == null) _operatingSystem = UnityEngine.SystemInfo.operatingSystem;
            userAgent.Append($",{_operatingSystem}");
            // Append device model
            if (_deviceModel == null) _deviceModel = UnityEngine.SystemInfo.deviceModel;
            userAgent.Append($",{_deviceModel}");

            // Append configuration log id
            string logId = configuration.GetConfigurationId();
            if (string.IsNullOrEmpty(logId))
            {
                logId = "not-yet-configured";
            }
            userAgent.Append($",{logId}");

            // Append app identifier
            if (_appIdentifier == null) _appIdentifier = Application.identifier;
            userAgent.Append($",{_appIdentifier}");

            // Append editor identifier
            #if UNITY_EDITOR
            userAgent.Append(",Editor");
            #else
            userAgent.Append(",Runtime");
            #endif

            // Append unity version
            if (_unityVersion == null) _unityVersion = Application.unityVersion;
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
        /// <summary>
        /// Performs a wit request using a generated unity web request
        /// </summary>
        /// <param name="unityRequest">The generated web request</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer Request(UnityWebRequest unityRequest,
            IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<UnityWebRequest, string> onComplete)
        {
            // Invalid access token
            if (configuration == null)
            {
                onComplete?.Invoke(null, "Wit Configuration Required");
                return null;
            }

            // Apply all wit headers
            Dictionary<string, string> headers = GetWitHeaders(configuration, useServerToken);
            if (headers != null)
            {
                foreach (var key in headers.Keys)
                {
                    unityRequest.SetRequestHeader(key, headers[key]);
                }
            }

            // Generate performer
            RequestPerformer performer = new RequestPerformer();

            // Setup & begin request
            performer.Setup(unityRequest, onProgress, (request) =>
            {
                // Error
                if (!string.IsNullOrEmpty(request.error))
                {
                    VLog.W($"Request Failed\nUri: {unityRequest.uri}\nError: {request.error}");
                    onComplete?.Invoke(request, request.error);
                    return;
                }

                // Complete
                onComplete?.Invoke(request, null);
            });

            // Return performer
            return performer;
        }
        #endregion

        #region TEXT
        /// <summary>
        /// Performs a wit request using a generated unity web request
        /// </summary>
        /// <param name="unityRequest">The generated web request</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer TextRequest<DATA_TYPE>(UnityWebRequest unityRequest,
            IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<DATA_TYPE, string> onComplete)
        {
            unityRequest.SetRequestHeader("Content-Type", "application/json");
            return Request(unityRequest, configuration, useServerToken, onProgress, (request, error) =>
            {
                // Error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(default(DATA_TYPE), error);
                    return;
                }

                // TODO: Async Parse
                string jsonString = request.downloadHandler.text;
                WitResponseNode jsonResults = JsonConvert.DeserializeToken(jsonString);
                if (jsonResults == null)
                {
                    VLog.W($"Decode Failed\nUri: {unityRequest.uri}\nError: {request.error}\n\n{jsonString}");
                    onComplete?.Invoke(default(DATA_TYPE), request.error);
                    return;
                }

                // TODO: Async Deserialize
                DATA_TYPE jsonData = JsonConvert.DeserializeObject<DATA_TYPE>(jsonResults);
                onComplete?.Invoke(jsonData, null);
            });
        }

        /// <summary>
        /// Perform a get to a wit endpoint
        /// </summary>
        /// <param name="uriPath">The endpoint path</param>
        /// <param name="uriParams">The endpoint url parameters</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        /// <returns></returns>
        public static RequestPerformer GetRequest<DATA_TYPE>(string uriPath, Dictionary<string, string> uriParams,
            IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<DATA_TYPE, string> onComplete)
        {
            // Get uri
            Uri uri = GetWitUri(uriPath, uriParams, configuration);

            // Get request
            UnityWebRequest unityRequest = UnityWebRequest.Get(uri);

            // Make request
            return TextRequest(unityRequest, configuration, useServerToken, onProgress, onComplete);
        }

        /// <summary>
        /// Perform a post of byte[] data to a wit endpoint
        /// </summary>
        /// <param name="uriPath">The endpoint path</param>
        /// <param name="uriParams">The endpoint url parameters</param>
        /// <param name="postString">The byte[] to be posted as post contents</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer PostDataRequest<DATA_TYPE>(string uriPath, Dictionary<string, string> uriParams,
            byte[] postData, IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<DATA_TYPE, string> onComplete)
        {
            // Get uri
            Uri uri = GetWitUri(uriPath, uriParams, configuration);

            // Get request
            UnityWebRequest unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
            unityRequest.uploadHandler = new UploadHandlerRaw(postData);
            unityRequest.disposeUploadHandlerOnDispose = true;
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.disposeUploadHandlerOnDispose = true;

            // Make request
            return TextRequest(unityRequest, configuration, useServerToken, onProgress, onComplete);
        }

        /// <summary>
        /// Perform a post of string data to a wit endpoint
        /// </summary>
        /// <param name="uriPath">The endpoint path</param>
        /// <param name="uriParams">The endpoint url parameters</param>
        /// <param name="postString">The string to be posted as post contents</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer PostTextRequest<DATA_TYPE>(string uriPath, Dictionary<string, string> uriParams,
            string postString, IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<DATA_TYPE, string> onComplete)
        {
            return PostDataRequest(uriPath, uriParams, Encoding.UTF8.GetBytes(postString), configuration, useServerToken, onProgress, onComplete);
        }
        #endregion

        #region FILE
        /// <summary>
        /// Performs a wit download request
        /// </summary>
        /// <param name="downloadPath">The location to download to</param>
        /// <param name="unityRequest">The generated web request</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer DownloadRequest(string downloadPath,
            UnityWebRequest unityRequest, IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<string> onComplete)
        {
            // Get temporary path for download
            string tempDownloadPath = downloadPath + ".tmp";
            try
            {
                if (File.Exists(tempDownloadPath))
                {
                    File.Delete(tempDownloadPath);
                }
            }
            catch (Exception e)
            {
                VLog.W($"Deleting Download File Failed\nPath: {tempDownloadPath}\n{e}");
            }

            // Add download handler
            DownloadHandlerFile fileHandler = new DownloadHandlerFile(tempDownloadPath, true);
            unityRequest.downloadHandler = fileHandler;
            unityRequest.disposeDownloadHandlerOnDispose = true;

            // Perform request
            return Request(unityRequest, configuration, useServerToken, onProgress, (response, error) =>
            {
                try
                {
                    if (File.Exists(tempDownloadPath))
                    {
                        // For error, remove
                        if (!string.IsNullOrEmpty(error))
                        {
                            File.Delete(tempDownloadPath);
                        }
                        // For success, move to final path
                        else
                        {
                            File.Move(tempDownloadPath, downloadPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    VLog.W($"Moving Download File Failed\nFrom: {tempDownloadPath}\nTo: {downloadPath}\n{e}");
                }

                // Complete
                onComplete?.Invoke(error);
            });
        }
        /// <summary>
        /// Performs a wit audio request and returns an audio clip
        /// </summary>
        /// <param name="unityRequest">The generated web request</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <typeparam name="DATA_TYPE">The type return data should be casted to</typeparam>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer AudioStreamRequest(UnityWebRequest unityRequest,
            IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<AudioClip, string> onComplete)
        {
            // Add audio handler
            DownloadHandlerAudioClip streamHandler = new DownloadHandlerAudioClip(unityRequest.uri, TTSAudioType);
            streamHandler.compressed = true;
            streamHandler.streamAudio = true;
            unityRequest.downloadHandler = streamHandler;
            unityRequest.disposeDownloadHandlerOnDispose = true;

            // Perform request
            return Request(unityRequest, configuration, useServerToken, onProgress, (response, error) =>
            {
                // Failed
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                }
                // Success
                else
                {
                    // Get clip
                    AudioClip clip = null;
                    try
                    {
                        clip = DownloadHandlerAudioClip.GetContent(response);
                    }
                    // Exception
                    catch (Exception exception)
                    {
                        onComplete?.Invoke(null, $"Failed to decode audio clip\n{exception.ToString()}");
                        return;
                    }

                    // Not found
                    if (clip == null)
                    {
                        onComplete?.Invoke(null, "Failed to decode audio clip");
                    }
                    // Success
                    else
                    {
                        clip.name = WIT_CLIP_NAME;
                        onComplete?.Invoke(clip, string.Empty);
                    }
                }
            });
        }
        #endregion

        #region MESSAGE
        // Endpoint
        public const string WIT_ENDPOINT_MESSAGE = "message";
        public const string WIT_ENDPOINT_MESSAGE_PARAM = "q";

        /// <summary>
        /// Voice message request
        /// </summary>
        /// <param name="transcription">Text to be sent to message endpoint</param>
        /// <param name="configurationId">The configuration id used for logging</param>
        /// <param name="accessToken">The access token used for authentication</param>
        /// <param name="onProgress">The progress delegate</param>
        /// <param name="onComplete">The completion delegate</param>
        /// <returns>A request performer that can be used to cancel the request early</returns>
        public static RequestPerformer MessageRequest(string transcription,
            IWitRequestConfiguration configuration, bool useServerToken,
            Action<float> onProgress, Action<WitResponseNode, string> onComplete)
        {
            Dictionary<string, string> uriParams = new Dictionary<string, string>();
            uriParams[WIT_ENDPOINT_MESSAGE_PARAM] = transcription;
            return GetRequest(WIT_ENDPOINT_MESSAGE, uriParams, configuration, useServerToken, onProgress, onComplete);
        }
        #endregion

        #region TTS
        // Audio type for tts
        public static AudioType TTSAudioType = AudioType.WAV;
        // TTS End point
        public const string WIT_ENDPOINT_TTS = "synthesize";
        // TTS Text to speak parameter
        public const string WIT_ENDPOINT_TTS_PARAM = "q";
        // TTS Text to speak parameter
        public const int TTS_MAX_SIZE = 140;

        // TTS End point
        public const string WIT_ENDPOINT_TTS_VOICES = "voices";

        // Return error with text to speak if any are found
        public static string GetTextInvalidError(string textToSpeak)
        {
            // Ensure text exists
            if (string.IsNullOrEmpty(textToSpeak))
            {
                return "No text provided";
            }
            // Success
            return string.Empty;
        }

        // Get TTS request
        private static UnityWebRequest GetTTSRequest(string textToSpeak, Dictionary<string, string> ttsData,
            IWitRequestConfiguration configuration, StringBuilder error)
        {
            // Check text
            string textError = GetTextInvalidError(textToSpeak);
            if (!string.IsNullOrEmpty(textError))
            {
                error.Append($"TTS Request Failed\n{textError}");
                return null;
            }

            // Get uri
            Uri ttsUri = GetWitUri(WIT_ENDPOINT_TTS, null, configuration);

            // Serialize into json
            ttsData[WIT_ENDPOINT_TTS_PARAM] = textToSpeak;
            string jsonString = JsonConvert.SerializeObject(ttsData);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

            // Generate post
            UnityWebRequest request = new UnityWebRequest(ttsUri, UnityWebRequest.kHttpVerbPOST);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", $"audio/{TTSAudioType.ToString().ToLower()}");
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            return request;
        }

        // Request a TTS service download
        public static RequestPerformer RequestTTSDownload(string textToSpeak, Dictionary<string, string> ttsData,
            string downloadPath, IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<string> onDownloadComplete)
        {
            // Get tts request
            StringBuilder error = new StringBuilder();
            UnityWebRequest ttsRequest = GetTTSRequest(textToSpeak, ttsData, configuration, error);
            if (ttsRequest == null || error.Length > 0)
            {
                onDownloadComplete?.Invoke(error.ToString());
                return null;
            }

            // Perform download request
            return DownloadRequest(downloadPath, ttsRequest, configuration, false, onProgress,
                onDownloadComplete);
        }

        // Request a TTS service stream
        public static RequestPerformer RequestTTSStream(string textToSpeak, Dictionary<string, string> ttsData,
            IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<AudioClip, string> onClipReady)
        {
            // Get tts request
            StringBuilder error = new StringBuilder();
            UnityWebRequest ttsRequest = GetTTSRequest(textToSpeak, ttsData, configuration, error);
            if (ttsRequest == null || error.Length > 0)
            {
                onClipReady?.Invoke(null, error.ToString());
                return null;
            }

            // Perform download request
            return AudioStreamRequest(ttsRequest, configuration, false, onProgress, onClipReady);
        }

        // Request TTS voices
        public static RequestPerformer RequestTTSVoices<VOICE_DATA>(IWitRequestConfiguration configuration,
            Action<float> onProgress, Action<Dictionary<string, VOICE_DATA[]>, string> onComplete)
        {
            return GetRequest(WIT_ENDPOINT_TTS_VOICES, null, configuration, true, onProgress, onComplete);
        }
        #endregion
    }
}
