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
using System.Collections.Generic;
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
        /// <summary>
        /// Whether both the request is complete and decoding is complete
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Whether the request was caused by an error
        /// </summary>
        public bool IsError { get; private set; }

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

        // Ring buffer and counters for decoding bytes
        private readonly byte[] _inRingBuffer = new byte[WitConstants.ENDPOINT_TTS_BUFFER_LENGTH];
        private int _inRingOffset = 0;
        private ulong _receivedBytes = 0;
        private ulong _decodedBytes = 0;
        private ulong _expectedBytes = 0;

        // If true the request is no longer being performed
        private bool _requestComplete = false;
        // If true there are no more bytes to be decoded
        private bool _decodeComplete => _decodedBytes == Max(_receivedBytes, _expectedBytes);
        // Returns the longer of two ulong
        private ulong Max(ulong var1, ulong var2) => var1 > var2 ? var1 : var2;

        // For logging
        private readonly LazyLogger _log = new(() => LoggerRegistry.Instance.GetLogger());

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
            // Store decoder and callbacks
            AudioDecoder = audioDecoder;
            OnSamplesDecoded = onSamplesDecoded;
            OnComplete = onComplete;

            // Begin decoding async
            _ = ThreadUtility.BackgroundAsync(_log, DecodeAsync);
        }

        /// <summary>
        /// If size is provided, determine if end size is too small for an audio file
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

            // Set expected length
            _expectedBytes = contentLength;

            // Assume error if less than 100ms of audio
            ulong minChunkSize = (ulong)Mathf.CeilToInt(AudioDecoder.Channels * AudioDecoder.SampleRate * WitConstants.ENDPOINT_TTS_ERROR_LENGTH / 1000f);
            IsError = _expectedBytes < minChunkSize;
        }

        /// <summary>
        /// Receive data and push it to the ring buffer
        /// </summary>
        [Preserve]
        protected override bool ReceiveData(byte[] bufferData, int length)
        {
            // Exit if desired
            if (!base.ReceiveData(bufferData, length) || IsComplete)
            {
                return false;
            }

            // Push all data to buffer
            PushChunk(bufferData, 0, length);

            // Success
            return true;
        }

        // Push to the ring buffer
        private void PushChunk(byte[] chunk, int offset, int length)
        {
            // Log error if looping prior to decoding
            var unDecoded = length + (int)(_receivedBytes - _decodedBytes);
            var maxLength = _inRingBuffer.Length;
            if (unDecoded > maxLength)
            {
                _log.Error("Buffer Overflow!\nReceived {0} bytes makes {1} bytes not yet decoded thereby overflowing {2} bytes in the entire ring buffer.",
                    length, unDecoded, maxLength);
            }

            // Decode a chunk
            while (length > 0)
            {
                // Get largest possible push length
                var pushLength = Mathf.Min(length, maxLength - _inRingOffset);

                // Push chunk
                Array.Copy(chunk, offset, _inRingBuffer, _inRingOffset, pushLength);

                // Attempt to iterate through the pushed data chunk
                offset += pushLength;
                length -= pushLength;
                // Loop offset as needed
                _inRingOffset = (_inRingOffset + pushLength) % maxLength;
                // Keep track of full received content length
                _receivedBytes += (ulong)pushLength;
            }
        }

        // Decode ring buffer on background thread
        private async Task DecodeAsync()
        {
            // Decoded variables
            int decodedOffset = 0;
            List<float> decodedSamples = new List<float>();
            int maxLength = _inRingBuffer.Length;

            // Decode while not complete
            while (!_requestComplete || !_decodeComplete)
            {
                // Ignore if nothing to decode
                if (_decodedBytes == _receivedBytes)
                {
                    await Task.Yield();
                    continue;
                }
                // If believed to be an error, consider decoded
                if (IsError)
                {
                    _decodedBytes = _receivedBytes;
                    continue;
                }

                // Get largest possible amount to decode
                var unDecoded = (int)(_receivedBytes - _decodedBytes);
                var decodeLength = Mathf.Min(unDecoded, maxLength - decodedOffset);

                // Decode chunk
                var appendedSamples = DecodeChunk(decodedOffset, decodeLength, decodedSamples);
                decodedOffset = (decodedOffset + decodeLength) % maxLength;
                _decodedBytes += (ulong)decodeLength;

                // Return samples via callback
                RaiseOnSamplesDecoded(decodedSamples.ToArray());
                decodedSamples.Clear();
            }

            // Try to finalize on main thread
            _ = ThreadUtility.CallOnMainThread(TryToFinalize);
        }

        // Decode a specific chunk of received bytes
        private int DecodeChunk(int offset, int length, List<float> samples)
        {
            try
            {
                var newSamples = AudioDecoder.Decode(_inRingBuffer, offset, length);
                samples.AddRange(newSamples);
                return newSamples.Length;
            }
            catch (Exception e)
            {
                _log.Error("AudioStreamHandler Decode Failed\nException: {0}", e);
                return 0;
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
                _log.Error("RaiseOnSamplesDecoded Exception\n\n{0}\n", e);
            }
        }

        // Used for error handling
        [Preserve]
        protected override string GetText()
        {
            if (IsError)
            {
                return Encoding.UTF8.GetString(_inRingBuffer, 0, _inRingOffset);
            }
            return null;
        }

        // Return progress if total samples has been determined
        [Preserve]
        protected override float GetProgress()
        {
            if (_expectedBytes > 0)
            {
                return Mathf.Clamp01(_decodedBytes / _expectedBytes);
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
            if (IsComplete || !_requestComplete || !_decodeComplete)
            {
                return;
            }

            // Stream complete
            IsComplete = true;
            RaiseOnComplete();

            // Dispose
            Dispose();
        }

        // Return
        private void RaiseOnComplete()
        {
            try
            {
                var error = GetText();
                OnComplete?.Invoke(error);
            }
            catch (Exception e)
            {
                _log.Error("RaiseOnComplete Exception\n\n{0}\n", e);
            }
        }

        /// <summary>
        /// Dispose and ensure OnComplete cannot be called if not yet done so
        /// </summary>
        public void CleanUp()
        {
            // Already complete
            if (IsComplete)
            {
                return;
            }

            // Dispose handler
            Dispose();

            // Complete
            IsComplete = true;
        }
    }
}
