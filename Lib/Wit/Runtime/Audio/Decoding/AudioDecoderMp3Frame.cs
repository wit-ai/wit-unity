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
using UnityEngine;
using UnityEngine.Scripting;
using Meta.Audio.NLayer;
using Meta.Voice.Logging;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder for raw MPEG audio data
    /// </summary>
    [Preserve]
    internal class AudioDecoderMp3Frame : IMpegFrame
    {
        // Data buffer to ensure all frame data exists across packets
        private byte[] _dataBuffer = new byte[192]; // Default mpeg packet size
        private int _dataOffset = 0;
        // Total header bytes decoded
        public bool IsHeaderDecoded => _dataOffset >= HeaderLength;
        private const int HeaderLength = 4;

        // Sample buffer with max samples per mpeg frame
        private float[] _sampleBuffer = new float[576]; // Default mpeg sample size

        // Script that handles decoding frames
        private readonly MpegFrameDecoder _decoder = new MpegFrameDecoder();

        // Bit offset of current bit stream
        private int _readOffset;
        // In progress 8 bits for decoding
        private ulong _bitBucket = 0UL;
        // Total bits read in current frame
        private int _bitsRead;

        // Index of how many frames have been decoded
        private uint _frameIndex;

        // For logging
        private IVLogger Log
        {
            get
            {
                if (_log == null)
                {
                    _log = LoggerRegistry.Instance.GetLogger();
                }
                return _log;
            }
        }
        private IVLogger _log;

        /// <summary>
        /// Clears all frame specific data every frame
        /// </summary>
        private void Clear()
        {
            _dataOffset = 0;
            Reset();
        }

        /// <summary>
        /// Resets all read specific data
        /// </summary>
        public void Reset()
        {
            _readOffset = 4 + (HasCrc ? 2 : 0); // Starts reading following header 4 bits & 2 CRC bits if applicable
            _bitBucket = 0UL; // In progress 8 bits for decoding
            _bitsRead = 0; // All bits decoded
        }

        /// <summary>
        /// Decodes the frame & returns the int of
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="decodedSamples">List to add all decoded samples to</param>
        public int Decode(byte[] buffer, int bufferOffset, int bufferLength, List<float> decodedSamples)
        {
            // Total decoded from the buffer
            int decodedLength = 0;

            // Header still needs decode
            if (!IsHeaderDecoded)
            {
                // Get as much of the required header as possible
                decodedLength = Mathf.Min(HeaderLength - _dataOffset, bufferLength);
                Array.Copy(buffer, bufferOffset, _dataBuffer, _dataOffset, decodedLength);
                _dataOffset += decodedLength;
                if (!IsHeaderDecoded)
                {
                    return decodedLength;
                }

                try
                {
                    DecodeHeader();
                }
                catch (Exception e)
                {
                    Log.Error("MP3 Frame {0} - Header Decode Failed\n\n{1}\n{2}", _frameIndex, e, this);
                    _frameIndex++;
                    Clear();
                    return decodedLength;
                }

                // Increase data buffer length if needed
                if (_dataBuffer.Length < FrameLength)
                {
                    Log.Warning("MP3 Frame {0} - Data Buffer Re-generated\nNew Frame Length: {1}\nOld Frame Length: {2}\n{3}",
                        _frameIndex, FrameLength, _dataBuffer.Length, this);
                    _dataBuffer = new byte[FrameLength];
                }
                // Increase sample buffer length if needed
                if (_sampleBuffer.Length < SampleCount)
                {
                    Log.Warning("MP3 Frame {0} - Sample Buffer Re-generated\nNew Sample Count: {1}\nOld Sample Count: {2}\n{3}",
                        _frameIndex, SampleCount, _sampleBuffer.Length, this);
                    _sampleBuffer = new float[SampleCount];
                }
            }

            // Copy as much as possible for frame
            var copyLength = Mathf.Min(FrameLength - _dataOffset, bufferLength - decodedLength);
            Array.Copy(buffer, bufferOffset + decodedLength, _dataBuffer, _dataOffset, copyLength);
            _dataOffset += copyLength;
            decodedLength += copyLength;

            // Wait until more data arrives
            if (_dataOffset < FrameLength)
            {
                return decodedLength;
            }

            // Decode as many as possible that are provided and within the frame remainder
            const int sampleOffset = 0;
            var sampleLength = _decoder.DecodeFrame(this, _sampleBuffer, sampleOffset);
            for (int i = 0; i < sampleLength; i++)
            {
                decodedSamples.Add(_sampleBuffer[sampleOffset + i]);
            }

            // Increment frame count & clear previous data
            _frameIndex++;
            Clear();

            // Return total decoded bytes
            return decodedLength;
        }

        #region HEADER
        /// <summary>
        /// Mpeg version enum
        /// </summary>
        public MpegVersion Version { get; private set; }

        /// <summary>
        /// MPEG Layer
        /// </summary>
        public MpegLayer Layer { get; private set; }

        /// <summary>
        /// Channel Mode
        /// </summary>
        public MpegChannelMode ChannelMode { get; private set; }

        /// <summary>
        /// The channel extension bits
        /// </summary>
        public int ChannelModeExtension { get; private set; }

        /// <summary>
        /// The bitrate index (directly from the header)
        /// </summary>
        public int BitRateIndex { get; private set; }

        /// <summary>
        /// Bit Rate
        /// </summary>
        public int BitRate { get; private set; }

        /// <summary>
        /// Bitrate lookup table
        /// [MPEG Version 1 == 0 & 2/2.5 == 1][Header Layer Index - 1][Header BitRate Index]
        /// </summary>
        static readonly int[][][] _bitRateTable =
        {
            new int[][]
            {
                new int[] { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 },
                new int[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 },
                new int[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 }
            },
            new int[][]
            {
                new int[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 },
                new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 },
                new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 }
            },
        };

        /// <summary>
        /// The samplerate index (directly from the header)
        /// </summary>
        public int SampleRateIndex { get; private set; }

        /// <summary>
        /// Sample rate of this frame
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Whether the copyright bit is set
        /// </summary>
        public bool IsCopyrighted { get; private set; }

        /// <summary>
        /// Whether a CRC is present
        /// </summary>
        public bool HasCrc { get; private set; }

        /// <summary>
        /// Whether the CRC check failed (use error concealment strategy)
        /// </summary>
        public bool IsCorrupted { get; private set; }

        /// <summary>
        /// Frame length in bytes
        /// </summary>
        public int FrameLength { get; private set; }

        /// <summary>
        /// The number of samples in this frame
        /// </summary>
        public int SampleCount { get; private set; }

        // Decode header data
        private void DecodeHeader()
        {
            // Reverse header bytes & encode to int32
            Array.Reverse(_dataBuffer, 0, HeaderLength);
            int headerData = BitConverter.ToInt32(_dataBuffer, 0);

            // Frame sync (31, 21)
            const int frameSyncMask = 2047; // All 1s
            int frameSync = BitRShift(headerData, 21) & frameSyncMask;
            if (frameSync != frameSyncMask)
            {
                throw new Exception($"Invalid frame {_frameIndex} sync\nBits: {GetBitString(headerData)}");
            }

            // Mpeg version (20, 19)
            int versionInt = BitRShift(headerData, 19) & 3;
            switch (versionInt)
            {
                case 1:
                    Version = MpegVersion.Version1;
                    break;
                case 2:
                    Version = MpegVersion.Version2;
                    break;
                case 0:
                    Version = MpegVersion.Version25; // MPEG v2.5
                    break;
                default:
                    Version = MpegVersion.Unknown;
                    throw new Exception($"Invalid frame {_frameIndex} Mpeg Version\nBits: {GetBitString(headerData)}");
            }

            // Layer description (18, 17)
            Layer = (MpegLayer)(4 - BitRShift(headerData, 17) & 3);
            if (Layer == MpegLayer.Unknown)
            {
                throw new Exception($"Invalid frame {_frameIndex} Mpeg Layer\nBits: {GetBitString(headerData)}");
            }

            // Protection bit (16)
            HasCrc = (BitRShift(headerData, 16) & 1) == 0;

            // Bitrate index (15 - 12)
            BitRateIndex = BitRShift(headerData, 12) & 0xF;
            if (BitRateIndex > 0)
            {
                BitRate = _bitRateTable[(int)Version / 10 - 1][(int)Layer - 1][BitRateIndex] * 1000;
            }
            else
            {
                throw new Exception($"Invalid frame {_frameIndex} bitrate index\nBits: {GetBitString(headerData)}");
            }

            // Sample rate index (11, 10)
            SampleRateIndex = BitRShift(headerData, 10) & 3;
            switch (SampleRateIndex)
            {
                case 0:
                    SampleRate = 44100;
                    break;
                case 1:
                    SampleRate= 48000;
                    break;
                case 2:
                    SampleRate = 32000;
                    break;
                default:
                    SampleRate = 0;
                    throw new Exception($"Invalid frame {_frameIndex} Mpeg sample rate index\nBits: {GetBitString(headerData)}");
            }
            if (Version == MpegVersion.Version2)
            {
                SampleRate /= 2;
            }
            else if (Version == MpegVersion.Version25) // MPEG v2.5
            {
                SampleRate /= 4;
            }
            if (Layer == MpegLayer.LayerI)
            {
                SampleCount = 384;
            }
            else if (Layer == MpegLayer.LayerIII && Version > MpegVersion.Version1)
            {
                SampleCount = 576;
            }
            else
            {
                SampleCount = 1152;
            }

            // Frame is padded (9)
            int padding = (BitRShift(headerData, 9) & 1);

            // Channel mode (7, 6)
            ChannelMode = (MpegChannelMode)(BitRShift(headerData, 6) & 3);

            // Channel mode extension [Joint Stereo] (5, 4)
            ChannelModeExtension = BitRShift(headerData, 4) & 3;

            // Audio is copyrighted (3)
            IsCopyrighted = (BitRShift(headerData, 3) & 1) != 0;

            // Calculate the frame's length
            if (BitRateIndex > 0)
            {
                if (Layer == MpegLayer.LayerI)
                {
                    FrameLength = 12 * BitRate / SampleRate + padding;
                    FrameLength <<= 2;
                }
                else
                {
                    FrameLength = 144 * BitRate / SampleRate;
                    if (Version == MpegVersion.Version2 || Version == MpegVersion.Version25) // MPEG v2 || v2.5
                    {
                        FrameLength >>= 1;
                    }
                    FrameLength += padding;
                }
            }
            // Not currently supported
            else
            {
                // "free" frame...  we have to calculate it later
                FrameLength = _bitsRead + GetSideDataSize() + padding; // we know the frame will be at least this big...
                // Bitrate is always an even multiple of 1000, so round
                BitRate = ((((FrameLength * 8) * SampleRate) / SampleCount + 499) + 500) / 1000 * 1000;
            }

            // Crc check disabled
            IsCorrupted = false;
        }
        // Simple bit shift for easy bit parsing
        internal static int BitRShift(int number, int bits)
        {
            if (number >= 0)
            {
                return number >> bits;
            }
            return (number >> bits) + (2 << ~bits);
        }
        // Determines side data size for frame length calculations
        internal int GetSideDataSize()
        {
            switch (Layer)
            {
                case MpegLayer.LayerI:
                    // mono
                    if (ChannelMode == MpegChannelMode.Mono)
                    {
                        return 16;
                    }
                    // full stereo / dual channel
                    if (ChannelMode == MpegChannelMode.Stereo || ChannelMode == MpegChannelMode.DualChannel)
                    {
                        return 32;
                    }
                    // joint stereo
                    switch (ChannelModeExtension)
                    {
                        case 0:
                            return 18;
                        case 1:
                            return 20;
                        case 2:
                            return 22;
                        case 3:
                            return 24;
                    }
                    break;
                case MpegLayer.LayerII:
                    return 0;
                case MpegLayer.LayerIII:
                    if (ChannelMode == MpegChannelMode.Mono && Version >= MpegVersion.Version2)
                    {
                        return 9;
                    }
                    if (ChannelMode != MpegChannelMode.Mono && Version < MpegVersion.Version2)
                    {
                        return 32;
                    }
                    return 17;
            }
            return 0;
        }
        #endregion

        #region DATA
        // Performs frame read
        public int ReadBits(int bitCount)
        {
            if (bitCount < 1 || bitCount > 32) throw new ArgumentOutOfRangeException("bitCount");
            if (IsCorrupted) return 0;

            while (_bitsRead < bitCount)
            {
                var b = ReadByte(_readOffset);
                if (b == -1) throw new System.IO.EndOfStreamException();

                ++_readOffset;

                _bitBucket <<= 8;
                _bitBucket |= (byte)(b & 0xFF);
                _bitsRead += 8;
            }

            var temp = (int)((_bitBucket >> (_bitsRead - bitCount)) & ((1UL << bitCount) - 1));
            _bitsRead -= bitCount;
            return temp;
        }

        // Read a specific byte from the buffer
        private int ReadByte(int offset)
        {
            if (_dataBuffer == null || offset < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            return (int)_dataBuffer[offset];
        }
        #endregion

        #region LOGGING
        public override string ToString()
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine($"MP3 Frame Data");
            if (!IsHeaderDecoded)
            {
                log.AppendLine($"\tNot yet decoded");
            }
            else
            {
                int headerData = BitConverter.ToInt32(_dataBuffer, 0);
                log.AppendLine($"\tBits: {GetBitString(headerData)}");
                log.AppendLine($"\tRaw: {BitConverter.ToString(_dataBuffer)}");
                log.AppendLine($"\tVersion: {Version.ToString()}");
                log.AppendLine($"\tLayer: {Layer.ToString()}");
                log.AppendLine($"\tChannel Mode: {ChannelMode.ToString()}");
                log.AppendLine($"\tCrc: {HasCrc}");
                log.AppendLine($"\tCopyright: {IsCopyrighted}");
                log.AppendLine($"\tBit Rate[{BitRateIndex}]: {BitRate}");
                log.AppendLine($"\tSample Rate[{SampleRateIndex}]: {SampleRate}");
                log.AppendLine($"\tSample Count: {SampleCount}");
                log.AppendLine($"\tFrame Length: {FrameLength}");
            }
            return log.ToString();
        }
        internal static string GetBitString(int headerData)
        {
            StringBuilder sb = new StringBuilder();
            for (int b = 31; b >= 0; b--)
            {
                sb.Append(BitRShift(headerData, b) & 1);
                if (b % 8 == 0 && b > 0)
                {
                    sb.Append(" ");
                }
            }
            return sb.ToString();
        }
        #endregion
    }
}
