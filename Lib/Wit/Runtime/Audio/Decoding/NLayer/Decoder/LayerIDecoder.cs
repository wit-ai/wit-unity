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

namespace Meta.Audio.NLayer.Decoder
{
    // Layer I is really just a special case of Layer II...  1 granule, 4 allocation bits per subband, 1 scalefactor per active subband, no grouping
    // That (of course) means we literally have no logic here
    class LayerIDecoder : LayerIIDecoderBase
    {
        static internal bool GetCRC(MpegFrame frame, ref uint crc)
        {
            return LayerIIDecoderBase.GetCRC(frame, _rateTable, _allocLookupTable, false, ref crc);
        }

        // this is simple: all 32 subbands have a 4-bit allocations, and positive allocation values are {bits per sample} - 1
        static readonly int[] _rateTable = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        static readonly int[][] _allocLookupTable = { new int[] { 4, 0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 } };

        internal LayerIDecoder() : base(_allocLookupTable, 1) { }

        protected override int[] GetRateTable(IMpegFrame frame)
        {
            return _rateTable;
        }

        protected override void ReadScaleFactorSelection(IMpegFrame frame, int[][] scfsi, int channels)
        {
            // this is a no-op since the base logic uses "2" as the "has energy" marker
        }
    }
}
