/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.Voice.Audio.Decoding;
using Meta.Voice.Net.Encoding.Wit;

namespace Meta.Voice.Net.WebSockets.Requests
{
    /// <summary>
    /// Performs a single authentication request
    /// </summary>
    public class WitWebSocketTtsRequest : WitWebSocketJsonRequest
    {
        /// <summary>
        /// Accessor for text to speak
        /// </summary>
        public string TextToSpeak { get; }
        /// <summary>
        /// Accessor for voice setting values
        /// </summary>
        public Dictionary<string, string> VoiceSettings { get; }
        /// <summary>
        /// Accessor for audio type
        /// </summary>
        public TTSWitAudioType AudioType { get; }
        /// <summary>
        /// Accessor for whether or not events should be requested
        /// </summary>
        public bool UseEvents { get; }
        /// <summary>
        /// If set, downloads decoded audio and events to this path.  Should include directory and
        /// file name without extension.
        /// </summary>
        public string DownloadPath { get; }

        /// <summary>
        /// Callback for sample received from service
        /// </summary>
        public Action<float[]> OnSamplesReceived;
        /// <summary>
        /// Callback for event json received from service
        /// </summary>
        public Action<WitResponseArray> OnEventsReceived;

        /// <summary>
        /// The audio decoder used to convert byte[] data to float[] samples
        /// </summary>
        private IAudioDecoder _audioDecoder;

        /// <summary>
        /// The file stream to be used for downloading audio files directly
        /// </summary>
        private FileStream _fileStream;

        /// <summary>
        /// Generates encoded chunk and applies reference data for all parameters
        /// </summary>
        public WitWebSocketTtsRequest(string textToSpeak, Dictionary<string, string> voiceSettings,
            TTSWitAudioType audioType, bool useEvents, string downloadPath = null)
            : base(GetTtsNode(textToSpeak, voiceSettings, audioType, useEvents))
        {
            TextToSpeak = textToSpeak;
            VoiceSettings = voiceSettings;
            AudioType = audioType;
            UseEvents = useEvents;
            DownloadPath = downloadPath;
            _audioDecoder = GetAudioDecoder(audioType);
            if (_audioDecoder != null)
            {
                _audioDecoder.Setup(WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE);
            }
        }

        /// <summary>
        /// Gets a json node from the provided tts data
        /// </summary>
        private static WitResponseNode GetTtsNode(string textToSpeak,
            Dictionary<string, string> voiceSettings, TTSWitAudioType audioType, bool useEvents)
        {
            WitResponseClass ttsNode = new WitResponseClass();
            WitResponseClass dataNode = new WitResponseClass();
            WitResponseClass synthNode = new WitResponseClass();
            synthNode[WitConstants.ENDPOINT_TTS_PARAM] = textToSpeak;
            foreach (var key in voiceSettings.Keys)
            {
                synthNode[key] = voiceSettings[key];
            }
            synthNode[WitConstants.WIT_SOCKET_ACCEPT_KEY] = WitConstants.GetAudioMimeType(audioType);
            if (useEvents)
            {
                synthNode[WitConstants.ENDPOINT_TTS_EVENTS] = new WitResponseData(true);
            }
            dataNode[WitConstants.ENDPOINT_TTS] = synthNode;
            ttsNode[WitConstants.WIT_SOCKET_DATA_KEY] = dataNode;
            return ttsNode;
        }

        /// <summary>
        /// Gets an audio decoder based on the audio type
        /// </summary>
        private static IAudioDecoder GetAudioDecoder(TTSWitAudioType audioType)
        {
            switch (audioType)
            {
                case TTSWitAudioType.PCM:
                    return new AudioDecoderPcm();
                case TTSWitAudioType.MPEG:
                    return new AudioDecoderMp3();
                default:
                    VLog.E(nameof(WitWebSocketTtsRequest), $"No audio decoder currently exists for '{audioType}' audio");
                    return null;
            }
        }

