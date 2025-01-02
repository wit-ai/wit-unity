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
using System.Threading.Tasks;
using Meta.Voice.Audio.Decoding;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A Wit VRequest subclass for handling TTS requests
    /// </summary>
    internal class WitTTSVRequest : WitVRequest
    {
        // The text to be requested
        public string TextToSpeak { get; set; }
        // The text settings
        public Dictionary<string, string> TtsParameters { get; set; }

        // The audio type to be used
        public TTSWitAudioType FileType { get; set; }
        // Whether audio should stream or not
        public bool Stream { get; set; }
        // Whether audio data should include events
        public bool UseEvents { get; set; }

        /// <summary>
        /// Audio decoder to be used
        /// </summary>
        private IAudioDecoder _decoder;

        /// <summary>
        /// Constructor for wit based text-to-speech VRequests
        /// </summary>
        /// <param name="configuration">The configuration interface to be used</param>
        /// <param name="requestId">A unique identifier that can be used to track the request</param>
        public WitTTSVRequest(IWitRequestConfiguration configuration,
            string requestId) : base(configuration, requestId, false)
        {
        }

        // Add headers to all requests
        protected override Dictionary<string, string> GetHeaders()
        {
            Dictionary<string, string> headers = base.GetHeaders();
            headers[WitConstants.HEADER_GET_CONTENT] = WitRequestSettings.GetAudioMimeType(FileType);
            return headers;
        }

        /// <summary>
        /// Requests stream from a disk location
        /// </summary>
        /// <param name="onSamplesDecoded">Called one or more times as audio samples are decoded.</param>
        /// <param name="onJsonDecoded">Called one or more times as json data is decoded.</param>
        public async Task<VRequestResponse<bool>> RequestStreamFromDisk(string diskPath,
            AudioSampleDecodeDelegate onSamplesDecoded,
            AudioJsonDecodeDelegate onJsonDecoded)
        {
            Url = $"{FilePrepend}{diskPath}";
            Method = VRequestMethod.HttpGet;
            _decoder = WitRequestSettings.GetTtsAudioDecoder(FileType, UseEvents ? onJsonDecoded : null);
            await ThreadUtility.CallOnMainThread(() =>
            {
                Downloader = new AudioStreamHandler(_decoder, onSamplesDecoded);
            });
            return await Request(DecodeSuccess);
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// provided audio clip stream.
        /// </summary>
        /// <param name="onSamplesDecoded">Called one or more times as audio samples are decoded.</param>
        /// <param name="onJsonDecoded">Called one or more times as json data is decoded.</param>
        public async Task<VRequestResponse<bool>> RequestStream(AudioSampleDecodeDelegate onSamplesDecoded,
            AudioJsonDecodeDelegate onJsonDecoded)
        {
            var errors = await SetupTts(false, onSamplesDecoded, onJsonDecoded);
            if (!string.IsNullOrEmpty(errors))
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL, errors);
            }
            return await Request(DecodeSuccess);
        }

        /// <summary>
        /// Performs a wit tts request that streams audio data into the
        /// a specific path on disk.
        /// </summary>
        /// <param name="downloadPath">Path to download the audio clip to</param>
        public async Task<VRequestResponse<bool>> RequestDownload(string downloadPath)
        {
            // Setup with errors
            var errors = await SetupTts(true, null, null);
            if (!string.IsNullOrEmpty(errors))
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL, errors);
            }

            // Perform an audio stream request
            return await RequestFileDownload(Url, downloadPath);
        }

        // Internal base method for tts request
        private async Task<string> SetupTts(bool download,
            AudioSampleDecodeDelegate onSamplesDecoded,
            AudioJsonDecodeDelegate onJsonDecoded)
        {
            // Error check
            string errors = GetWebErrors(download);
            if (!string.IsNullOrEmpty(errors))
            {
                return errors;
            }

            // Encode post data
            byte[] postData = EncodePostData();
            if (postData == null)
            {
                return WitConstants.ERROR_TTS_DECODE;
            }

            // Set url, method type and content type
            Url = Configuration.GetEndpointInfo().Synthesize;
            Method = VRequestMethod.HttpPost;
            ContentType = WitConstants.ENDPOINT_JSON_MIME;

            // Generate decoder
            if (!download)
            {
                _decoder = WitRequestSettings.GetTtsAudioDecoder(FileType, UseEvents ? onJsonDecoded : null);
            }

            // Set upload handler and download handler if applicable
            await ThreadUtility.CallOnMainThread(() =>
            {
                Uploader = new UploadHandlerRaw(postData);
                if (!download)
                {
                    Downloader = new AudioStreamHandler(_decoder, onSamplesDecoded);
                }
            });

            // Success
            return string.Empty;
        }

        // Performs web error check locally
        private string GetWebErrors(bool downloadOnly = false)
        {
            // Get errors
            string errors = WitRequestSettings.GetTtsErrors(TextToSpeak, Configuration);
            // Warn if incompatible with streaming
            if (!downloadOnly && Stream)
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    Logger.Warning("Wit cannot currently stream TTS in WebGL");
                    Stream = false;
                }
                else if (!WitRequestSettings.CanStreamAudio(FileType))
                {
                    Logger.Warning("Wit cannot stream {0} files please use {1} instead.",
                        FileType,
                        WitConstants.TTS_TYPE_DEFAULT);
                    Stream = false;
                }
            }
            // Return errors
            return errors;
        }

        // Encode tts post bytes
        private byte[] EncodePostData()
        {
            var ttsData = new Dictionary<string, string>();
            ttsData[WitConstants.ENDPOINT_TTS_PARAM] = TextToSpeak;
            ttsData[WitConstants.ENDPOINT_TTS_EVENTS] = UseEvents.ToString().ToLower();
            if (TtsParameters != null)
            {
                foreach (var item in TtsParameters)
                {
                    ttsData[item.Key] = item.Value;
                }
            }
            string jsonString = JsonConvert.SerializeObject(ttsData);
            return Encoding.UTF8.GetBytes(jsonString);
        }
    }
}
