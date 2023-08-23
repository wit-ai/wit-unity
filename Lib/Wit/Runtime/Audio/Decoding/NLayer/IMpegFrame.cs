/*
 * MIT License
 *
 * Copyright (c) 2018 Mark Heath, Andrew Ward & Contributors
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 */

namespace Meta.Audio.NLayer
{
    /// <summary>
    /// Defines a standard way of representing a MPEG frame to the decoder
    /// </summary>
    public interface IMpegFrame
    {
        /// <summary>
        /// Sample rate of this frame
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// The samplerate index (directly from the header)
        /// </summary>
        int SampleRateIndex { get; }

        /// <summary>
        /// Frame length in bytes
        /// </summary>
        int FrameLength { get; }

        /// <summary>
        /// Bit Rate
        /// </summary>
        int BitRate { get; }

        /// <summary>
        /// MPEG Version
        /// </summary>
        MpegVersion Version { get; }

        /// <summary>
        /// MPEG Layer
        /// </summary>
        MpegLayer Layer { get; }

        /// <summary>
        /// Channel Mode
        /// </summary>
        MpegChannelMode ChannelMode { get; }

        /// <summary>
        /// The number of samples in this frame
        /// </summary>
        int ChannelModeExtension { get; }

        /// <summary>
        /// The channel extension bits
        /// </summary>
        int SampleCount { get; }

        /// <summary>
        /// The bitrate index (directly from the header)
        /// </summary>
        int BitRateIndex { get; }

        /// <summary>
        /// Whether the Copyright bit is set
        /// </summary>
        bool IsCopyrighted { get; }

        /// <summary>
        /// Whether a CRC is present
        /// </summary>
        bool HasCrc { get; }

        /// <summary>
        /// Whether the CRC check failed (use error concealment strategy)
        /// </summary>
        bool IsCorrupted { get; }

        /// <summary>
        /// Resets the bit reader so frames can be reused
        /// </summary>
        void Reset();

        /// <summary>
        /// Provides sequential access to the bitstream in the frame (after the header and optional CRC)
        /// </summary>
        /// <param name="bitCount">The number of bits to read</param>
        /// <returns>-1 if the end of the frame has been encountered, otherwise the bits requested</returns>
        int ReadBits(int bitCount);
    }
}
