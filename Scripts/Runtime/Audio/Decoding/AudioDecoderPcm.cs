/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi;
using UnityEngine;
using UnityEngine.Scripting;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// Various pcm types
    /// </summary>
    public enum AudioDecoderPcmType
    {
        Int16,
        Int32,
        Int64,
        UInt16,
        UInt32,
        UInt64
    }

    /// <summary>
    /// An audio decoder for raw PCM audio data
    /// </summary>
    [Preserve]
    public class AudioDecoderPcm : IAudioDecoder
    {
        #region INSTANCE
        // Provides information on current pcm type
        public readonly AudioDecoderPcmType PcmType;
        // Total bytes per pcm sample
        private readonly int _byteCount;
        // Decode method from bytes to float
        private readonly PcmDecodeDelegate _decoder;

        // Storage of overflow bytes
        private int _overflowOffset = 0;
        private readonly byte[] _overflow;

        // Samples
        private readonly float[] _samples;

        /// <summary>
        /// Constructor that allows selection of pcm type
        /// </summary>
        [Preserve]
        public AudioDecoderPcm(AudioDecoderPcmType pcmType,
            int sampleBufferLength = WitConstants.ENDPOINT_TTS_DEFAULT_SAMPLE_LENGTH)
        {
            PcmType = pcmType;
            _byteCount = GetByteCount(PcmType);
            _overflow = new byte[_byteCount];
            _samples = new float[sampleBufferLength];
            _decoder =  GetPcmDecoder(PcmType);
        }

        /// <summary>
        /// All pcm decoding should occur in background due to large amount of data
        /// </summary>
        public bool WillDecodeInBackground => true;

        /// <summary>
        /// A method for decoded bytes and calling an AddSample delegate for each
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback following a sample decode</param>
        public virtual void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded)
        {
            // Append previous overflow
            if (_overflowOffset > 0)
            {
                // Finish overflow
                var overflowLength = Mathf.Min(_byteCount - _overflowOffset, bufferLength);
                Array.Copy(buffer, bufferOffset, _overflow, _overflowOffset, overflowLength);

                // Decode and add overflow sample
                _samples[0] = _decoder(_overflow, 0);
                onSamplesDecoded?.Invoke(_samples, 0, 1);

                // Increment buffer offset/decrement length
                bufferOffset += overflowLength;
                bufferLength -= overflowLength;
                _overflowOffset = 0;
            }

            // Decode and append while there are enough for a sample
            while (bufferLength >= _byteCount)
            {
                var remaining = Mathf.FloorToInt((float)bufferLength / _byteCount);
                var length = Mathf.Min(remaining, _samples.Length);
                for (int i = 0; i < length; i++)
                {
                    _samples[i] = _decoder(buffer, bufferOffset + i * _byteCount);
                }
                onSamplesDecoded?.Invoke(_samples, 0, length);
                length *= _byteCount;
                bufferOffset += length;
                bufferLength -= length;
            }

            // Store remaining buffer into overflow
            if (bufferLength > 0)
            {
                Array.Copy(buffer, bufferOffset, _overflow, _overflowOffset, bufferLength);
                _overflowOffset += bufferLength;
            }
        }
        #endregion

        #region STATIC
        /// <summary>
        /// Returns total bytes per sample
        /// </summary>
        public static int GetByteCount(AudioDecoderPcmType pcmType)
        {
            switch (pcmType)
            {
                case AudioDecoderPcmType.Int16:
                case AudioDecoderPcmType.UInt16:
                    return 2;
                case AudioDecoderPcmType.UInt32:
                case AudioDecoderPcmType.Int32:
                    return 4;
                case AudioDecoderPcmType.UInt64:
                case AudioDecoderPcmType.Int64:
                    return 8;
            }
            return 0;
        }

        /// <summary>
        /// Gets pcm sample count from byte content length (1 sample = 2 bytes)
        /// </summary>
        /// <param name="contentLength">The provided number of bytes</param>
        public static long GetTotalSamplesPcm(long contentLength, AudioDecoderPcmType pcmType = AudioDecoderPcmType.Int16)
            => contentLength / GetByteCount(pcmType);

        /// <summary>
        /// Decodes an array of pcm data
        /// </summary>
        /// <param name="rawData">Raw array of pcm bytes</param>
        /// <param name="pcmType">The pcm decode type to be used</param>
        public static float[] DecodePcm(byte[] rawData, AudioDecoderPcmType pcmType = AudioDecoderPcmType.Int16)
        {
            var decoder = GetPcmDecoder(pcmType);
            int totalSamples = (int)GetTotalSamplesPcm(rawData.Length, pcmType);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = decoder(rawData, i * 2);
            }
            return samples;
        }

        /// <summary>
        /// Decodes a specific buffer index
        /// </summary>
        internal delegate float PcmDecodeDelegate(byte[] buffer, int bufferOffset);

        /// <summary>
        /// Returns a decode method depending on the pcm type
        /// </summary>
        internal static PcmDecodeDelegate GetPcmDecoder(AudioDecoderPcmType pcmType)
        {
            switch (pcmType)
            {
                case AudioDecoderPcmType.Int16:
                    return DecodeSample_Pcm16;
                case AudioDecoderPcmType.Int32:
                    return DecodeSample_Pcm32;
                case AudioDecoderPcmType.Int64:
                    return DecodeSample_Pcm64;
                case AudioDecoderPcmType.UInt16:
                    return DecodeSample_PcmU16;
                case AudioDecoderPcmType.UInt32:
                    return DecodeSample_PcmU32;
                case AudioDecoderPcmType.UInt64:
                    return DecodeSample_PcmU64;
            }
            return DecodeSample_Pcm16;
        }

        /// <summary>
        /// Decodes an Int16 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_Pcm16(byte[] rawData, int index)
        {
            return (float)BitConverter.ToInt16(rawData, index) / Int16.MaxValue;
        }

        /// <summary>
        /// Decodes an Int32 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_Pcm32(byte[] rawData, int index)
        {
            return (float)BitConverter.ToInt32(rawData, index) / Int32.MaxValue;
        }

        /// <summary>
        /// Decodes an Int64 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_Pcm64(byte[] rawData, int index)
        {
            return (float)((double)BitConverter.ToInt64(rawData, index) / Int64.MaxValue);
        }

        /// <summary>
        /// Decodes an UInt16 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_PcmU16(byte[] rawData, int index)
        {
            return (float)BitConverter.ToUInt16(rawData, index) / UInt16.MaxValue;
        }

        /// <summary>
        /// Decodes an UInt32 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_PcmU32(byte[] rawData, int index)
        {
            return (float)BitConverter.ToUInt32(rawData, index) / UInt32.MaxValue;
        }

        /// <summary>
        /// Decodes an UInt64 sample into a float from 0 to 1
        /// </summary>
        /// <param name="rawData">Raw data to be decoded into a single sample</param>
        /// <param name="index">Offset of the data</param>
        public static float DecodeSample_PcmU64(byte[] rawData, int index)
        {
            return (float)((double)BitConverter.ToUInt64(rawData, index) / UInt64.MaxValue);
        }
        #endregion
    }
}
