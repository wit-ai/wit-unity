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
using System.Threading.Tasks;
using Meta.Voice.Logging;
using UnityEngine;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Events;
using Meta.WitAi.TTS.Interfaces;
using Meta.WitAi.Utilities;
using Meta.WitAi.Requests;

namespace Meta.WitAi.TTS.Integrations
{
    [LogCategory(LogCategory.TextToSpeech)]
    public class TTSDiskCache : MonoBehaviour, ITTSDiskCacheHandler
    {
        [Header("Disk Cache Settings")]
        /// <summary>
        /// The relative path from the DiskCacheLocation in TTSDiskCacheSettings
        /// </summary>
        [SerializeField] private string _diskPath = "TTS/";
        public string DiskPath => _diskPath;

        /// <summary>
        /// The cache default settings
        /// </summary>
        [SerializeField] private TTSDiskCacheSettings _defaultSettings = new TTSDiskCacheSettings();
        public TTSDiskCacheSettings DiskCacheDefaultSettings => _defaultSettings;

        /// <summary>
        /// The cache streaming events
        /// </summary>
        [SerializeField] private TTSStreamEvents _events = new TTSStreamEvents();
        public TTSStreamEvents DiskStreamEvents
        {
            get => _events;
            set { _events = value; }
        }

        // All currently performing stream requests
        private Dictionary<string, VRequest> _streamRequests = new Dictionary<string, VRequest>();
        // Log
        private IVLogger _log = LoggerRegistry.Instance.GetLogger();

        // Cancel all requests
        protected virtual void OnDestroy()
        {
            Dictionary<string, VRequest> requests = _streamRequests;
            _streamRequests.Clear();
            foreach (var request in requests.Values)
            {
                request.Cancel();
            }
        }

        /// <summary>
        /// Builds full cache path
        /// </summary>
        /// <param name="clipData"></param>
        /// <returns></returns>
        public string GetDiskCachePath(TTSClipData clipData)
        {
            // Disabled
            if (!ShouldCacheToDisk(clipData))
            {
                return string.Empty;
            }

            // Get directory path
            TTSDiskCacheLocation location = clipData.diskCacheSettings.DiskCacheLocation;
            string directory = string.Empty;
            switch (location)
            {
                case TTSDiskCacheLocation.Persistent:
                    directory = Application.persistentDataPath;
                    break;
                case TTSDiskCacheLocation.Temporary:
                    directory = Application.temporaryCachePath;
                    break;
                case TTSDiskCacheLocation.Preload:
                    directory = Application.streamingAssetsPath;
                    break;
            }
            if (string.IsNullOrEmpty(directory))
            {
                return string.Empty;
            }

            // Add tts cache path & clean
            directory = Path.Combine(directory, DiskPath);

            // Generate tts directory if possible
            if (location != TTSDiskCacheLocation.Preload || !Application.isPlaying)
            {
                if (!IOUtility.CreateDirectory(directory, true))
                {
                    VLog.E($"Failed to create tts directory\nPath: {directory}\nLocation: {location}");
                    return string.Empty;
                }
            }

            // Return clip path
            return Path.Combine(directory, clipData.clipID + "." + WitTTSVRequest.GetAudioExtension(clipData.audioType, clipData.useEvents));
        }

        /// <summary>
        /// Determine if should cache to disk or not
        /// </summary>
        /// <param name="clipData">All clip data</param>
        /// <returns>Returns true if should cache to disk</returns>
        public bool ShouldCacheToDisk(TTSClipData clipData)
        {
            return clipData != null && clipData.diskCacheSettings.DiskCacheLocation != TTSDiskCacheLocation.Stream && !string.IsNullOrEmpty(clipData.clipID);
        }

        /// <summary>
        /// Determines if file is cached on disk
        /// </summary>
        /// <param name="clipData">Request data</param>
        /// <returns>True if file is on disk</returns>
        public void CheckCachedToDisk(TTSClipData clipData, Action<TTSClipData, bool> onCheckComplete)
        {
            _ = ThreadUtility.BackgroundAsync(_log, async () => await CheckCachedToDiskAsync(clipData, onCheckComplete));
        }

