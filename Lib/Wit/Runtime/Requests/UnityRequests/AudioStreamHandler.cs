/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using Meta.Voice.Audio.Decoding;
using Meta.Voice.Logging;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A delegate to be called when audio samples are decoded from a web stream
    /// </summary>
    public delegate void AudioSampleDecodeDelegate(float[] samples);
    /// <summary>
    /// A delegate to be called when all audio samples have been decoded
    /// </summary>
    public delegate void AudioDecodeCompleteDelegate(string error);

    /// <summary>
    /// A download handler for UnityWebRequest that decodes audio data and
    /// performs audio sample decoded callbacks.
    /// </summary>
    [Preserve]
    [LogCategory(LogCategory.Audio, LogCategory.Output)]
    public class AudioStreamHandler : DownloadHandlerScript
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        /// <summary>
        /// Whether both the request is complete and decoding is complete
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// The script being used to decode audio
        /// </summary>
        public IAudioDecoder AudioDecoder { get; }

        /// <summary>
        /// Callback for audio sample decode
        /// </summary>
        public AudioSampleDecodeDelegate OnSamplesDecoded { get; }

        /// <summary>
        /// Callback for decode completion
        /// </summary>
        public AudioDecodeCompleteDelegate OnComplete { get; }

        // Whether or not the request is complete
        private bool _requestComplete = false;
        // Queue of decoding chunks
        private int _receivedChunks = 0;
        private int _decodedChunks = 0;
        // Used to track error responses in audio stream
        private int _errorDecoded;
        private byte[] _errorBytes;

        /// <summary>
        /// The constructor that generates the decoder and handles routing callbacks
        /// </summary>
        /// <param name="audioDecoder">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="onSamplesDecoded">Called one or more times as audio samples are decoded.</param>
        /// <param name="onComplete">Called when all audio samples have been successfully decoded.</param>
        public AudioStreamHandler(IAudioDecoder audioDecoder,
            AudioSampleDecodeDelegate onSamplesDecoded,
            AudioDecodeCompleteDelegate onComplete)
        {
            // Set all methods immediately
            AudioDecoder = audioDecoder;
            OnSamplesDecoded = onSamplesDecoded;
            OnComplete = onComplete;

            // Setup data
            _requestComplete = false;
            _receivedChunks = 0;
            _decodedChunks = 0;
            _errorBytes = null;
            _errorDecoded = 0;

            // Begin stream
            _log.Info($"Init\nDecoder: {AudioDecoder?.GetType().Name ?? "Null"}");
        }

        /// <summary>
        /// If size is provided, generate error buffer size
        /// </summary>
        /// <param name="contentLength"></param>
        [Preserve]
        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            // Ignore if already complete
            if (contentLength == 0 || IsComplete)
            {
                return;
            }

            // Assume error if less than 100ms of audio
            int minChunkSize = Mathf.CeilToInt(0.1f * AudioDecoder.Channels * AudioDecoder.SampleRate);
            if (contentLength < (ulong)minChunkSize)
            {
                _errorBytes = new byte[contentLength];
            }
        }

        /// <summary>
        /// Receive data and send it to be decoded asynchronously
        /// </summary>
        [Preserve]
        protected override bool ReceiveData(byte[] bufferData, int length)
        {
            // Exit if desired
            if (!base.ReceiveData(bufferData, length) || IsComplete)
            {
                return false;
            }
            // Append error bytes
            if (_errorBytes != null)
            {
                length = Mathf.Min(length, _errorBytes.Length - _errorDecoded);
                Array.Copy(bufferData, 0, _errorBytes, _errorDecoded, length);
                _errorDecoded += length;
                return true;
            }

            // Handle decode async
            _ = ReceiveDataAsync(bufferData, length);

            // Success
            return true;
        }

        /// <summary>
        /// Decodes data asynchronously
        /// </summary>
        private async Task ReceiveDataAsync(byte[] bufferData, int length)
        {
            // Get index & increment
            int index = _receivedChunks;
            _receivedChunks++;

            // Create new array & copy received data in
            var receiveData = new byte[length];
            Array.Copy(bufferData, receiveData, length);

            // Run on background thread
            await Task.Yield();

            // If requires sequential decode, wait for previous decodes to complete
            if (AudioDecoder.RequireSequentialDecode)
            {
                await TaskUtility.WaitWhile(() => index > _decodedChunks);
            }

            // Decode samples
            float[] samples = DecodeData(receiveData);

            // If not already waited, do so now to ensure all previously decoded chunks have completed
            if (!AudioDecoder.RequireSequentialDecode)
            {
                await TaskUtility.WaitWhile(() => index > _decodedChunks);
            }

            // Raise sample decoded callback
            RaiseOnSamplesDecoded(samples);

            // Increment decode count
            _decodedChunks++;

            // Try to finalize
            TryToFinalize();
        }
        // Decode data
        private float[] DecodeData(byte[] receiveData)
        {
            try
            {
                return AudioDecoder.Decode(receiveData, 0, receiveData.Length);
            }
            catch (Exception e)
            {
                VLog.E(GetType().Name, "Decode Failed", e);
                return null;
            }
        }
        // Return data
        private void RaiseOnSamplesDecoded(float[] samples)
        {
            try
            {
                OnSamplesDecoded?.Invoke(samples);
            }
            catch (Exception e)
            {
                VLog.E(GetType().Name, "RaiseOnSamplesDecoded Failed", e);
            }
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
            if (IsComplete || !_requestComplete || _receivedChunks != _decodedChunks)
            {
                return;
            }

            // Stream complete
            IsComplete = true;
            RaiseOnComplete();

            // Dispose
            Dispose();
        }
        // Return data
        private void RaiseOnComplete()
        {
            try
            {
                var error = GetText();
                OnComplete?.Invoke(error);
                _log.Info($"Complete\nError: {error ?? "Null"}");
            }
            catch (Exception e)
            {
                VLog.E(GetType().Name, "RaiseOnComplete Failed", e);
            }
        }

        // Destroy old clip
        public void CleanUp()
        {
            // Already complete
            if (IsComplete)
            {
                _errorBytes = null;
                return;
            }

            // Dispose handler
            Dispose();

            // Complete
            IsComplete = true;
        }
    }
}
