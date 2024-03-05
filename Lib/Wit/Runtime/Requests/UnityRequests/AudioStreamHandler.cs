/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using Meta.Voice.Audio;
using Meta.Voice.Audio.Decoding;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A download handler for UnityWebRequest that decodes audio data, passes
    /// the data into an iAudioClipStream & provides download state information.
    /// </summary>
    [Preserve]
    public class AudioStreamHandler : DownloadHandlerScript, IVRequestStreamable
    {
        /// <summary>
        /// Clip used to cache audio data
        /// </summary>
        public IAudioClipStream ClipStream { get; private set; }

        /// <summary>
        /// Audio stream data is ready to be played
        /// </summary>
        public bool IsStreamReady { get; private set; }

        /// <summary>
        /// Audio stream data has completed reception
        /// </summary>
        public bool IsComplete { get; private set; }

        // The script responsible for decoding incoming data into audio
        private IAudioDecoder _decoder;
        // Current samples received
        private int _receivedChunks = 0;
        private int _decodedChunks = 0;
        private bool _requestComplete = false;
        // Error handling
        private int _errorDecoded;
        private byte[] _errorBytes;

        // Generate
        public AudioStreamHandler(IAudioClipStream newClipStream, IAudioDecoder newDecoder)
        {
            // Apply parameters
            ClipStream = newClipStream;
            _decoder = newDecoder;
            _decoder?.Setup(ClipStream.Channels, ClipStream.SampleRate);

            // Setup data
            _receivedChunks = 0;
            _decodedChunks = 0;
            _requestComplete = false;
            IsStreamReady = false;
            IsComplete = false;
            _errorBytes = null;
            _errorDecoded = 0;

            // Begin stream
            VLog.I($"Clip Stream - Began\nClip Stream: {ClipStream.GetType()}\nDecoder: {(newDecoder == null ? "NULL" : newDecoder.GetType().Name)}");
        }

        // If size is provided, generate clip using size
        [Preserve]
        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            // Ignore if already complete
            if (contentLength == 0 || IsComplete)
            {
                return;
            }

            // Assume text if less than min chunk size
            int minChunkSize = Mathf.Max(100, Mathf.CeilToInt(0.1f * ClipStream.Channels * ClipStream.SampleRate));
            if (contentLength < (ulong)minChunkSize)
            {
                _errorBytes = new byte[minChunkSize];
                return;
            }

            // Apply size if possible
            int totalSamples = _decoder.GetTotalSamples(contentLength);
            if (totalSamples > 0)
            {
                VLog.I($"Clip Stream - Received Size\nTotal Samples: {totalSamples}\nContent Length: {contentLength}");
                ClipStream.SetExpectedSamples(totalSamples);
            }
        }

        // Receive data
        [Preserve]
        protected override bool ReceiveData(byte[] receiveData, int dataLength)
        {
            // Exit if desired
            if (!base.ReceiveData(receiveData, dataLength) || IsComplete)
            {
                return false;
            }

            // Decode data async
            #pragma warning disable CS4014
            DecodeDataAsync(receiveData, dataLength);
            #pragma warning restore CS4014

            // Success
            return true;
        }
        // Decode data asynchronously
        private async Task DecodeDataAsync(byte[] receiveData, int dataLength)
        {
            // Append to error async
            if (_errorBytes != null)
            {
                await Task.Run(() =>
                {
                    int errorLength = Mathf.Min(dataLength, _errorBytes.Length - _errorDecoded);
                    Array.Copy(receiveData, 0, _errorBytes, _errorDecoded, errorLength);
                    _errorDecoded += errorLength;
                });
                return;
            }

            // Increment receive chunk count
            int current = _receivedChunks;
            _receivedChunks++;

            // If must decode in sequence, wait for previous to complete
            bool sequentialDecode = _decoder.RequireSequentialDecode;
            await TaskUtility.WaitWhile(() => sequentialDecode && _decodedChunks < current);

            // Perform decode async
            float[] samples = null;
            string newError = null;
            await Task.Run(() =>
            {
                try
                {
                    samples = _decoder?.Decode(receiveData, 0 /*chunkStart*/, dataLength);
                }
                catch (Exception e)
                {
                    newError = e.ToString();
                }
            });

            // Needs to wait for sequential prior to returning if not done previously
            await TaskUtility.WaitWhile(() => !sequentialDecode && _decodedChunks < current);

            // Increment decoded chunk count
            _decodedChunks++;

            // Decode complete
            OnDecodeComplete(samples, newError);
        }
        // Decode complete
        private void OnDecodeComplete(float[] newSamples, string decodeError)
        {
            // Fail with error
            if (!string.IsNullOrEmpty(decodeError))
            {
                VLog.W($"Decode Chunk Failed\n{decodeError}");
                TryToFinalize();
                return;
            }
            // Fail without samples
            if (newSamples == null)
            {
                VLog.W($"Decode Chunk Failed\nNo samples returned");
                TryToFinalize();
                return;
            }

            // Add to clip
            if (newSamples.Length > 0)
            {
                ClipStream.AddSamples(newSamples);
            }

            // Stream is now ready
            if (!IsStreamReady && ClipStream.IsReady)
            {
                IsStreamReady = true;
                VLog.I($"Clip Stream - Stream Ready");
            }

            // Try to finalize
            TryToFinalize();
        }

        // Used for error handling
        [Preserve]
        protected override string GetText()
        {
            return _errorBytes != null ? Encoding.UTF8.GetString(_errorBytes) : string.Empty;
        }

        // Return progress if total samples has been determined
        [Preserve]
        protected override float GetProgress()
        {
            if (_errorBytes != null && _errorBytes.Length > 0)
            {
                return (float) _errorDecoded / _errorBytes.Length;
            }
            if (ClipStream.TotalSamples > 0)
            {
                return (float) ClipStream.AddedSamples / ClipStream.TotalSamples;
            }
            return 0f;
        }

        // Clean up clip with final sample count
        [Preserve]
        protected override void CompleteContent()
        {
            // Ignore if called multiple times
            if (_requestComplete)
            {
                return;
            }

            // Complete
            _requestComplete = true;
            TryToFinalize();
        }

        // Handle completion
        private void TryToFinalize()
        {
            // Already finalized or not yet complete
            if (IsComplete || !_requestComplete || _receivedChunks != _decodedChunks || ClipStream == null)
            {
                return;
            }

            // Wait a single frame prior to final completion to ensure OnReady is called first
            if (!IsStreamReady)
            {
                IsStreamReady = true;
                VLog.I($"Clip Stream - Stream Ready");
                CoroutineUtility.StartCoroutine(FinalWait());
                return;
            }

            // Stream complete
            IsComplete = true;
            ClipStream.SetExpectedSamples(ClipStream.AddedSamples);
            VLog.I($"Clip Stream - Complete\nLength: {ClipStream.Length:0.00} secs");

            // Dispose
            Dispose();
        }

        // A final wait callback that ensures onready is called first for non-streaming instances
        private IEnumerator FinalWait()
        {
            yield return null;
            TryToFinalize();
        }

        // Destroy old clip
        public void CleanUp()
        {
            // Already complete
            if (IsComplete)
            {
                _decoder = null;
                _errorBytes = null;
                ClipStream = null;
                return;
            }

            // Destroy clip
            if (ClipStream != null)
            {
                ClipStream.Unload();
                ClipStream = null;
            }

            // Dispose handler
            Dispose();

            // Complete
            IsComplete = true;
            VLog.I($"Clip Stream - Cleaned Up");
        }
    }
}
