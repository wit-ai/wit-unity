﻿/*
 * MIT License
 *
 * Copyright (c) 2018 Mark Heath, Andrew Ward and Contributors
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

namespace Meta.Voice.NLayer.Decoder
{
    class VBRInfo
    {
        internal VBRInfo() { }

        internal int SampleCount { get; set; }
        internal int SampleRate { get; set; }
        internal int Channels { get; set; }
        internal int VBRFrames { get; set; }
        internal int VBRBytes { get; set; }
        internal int VBRQuality { get; set; }
        internal int VBRDelay { get; set; }

        internal long VBRStreamSampleCount
        {
            get
            {
                // we assume the entire stream is consistent wrt samples per frame
                return VBRFrames * SampleCount;
            }
        }

        internal int VBRAverageBitrate
        {
            get
            {
                return (int)((VBRBytes / (VBRStreamSampleCount / (double)SampleRate)) * 8);
            }
        }
    }
}
