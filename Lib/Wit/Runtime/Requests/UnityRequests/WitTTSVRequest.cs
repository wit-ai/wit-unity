/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

// Uncomment when added to Wit.ai
//#define OGG_SUPPORT

using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.Voice.Audio;
using Meta.Voice.Audio.Decoding;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.WitAi.Requests
{
    // Supported audio types
    public enum TTSWitAudioType
    {
        PCM = 0,
        MPEG = 1,
        #if OGG_SUPPORT
        OGG = 3,
        #endif
        WAV = 2
    }

    /// <summary>
    /// A Wit VRequest subclass for handling TTS requests
    /// </summary>
    public class WitTTSVRequest : WitVRequest
    {
        // The text to be requested
        public string TextToSpeak { get; }
        // The text settings
        public Dictionary<string, string> TtsData { get; }

        // The audio type to be used
        public TTSWitAudioType FileType { get; }
        // Whether audio should stream or not
        public bool Stream { get; private set; }

        // Whether audio data should include events
        public bool UseTextStream { get; }
        // Callback when events are decoded within audio stream
        public AudioTextDecodeDelegate OnTextDecoded { get; }

        /// <summary>
        /// Constructor for wit based text-to-speech VRequests
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        /// <param name="requestId">A unique identifier that can be used to track the request</param>
        /// <param name="textToSpeak">The text to be spoken by the request</param>
        /// <param name="ttsData">The text parameters used for the request</param>
        /// <param name="audioFileType">The expected audio file type of the request</param>
        /// <param name="audioStream">Whether the audio should be played while streaming or should wait until completion.</param>
        /// <param name="onDownloadProgress">The callback for progress related to downloading</param>
        /// <param name="onFirstResponse">The callback for the first response of data from a request</param>
        /// <param name="useEvents">Whether or not events should be requested with this audio request</param>
        /// <param name="onTextDecoded">Whether or not events should be requested with this audio request</param>
        public WitTTSVRequest(IWitRequestConfiguration configuration, string requestId, string textToSpeak,
            Dictionary<string, string> ttsData, TTSWitAudioType audioFileType, bool audioStream = false,
            RequestProgressDelegate onDownloadProgress = null,
            RequestFirstResponseDelegate onFirstResponse = null,
            bool useTextStream = false, AudioTextDecodeDelegate onTextDecoded = null)
            : base(configuration, requestId, false, onDownloadProgress, onFirstResponse)
        {
            TextToSpeak = textToSpeak;
            TtsData = ttsData;
            FileType = audioFileType;
            Stream = audioStream;
            UseTextStream = useTextStream;
            OnTextDecoded = onTextDecoded;
            Timeout = WitConstants.ENDPOINT_TTS_TIMEOUT;
        }

        // Add headers to all requests
        protected override Dictionary<string, string> GetHeaders()
        {
            Dictionary<string, string> headers = base.GetHeaders();
            headers[WitConstants.HEADER_POST_CONTENT] = "application/json";
            headers[WitConstants.HEADER_GET_CONTENT] = GetAudioMimeType(FileType);
            return headers;
        }

        // Performs web error check locally
        private string GetWebErrors(bool downloadOnly = false)
        {
            // Get errors
            string errors = GetWebErrors(TextToSpeak, Configuration);
            // Warn if incompatible with streaming
            if (!downloadOnly && Stream)
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    VLog.W($"Wit cannot currently stream TTS in WebGL");
                    Stream = false;
                }
                else if (!CanStreamAudio(FileType))
                {
                    VLog.W($"Wit cannot stream {FileType} files please use {TTSWitAudioType.PCM} instead.");
                    Stream = false;
                }
            }
            // Return errors
            return errors;
        }

        /// <summary>
        /// Method for determining if there are problems that will arise
        /// with performing a web request prior to doing so
        /// </summary>
        public static string GetWebErrors(string textToSpeak, IWitRequestConfiguration configuration)
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
            #if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            // Mobile network reachability check
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return WitConstants.ERROR_REACHABILITY;
            }
            #endif
            // Should be good
            return string.Empty;
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// provided audio clip stream.
        /// </summary>
        /// <param name="clipStream">The audio clip stream used
        /// to handle audio data caching</param>
        /// <param name="onClipReady">The callback when the audio
        /// clip stream is ready for playback</param>
        /// <returns>An error string if applicable</returns>
        public string RequestStream(IAudioClipStream clipStream,
            RequestCompleteDelegate<IAudioClipStream> onClipReady)
        {
            // Error check
            string errors = GetWebErrors();
            if (!string.IsNullOrEmpty(errors))
            {
                onClipReady?.Invoke(clipStream, errors);
                return errors;
            }

            // Encode post data
            byte[] bytes = EncodePostData(TextToSpeak, UseTextStream, TtsData);
            if (bytes == null)
            {
                errors = WitConstants.ERROR_TTS_DECODE;
                onClipReady?.Invoke(clipStream, errors);
                return errors;
            }

            // Get tts unity request
            UnityWebRequest unityRequest = GetUnityRequest(bytes);

            // Perform an audio stream request
            if (!RequestAudioStream(clipStream, unityRequest, onClipReady, GetAudioType(FileType), Stream, UseTextStream, OnTextDecoded))
            {
                return "Failed to start audio stream";
            }
            return string.Empty;
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// provided audio clip stream & returns once the request is ready
        /// </summary>
        /// <param name="clipStream">The audio clip stream used
        /// to handle audio data caching</param>
        /// <param name="onClipReady">The callback when the audio
        /// clip stream is ready for playback</param>
        /// <returns>An error string if applicable</returns>
        public async Task<RequestCompleteResponse<IAudioClipStream>> RequestStreamAsync(IAudioClipStream clipStream)
        {
            // Error check
            string errors = GetWebErrors();
            if (!string.IsNullOrEmpty(errors))
            {
                return new RequestCompleteResponse<IAudioClipStream>(clipStream, errors);
            }

            // Async encode
            var bytes = await EncodePostBytesAsync(TextToSpeak, UseTextStream, TtsData);
            if (bytes == null)
            {
                errors = WitConstants.ERROR_TTS_DECODE;
                return new RequestCompleteResponse<IAudioClipStream>(clipStream, errors);
            }

            // Get tts unity request
            UnityWebRequest unityRequest = GetUnityRequest(bytes);

            // Perform request async
            return await RequestAudioStreamAsync(clipStream, unityRequest, GetAudioType(FileType), Stream, UseTextStream, OnTextDecoded);
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// a specific path on disk.
        /// </summary>
        /// <param name="downloadPath">Path to download the audio clip to</param>
        /// <param name="onComplete">The callback when the clip is
        /// either completely downloaded or failed to download</param>
        /// <returns>An error string if applicable</returns>
        public string RequestDownload(string downloadPath,
            RequestCompleteDelegate<bool> onComplete)
        {
            // Error check
            string errors = GetWebErrors(true);
            if (!string.IsNullOrEmpty(errors))
            {
                onComplete?.Invoke(false, errors);
                return errors;
            }

            // Encode post data
            byte[] bytes = EncodePostData(TextToSpeak, UseTextStream, TtsData);
            if (bytes == null)
            {
                errors = WitConstants.ERROR_TTS_DECODE;
                onComplete?.Invoke(false, errors);
                return errors;
            }

            // Get tts unity request
            UnityWebRequest unityRequest = GetUnityRequest(bytes);

            // Perform an audio download request
            if (!RequestFileDownload(downloadPath, unityRequest, onComplete))
            {
                return "Failed to start audio stream";
            }
            return string.Empty;
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// a specific path on disk and asynchronously returns any errors
        /// encountered once complete
        /// </summary>
        /// <param name="downloadPath">Path to download the audio clip to</param>
        /// <returns>An error string if applicable</returns>
        public async Task<string> RequestDownloadAsync(string downloadPath)
        {
            // Error check
            string errors = GetWebErrors(true);
            if (!string.IsNullOrEmpty(errors))
            {
                return errors;
            }

            // Async encode
            byte[] bytes = await EncodePostBytesAsync(TextToSpeak, UseTextStream, TtsData);
            if (bytes == null)
            {
                return WitConstants.ERROR_TTS_DECODE;
            }

            // Get tts unity request
            UnityWebRequest unityRequest = GetUnityRequest(bytes);

            // Perform request async
            return await RequestFileDownloadAsync(downloadPath, unityRequest);
        }

        // Encode post bytes async
        private async Task<byte[]> EncodePostBytesAsync(string textToSpeak, bool useEvents, Dictionary<string, string> ttsData)
        {
            byte[] results = null;
            await Task.Run(() => results = EncodePostData(textToSpeak, useEvents, ttsData));
            return results;
        }

        // Encode tts post bytes
        private byte[] EncodePostData(string textToSpeak, bool useEvents, Dictionary<string, string> ttsData)
        {
            if (ttsData == null)
            {
                ttsData = new Dictionary<string, string>();
            }
            ttsData[WitConstants.ENDPOINT_TTS_PARAM] = textToSpeak;
            ttsData[WitConstants.ENDPOINT_TTS_EVENTS] = useEvents.ToString().ToLower();
            string jsonString = JsonConvert.SerializeObject(ttsData);
            return Encoding.UTF8.GetBytes(jsonString);
        }

        // Internal base method for tts request
        private UnityWebRequest GetUnityRequest(byte[] postData)
        {
            // Get uri
            Uri uri = GetUri(Configuration.GetEndpointInfo().Synthesize);

            // Generate request
            UnityWebRequest unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);

            // Add upload handler
            unityRequest.uploadHandler = new UploadHandlerRaw(postData);

            // Perform json request
            return unityRequest;
        }

        // Cast audio type
        public static TTSWitAudioType GetWitAudioType(AudioType audioType)
        {
            switch (audioType)
            {
                #if OGG_SUPPORT
                case AudioType.OGGVORBIS:
                    return TTSWitAudioType.OGGVORBIS;
                #endif
                case AudioType.MPEG:
                    return TTSWitAudioType.MPEG;
                case AudioType.WAV:
                    return TTSWitAudioType.WAV;
                default:
                    return TTSWitAudioType.PCM;
            }
        }
        // Cast audio type
        public static AudioType GetAudioType(TTSWitAudioType witAudioType)
        {
            switch (witAudioType)
            {
                #if OGG_SUPPORT
                case TTSWitAudioType.OGG:
                    return AudioType.OGGVORBIS;
                #endif
                case TTSWitAudioType.MPEG:
                    return AudioType.MPEG;
                case TTSWitAudioType.WAV:
                    return AudioType.WAV;
                // Custom implementation
                case TTSWitAudioType.PCM:
                default:
                    return AudioType.UNKNOWN;
            }
        }
        // Get audio type
        public static string GetAudioMimeType(TTSWitAudioType witAudioType)
        {
            switch (witAudioType)
            {
                // PCM
                case TTSWitAudioType.PCM:
                    return "audio/raw";
                #if OGG_SUPPORT
                // OGG
                case TTSWitAudioType.OGG:
                #endif
                // MP3 & WAV
                case TTSWitAudioType.MPEG:
                case TTSWitAudioType.WAV:
                default:
                    return $"audio/{witAudioType.ToString().ToLower()}";
            }
        }
        /// <summary>
        /// Performs a check on whether audio can be streamed or not
        /// </summary>
        public static bool CanStreamAudio(TTSWitAudioType witAudioType) =>
            CanStreamAudio(GetAudioType(witAudioType));
    }
}
