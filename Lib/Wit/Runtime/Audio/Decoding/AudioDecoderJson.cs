/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using UnityEngine.Scripting;
using Meta.WitAi.Json;
using Meta.Voice.Net.Encoding.Wit;

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
        // Temp list for wit chunk info and the decoder itself
        private readonly List<WitChunk> _decodedChunks = new List<WitChunk>();
        private readonly WitChunkConverter _chunkDecoder = new WitChunkConverter();

        // Temp list for holding decoded json nodes and callback following json decode
        private readonly List<WitResponseNode> _decodedJson = new List<WitResponseNode>();
        private readonly AudioJsonDecodeDelegate _onJsonDecoded;

        // Handles audio decoding
        private readonly IAudioDecoder _audioDecoder;
        // Used for DecodeAudio method
        private AudioSampleDecodeDelegate _onSamplesDecoded;

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
        /// Decode json on background thread due to decode speed
        /// </summary>
        public bool DecodeInBackground => true;

        /// <summary>
        /// A method for decoded bytes and calling an AddSample delegate for each sample
        /// </summary>
        /// <param name="buffer">A buffer of bytes to be decoded into audio sample data</param>
        /// <param name="bufferOffset">The buffer start offset used for decoding a reused buffer</param>
        /// <param name="bufferLength">The total number of bytes to be used from the buffer</param>
        /// <param name="onSamplesDecoded">Callback following a sample decode</param>
        public void Decode(byte[] buffer, int bufferOffset, int bufferLength, AudioSampleDecodeDelegate onSamplesDecoded)
        {
            // Decode audio and json
            _onSamplesDecoded = onSamplesDecoded;
            _chunkDecoder.Decode(buffer, bufferOffset, bufferLength, _decodedChunks, DecodeAudio);
            _onSamplesDecoded = null;

            // If chunks exist, iterate
            if (_decodedChunks.Count == 0)
            {
                return;
            }
            for (int i = 0; i < _decodedChunks.Count; i++)
            {
                var jsonNode = _decodedChunks[i].jsonData;
                if (jsonNode is WitResponseArray jsonArray)
                {
                    _decodedJson.AddRange(jsonArray.Childs);
                }
                else if (jsonNode != null)
                {
                    _decodedJson.Add(jsonNode);
                }
            }
            _decodedChunks.Clear();
            if (_decodedJson.Count == 0)
            {
                return;
            }

            // Return json
            _onJsonDecoded?.Invoke(_decodedJson);
            _decodedJson.Clear();
        }

        // Performs the audio decode using the provided buffer offset and length
        private void DecodeAudio(byte[] buffer, int bufferOffset, int bufferLength)
            => _audioDecoder.Decode(buffer, bufferOffset, bufferLength, _onSamplesDecoded);
    }
}
