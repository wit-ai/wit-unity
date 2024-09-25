/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Logging;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;
using UnityEngine;

namespace Meta.WitAi.TTS.Debugger
{
    /// <summary>
    /// A script for generating TTS pcm 16 files for every streamed tts file
    /// </summary>
    public class TTSDebugger : MonoBehaviour, ILogSource
    {
        [Tooltip("The TTS service that will generate tts output files")]
        [SerializeField] private TTSService _service;

        [Tooltip("The location within the Assets directory that will output all tts files")]
        [SerializeField] private string _outputDirectory = "TtsDebugger";

        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Logging);

        /// <summary>
        /// Regex for cleaning file names
        /// </summary>
        private static Regex _fileCleanupRegex;

        /// <summary>
        /// Currently debugged stream data
        /// </summary>
        private ConcurrentDictionary<string, TTSDebuggerFileStream> _streams = new ConcurrentDictionary<string, TTSDebuggerFileStream>();

        /// <summary>
        /// On editor script add, grab service
        /// </summary>
        private void Reset()
        {
            if (!_service)
            {
                _service = gameObject.GetComponent<TTSService>();
            }
        }

        /// <summary>
        /// Generate static regex if not yet done
        /// </summary>
        private static void SetupRegex()
        {
            // Ignore if already created
            if (_fileCleanupRegex != null)
            {
                return;
            }
            // Generate regex
            string invalid = new string(Path.GetInvalidFileNameChars());
            string pattern = $"[{Regex.Escape(invalid)}]";
            _fileCleanupRegex = new Regex(pattern);
        }

        /// <summary>
        /// Finds service and adds listeners
        /// </summary>
        private void OnEnable()
        {
            if (!_service)
            {
                _service = gameObject.GetComponentInChildren<TTSService>();
            }
            SetListeners(true);
        }

        /// <summary>
        /// Removes listeners
        /// </summary>
        private void OnDisable()
        {
            SetListeners(false);
        }

        /// <summary>
        /// Adds or removes listeners
        /// </summary>
        private void SetListeners(bool add)
        {
            if (!_service)
            {
                return;
            }
            _service.Events.Stream.OnStreamBegin.SetListener(OnStreamBegin, add);
            _service.Events.Stream.OnStreamComplete.SetListener(OnStreamComplete, add);
        }

        private string GetClipName(TTSClipData clipData) => $"{clipData.clipID}{clipData.extension}";

        private void OnStreamBegin(TTSClipData clipData)
        {
            // Get clip name
            var clipName = GetClipName(clipData);

            // Create directory if needed
            #if UNITY_EDITOR
            var directory = $"{Application.dataPath.Replace("Assets", "")}{_outputDirectory}";
            #else
            var directory = $"{Application.persistentDataPath}/{_outputDirectory}";
            #endif
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Remove invalid characters
            SetupRegex();
            var id = _fileCleanupRegex.Replace(clipData.clipID, string.Empty).ToLower();

            // Delete file if needed
            var date = DateTime.Now;
            var filePath = string.Format("{0}/{1}_{2}_{3:0000}{4:00}{5:00}_{6:00}{7:00}",
                directory,
                id,
                clipData.extension.Substring(1),
                date.Year,
                date.Month,
                date.Day,
                date.Hour,
                date.Minute);

            // Log and begin debugging
            var stream = new TTSDebuggerFileStream(filePath);
            Logger.Info("TTS Debugger - Begin\nId: {0}\nText: {1}\nVoice: {2}\nFile Type: {3}\nPath: {4}\n{5}{6}",
                id,
                clipData?.textToSpeak ?? "Null",
                clipData?.voiceSettings?.UniqueId ?? "Null",
                clipData?.extension ?? "Null",
                stream.FilePath);

            clipData.clipStream.OnAddSamples += stream.AddSamples;
            clipData.Events.OnEventJsonAdded += stream.AddEvent;
            _streams[clipName] = stream;
        }

        private void OnStreamComplete(TTSClipData clipData)
        {
            var clipName = GetClipName(clipData);
            if (!_streams.TryRemove(clipName, out var stream))
            {
                return;
            }

            // Generate info json
            var info = new WitResponseClass();
            info["requestId"] = new WitResponseData(clipData?.queryRequestId ?? "Null");
            info["fileType"] = new WitResponseData(clipData?.extension?.Substring(1) ?? "Null");
            info["clipId"] = new WitResponseData(clipData?.clipID ?? "Null");
            info["textToSpeak"] = new WitResponseData(clipData?.textToSpeak ?? "Null");
            info["readyDuration"] = new WitResponseData($"{clipData?.readyDuration:0.00} seconds");
            info["completeDuration"] = new WitResponseData($"{clipData?.completeDuration:0.00} seconds");
            info["length"] = new WitResponseData($"{clipData?.clipStream?.Length:0.00} seconds");
            var voiceSettings = new WitResponseClass();
            foreach (var voiceSetting in clipData?.voiceSettings.EncodedValues)
            {
                voiceSettings[voiceSetting.Key] = new WitResponseData(voiceSetting.Value);
            }
            info["voiceSettings"] = voiceSettings;
            var events = new WitResponseArray();
            for (int i = 0; i < stream.EventNodes.Count; i++)
            {
                events[i] = stream.EventNodes[i];
            }
            info["events"] = events;
            var json = info.ToString();

            // Log info & save
            Logger.Info("TTS Debugger - Complete\nText: {0}\nVoice: {1}\nFile Type: {2}\nPath: {3}",
                clipData?.textToSpeak ?? "Null",
                clipData?.voiceSettings?.UniqueId ?? "Null",
                clipData?.extension ?? "Null",
                stream.FilePath);

            // Save
            var path = stream.FilePath + ".json";
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.WriteAllText(path, json);

            // Dispose
            clipData.clipStream.OnAddSamples -= stream.AddSamples;
            clipData.Events.OnEventJsonAdded -= stream.AddEvent;
            stream.Dispose();
        }

        private class TTSDebuggerFileStream
        {
            public readonly string FilePath;
            public readonly List<WitResponseNode> EventNodes;
            private readonly FileStream _audioStream;

            public TTSDebuggerFileStream(string filePath)
            {
                // Set path
                FilePath = filePath;

                // Audio stream
                var path = FilePath + ".raw";
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                _audioStream = new FileStream(path, FileMode.Create);

                // Event stream
                EventNodes = new List<WitResponseNode>();
            }
            public void AddSamples(float[] samples, int offset, int length)
            {
                for (int i = 0; i < length; i++)
                {
                    var sample = (short) Mathf.Clamp(samples[offset + i] * short.MaxValue, short.MinValue, short.MaxValue);
                    _audioStream.Write(BitConverter.GetBytes(sample));
                }
            }
            public void AddEvent(WitResponseNode ttsEvent)
            {
                EventNodes.Add(ttsEvent);
            }
            public void Dispose()
            {
                EventNodes.Clear();
                _audioStream.Close();
            }
        }
    }
}
