/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.WitAi.Requests
{
    // Audio stream support type
    public enum AudioStreamDecodeType
    {
        PCM16,
        MP3
    }

    // Audio stream handler
    public class AudioStreamHandler : DownloadHandlerScript, IVRequestStreamable
    {
        // Generated clip name
        public string ClipName { get; }
        // Type of audio file to be decoded
        public AudioStreamDecodeType DecodeType { get; }
        // Number of channels sent by stream
        public int DecodeChannels { get; }
        // Decode sample rate
        public int DecodeSampleRate { get; }
        // Length of clip chunk size in seconds
        public int DecodeChunkLength { get; }

        // Current audio clip
        public AudioClip Clip { get; private set; }
        // Ready to stream
        public bool IsStreamReady { get; private set; }
        // Ready to stream
        public bool IsStreamComplete { get; private set; }
        // Amount of time required before stream is considered ready (seconds)
        public float StreamReadyDuration{ get; private set; }

        // Current total samples loaded
        private int _sampleCount = 0;
        // Leftover byte
        private bool _hasLeftover = false;
        private byte[] _leftovers = new byte[2];

        // Delegate that accepts an old clip and a new clip
        public delegate void AudioStreamClipUpdateDelegate(AudioClip oldClip, AudioClip newClip);
        // Callback for audio clip update during stream
        public static event AudioStreamClipUpdateDelegate OnClipUpdated;
        // Callback for audio stream complete
        public static event Action<AudioClip> OnStreamComplete;

        // Generate
        public AudioStreamHandler(string clipName, AudioStreamDecodeType decodeType, int decodeChannels, int decodeSampleRate, int decodeChunkLength = 5, float streamReadyDuration = 0.5f) : base()
        {
            // Apply parameters
            ClipName = clipName;
            DecodeType = decodeType;
            DecodeChannels = decodeChannels;
            DecodeSampleRate = decodeSampleRate;
            DecodeChunkLength = decodeChunkLength;
            StreamReadyDuration = streamReadyDuration;

            // Setup data
            _sampleCount = 0;
            _hasLeftover = false;
            IsStreamReady = false;
            IsStreamComplete = false;

            // Generate initial clip
            GenerateClip(DecodeChunkLength * DecodeSampleRate);
        }

        // Receive data
        protected override bool ReceiveData(byte[] receiveData, int dataLength)
        {
            // Exit if desired
            if (!base.ReceiveData(receiveData, dataLength) || Clip == null)
            {
                return false;
            }

            // Next decoded samples
            float[] newSamples = null;

            // Decode PCM chunk
            if (DecodeType == AudioStreamDecodeType.PCM16)
            {
                newSamples = DecodeChunkPCM16(receiveData, dataLength, ref _hasLeftover, ref _leftovers);
            }
            // Not supported
            else
            {
                VLog.E($"Not Supported Decode File Type\nType: {DecodeType}");
            }
            // Failed
            if (newSamples == null)
            {
                return false;
            }

            // Generate larger clip
            if (_sampleCount + newSamples.Length >= Clip.samples)
            {
                int newMaxSamples = Mathf.Max(Clip.samples + (DecodeChunkLength * DecodeSampleRate),
                    _sampleCount + newSamples.Length);
                GenerateClip(newMaxSamples);
            }

            // Apply to clip
            Clip.SetData(newSamples, _sampleCount);
            _sampleCount += newSamples.Length;

            // Stream is now ready
            if (!IsStreamReady && (float)_sampleCount / DecodeSampleRate >= StreamReadyDuration)
            {
                IsStreamReady = true;
            }

            // Return data
            return true;
        }

        // Clean up clip with final sample count
        protected override void CompleteContent()
        {
            // Ignore if called multiple times
            if (IsStreamComplete)
            {
                return;
            }

            // Reduce to actual size
            GenerateClip(_sampleCount);

            // Stream complete
            IsStreamComplete = true;
            OnStreamComplete?.Invoke(Clip);
        }

        // Generate clip
        private void GenerateClip(int samples)
        {
            // Get old clip if applicable
            AudioClip oldClip = Clip;

            // Generate new clip
            Clip = AudioClip.Create(ClipName, samples, DecodeChannels, DecodeSampleRate, false);

            // If previous clip existed, get previous data
            if (oldClip != null)
            {
                // Apply existing data
                int oldSampleCount = Mathf.Min(oldClip.samples, samples);
                float[] oldSamples = new float[oldSampleCount];
                oldClip.GetData(oldSamples, 0);
                Clip.SetData(oldSamples, 0);

                // Invoke clip updated callback
                OnClipUpdated?.Invoke(oldClip, Clip);

                // Destroy previous clip
                if (Application.isPlaying)
                {
                    MonoBehaviour.Destroy(oldClip);
                }
                else
                {
                    MonoBehaviour.DestroyImmediate(oldClip);
                }
            }
        }

        #region STATIC
        // Decode raw pcm data
        public static AudioClip GetClipFromRawData(byte[] rawData, AudioStreamDecodeType decodeType)
        {
            // Get channels & sample rate
            int channels = WitConstants.ENDPOINT_TTS_CHANNELS;
            int sampleRate = WitConstants.ENDPOINT_TTS_SAMPLE_RATE;

            // Decode data
            float[] samples = null;
            if (decodeType == AudioStreamDecodeType.PCM16)
            {
                samples = DecodePCM16(rawData);
            }
            // Not supported
            else
            {
                VLog.E($"Not Supported Decode File Type\nType: {decodeType}");
            }
            // Failed to decode
            if (samples == null)
            {
                return null;
            }

            // Generate clip
            AudioClip result = AudioClip.Create(WitConstants.ENDPOINT_TTS_CLIP, samples.Length, channels, sampleRate, false);
            result.SetData(samples, 0);
            return result;
        }
        #endregion

        #region PCM DECODE
        // Decode an entire array
        public static float[] DecodePCM16(byte[] rawData)
        {
            float[] samples = new float[Mathf.FloorToInt(rawData.Length / 2f)];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = DecodeSamplePCM16(rawData, i * 2);
            }
            return samples;
        }
        // Decode a single chunk
        private static float[] DecodeChunkPCM16(byte[] chunkData, int chunkLength, ref bool hasLeftover, ref byte[] leftovers)
        {
            // Determine if previous chunk had a leftover or if newest chunk contains one
            bool prevLeftover = hasLeftover;
            bool nextLeftover = (chunkLength - (prevLeftover ? 1 : 0)) % 2 != 0;
            hasLeftover = nextLeftover;

            // Generate sample array
            int startOffset = prevLeftover ? 1 : 0;
            int endOffset = nextLeftover ? 1 : 0;
            int newSampleCount = (chunkLength + startOffset - endOffset) / 2;
            float[] newSamples = new float[newSampleCount];

            // Append first byte to previous array
            if (prevLeftover)
            {
                // Append first byte to leftover array
                leftovers[1] = chunkData[0];
                // Decode first sample
                newSamples[0] = DecodeSamplePCM16(leftovers, 0);
            }

            // Store last byte
            if (nextLeftover)
            {
                leftovers[0] = chunkData[chunkLength - 1];
            }

            // Decode remaining samples
            for (int i = startOffset; i < newSamples.Length - startOffset; i++)
            {
                newSamples[i] = DecodeSamplePCM16(chunkData, startOffset + i * 2);
            }

            // Return samples
            return newSamples;
        }
        // Decode a single sample
        private static float DecodeSamplePCM16(byte[] rawData, int index)
        {
            return Mathf.Clamp((float)BitConverter.ToInt16(rawData, index) / Int16.MaxValue, -1f, 1f);
        }
        #endregion
    }
}
