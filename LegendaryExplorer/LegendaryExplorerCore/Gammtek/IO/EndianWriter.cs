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
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO.Converters;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;

namespace LegendaryExplorerCore.Gammtek.IO
{
    /// <summary>
    ///     A BinaryWriter implementation to write individual bits to a stream
    ///     and support writing data types for platforms with different Endian
    ///     configurations.
    ///     Note: for compatibility with streams written with BinaryWriter,
    ///     character data uses the provided encoding and does not perform any
    ///     Endian swapping with the data.
    /// </summary>
    public sealed class EndianWriter : BinaryWriter
    {
        private bool NoConvert;
        private Endian _endian;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianWriter" /> class
        ///     with the underlying <paramref name="stream" /> and <paramref name="encoding" />.
        /// </summary>
        public EndianWriter(Stream stream, Encoding encoding) : base(stream, encoding)
        {
            Endian = Endian.Native;
            NoConvert = Endian.IsNative;
        }

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
            : this(MemoryManager.GetMemoryStream(), Encoding.UTF8) { }

        /// <summary>
        ///     The Endian setting of the stream.
        /// </summary>
        public Endian Endian
        {
            get => _endian;
            set { 
                _endian = value;
                NoConvert = _endian.IsNative;
            }
        }

        /// <summary>
        ///     Writes an eight byte double value to the stream in the target Endian format.
        /// </summary>
        public override void Write(double value)
        {
            base.Write(NoConvert ? value : value.Swap());
        }

        /// <summary>
        ///     Writes a four byte float value to the stream in the target Endian format.
        /// </summary>
        public override void Write(float value)
        {
            base.Write(NoConvert ? value : value.Swap());
        }

        /// <summary>
        ///     Writes a four byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(int value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Writes an eight byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(long value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Writes a one byte signed byte value to the stream.
        /// </summary>
        public override void Write(sbyte value)
        {
            base.Write((byte)value);
        }

        /// <summary>
        ///     Writes a two byte signed integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(short value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Writes a four byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(uint value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Writes an eight byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(ulong value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Writes a two byte unsigned integer value to the stream in the target Endian format.
        /// </summary>
        public override void Write(ushort value)
        {
            base.Write(NoConvert ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        /// <summary>
        ///     Directly writes an ASCII string to the stream. Must use a multiple of 4
        /// </summary>
        public void WriteStringLatin1(string asciistr)
        {
            //this will be important to write magic numbers backwards.
            //if (asciistr.Length % 4 != 0) throw new Exception("Cannot write endian-aware strings that are not multiples of 4");
            //List<char[]> charsets = new List<char[]>(asciistr.Length / 4);
            //for (int i = 0; i < asciistr.Length / 4; i++)
            //{
            //    charsets.Add(asciistr.Substring(i * 4, 4).ToCharArray());
            //    if (Endian == Endian.Big) charsets[i].Reverse();
            //}
            OutStream.WriteStringLatin1(asciistr);
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

        public void WriteFromBuffer(ReadOnlySpan<byte> buffer)
        {
            Write(buffer);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            OutStream.Write(buffer);
        }

        public void WriteBytes(byte[] bytes) => Write(bytes);

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
            Write(val ? 1 : 0);
        }

        public void WriteBoolByte(bool val)
        {
            Write(val);
        }

        public void WriteZeros(int count)
        {
            Write(new byte[count]);
        }

        public void WriteGuid(Guid value)
        {
            Span<byte> data = stackalloc byte[16];
            MemoryMarshal.Write(data, in value);

            if (NoConvert)
            {
                Write(data);
                return;
            }

            WriteInt32(BitConverter.ToInt32(data));
            WriteInt16(BitConverter.ToInt16(data.Slice(4)));
            WriteInt16(BitConverter.ToInt16(data.Slice(6)));
            Write(data.Slice(8));
        }

        /// <summary>
        /// Copies stream to a new array. Consider using a more performant method if at all possible.
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            Stream baseStream = BaseStream;
            var pos = baseStream.Position;
            baseStream.Position = 0;
            var data = baseStream.ReadToBuffer(pos);
            baseStream.Position = pos;
            return data;
        }
    }
}
