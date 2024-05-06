/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Voice.Net.Encoding.Wit
{
    /// <summary>
    /// A static class used to encode and decode wit data chunks
    /// consisting of json mixed with binary data.
    /// </summary>
    public class WitChunkConverter
    {
        /// <summary>
        /// Class used to encode/decode text
        /// </summary>
        private static UTF8Encoding _textEncoding = new UTF8Encoding();

        #region DECODING
        // Header decode data
        private int _headerDecoded = 0;
        private byte[] _headerBytes = new byte[HEADER_SIZE];

        // Used to count down as bytes arrive
        private int _jsonLength;
        private int _binaryLength;

        // Current json chunk data
        private byte[] _jsonData;

        // Current binary chunk handler
        private byte[] _binaryData;

        // Text constants
        private const int FLAG_SIZE = 1;
        private const int LONG_SIZE = 8;
        private const int HEADER_SIZE = FLAG_SIZE + (LONG_SIZE * 2); // 1 flag byte + 8 json size bytes + 8 audio size bytes

        /// <summary>
        /// Decodes an array of chunk data
        /// </summary>
        /// <param name="rawData">A chunk of bytes to be split into json data and binary data</param>
        /// <param name="start">The chunk array start index used for decoding</param>
        /// <param name="length">The total number of bytes to be used within chunkData</param>
        public WitChunk[] Decode(byte[] rawData, int start, int length)
        {
            var chunks = new List<WitChunk>();
            int index = start;
            while (index < length)
            {
                int remainder = length - index;
                var chunk = DecodeChunk(rawData, ref index, remainder);
                if (chunk != null)
                {
                    chunks.Add(chunk);
                }
            }
            return chunks.ToArray();
        }

        /// <summary>
        /// Decodes an array of chunk data
        /// </summary>
        /// <param name="rawData">A chunk of bytes to be split into json data and binary data</param>
        /// <param name="start">The chunk array start index used for decoding</param>
        /// <param name="length">The total number of bytes to be used within chunkData</param>
        private WitChunk DecodeChunk(byte[] rawData, ref int start, int length)
        {
            // Header still needs decode
            if (_headerDecoded < HEADER_SIZE)
            {
                // Decode as much of header as possible
                int decodeLength = Mathf.Min(length, HEADER_SIZE - _headerDecoded);
                Array.Copy(rawData, start, _headerBytes, _headerDecoded, decodeLength);
                start += decodeLength;
                length -= decodeLength;
                _headerDecoded += decodeLength;

                // Needs more
                if (_headerDecoded < HEADER_SIZE)
                {
                    return null;
                }

                // Decode all header data
                DecodeHeader(_headerBytes, out var jsonLength, out var binaryLength, out var invalid);
                _jsonLength = (int)jsonLength;
                _binaryLength = (int)binaryLength;

                // Invalid scenario, log a warning
                if (invalid)
                {
                    start -= decodeLength;
                    length += decodeLength;
                    _jsonLength = 0;
                    _binaryLength = length;
                    VLog.W(GetType().Name, $"Chunk Header Decode Failed: Header is invalid - assuming all binary data\n{GetHeaderLog(_headerBytes, _jsonLength, _binaryLength, true)}\n");
                }
                // Uncomment for verbose per chunk debugging
                //else VLog.I(GetType().Name, $"Chunk Header Decode Success\n{GetHeaderLog(_headerBytes, _jsonLength, _binaryLength, false)}\n");

                // Generate json data array
                if (_jsonLength > 0)
                {
                    _jsonData = new byte[_jsonLength];
                }
                // Generate binary data array
                if (_binaryLength > 0)
                {
                    _binaryData = new byte[_binaryLength];
                }
            }

            // Copy json chunk if into json byte[] if applicable
            CopyRawChunk(rawData, ref start, ref length, _jsonData, ref _jsonLength);

            // Copy binary chunk if into binary byte[] if applicable
            CopyRawChunk(rawData, ref start, ref length, _binaryData, ref _binaryLength);

            // If audio and text is complete, reset chunk
            if (_jsonLength == 0 && _binaryLength == 0)
            {
                // Get data chunk
                WitChunk chunkData = new WitChunk();
                if (_jsonData != null)
                {
                    // Decode string
                    chunkData.jsonString = DecodeString(_jsonData, 0, _jsonData.Length);
                    // Deserialize string into a token
                    chunkData.jsonData = JsonConvert.DeserializeToken(chunkData.jsonString);
                }
                // Apply binary data
                chunkData.binaryData = _binaryData;
                // Reset and return finalized chunk
                ResetChunk();
                return chunkData;
            }

            // Awaiting more data
            return null;
        }

        // Resets all stored chunk data
        private void ResetChunk()
        {
            _headerDecoded = 0;
            _jsonLength = 0;
            _binaryLength = 0;
            _jsonData = null;
            _binaryData = null;
        }

        /// <summary>
        /// Decodes header flags, json length, binary length and determines checks the following invalid scenarios
        /// 1. Unhandled flags enabled
        /// 2. Invalid json byte[] length
        /// 3. Json length exists but hasJson flag is disabled
        /// 4. Invalid binary byte[] length
        /// 5. Binary length exists but hasBinary flag is disabled
        /// </summary>
        /// <param name="bytes">Bytes to be used for the header decode</param>
        /// <param name="jsonLength">The returned json length in bytes</param>
        /// <param name="binaryLength">The returned binary length in bytes</param>
        /// <param name="invalid">Whether or not</param>
        private static void DecodeHeader(byte[] bytes, out long jsonLength, out long binaryLength, out bool invalid)
        {
            // Get first byte
            var flags = bytes[0];
            // Valid if bit 0 = 1
            bool hasBinary = (flags & 1) != 0;
            // Valid if bit 1 = 1
            bool hasJson = (SafeShift(flags, 1) & 1) != 0;
            // Invalid if any bits 2-8 != 0
            invalid = (SafeShift(flags, 2) & 0x3F) != 0;

            // Get json length and determine if invalid
            jsonLength = BitConverter.ToInt64(bytes, FLAG_SIZE);
            invalid |= jsonLength < 0 || jsonLength >= int.MaxValue;
            invalid |= jsonLength > 0 && !hasJson;

            // Get binary length and determine if invalid
            binaryLength = BitConverter.ToInt64(bytes, FLAG_SIZE + LONG_SIZE);
            invalid |= binaryLength < 0 || binaryLength >= int.MaxValue;
            invalid |= binaryLength > 0 && !hasBinary;
        }

        /// <summary>
        /// Shifts flag bits a specified amount of indices towards the start of the byte.
        /// </summary>
        /// <param name="flags">The byte to be shifted</param>
        /// <param name="index">Total indices to shift</param>
        private static int SafeShift(byte flags, int index)
            => BitConverter.IsLittleEndian ? flags >> index : flags << index;

        /// <summary>
        /// Returns a log string for a byte[] by returning the individual bytes for each section.
        /// Determines specific settings before returning the log.
        /// </summary>
        public static string GetHeaderLog(byte[] bytes)
        {
            DecodeHeader(bytes, out var jsonLength, out var binaryLength, out var invalid);
            return GetHeaderLog(bytes, jsonLength, binaryLength, invalid);
        }

        /// <summary>
        /// Returns a log string for a byte[] by returning the individual bytes for each section.
        /// </summary>
        public static string GetHeaderLog(byte[] bytes, long jsonLength, long binaryLength, bool invalid)
        {
            string log = $"Flags: {bytes[0]} ({GetBitString(bytes, 0, FLAG_SIZE)})";
            log += $"\nInvalid: {invalid}";
            log += $"\nJson Length: {jsonLength} ({GetByteString(bytes, FLAG_SIZE, LONG_SIZE)})";
            log += $"\nBinary Length: {binaryLength} ({GetByteString(bytes, FLAG_SIZE + LONG_SIZE, LONG_SIZE)})";
            log += $"\nFull Header:\n{GetByteString(bytes, 0, HEADER_SIZE)}";
            return log;
        }

        /// <summary>
        /// Returns a string of all bytes within an array
        /// </summary>
        public static string GetByteString(byte[] bytes, int start, int length, bool reverse = false)
        {
            string results = BitConverter.ToString(bytes, start, length);
            if (reverse)
            {
                return new string(results.Reverse().ToArray());
            }
            return results;
        }

        /// <summary>
        /// Returns a string of the individual bits within a byte array
        /// </summary>
        public static string GetBitString(byte[] bytes, int start, int length, bool reverse = false)
        {
            StringBuilder sb = new StringBuilder();
            for (int by = start; by < length; by++)
            {
                for (int bi = 0; bi < 8; bi++)
                {
                    sb.Append(SafeShift(bytes[by], bi) & 1);
                }
                if (by != length - 1)
                {
                    sb.Append(" ");
                }
            }
            if (reverse)
            {
                return new string(sb.ToString().Reverse().ToArray());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Copies as much of a provided chunk as possible using the provided settings
        /// </summary>
        private void CopyRawChunk(byte[] rawData, ref int start, ref int length, byte[] chunk, ref int remainder)
        {
            // Decode either the remainder of bytes or the full raw data length
            int decodeLength = Mathf.Min(remainder, length);
            if (decodeLength <= 0)
            {
                return;
            }

            // Copy
            Array.Copy(rawData, start, chunk, chunk.Length - remainder, decodeLength);

            // Subtract decode length
            start += decodeLength;
            length -= decodeLength;
            remainder -= decodeLength;
        }

        /// <summary>
        /// Method for decoding raw data into a string
        /// </summary>
        public static string DecodeString(byte[] rawData, int offset, int length) => _textEncoding.GetString(rawData, offset, length);
        #endregion DECODING

        #region ENCODING
        /// <summary>
        /// Encodes a chunk by using the jsonString if found, otherwise
        /// serializes the json data itself.
        /// </summary>
        public static byte[] Encode(WitChunk chunkData)
        {
            if (string.IsNullOrEmpty(chunkData.jsonString))
            {
                chunkData.jsonString = chunkData.jsonData?.ToString();
            }
            return Encode(chunkData.jsonString, chunkData.binaryData);
        }

        /// <summary>
        /// Encodes a binary data into a wit stream
        /// </summary>
        public static byte[] Encode(byte[] binaryData)
            => Encode((byte[])null, binaryData);

        /// <summary>
        /// Encodes a json token and raw binary data into a single stream
        /// </summary>
        public static byte[] Encode(WitResponseNode jsonToken, byte[] binaryData = null)
            => Encode(jsonToken?.ToString(), binaryData);

        /// <summary>
        /// Encodes a json string and raw binary data into a single stream
        /// </summary>
        public static byte[] Encode(string jsonString, byte[] binaryData = null)
            => Encode(EncodeString(jsonString), binaryData);

        /// <summary>
        /// Encodes a json byte[] and raw binary data into a single stream
        /// </summary>
        public static byte[] Encode(byte[] jsonData, byte[] binaryData)
        {
            // Determine final length
            int jsonLength = jsonData?.Length ?? 0;
            int binaryLength = binaryData?.Length ?? 0;
            int totalLength = HEADER_SIZE + jsonLength + binaryLength;

            // Apply to final array
            var results = new byte[totalLength];
            results[0] = EncodeFlag(jsonLength > 0, binaryLength > 0);
            int offset = 1;
            EncodeLength(results, ref offset, jsonLength);
            EncodeLength(results, ref offset, binaryLength);
            EncodeBytes(results, ref offset, jsonData);
            EncodeBytes(results, ref offset, binaryData);

            // Return array
            return results;
        }

        /// <summary>
        /// Method for encoding a string into raw data
        /// </summary>
        public static byte[] EncodeString(string stringData)
            => string.IsNullOrEmpty(stringData) ? null : _textEncoding.GetBytes(stringData);

        /// <summary>
        /// Determine flag based on data provided
        /// </summary>
        private const byte FLAG_NO_JSON_NO_BINARY = 0x0;
        private const byte FLAG_NO_JSON_YES_BINARY = 0x1;
        private const byte FLAG_YES_JSON_NO_BINARY = 0x2;
        private const byte FLAG_YES_JSON_YES_BINARY = 0x3;
        private static byte EncodeFlag(bool hasJson, bool hasBinary)
        {
            if (hasJson)
            {
                return (hasBinary ? FLAG_YES_JSON_YES_BINARY : FLAG_YES_JSON_NO_BINARY);
            }
            return (hasBinary ? FLAG_NO_JSON_YES_BINARY : FLAG_NO_JSON_NO_BINARY);
        }

        /// <summary>
        /// Convert length into bytes and copy bytes to desired offset
        /// </summary>
        private static void EncodeLength(byte[] destination, ref int offset, long length)
        {
            var lengthBytes = BitConverter.GetBytes(length);
            EncodeBytes(destination, ref offset, lengthBytes);
        }

        /// <summary>
        /// Safely copy bytes to desired offset & increment offset if applicable
        /// </summary>
        private static void EncodeBytes(byte[] destination, ref int offset, byte[] source)
        {
            if (source == null || source.Length == 0)
            {
                return;
            }
            Array.Copy(source, 0, destination, offset, source.Length);
            offset += source.Length;
        }
        #endregion ENCODING
    }
}
