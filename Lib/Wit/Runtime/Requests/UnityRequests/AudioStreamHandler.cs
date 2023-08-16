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
    /// The various supported audio decode options
    /// </summary>
    public enum AudioStreamDecodeType
    {
        PCM16,
        MP3,
        WAV
    }

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
        /// The audio stream decode option
        /// </summary>
        public AudioStreamDecodeType DecodeType { get; private set; }
        private IAudioDecoder _decoder;

        /// <summary>
        /// Audio stream data is ready to be played
        /// </summary>
        public bool IsStreamReady { get; private set; }

        /// <summary>
        /// Audio stream data has completed reception
        /// </summary>
        public bool IsComplete { get; private set; }

        // Current samples received
        private int _decodingChunks = 0;
        private bool _requestComplete = false;
        // Error handling
        private int _errorDecoded;
        private byte[] _errorBytes;

        // Generate
        public AudioStreamHandler(IAudioClipStream newClipStream, AudioType newDecodeType)
        {
            // Apply parameters
            ClipStream = newClipStream;
            DecodeType = GetDecodeType(newDecodeType);
            _decoder = GetDecoder(DecodeType);
            _decoder?.Setup(ClipStream.Channels, ClipStream.SampleRate);

            // Setup data
            _decodingChunks = 0;
            _requestComplete = false;
            IsStreamReady = false;
            IsComplete = false;
            _errorBytes = null;
            _errorDecoded = 0;

            // Begin stream
            VLog.I($"Clip Stream - Began\nClip Stream: {ClipStream.GetType()}\nFile Type: {DecodeType}");
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

            // Apply size
            int newSamples = GetClipSamplesFromContentLength(contentLength, DecodeType);
            VLog.I($"Clip Stream - Received Size\nTotal Samples: {newSamples}");
            ClipStream.SetTotalSamples(newSamples);
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

            // Append to error
            if (_errorBytes != null)
            {
                for (int i = 0; i < Mathf.Min(dataLength, _errorBytes.Length - _errorDecoded); i++)
                {
                    _errorBytes[_errorDecoded + i] = receiveData[i];
                }
                _errorDecoded += dataLength;
                return true;
            }

            // Decode data async
            _decodingChunks++;
            DecodeDataAsync(receiveData, dataLength);

            // Return data
            return true;
        }
        // Decode data
        private async Task DecodeDataAsync(byte[] receiveData, int dataLength)
        {
            // Perform decode async
            float[] samples = null;
            string newError = null;
            await Task.Run(() =>
            {
                try
                {
                    samples = _decoder?.Decode(receiveData, dataLength);
                }
                catch (Exception e)
                {
                    newError = e.ToString();
                }
            });

            // Decode complete
            OnDecodeComplete(samples, newError);
        }
        // Decode complete
        private void OnDecodeComplete(float[] newSamples, string decodeError)
        {
            // Complete
            _decodingChunks--;

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
                VLog.I($"Clip Stream - Decoded {newSamples.Length} Samples");
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
            if (IsComplete || !_requestComplete || _decodingChunks > 0 || ClipStream == null)
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
            ClipStream.SetTotalSamples(ClipStream.AddedSamples);
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

        #region STATIC
        /// <summary>
        /// Determine decode type based on audio type
        /// </summary>
        public static AudioStreamDecodeType GetDecodeType(AudioType audioType)
        {
            switch (audioType)
            {
                case AudioType.WAV:
                    return AudioStreamDecodeType.WAV;
                case AudioType.MPEG:
                    return AudioStreamDecodeType.MP3;
            }
            return AudioStreamDecodeType.PCM16;
        }

        /// <summary>
        /// Returns the class type of a decoder for specified audio type
        /// </summary>
        private static Type GetDecoderType(AudioStreamDecodeType audioType)
        {
            #if !UNITY_WEBGL
            switch (audioType)
            {
                case AudioStreamDecodeType.PCM16:
                    return typeof(AudioDecoderPcm);
            }
            #endif
            return null;
        }

        /// <summary>
        /// Successful if a decoder type exists for the specified type
        /// </summary>
        public static bool CanDecodeType(AudioStreamDecodeType audioType) => GetDecoderType(audioType) != null;

        /// <summary>
        /// Successful if a decoder type exists for the specified type
        /// </summary>
        public static bool CanDecodeType(AudioType audioType) => CanDecodeType(GetDecodeType(audioType));

        /// <summary>
        /// Instantiates an audio decoder if possible
        /// </summary>
        public static IAudioDecoder GetDecoder(AudioStreamDecodeType audioType)
        {
            Type decodeType = GetDecoderType(audioType);
            if (decodeType != null)
            {
                return Activator.CreateInstance(decodeType) as IAudioDecoder;
            }
            return null;
        }

        /// <summary>
        /// Decodes full raw data
        /// </summary>
        public static float[] DecodeAudio(AudioStreamDecodeType decodeType, int channels, int sampleRate, byte[] rawData)
        {
            // Get decoder if possible
            IAudioDecoder decoder = GetDecoder(decodeType);
            if (decoder != null)
            {
                decoder.Setup(channels, sampleRate);
                return decoder.Decode(rawData, rawData.Length);
            }

            // Return nothing
            return null;
        }

        /// <summary>
        /// Decodes full raw data & assumes wit tts
        /// </summary>
        public static float[] DecodeAudio(AudioStreamDecodeType decodeType, byte[] rawData)
            => DecodeAudio(decodeType, WitConstants.ENDPOINT_TTS_CHANNELS, WitConstants.ENDPOINT_TTS_SAMPLE_RATE,
                rawData);

        // Get audio clip from samples
        private static AudioClip GetClipFromSamples(float[] samples, string clipName, int channels, int sampleRate)
        {
            AudioClip result = AudioClip.Create(clipName, samples.Length, channels, sampleRate, false);
            result.SetData(samples, 0);
            return result;
        }

        // Determines clip sample count via content length dependent on file type
        public static int GetClipSamplesFromContentLength(ulong contentLength, AudioStreamDecodeType decodeType)
        {
            switch (decodeType)
            {
                    case AudioStreamDecodeType.PCM16:
                        return Mathf.FloorToInt(contentLength / 2f);
            }
            return 0;
        }
        #endregion
    }
}
