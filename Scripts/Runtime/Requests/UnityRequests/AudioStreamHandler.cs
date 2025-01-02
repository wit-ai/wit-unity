/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using UnityEngine.Profiling;
using Meta.Voice.Audio.Decoding;
using Meta.Voice.Logging;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A download handler for UnityWebRequest that decodes audio data and
    /// performs audio sample decoded callbacks.
    /// </summary>
    [Preserve]
    [LogCategory(LogCategory.Audio, LogCategory.Output)]
    internal class AudioStreamHandler : DownloadHandlerScript, IVRequestDownloadDecoder, ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Output);

        /// <summary>
        /// Whether data has arrived
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <inheritdoc />
        public event VRequestResponseDelegate OnFirstResponse;

        /// <inheritdoc />
        public event VRequestResponseDelegate OnResponse;

        /// <summary>
        /// Current progress of the download
        /// </summary>
        public float Progress { get; private set; }
        /// <summary>
        /// Callback for download progress
        /// </summary>
        public event VRequestProgressDelegate OnProgress;

        /// <summary>
        /// Whether or not complete
        /// </summary>
        public bool IsComplete { get; private set; } = false;
        /// <summary>
        /// Completion source task
        /// </summary>
        public TaskCompletionSource<bool> Completion { get; } = new TaskCompletionSource<bool>();

        /// <summary>
        /// Whether the request was caused by an error
        /// </summary>
        public bool IsError { get; private set; }

        /// <summary>
        /// The script being used to decode audio
        /// </summary>
        public IAudioDecoder AudioDecoder { get; }

        /// <summary>
        /// Quick accessor for audio decoder's will decode in background method.
        /// If true, performant but requires multiple buffers.
        /// If false, buffers are not required but less performant.
        /// </summary>
        public bool WillDecodeInBackground => AudioDecoder.WillDecodeInBackground;

        /// <summary>
        /// Callback for audio sample decode
        /// </summary>
        public AudioSampleDecodeDelegate OnSamplesDecoded { get; }

        // Ring buffer and counters for decoding bytes
        private const int BUFFER_LENGTH = WitConstants.ENDPOINT_TTS_BUFFER_LENGTH;
        private static readonly ArrayPool<byte> _bufferPool = new (BUFFER_LENGTH);
        private readonly Queue<byte[]> _buffers; // All currently used buffers
        private byte[] _inBuffer;
        private int _inBufferOffset = 0;
        private byte[] _decodeBuffer;
        private int _decodeBufferOffset = 0;

        // Total bytes expected to arrive
        private ulong _expectedBytes = 0;
        // Total bytes arrived and undecoded
        private ulong _receivedBytes = 0;
        // Total bytes decoded into samples
        private ulong _decodedBytes = 0;

        // If true the request is no longer being performed
        private bool _requestComplete = false;
        // If true there are no more bytes to be decoded
        private bool _decodeComplete => _decodedBytes == Max(_receivedBytes, _expectedBytes);
        // Returns the longer of two ulong
        private ulong Max(ulong var1, ulong var2) => var1 > var2 ? var1 : var2;

        // Task performing decode
        private Task _decoder;
        private bool _unloaded = false;

        /// <summary>
        /// The constructor that generates the decoder and handles routing callbacks
        /// </summary>
        /// <param name="audioDecoder">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="onSamplesDecoded">Called on background thread for every chunk of samples decoded.</param>
        public AudioStreamHandler(IAudioDecoder audioDecoder,
            AudioSampleDecodeDelegate onSamplesDecoded)
        {
            AudioDecoder = audioDecoder;
            OnSamplesDecoded = onSamplesDecoded;
            if (WillDecodeInBackground)
            {
                _buffers = new Queue<byte[]>();
            }
        }

        /// <summary>
        /// Ensure buffer is always pooled
        /// </summary>
        ~AudioStreamHandler()
        {
            UnloadBuffers();
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

            // Assume error if less than max error length
            IsError = _expectedBytes < WitConstants.ENDPOINT_TTS_ERROR_MAX_LENGTH;
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

            // Started
            if (!IsStarted)
            {
                IsStarted = true;
                OnFirstResponse?.Invoke();
            }

            // Perform response callback
            OnResponse?.Invoke();

            // Enqueue and then decode async
            if (WillDecodeInBackground)
            {
                EnqueueAndDecodeChunkAsync(bufferData, 0, length);
            }
            // Decode immediately
            else
            {
                _receivedBytes += (uint)length;
                DecodeChunk(bufferData, 0, length);
                if (_decodeComplete)
                {
                    TryToFinalize();
                }
            }

            // Success
            return true;
        }

        // Push to the ring buffer then decode async
        private void EnqueueAndDecodeChunkAsync(byte[] chunk, int offset, int length)
        {
            // Decode a chunk
            while (length > 0)
            {
                // Get new buffer
                if (_inBuffer == null)
                {
                    _inBuffer = _bufferPool.Get();
                    lock (_buffers)
                    {
                        _buffers.Enqueue(_inBuffer);
                    }
                }

                // Get largest possible push length
                var pushLength = Mathf.Min(length, _inBuffer.Length - _inBufferOffset);

                // Push chunk
                Array.Copy(chunk, offset, _inBuffer, _inBufferOffset, pushLength);

                // Attempt to iterate through the pushed data chunk
                offset += pushLength;
                length -= pushLength;
                // Loop offset as needed
                _inBufferOffset += pushLength;
                if (_inBufferOffset >= _inBuffer.Length)
                {
                    _inBufferOffset = 0;
                    _inBuffer = null;
                }
                // Increment total received bytes
                _receivedBytes += (ulong)pushLength;
            }

            // Enqueue decode task
            if (_decoder == null)
            {
                _decoder = ThreadUtility.Background(Logger,  DecodeAsync);
            }
        }

        /// <summary>
        /// Background thread decode
        /// </summary>
        private void DecodeAsync()
        {
            // If believed to be an error, consider decoded
            if (IsError)
            {
                _decodedBytes = _receivedBytes;
                _ = ThreadUtility.CallOnMainThread(TryToFinalize);
                return;
            }

            // Decode all undecoded bytes
            while (_decodedBytes < _receivedBytes)
            {
                // Dequeue the next buffer
                if (_decodeBuffer == null)
                {
                    lock (_buffers)
                    {
                        if (!_buffers.TryDequeue(out _decodeBuffer))
                        {
                            break;
                        }
                    }
                }

                // Determine decode length and offset
                var remainder = (int)(_receivedBytes - _decodedBytes);
                var decodeLength = Mathf.Min(remainder, _decodeBuffer.Length - _decodeBufferOffset);

                // Decode
                DecodeChunk(_decodeBuffer, _decodeBufferOffset, decodeLength);

                // Increment
                _decodeBufferOffset += decodeLength;

                // Unload once completely used
                if (_decodeBufferOffset >= _decodeBuffer.Length)
                {
                    _decodeBufferOffset = 0;
                    _bufferPool.Return(_decodeBuffer);
                    _decodeBuffer = null;
                }

                // Refresh
                RefreshProgress();
            }

            // Remove decoder task reference
            _decoder = null;

            // Try to finalize on main thread
            if (_decodeComplete)
            {
                _ = ThreadUtility.CallOnMainThread(TryToFinalize);
            }
        }

        // Decode chunk if possible
        private void DecodeChunk(byte[] chunk, int offset, int length)
        {
            try
            {
                Profiler.BeginSample("[VSDK] Audio Decode");
                AudioDecoder.Decode(chunk, offset, length, OnSamplesDecoded);
            }
            catch (Exception e)
            {
                Logger.Error("AudioStreamHandler Decode Failed\nException: {0}", e);
            }
            finally
            {
                _decodedBytes += (ulong)length;
                Profiler.EndSample();
            }
        }

        // Used for error handling
        [Preserve]
        protected override string GetText()
        {
            if (IsError && _inBuffer != null)
            {
                return Encoding.UTF8.GetString(_inBuffer, 0, _inBufferOffset);
            }
            return null;
        }

        /// <summary>
        /// Refreshes progress if expected bytes has been set
        /// </summary>
        private void RefreshProgress()
        {
            if (_expectedBytes <= 0)
            {
                return;
            }
            var progress = GetProgress();
            if (!Progress.Equals(progress))
            {
                Progress = progress;
                OnProgress?.Invoke(progress);
            }
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

            // Dispose handler and pool buffer
            Dispose();
        }

        /// <summary>
        /// Dispose ring buffer
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            UnloadBuffers();
            IsComplete = true;
            Completion.TrySetResult(true);
        }

        // Safely unload buffers back into pool
        private void UnloadBuffers()
        {
            if (_unloaded)
            {
                return;
            }
            // Remove in buffer (Already in buffers queue)
            _inBuffer = null;
            // Dequeue and unload each buffer
            if (WillDecodeInBackground)
            {
                lock (_buffers)
                {
                    while (_buffers.TryDequeue(out var buffer))
                    {
                        _bufferPool.Return(buffer);
                    }
                }
            }
            // Unload out buffer
            if (_decodeBuffer != null)
            {
                _bufferPool.Return(_decodeBuffer);
                _decodeBuffer = null;
            }
            // Unloaded
            _unloaded = true;
        }
    }
}
