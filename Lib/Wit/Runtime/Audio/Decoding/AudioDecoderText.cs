/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meta.WitAi;
using UnityEngine;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// A delegate to be called when text data is decoded from an audio stream
    /// </summary>
    public delegate void AudioTextDecodeDelegate(string decodedText);

    /// <summary>
    /// A decoder for text and audio within a single data stream.  Decodes
    /// the data into split audio and text streams
    /// </summary>
    public class AudioDecoderText : IAudioDecoder
    {
        // Used for audio decoding
        private IAudioDecoder _audioDecoder;

        // The decode callback that is called for every json decode
        private AudioTextDecodeDelegate _onTextDecoded;

        /// <summary>
        /// Due to headers, sequential decode is required
        /// </summary>
        public bool RequireSequentialDecode => true;

        // Header decode data
        private int _headerDecoded = 0;
        private byte[] _headerBytes = new byte[HEADER_SIZE];

        // Text decode data
        private bool _hasText = false;
        private byte _flags = 0x0;
        private long _textLength = 0;
        private long _audioLength = 0;

        // The current text chunk
        private StringBuilder _textChunk = new StringBuilder();
        private int _chunksDecoded = 0;

        // Text constants
        private const int FLAG_SIZE = 1;
        private const int LONG_SIZE = 8;
        private const int HEADER_SIZE = FLAG_SIZE + (LONG_SIZE * 2); // 1 flag byte + 8 audio size bytes + 8 text size bytes
        private const int MAX_TEXT_LENGTH = 10000; // Max 10k characters
        private const int MAX_AUDIO_LENGTH = WitConstants.ENDPOINT_TTS_CHANNELS * WitConstants.ENDPOINT_TTS_SAMPLE_RATE * 2 * 30; // Max 30 seconds of audio

        /// <summary>
        /// Constructor that takes in an audio decoder and decode callback delegate
        /// </summary>
        /// <param name="audioDecoder">The audio decoder to receive </param>
        /// <param name="onTextDecode">The delegate to be called every time a text chunk is decoded</param>
        public AudioDecoderText(IAudioDecoder audioDecoder, AudioTextDecodeDelegate onTextDecode)
        {
            _audioDecoder = audioDecoder;
            _onTextDecoded = onTextDecode;
        }

        /// <summary>
        /// Performs an audio decode setup with specified channels and sample rate
        /// </summary>
        /// <param name="channels">Channels supported by audio file</param>
        /// <param name="sampleRate">Sample rate supported by audio file</param>
        public void Setup(int channels, int sampleRate)
        {
            _audioDecoder.Setup(channels, sampleRate);
            ResetChunk();
        }

        /// <summary>
        /// Cannot determine total samples via content length alone
        /// </summary>
        public int GetTotalSamples(ulong contentLength) => -1;

        /// <summary>
        /// Performs a decode of full chunk data
        /// </summary>
        /// <param name="chunkData">A chunk of bytes to be decoded into audio data</param>
        /// <param name="chunkStart">The array start index into account when decoding</param>
        /// <param name="chunkLength">The total number of bytes to be used within chunkData</param>
        /// <returns>Returns an array of audio data from 0-1</returns>
        public float[] Decode(byte[] chunkData, int chunkStart, int chunkLength)
        {
            // Resultant float array
            int start = chunkStart;
            List<float> samples = new List<float>();

            // Iterate until chunk is complete
            while (start < chunkLength)
            {
                // Decode a single frame, return samples if possible and update start position
                int length = chunkLength - start;
                DecodeChunk(chunkData, ref start, length, samples);
            }

            // Return results
            return samples.ToArray();
        }

        // Reset chunk data
        private void ResetChunk()
        {
            _headerDecoded = 0;
            _audioLength = 0;
            _textLength = 0;
            _hasText = false;
            _textChunk.Clear();
        }

        // Decodes a chunk by checking header, applying samples and returning text if applicable
        private void DecodeChunk(byte[] chunkData, ref int start, int length, List<float> samples)
        {
            // Header still needs decode
            if (_headerDecoded < HEADER_SIZE)
            {
                // Decode either remainder of bytes or full length
                int decodeLength = Mathf.Min(length, HEADER_SIZE - _headerDecoded);

                // Add to header bytes array
                Array.Copy(chunkData, start, _headerBytes, _headerDecoded, decodeLength);
                start += decodeLength;
                length -= decodeLength;
                _headerDecoded += decodeLength;

                // Needs more
                if (_headerDecoded < HEADER_SIZE)
                {
                    return;
                }

                // Decode header items
                _flags = _headerBytes[0];
                _textLength = Max(0, BitConverter.ToInt64(_headerBytes, FLAG_SIZE));
                _audioLength = Max(0, BitConverter.ToInt64(_headerBytes, FLAG_SIZE + LONG_SIZE));

                // Has text if flags are not empty and text length or audio length is more than 0
                _hasText = true;
                if (_flags == 0x0)
                {
                    VLog.E(GetType().Name, $"Chunk Header Decode Failed: Invalid Flags\n{GetChunkLog()}\n");
                    _hasText = false;
                }
                else if (_textLength < 0 || _textLength > MAX_TEXT_LENGTH)
                {
                    VLog.E(GetType().Name, $"Chunk Header Decode Failed: Invalid Text Length\n{GetChunkLog()}\n");
                    _hasText = false;
                }
                else if (_audioLength < 0 || _audioLength > MAX_AUDIO_LENGTH)
                {
                    VLog.E(GetType().Name, $"Chunk Header Decode Failed: Invalid Audio Length\n{GetChunkLog()}\n");
                    _hasText = false;
                }

                // Text data not included, decode header
                if (!_hasText)
                {
                    samples.AddRange(_audioDecoder.Decode(_headerBytes, 0, HEADER_SIZE));
                }
            }

            // No text, decode remaining contents
            if (!_hasText)
            {
                samples.AddRange(_audioDecoder.Decode(chunkData, start, length));
                start += length;
                return;
            }

            // Decode text if applicable
            DecodeTextChunk(chunkData, ref start, ref length);

            // Decode audio if applicable
            DecodeAudioChunk(chunkData, ref start, ref length, samples);

            // If audio and text is complete, reset chunk
            if (_textLength == 0 && _audioLength == 0)
            {
                _chunksDecoded++;
                ResetChunk();
            }
        }

        // Return header info in log string
        private string GetChunkLog()
        {
            return $"Chunk Index: {_chunksDecoded}\nFlags: {_flags} ({GetBitString(_headerBytes, 0, FLAG_SIZE)})\nText Length: {_textLength} (Max {MAX_TEXT_LENGTH}) ({GetBitString(_textLength)})\nAudio Length: {_audioLength} (Max {MAX_AUDIO_LENGTH}) ({GetBitString(_audioLength)})\n\nRaw:\n{GetBitString(_headerBytes).Reverse().ToArray()}";
        }

        private void DecodeTextChunk(byte[] chunkData, ref int start, ref int length)
        {
            // No viseme to be decoded
            if (length <= 0 || _textLength <= 0)
            {
                return;
            }

            // Decode either the remainder of bytes or the full audio length
            int decodeLength = Mathf.Min(length, SafeCast(_textLength));

            // Decode text chunk
            string chunk = Encoding.UTF8.GetString(chunkData, start, decodeLength);
            _textChunk.Append(chunk);

            // Subtract decode length
            start += decodeLength;
            length -= decodeLength;
            _textLength -= decodeLength;

            // Text decode complete
            if (_textLength == 0)
            {
                _onTextDecoded?.Invoke(_textChunk.ToString());
            }
        }

        private void DecodeAudioChunk(byte[] chunkData, ref int start, ref int length, List<float> samples)
        {
            // No audio data expected
            if (length <= 0 || _audioLength <= 0)
            {
                return;
            }

            // Decode either the remainder of bytes or the full audio length
            int decodeLength = Mathf.Min(length, SafeCast(_audioLength));

            // Decode audio chunk
            samples.AddRange(_audioDecoder.Decode(chunkData, start, decodeLength));

            // Subtract decode length
            start += decodeLength;
            length -= decodeLength;
            _audioLength -= decodeLength;
        }

        // Casts to int while clamping to int max
        private static int SafeCast(long length)
        {
            long intMax = (long)int.MaxValue;
            return length >= intMax ? int.MaxValue : (int)length;
        }

        private static long Max(long option1, long option2) =>
            option1 > option2 ? option1 : option2;

        private static string GetBitString(byte[] bytes, int start, int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int by = start + length - 1; by >= start; by--)
            {
                for (int bi = 7; bi >= 0; bi--)
                {
                    sb.Append((bytes[by] >> bi) & 1);
                }
                if (by != start)
                {
                    sb.Append(" ");
                }
            }
            return sb.ToString();
        }
        private static string GetBitString(byte[] bytes) =>
            GetBitString(bytes, 0, bytes.Length);
        private static string GetBitString(long number) =>
            GetBitString(BitConverter.GetBytes(number));
        private static string GetBitString(int number) =>
            GetBitString(BitConverter.GetBytes(number));
    }
}
