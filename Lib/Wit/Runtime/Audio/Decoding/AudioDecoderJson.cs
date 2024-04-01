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
using Meta.Voice.Net.Encoding.Wit;
using Meta.WitAi;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// A delegate to be called when text data is decoded from an audio stream
    /// </summary>
    public delegate void AudioJsonDecodeDelegate(WitResponseNode[] jsonNodes);

    /// <summary>
    /// A decoder for json data and audio within a single data stream.
    /// Decodes the data into split audio and text streams
    /// </summary>
    public class AudioDecoderJson : IAudioDecoder
    {
        // Used for mixed json/binary data decoding
        private readonly WitChunkConverter _chunkDecoder = new WitChunkConverter();
        // Used for audio decoding
        private readonly IAudioDecoder _audioDecoder;
        // The decode callback that is called for every json decode
        private readonly AudioJsonDecodeDelegate _onJsonDecoded;

        /// <summary>
        /// Once setup this should display the number of channels expected to be decoded
        /// </summary>
        public int Channels => _audioDecoder.Channels;

        /// <summary>
        /// Once setup this should display the number of samples per second expected
        /// </summary>
        public int SampleRate => _audioDecoder.SampleRate;

        /// <summary>
        /// Due to headers, sequential decode is required
        /// </summary>
        public bool RequireSequentialDecode => true;

        /// <summary>
        /// Constructor that takes in an audio decoder and decode callback delegate
        /// </summary>
        /// <param name="audioDecoder">The audio decoder to receive </param>
        /// <param name="onJsonDecoded">The delegate to be called every time a json chunk is decoded</param>
        public AudioDecoderJson(IAudioDecoder audioDecoder, AudioJsonDecodeDelegate onJsonDecoded)
        {
            _audioDecoder = audioDecoder;
            _onJsonDecoded = onJsonDecoded;
        }

        /// <summary>
        /// Performs an audio decode setup with specified channels and sample rate
        /// </summary>
        /// <param name="channels">Channels supported by audio file</param>
        /// <param name="sampleRate">Sample rate supported by audio file</param>
        public void Setup(int channels, int sampleRate)
        {
            _audioDecoder.Setup(channels, sampleRate);
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
        /// <returns>Returns an array of audio data floats that range from 0 to 1</returns>
        public float[] Decode(byte[] chunkData, int chunkStart, int chunkLength)
        {
            // Resultant arrays
            List<WitResponseNode> jsonNodes = new List<WitResponseNode>();
            List<float> audioSamples = new List<float>();

            // Decode into wit chunks
            var chunks = _chunkDecoder.Decode(chunkData, chunkStart, chunkLength);
            foreach (var chunk in chunks)
            {
                if (chunk.jsonData != null)
                {
                    jsonNodes.Add(chunk.jsonData);
                }
                if (chunk.binaryData != null && chunk.binaryData.Length > 0)
                {
                    var samples = _audioDecoder.Decode(chunk.binaryData, 0, chunk.binaryData.Length);
                    audioSamples.AddRange(samples);
                }
            }

            // Return json nodes
            if (jsonNodes.Count > 0)
            {
                _onJsonDecoded?.Invoke(jsonNodes.ToArray());
            }
            // Return audio samples
            return audioSamples.ToArray();
        }
    }
}