        private async Task CheckCachedToDiskAsync(TTSClipData clipData, Action<TTSClipData, bool> onCheckComplete)
        {
            // Get path
            string cachePath = GetDiskCachePath(clipData);
            if (string.IsNullOrEmpty(cachePath))
            {
                onCheckComplete?.Invoke(clipData, false);
                return;
            }

            var request = new VRequest();
            var result = await request.RequestFileExists(cachePath);
            onCheckComplete?.Invoke(clipData, result.Value);
        }

        /// <summary>
        /// Performs async load request
        /// </summary>
        public void StreamFromDiskCache(TTSClipData clipData, Action<TTSClipData, float> onProgress)
        {
            _ = ThreadUtility.BackgroundAsync(_log,
                async () => await StreamFromDiskCacheAsync(clipData, onProgress));
        }

        private async Task StreamFromDiskCacheAsync(TTSClipData clipData, Action<TTSClipData, float> onProgress)
        {
            // Invoke begin
            DiskStreamEvents?.OnStreamBegin?.Invoke(clipData);

            // Get file path
            string filePath = GetDiskCachePath(clipData);

            // Generate request & store
            VRequest request = new VRequest();
            request.Url = filePath;
            request.OnDownloadProgress += (progress) => onProgress?.Invoke(clipData, progress);
            _streamRequests[clipData.clipID] = request;

            // Add handlers for ready
            clipData.clipStream.OnStreamReady = (clipStream) => OnStreamReady(clipData, null);

            // Perform request
            var result = await request.RequestAudio(clipData.audioType,
                clipData.clipStream.AddSamples,
                clipData.useEvents ? clipData.Events.AddEvents : null);

            // Stream complete
            if (string.IsNullOrEmpty(result.Error))
            {
                clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
            }
            OnStreamComplete(clipData, result.Error);
        }
        /// <summary>
        /// Cancels unity request
        /// </summary>
        public void CancelDiskCacheStream(TTSClipData clipData)
        {
            // Ignore if not currently streaming
            if (!_streamRequests.TryGetValue(clipData.clipID, out var request))
            {
                return;
            }

            // Cancel immediately
            request?.Cancel();
        }

        /// <summary>
        /// Performs on ready callback if no error is present, otherwise considered complete
        /// </summary>
        /// <param name="clipData">The audio clip data container</param>
        /// <param name="error">Any error that occured during the stream.</param>
        protected virtual void OnStreamReady(TTSClipData clipData, string error)
        {
            // Errors are considered complete
            if (!string.IsNullOrEmpty(error))
            {
                OnStreamComplete(clipData, error);
                return;
            }
            // Perform ready callback
            DiskStreamEvents?.OnStreamReady?.Invoke(clipData);
        }

        /// <summary>
        /// Removes request from list and performs final callbacks
        /// </summary>
        /// <param name="clipData">The audio clip data container</param>
        /// <param name="error">Any error that occured during the stream.</param>
        protected virtual void OnStreamComplete(TTSClipData clipData, string error)
        {
            // Ignore if already completed
            if (!_streamRequests.ContainsKey(clipData.clipID))
            {
                return;
            }

            // Remove request
            _streamRequests.Remove(clipData.clipID);

            // Cancelled
            if (string.Equals(error, WitConstants.CANCEL_ERROR))
            {
                DiskStreamEvents?.OnStreamCancel?.Invoke(clipData);
            }
            // Error
            else if (!string.IsNullOrEmpty(error))
            {
                DiskStreamEvents?.OnStreamError?.Invoke(clipData, error);
            }
            // Success
            else
            {
                // If expected samples was never set, assign now
                if (clipData.clipStream.ExpectedSamples == 0)
                {
                    clipData.clipStream.SetExpectedSamples(clipData.clipStream.AddedSamples);
                }

                // Stream complete
                DiskStreamEvents?.OnStreamComplete?.Invoke(clipData);
            }
        }
    }
}
