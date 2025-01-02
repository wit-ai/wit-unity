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
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Logging;
using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Voice.Net.Encoding.Wit
{
    /// <summary>
    /// A static class used to encode and decode wit data chunks
    /// consisting of json mixed with binary data.
    /// </summary>
    [LogCategory(LogCategory.Encoding)]
    public class WitChunkConverter: ILogSource
    {
        /// <summary>
        /// Class used to encode/decode text
        /// </summary>
        private static readonly UTF8Encoding TextEncoding = new UTF8Encoding();

        // For logging
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Encoding);

        #region DECODING
        // The current chunk being decoded
        private WitChunk _currentChunk = new WitChunk();

        // Decoding header
        private int _headerDecoded = 0;
        private byte[] _headerBytes = new byte[HEADER_SIZE];
        private bool IsHeaderDecoded => _headerDecoded >= HEADER_SIZE;

        // Decoding json
        private int _jsonDecoded = 0;
        private StringBuilder _jsonBuilder = new StringBuilder();
        private bool IsJsonDecoded => _jsonDecoded >= _currentChunk.header.jsonLength;

        // Decoding binary
        private ulong _binaryDecoded = 0;
        private bool IsBinaryDecoded => _binaryDecoded >= _currentChunk.header.binaryLength;

        // Text constants
        private const int FLAG_SIZE = 1;
        private const int LONG_SIZE = 8;
        private const int HEADER_SIZE = FLAG_SIZE + (LONG_SIZE * 2); // 1 flag byte + 8 json size bytes + 8 audio size bytes

        // Resets all stored chunk data
        private void ResetChunk()
        {
            _headerDecoded = 0;
            _jsonDecoded = 0;
            _jsonBuilder.Clear();
            _binaryDecoded = 0;
            _currentChunk.jsonString = null;
            _currentChunk.jsonData = null;
            _currentChunk.binaryData = null;
        }

        /// <summary>
        /// Decodes a buffer of raw bytes and appends chunks
        /// </summary>
        /// <param name="buffer">A chunk of bytes to be split into json data and binary data</param>
        /// <param name="bufferOffset">The chunk array start index used for decoding</param>
        /// <param name="bufferLength">The total number of bytes to be used within chunkData</param>
        /// <param name="decodedChunks">A list that newly decoded chunks will be added to</param>
        /// <param name="customBinaryDecoder">If exists, binary data will be sent back here instead of the WitChunk</param>
        public void Decode(byte[] buffer, int bufferOffset, int bufferLength,
            Action<WitChunk> onChunkDecoded,
            Action<byte[], int, int> customBinaryDecoder = null)
        {
            while (bufferLength > 0)
            {
                // Decode a single chunk
                var decodeLength = DecodeChunk(buffer, bufferOffset, bufferLength, onChunkDecoded, customBinaryDecoder);

                // Increment counts
                bufferOffset += decodeLength;
                bufferLength -= decodeLength;
            }
        }

        /// <summary>
        /// Decodes an array of chunk data
        /// </summary>
        private int DecodeChunk(byte[] buffer, int bufferOffset, int bufferLength,
            Action<WitChunk> onChunkDecoded,
            Action<byte[], int, int> customBinaryDecoder)
        {
            // Total decoded from the buffer
            int decodeLength = 0;

            // Header still needs decode
            if (!IsHeaderDecoded)
            {
                // Decode header if possible
                decodeLength = DecodeHeader(buffer, bufferOffset, bufferLength);
                if (!IsHeaderDecoded)
                {
                    return decodeLength;
                }
                bufferOffset += decodeLength;
                bufferLength -= decodeLength;

                // Header decode failed
                if (_currentChunk.header.invalid)
                {
                    Logger.Error("WitChunk Header Decode Failed: Header is invalid\nHeader: {0}",
                        WitRequestSettings.GetByteString(_headerBytes, 0, HEADER_SIZE));
                    ResetChunk();
                    return decodeLength;
                }

                // Generate binary data if not handled directly
                if (customBinaryDecoder == null)
                {
                    var curLength = _currentChunk.binaryData?.Length ?? 0;
                    var desLength = (int)_currentChunk.header.binaryLength;
                    if (curLength != desLength)
                    {
                        _currentChunk.binaryData = new byte[desLength];
                    }
                }
            }

            // Decode json if possible
            if (!IsJsonDecoded)
            {
                var jsonLength = DecodeJson(buffer, bufferOffset, bufferLength);
                decodeLength += jsonLength;
                bufferOffset += jsonLength;
                bufferLength -= jsonLength;

                // If custom binary handler exists, return json asap
                if (IsJsonDecoded && customBinaryDecoder != null)
                {
                    onChunkDecoded?.Invoke(_currentChunk);
                }
            }

            // Decode binary if possible
            if (!IsBinaryDecoded)
            {
                var binaryLength = DecodeBinary(buffer, bufferOffset, bufferLength, customBinaryDecoder);
                decodeLength += binaryLength;
            }

            // If audio and text is complete, add and reset chunk
            if (IsJsonDecoded && IsBinaryDecoded)
            {
                // If no custom binary handler, return once complete
                if (customBinaryDecoder == null)
                {
                    onChunkDecoded?.Invoke(_currentChunk);
                }
                // Reset chunk
                ResetChunk();
            }

            // Return decoded length
            return decodeLength;
        }

        /// <summary>
        /// Decodes as much of header as possible and generates header when complete
        /// </summary>
        private int DecodeHeader(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Decode as much of header as possible
            var offset = _headerDecoded;
            var remainder = HEADER_SIZE - _headerDecoded;
            int decodeLength = Mathf.Min(bufferLength, remainder);
            Array.Copy(buffer, bufferOffset, _headerBytes, offset, decodeLength);
            _headerDecoded += decodeLength;

            // If fully decoded, return the header
            if (IsHeaderDecoded)
            {
                _currentChunk.header = GetHeader(_headerBytes, 0);
            }

            // Return decoded length
            return decodeLength;
        }

        /// <summary>
        /// Decodes as much of json as possible and decodes json when complete
        /// </summary>
        private int DecodeJson(byte[] buffer, int bufferOffset, int bufferLength)
        {
            // Nothing to decode
            var offset = _jsonDecoded;
            var remainder = _currentChunk.header.jsonLength - offset;
            int decodeLength = Mathf.Min(bufferLength, remainder);
            if (decodeLength <= 0)
            {
                return 0;
            }

            // Append decoded json
            var decodedStringChunk = DecodeString(buffer, bufferOffset, decodeLength);
            _jsonBuilder.Append(decodedStringChunk);
            _jsonDecoded += decodeLength;

            // Fully decoded
            if (IsJsonDecoded)
            {
                var jsonString = _jsonBuilder.ToString();
                _currentChunk.jsonString = jsonString;
                _currentChunk.jsonData = JsonConvert.DeserializeToken(jsonString);
            }

            // Return decode length
            return decodeLength;
        }

        /// <summary>
        /// Decodes as much binary data as possible and decodes json when complete
        /// </summary>
        private int DecodeBinary(byte[] buffer, int bufferOffset, int bufferLength,
            Action<byte[], int, int> customBinaryDecoder)
        {
            // Nothing to decode
            var offset = _binaryDecoded;
            var remainder = _currentChunk.header.binaryLength - offset;
            int decodeLength = Mathf.Min(bufferLength, (int)remainder);
            if (decodeLength <= 0)
            {
                return 0;
            }

            // If custom binary decoder exists perform directly
            if (customBinaryDecoder != null)
            {
                customBinaryDecoder.Invoke(buffer, bufferOffset, decodeLength);
            }
            // Copy into generated array
            else if (_currentChunk.binaryData != null)
            {
                Array.Copy(buffer, bufferOffset, _currentChunk.binaryData, (int)_binaryDecoded, decodeLength);
            }

            // Increment binary decode count
            _binaryDecoded += (ulong)decodeLength;

            // Return decode length
            return decodeLength;
        }

        /// <summary>
        /// Method for decoding raw data into a string
        /// </summary>
        public static string DecodeString(byte[] rawData, int offset, int length) => TextEncoding.GetString(rawData, offset, length);
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
            => string.IsNullOrEmpty(stringData) ? null : TextEncoding.GetBytes(stringData);

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

        #region HEADER
        /// <summary>
        /// Decodes header flags, json length, binary length and determines checks the following invalid scenarios
        /// 1. Unhandled flags enabled
        /// 2. Invalid json byte[] length
        /// 3. Json length exists but hasJson flag is disabled
        /// 4. Invalid binary byte[] length
        /// 5. Binary length exists but hasBinary flag is disabled
        /// </summary>
        /// <param name="bytes">Bytes to be used for the header decode</param>
        /// <param name="offset">The offset of the bytes provided</param>
        private static WitChunkHeader GetHeader(byte[] bytes, int offset)
        {
            // Generate header
            WitChunkHeader header = new WitChunkHeader();

            // Get first byte
            var flags = bytes[offset];
            // Valid if bit 0 = 1
            bool hasBinary = (flags & 1) != 0;
            // Valid if bit 1 = 1
            bool hasJson = (SafeShift(flags, 1) & 1) != 0;
            // Invalid if any bits 2-8 != 0
            header.invalid = (SafeShift(flags, 2) & 0x3F) != 0;

            // Get json length and determine if invalid
            var jsonLength = BitConverter.ToInt64(bytes, offset + FLAG_SIZE);
            header.jsonLength = (int)jsonLength; // Cast to int since json string cannot be long
            header.invalid |= jsonLength < 0 && hasJson;
            header.invalid |= jsonLength > 0 && !hasJson;

            // Get binary length and determine if invalid
            var binaryLength = BitConverter.ToInt64(bytes, offset + FLAG_SIZE + LONG_SIZE);
            header.binaryLength = (ulong)binaryLength; // Cast to ulong binary data length cannot be negative
            header.invalid |= binaryLength < 0 && hasBinary;
            header.invalid |= binaryLength > 0 && !hasBinary;

            // Return new header
            return header;
        }

        /// <summary>
        /// Shifts flag bits a specified amount of indices towards the start of the byte.
        /// </summary>
        /// <param name="flags">The byte to be shifted</param>
        /// <param name="index">Total indices to shift</param>
        private static int SafeShift(byte flags, int index)
            => BitConverter.IsLittleEndian ? flags >> index : flags << index;
        #endregion HEADER
    }
}
