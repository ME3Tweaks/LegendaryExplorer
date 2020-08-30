/*	Copyright 2012 Brent Scriver

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

		http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
*/

using System;
using System.IO;
using System.Text;

namespace ME3ExplorerCore.Gammtek.IO
{
    /// <summary>
    ///     A BinaryWriter implementation to write individual bits to a stream
    ///     and support writing data types for platforms with different Endian
    ///     configurations.
    ///     Note: for compatibility with streams written with BinaryWriter,
    ///     character data uses the provided encoding and does not perform any
    ///     Endian swapping with the data.
    /// </summary>
    public class EndianWriter : BinaryWriter
    {
        private readonly BinaryWriter _source;

        public new Stream BaseStream => _source.BaseStream;
        /// <summary>
        ///     Creates an EndianWriter using the given <paramref name="source" /> BinaryWriter.
        /// </summary>
        public EndianWriter(BinaryWriter source)
        {
            _source = source;
            Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianWriter" /> class
        ///     with the underlying <paramref name="stream" /> and <paramref name="encoding" />.
        /// </summary>
        public EndianWriter(Stream stream, Encoding encoding)
            : this(new BinaryWriter(stream, encoding)) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianWriter" /> class
        ///     with the underlying <paramref name="stream" /> and default encoding (UTF8).
        /// </summary>
        public EndianWriter(Stream stream)
            : this(stream, Encoding.UTF8) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianWriter" /> class.
        /// </summary>
        public EndianWriter()
            : this(null, Encoding.UTF8) { }

        /// <summary>
        ///     The Endian setting of the stream.
        /// </summary>
        public Endian Endian { get; set; }

        /// <summary>
        ///     Clears all buffers for the current writer and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
            _source.Flush();
        }

        /// <summary>
        ///     Sets the position within the current stream.
        /// </summary>
        /// <returns>
        ///     The position with the current stream.
        /// </returns>
        public override long Seek(int offset, SeekOrigin origin)
        {
            return _source.Seek(offset, origin);
        }

        /// <summary>
        ///     Writes a one-byte Boolean value to the current stream, with 0 representing false and 1 representing true.
        /// </summary>
        public override void Write(bool value)
        {
            _source.Write(value);
        }

        /// <summary>
        ///     Writes one byte to the current stream.
        /// </summary>
        public override void Write(byte value)
        {
            _source.Write(value);
        }

        /// <summary>
        ///     Writes the bytes in the buffer to the stream.
        /// </summary>
        public override void Write(byte[] buffer)
        {
            _source.Write(buffer);
        }

        /// <summary>
        ///     Writes a region of a byte array to the current stream.
        /// </summary>
        public override void Write(byte[] buffer, int index, int count)
        {
            _source.Write(buffer, index, count);
        }

        /// <summary>
        ///     Writes a character to the stream using the provided encoding.
        /// </summary>
        public override void Write(char ch)
        {
            _source.Write(ch);
        }

        /// <summary>
        ///     Writes an array of characters to the stream using the provided encoding.
        /// </summary>
        public override void Write(char[] chars)
        {
            _source.Write(chars);
        }

        /// <summary>
        ///     Writes a region of a character array to the current stream.
        /// </summary>
        public override void Write(char[] chars, int index, int count)
        {
            _source.Write(chars, index, count);
        }

        /// <summary>
        ///     Writes an eight byte double value to the stream in the target Endian format.
        /// </summary>
        public override void Write(double value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes a four byte float value to the stream in the target Endian format.
        /// </summary>
        public override void Write(float value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes a four byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(int value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes an eight byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(long value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes a one byte signed byte value to the stream.
        /// </summary>
        public override void Write(sbyte value)
        {
            _source.Write((byte)value);
        }

        /// <summary>
        ///     Writes a two byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(short value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes a string to the stream using the provided encoding and a variable integer
        ///     length marker.
        /// </summary>
        public override void Write(string value)
        {
            _source.Write(value);
        }

        /// <summary>
        ///     Writes a four byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(uint value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes an eight byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(ulong value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Writes a two byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(ushort value)
        {
            _source.Write(Endian.Native.To(Endian).Convert(value));
        }

        /// <summary>
        ///     Directly writes an ASCII string to the stream. Must use a multiple of 4
        /// </summary>
        public void WriteStringASCII(string asciistr)
        {
            //this will be important to write magic numbers backwards.
            //if (asciistr.Length % 4 != 0) throw new Exception("Cannot write endian-aware strings that are not multiples of 4");
            //List<char[]> charsets = new List<char[]>(asciistr.Length / 4);
            //for (int i = 0; i < asciistr.Length / 4; i++)
            //{
            //    charsets.Add(asciistr.Substring(i * 4, 4).ToCharArray());
            //    if (Endian == Endian.Big) charsets[i].Reverse();
            //}
            _source.BaseStream.WriteStringASCII(asciistr);
            //foreach (var charset in charsets)
            //{
            //    foreach (var c in charset)
            //    {
            //        _source.BaseStream.WriteByte((byte)c);
            //    }
            //}
        }

        public void WriteFromBuffer(byte[] buffer)
        {
            Write(buffer);
        }

        public void WriteBytes(byte[] bytes) => WriteFromBuffer(bytes);

        public void WriteByte(byte b)
        {
            Write(b);
        }

        public void WriteFloat(float val)
        {
            Write(val);
        }

        public void WriteInt64(long val)
        {
            Write(val);
        }

        public void WriteUInt16(ushort val)
        {
            Write(val);
        }

        public void WriteInt16(short val)
        {
            Write(val);
        }

        public void WriteUInt64(ulong val)
        {
            Write(val);
        }

        public void WriteUInt32(uint val)
        {
            Write(val);
        }

        public void WriteInt32(int val)
        {
            Write(val);
        }

        public void WriteDouble(double val)
        {
            Write(val);
        }

        public void WriteBoolInt(bool val)
        {
            //this needs checked. i think its right
            Write(val ? 1 : 0);
        }

        public void WriteBoolByte(bool val)
        {
            //this needs checked. i think its right
            Write(val);
        }

        public void WriteZeros(int count)
        {
            _source.Write(new byte[count]);
        }
    }
}
