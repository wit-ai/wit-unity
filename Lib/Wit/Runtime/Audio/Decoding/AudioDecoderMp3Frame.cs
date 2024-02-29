/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;
using Meta.Audio.NLayer;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// An audio decoder for raw MPEG audio data
    /// </summary>
    [Preserve]
    internal class AudioDecoderMp3Frame : IMpegFrame
    {
        /// <summary>
        /// Whether or not the header has been decoded
        /// </summary>
        private bool _headerDecoded = false;
        private byte[] _headerBytes = new byte[4];

        // Array buffers used to reduce allocations
        private byte[] _frameBuffer = null;
        private float[] _sampleBuffer = null;
        private int _frameOffset;

        // Script that handles decoding frames
        private MpegFrameDecoder _decoder = new MpegFrameDecoder();

        // Bit offset of current bit stream
        private int _readOffset;
        // In progress 8 bits for decoding
        private ulong _bitBucket = 0UL;
        // Total bits read in current frame
        private int _bitsRead;

        // Index of how many frames have been decoded
        private int _frameIndex;

        /// <summary>
        /// Resets all frame specific data
        /// </summary>
        private void Clear()
        {
            _headerDecoded = false;
            _frameOffset = 0;
            Reset();
        }

        /// <summary>
        /// Resets all read data
        /// </summary>
        public void Reset()
        {
            _readOffset = 4 + (HasCrc ? 2 : 0); // Starts reading following header 4 bits & 2 CRC bits if applicable
            _bitBucket = 0UL; // In progress 8 bits for decoding
            _bitsRead = 0; // All bits decoded
        }

        /// <summary>
        /// Decodes the frame & returns the number of leftover
        /// </summary>
        /// <param name="chunkData">A chunk of bytes received from a web service</param>
        /// <param name="start">The location to begin decoding chunkData</param>
        /// <param name="length">The total number of bytes to be used within chunkData</param>
        /// <returns>Returns an array of audio data from 0-1</returns>
        public float[] Decode(byte[] chunkData, ref int start, int length)
        {
            // Header still needs decode
            if (!_headerDecoded)
            {
                // Too small
                if (length < 4)
                {
                    Debug.LogError($"MP3 Frame {_frameIndex} - Not enough bytes for header decode");
                    start += length;
                    return null;
                }

                // Get header bytes
                for (int i = 0; i < _headerBytes.Length; i++)
                {
                    _headerBytes[3 - i] = chunkData[start + i];
                }

                // Decode header
                try
                {
                    int headerData = BitConverter.ToInt32(_headerBytes, 0);
                    DecodeHeader(headerData);
                    _headerDecoded = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"MP3 Frame {_frameIndex} - Header Decode Failed\n\n{e}\n");
                    _frameOffset += 1;
                    start += 1;
                    return null;
                }

                // Generate buffers on first frame (Assumes all frames have same amount
                if (_frameBuffer == null || _frameBuffer.Length != FrameLength)
                {
                    _frameBuffer = new byte[FrameLength];
                    _sampleBuffer = new float[SampleCount];
                }
            }

            // Decode either the remainder of bytes
            int decodeLength = Mathf.Min(length, FrameLength - _frameOffset);

            // Copy chunk data into frame buffer
            Array.Copy(chunkData, start, _frameBuffer, _frameOffset, decodeLength);
            _frameOffset += decodeLength;
            start += decodeLength;

            // Ignore until finished
            if (_frameOffset < FrameLength)
            {
                return null;
            }

            // Decode audio into sample buffer
            _decoder.DecodeFrame(this, _sampleBuffer, 0);
            var results = _sampleBuffer;

            // Increment frame count & clear previous data
            _frameIndex++;
            Clear();

            // Return results
            return results;
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
        private void DecodeHeader(int headerData)
        {
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
            if (_frameBuffer == null || offset < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (offset >= _frameBuffer.Length)
            {
                return -1;
            }
            return (int)_frameBuffer[offset];
        }
        #endregion

        #region LOGGING
        public override string ToString()
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine($"MP3 Frame Data");
            if (!_headerDecoded)
            {
                log.AppendLine($"\tNot yet decoded");
            }
            else
            {
                int headerData = BitConverter.ToInt32(_headerBytes, 0);
                log.AppendLine($"\tBits: {GetBitString(headerData)}");
                log.AppendLine($"\tRaw: {BitConverter.ToString(_headerBytes)}");
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