        /// <summary>
        /// Handles the download, decode and callbacks for audio & audio event data
        /// </summary>
        /// <param name="jsonData">Decoded json data object.</param>
        /// <param name="binaryData">Decoded binary data chunk which may be null or empty.</param>
        public override void HandleDownload(string jsonString, WitResponseNode jsonData, byte[] binaryData)
        {
            // Ignore if complete
            if (IsComplete || jsonData == null)
            {
                return;
            }
            // Begin downloading
            if (!IsDownloading)
            {
                HandleDownloadBegin();
            }

            // Callback for raw response
            ReturnRawResponse(jsonString);
            // Throw error if found
            SetResponseData(jsonData);
            if (!string.IsNullOrEmpty(Error))
            {
                HandleComplete();
                return;
            }

            try
            {
                // Append json events
                var events = jsonData[WitConstants.ENDPOINT_TTS_EVENTS]?.AsArray;
                if (events != null && events.Count > 0)
                {
                    OnEventsReceived?.Invoke(events);
                }

                // Append binary audio data
                if (binaryData != null && binaryData.Length > 0)
                {
                    // Decode samples & perform callback
                    if (OnSamplesReceived != null && _audioDecoder != null)
                    {
                        var samples = _audioDecoder.Decode(binaryData, 0, binaryData.Length);
                        OnSamplesReceived.Invoke(samples);
                    }
                }

                // If file stream exists
                if (_fileStream != null)
                {
                    // Encode with events
                    if (events != null)
                    {
                        var witChunk = new WitChunk()
                        {
                            jsonData = events,
                            binaryData = binaryData
                        };
                        var encodedWitChunk = WitChunkConverter.Encode(witChunk);
                        _fileStream.Write(encodedWitChunk, 0, encodedWitChunk.Length);
                    }
                    // Encode binary only
                    else if (binaryData != null)
                    {
                        _fileStream.Write(binaryData, 0, binaryData.Length);
                    }
                }
            }
            catch (Exception e)
            {
                VLog.E(GetType().Name, $"Decode Response Failed\n{ToString()}\n{e}");
            }

            // Check for end of stream
            bool endOfStream = jsonData[WitConstants.WIT_SOCKET_END_KEY]?.AsBool ?? false;
            if (endOfStream)
            {
                HandleComplete();
            }
        }

        /// <summary>
        /// Method called for first response handling to mark download begin,
        /// setup file strean & perform first response callback on main thread.
        /// </summary>
        protected override void HandleDownloadBegin()
        {
            // Download began
            base.HandleDownloadBegin();

            // Ignore without download path
            if (string.IsNullOrEmpty(DownloadPath))
            {
                return;
            }
            // Get directory & ensure it exists
            string downloadDirectory = Path.GetDirectoryName(DownloadPath);
            if (!Directory.Exists(downloadDirectory))
            {
                VLog.E(nameof(WitWebSocketTtsRequest), $"Tts download file directory does not exist\nPath: {downloadDirectory}\n{ToString()}");
                return;
            }
            try
            {
                // Get file name
                string downloadFileName = Path.GetFileNameWithoutExtension(DownloadPath);
                string audioExt = WitConstants.GetAudioExtension(AudioType, UseEvents);
                string audioFilePath = Path.Join(downloadDirectory, downloadFileName + audioExt);
                // Create file stream
                _fileStream = new FileStream(audioFilePath, FileMode.Create);
            }
            catch (Exception e)
            {
                VLog.E(nameof(WitWebSocketTtsRequest), $"Tts download file stream generation failed\n{ToString()}\n{e}");
            }
        }

        /// <summary>
        /// Callback when last chunk has been downloaded
        /// </summary>
        protected override void HandleComplete()
        {
            // Close both file streams if they exist
            if (_fileStream != null)
            {
                _fileStream.Close();
                _fileStream = null;
            }
            // Handle complete
            base.HandleComplete();
        }

        /// <summary>
        /// Provides information of all parameters
        /// </summary>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendLine(base.ToString());
            result.AppendLine($"Text: {TextToSpeak}");
            result.AppendLine($"Voice Settings");
            foreach (var key in VoiceSettings.Keys)
            {
                result.AppendLine($"\t{key}: {VoiceSettings[key]}");
            }
            result.AppendLine($"Audio Type: {AudioType}");
            result.AppendLine($"Use Events: {UseEvents}");
            result.AppendLine($"Download Path: {(string.IsNullOrEmpty(DownloadPath) ? "-" : DownloadPath)}");
            return result.ToString();
        }
    }
}
