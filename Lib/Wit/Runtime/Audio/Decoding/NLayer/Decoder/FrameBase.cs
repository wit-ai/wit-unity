/*
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

using System;

namespace Meta.Audio.NLayer.Decoder
{
    abstract class FrameBase
    {
        static int _totalAllocation = 0;
        static internal int TotalAllocation
        {
            get { return System.Threading.Interlocked.CompareExchange(ref _totalAllocation, 0, 0); }
        }

        internal long Offset { get; private set; }
        internal int Length { get; set; }

        MpegStreamReader _reader;

        byte[] _savedBuffer;

        protected FrameBase() { }

        internal bool Validate(long offset, MpegStreamReader reader)
        {
            Offset = offset;
            _reader = reader;

            var len = Validate();

            if (len > 0)
            {
                Length = len;
                return true;
            }
            return false;
        }

        protected int Read(int offset, byte[] buffer)
        {
            return Read(offset, buffer, 0, buffer.Length);
        }

        protected int Read(int offset, byte[] buffer, int index, int count)
        {
            if (_savedBuffer != null)
            {
                if (index < 0 || index + count > buffer.Length) return 0;   // check against caller's buffer
                if (offset < 0 || offset >= _savedBuffer.Length) return 0;  // check against saved buffer
                if (offset + count > _savedBuffer.Length) count = _savedBuffer.Length - index;  // twiddle the size as needed

                Array.Copy(_savedBuffer, offset, buffer, index, count);
                return count;
            }
            else
            {
                return _reader.Read(Offset + offset, buffer, index, count);
            }
        }

        protected int ReadByte(int offset)
        {
            if (_savedBuffer != null)
            {
                if (offset < 0) throw new ArgumentOutOfRangeException();
                if (offset >= _savedBuffer.Length) return -1;

                return (int)_savedBuffer[offset];
            }
            else
            {
                return _reader.ReadByte(Offset + offset);
            }
        }

        /// <summary>
        /// Called to validate the frame header
        /// </summary>
        /// <returns>The length of the frame, or -1 if frame is invalid</returns>
        abstract protected int Validate();

        internal void SaveBuffer()
        {
            _savedBuffer = new byte[Length];
            _reader.Read(Offset, _savedBuffer, 0, Length);
            System.Threading.Interlocked.Add(ref _totalAllocation, Length);
        }

        internal void ClearBuffer()
        {
            System.Threading.Interlocked.Add(ref _totalAllocation, -Length);
            _savedBuffer = null;
        }

        /// <summary>
        /// Called when the stream is not "seek-able"
        /// </summary>
        virtual internal void Parse() { }
    }
}
