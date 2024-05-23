/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.Voice.Net.Encoding.Wit;
using Meta.WitAi.Json;
using UnityEngine.Scripting;

namespace Meta.Voice.Audio.Decoding
{
    /// <summary>
    /// A delegate to be called when text data is decoded from an audio stream
    /// </summary>
    public delegate void AudioJsonDecodeDelegate(List<WitResponseNode> jsonNode);

    /// <summary>
    /// A decoder for json data and audio within a single data stream.
    /// Decodes the data into split audio and text streams
    /// </summary>
    [Preserve]
    public class AudioDecoderJson : IAudioDecoder
    {
        // Used for mixed json/binary data decoding
        private readonly WitChunkConverter _chunkDecoder = new WitChunkConverter();
        // Used for audio decoding
        private readonly IAudioDecoder _audioDecoder;

        // Decoded json
        private readonly List<WitResponseNode> _decodedJson = new List<WitResponseNode>();
        // The decode callback that is called for every json decode
        private readonly AudioJsonDecodeDelegate _onJsonDecoded;

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
        /// A method for decoded bytes and calling an AddSample delegate for each sample
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="decodedSamples">List to add all decoded samples to</param>
        public void Decode(byte[] buffer, int bufferOffset, int bufferLength, List<float> decodedSamples)
        {
            // Decode into wit chunks
            var chunks = _chunkDecoder.Decode(buffer, bufferOffset, bufferLength);
            foreach (var chunk in chunks)
            {
                // Decode audio data
                if (chunk.binaryData != null && chunk.binaryData.Length > 0)
                {
                    _audioDecoder.Decode(chunk.binaryData, 0, chunk.binaryData.Length, decodedSamples);
                }
                // Append json chunks
                if (chunk.jsonData != null)
                {
                    if (chunk.jsonData is WitResponseArray array)
                    {
                        _decodedJson.AddRange(array.Childs);
                    }
                    else
                    {
                        _decodedJson.Add(chunk.jsonData);
                    }
                }
            }

            // Json decoded callback
            if (_decodedJson.Count > 0)
            {
                _onJsonDecoded?.Invoke(_decodedJson);
                _decodedJson.Clear();
            }
        }
    }
}
