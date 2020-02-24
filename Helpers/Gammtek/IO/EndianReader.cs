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
using System.Runtime.InteropServices;
using System.Text;
using Gammtek.Conduit.Extensions.IO;
using StreamHelpers;

namespace Gammtek.Conduit.IO
{
    /// <summary>
    ///     BinaryReader that supports reading and writing individual bits from
    ///     the stream and reordering values based on Endian settings between
    ///     the system and the stream.
    /// You can write data through the Writer member.
    /// </summary>
    public class EndianReader : BinaryReader
    {
        private readonly BinaryReader _source;
        public readonly EndianWriter Writer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianReader" /> class
        ///     using the <paramref name="source" /> BinaryReader.
        /// </summary>
        public EndianReader(BinaryReader source)
            : base(source.BaseStream, Encoding.UTF8)
        {
            _source = source;
            if (source.BaseStream.CanWrite)
            {
                Writer = new EndianWriter(source.BaseStream);
            }
            Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;

        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianReader" /> class.
        /// </summary>
        public EndianReader(Stream input, Encoding encoding)
            : base(input, encoding)
        {
            _source = new BinaryReader(input, encoding);
            if (input.CanWrite)
            {
                Writer = new EndianWriter(input);
            }
            Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianReader" /> class
        ///     using stream <paramref name="input" />.
        /// </summary>
        public EndianReader(Stream input)
            : this(input, Encoding.UTF8)
        {
        }

        private Endian _endian;
        /// <summary>
        ///     The Endian setting of the stream.
        /// </summary>
        public Endian Endian
        {
            get => _endian;
            set
            {
                _endian = value;
                if (Writer != null)
                    Writer.Endian = Endian;
            }

        }

        /// <summary>
        ///     Returns the next available character and does not advance the byte or character position.
        /// </summary>
        /// <returns>
        ///     The next available character, or -1 if no more characters are available or the stream does not support seeking.
        /// </returns>
        public override int PeekChar()
        {
            return _source.PeekChar();
        }

        /// <summary>
        ///     Reads characters from the underlying stream and advances the current position of the stream in accordance with the
        ///     Encoding used and the specific character being read from the stream.
        /// </summary>
        /// <returns>
        ///     The next character from the input stream, or -1 if no characters are currently available.
        /// </returns>
        public override int Read()
        {
            return _source.Read();
        }

        /// <summary>
        ///     Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        public override int Read(byte[] buffer, int index, int count)
        {
            return _source.Read(buffer, index, count);
        }

        /// <summary>
        ///     Reads the specified number of characters from the stream, starting from a specified point in the byte array.
        /// </summary>
        public override int Read(char[] buffer, int index, int count)
        {
            return _source.Read(buffer, index, count);
        }

        /// <summary>
        ///     Reads the specified number of ASCII characters from the stream.
        /// </summary>
        public string ReadEndianASCIIString(int count)
        {
            char[] characters = new char[count];
            char[] buffer = ReadChars(count);
            if (Endian == Endian.Big)
            {
                for (int i = count; i > 0; i--)
                {
                    characters[i - 1] = buffer[count - i];
                }
                return new string(characters);
            }
            else
            {
                return new string(buffer);
            }
        }

        /// <summary>
        /// Reads an unreal-style prefixed string from the underlying stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public string ReadUnrealString()
        {
            int length = ReadInt32();
            if (length == 0)
            {
                return "";
            }
            return length < 0 ? _source.BaseStream.ReadStringUnicodeNull(length * -2) : _source.BaseStream.ReadStringASCIINull(length);
        }

        /// <summary>
        ///     Reads a Boolean value from the current stream and advances the current position of the stream by one bit.
        /// </summary>
        /// <returns>
        ///     true if the bit is nonzero; otherwise, false.
        /// </returns>
        public override bool ReadBoolean()
        {
            return ReadBoolByte();
        }

        /// <summary>
        ///     Reads the next byte from the current stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>
        ///     The next byte read from the current stream.
        /// </returns>
        public override byte ReadByte()
        {
            var b = _source.ReadByte();
            LittleEndianStream?.WriteByte(b);
            return b;
        }

        /// <summary>
        ///     Reads the specified number of bytes from the current stream into a byte array and advances the current position by
        ///     that number of bytes.
        /// </summary>
        /// <returns>
        ///     A byte array containing data read from the underlying stream. This might be less than the number of bytes
        ///     requested if the end of the stream is reached.
        /// </returns>
        /// <param name='count'>
        ///     The number of bytes to read.
        /// </param>
        public override byte[] ReadBytes(int count)
        {
            var bytes = _source.ReadBytes(count);
            LittleEndianStream?.WriteFromBuffer(bytes);
            return bytes;
        }

        /// <summary>
        ///     Reads the next character from the current stream and advances the current position of the stream in accordance
        ///     with the Encoding used and the specific character being read from the stream.
        /// </summary>
        /// <returns>
        ///     A character read from the current stream.
        /// </returns>
        public override char ReadChar()
        {
            return _source.ReadChar();
        }

        /// <summary>
        ///     Reads the specified number of characters from the current stream, returns the data in a character array, and
        ///     advances the current position in accordance with the Encoding used and the specific character being read from the stream.
        /// </summary>
        /// <returns>
        ///     A character array containing data read from the underlying stream. This might be less than the number of
        ///     characters requested if the end of the stream is reached.
        /// </returns>
        /// <param name='count'>
        ///     The number of characters to read.
        /// </param>
        public override char[] ReadChars(int count)
        {
            return _source.ReadChars(count);
        }

        /// <summary>
        ///     Reads an 8-byte floating point value from the current stream and advances the current position of the stream by
        ///     eight bytes.
        /// </summary>
        /// <returns>
        ///     An 8-byte floating point value read from the current stream.
        /// </returns>
        public override double ReadDouble()
        {
            var val = Endian.To(Endian.Native).Convert(_source.ReadDouble());
            LittleEndianStream?.WriteDouble(val);
            return val;
        }

        /// <summary>
        ///     Reads a 2-byte signed integer from the current stream and advances the current position of the stream by two bytes.
        /// </summary>
        /// <returns>
        ///     A 2-byte signed integer read from the current stream.
        /// </returns>
        public override short ReadInt16()
        {
            var val = Endian.To(Endian.Native).Convert(_source.ReadInt16());
            LittleEndianStream?.WriteInt16(val);
            return val;
        }

        /// <summary>
        ///     Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>
        ///     A 4-byte signed integer read from the current stream.
        /// </returns>
        public override int ReadInt32()
        {
            var val = Endian.To(Endian.Native).Convert(_source.ReadInt32());
            LittleEndianStream?.WriteInt32(val);
            return val;
        }

        /// <summary>
        ///     Reads an 8-byte signed integer from the current stream and advances the current position of the stream by eight bytes.
        /// </summary>
        /// <returns>
        ///     An 8-byte signed integer read from the current stream.
        /// </returns>
        public override long ReadInt64()
        {
            var val = Endian.To(Endian.Native).Convert(_source.ReadInt64());
            LittleEndianStream?.WriteInt64(val);
            return val;
        }

        /// <summary>
        ///     Reads a signed byte from this stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>
        ///     A signed byte read from the current stream.
        /// </returns>
        public override sbyte ReadSByte()
        {
            var val = (sbyte)ReadByte();
            LittleEndianStream?.WriteByte((byte)val);
            return val;
        }

        /// <summary>
        ///     Reads a 4-byte floating point value from the current stream and advances the current position of the stream by
        ///     four bytes.
        /// </summary>
        /// <returns>
        ///     A 4-byte floating point value read from the current stream.
        /// </returns>
        public override float ReadSingle()
        {
            var readvalue = Endian.To(Endian.Native).Convert(_source.ReadSingle());
            LittleEndianStream?.WriteFloat(readvalue);
            return readvalue;
        }

        public float ReadFloat() => ReadSingle();

        /// <summary>
        ///     Reads a string from the current stream. The string is prefixed with the length, encoded as an integer seven bits
        ///     at a time.
        /// </summary>
        /// <returns>
        ///     The string being read.
        /// </returns>
        public override string ReadString()
        {
            return _source.ReadString();
        }

        /// <summary>
        ///     Reads a 2-byte unsigned integer from the current stream using little-endian encoding and advances the position of
        ///     the stream by two bytes.
        /// </summary>
        /// <returns>
        ///     A 2-byte unsigned integer read from this stream.
        /// </returns>
        public override ushort ReadUInt16()
        {
            var readvalue = Endian.To(Endian.Native).Convert(_source.ReadUInt16());
            LittleEndianStream?.WriteUInt16(readvalue);
            return readvalue;
        }

        /// <summary>
        ///     Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
        /// </summary>
        /// <returns>
        ///     A 4-byte unsigned integer read from this stream.
        /// </returns>
        public override uint ReadUInt32()
        {
            var readvalue = Endian.To(Endian.Native).Convert(_source.ReadUInt32());
            LittleEndianStream?.WriteUInt32(readvalue);
            return readvalue;
        }

        /// <summary>
        ///     Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
        /// </summary>
        /// <returns>
        ///     An 8-byte unsigned integer read from this stream.
        /// </returns>
        public override ulong ReadUInt64()
        {
            var val = Endian.To(Endian.Native).Convert(_source.ReadUInt64());
            LittleEndianStream?.WriteUInt64(val);
            return val;
        }

        /// <summary>
        ///     Skips 4 bytes in the stream.
        /// </summary>
        public void SkipInt32()
        {
            ReadInt32();
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            _source.BaseStream.Seek(offset, origin);
        }

        public long Position
        {
            get => _source.BaseStream.Position;
            set => _source.BaseStream.Position = value;
        }

        public long Length => BaseStream.Length;

        /// <summary>
        /// Generates an EndianReader object that takes a stream that uses the specified magic number to set the endianness. The stream is advanced by 4 and the out variable is set to what the value would be if itw as read in the correct endianness.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static EndianReader SetupForReading(Stream input, int magic, out int readvalue)
        {
            EndianReader er = new EndianReader(input);
            var readMagic = er.ReadUInt32();
            if (readMagic == magic)
            {
                er.Endian = Endian.Native;
                readvalue = magic;
            }
            else
            {
                //cast to int to ensure we have some comparisons.
                var reversed = (int)IO.Endian.Native.To(Endian.NonNative).Convert(readMagic);
                if (reversed != magic)
                {
                    throw new Exception($"Magic number {readMagic:X8} does not match either big or little endianness for expected value 0x{magic:X8}");
                }
                else
                {
                    er.Endian = Endian.NonNative;
                    readvalue = unchecked((int)reversed);
                }
            }

            return er;
        }

        /// <summary>
        /// Generates an EndianReader object that takes a stream that is the start of a package file. It automatically will set the endianness and reset the position to 0.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static EndianReader SetupForPackageReading(Stream input)
        {
            EndianReader er = new EndianReader(input);
            var packageTag = er.ReadUInt32();
            if (packageTag != packageTagBigEndian && packageTag != packageTagLittleEndian) throw new Exception("Magic number for this file doesn't match known value, this is not a UE3 file");
            if (packageTag == packageTagBigEndian) er.Endian = Endian.Big;
            input.Position -= 4;
            return er;
        }
        public const uint packageTagLittleEndian = 0x9E2A83C1; //Default, PC
        public const uint packageTagBigEndian = 0xC1832A9E;


        /// <summary>
        /// Reads boolean integer from the stream. 0 is false, > 0 is true
        /// </summary>
        /// <returns></returns>
        public bool ReadBoolInt()
        {
            return ReadUInt32() > 0;
        }

        public float ReadFloat16()
        {
            var buffer = ReadBytes(sizeof(ushort));
            if (buffer.Length != sizeof(ushort))
                throw new Exception("Could not read float16");
            ushort u = ToUInt16(buffer, 0, Endian);

            //This definitely needs checked
            if (LittleEndianStream != null)
            {
                if (Endian == Endian.Big)
                {
                    LittleEndianStream.WriteByte(buffer[1]);
                    LittleEndianStream.WriteByte(buffer[0]);
                }
                else
                {
                    LittleEndianStream.WriteByte(buffer[0]);
                    LittleEndianStream.WriteByte(buffer[1]);
                }
            }

            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            switch (exp)
            {
                case 0:
                    return 0f;
                case 31:
                    return 65504f;
            }
            exp += (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        /// <summary>
        /// Reads boolean byte from the stream. 0 is false, > 0 is true
        /// </summary>
        /// <returns></returns>
        public bool ReadBoolByte()
        {
            return ReadByte() > 0;
        }

        public void JumpTo(int position)
        {
            Position = position;
        }

        public void JumpTo(long position)
        {
            Position = position;
        }

        public EndianReader Skip(int count)
        {
            Position += count;
            return this;
        }
        public EndianReader Skip(long count)
        {
            Position += count;
            return this;
        }

        public byte[] ReadToBuffer(int size)
        {
            return ReadBytes(size);
        }

        public byte[] ToArray()
        {
            var pos = Position;
            Position = 0;
            var data = _source.ReadBytes((int)Length);
            Position = pos;
            return data;
        }

        #region BITCONVERTER STATIC METHODS

        public static float ToSingle(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToSingle(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        public static ushort ToUInt16(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToUInt16(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an int32 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static int ToInt32(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToInt32(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an int16 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static int ToInt16(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToInt16(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an uint32 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static uint ToUInt32(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToUInt32(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an uint32 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static ulong ToUInt64(byte[] buffer, int offset, Endian endianness)
        {
            var readMagic = BitConverter.ToUInt64(buffer, offset);
            if (IO.Endian.Native != endianness)
            {
                //swap
                return Endian.Native.To(Endian.NonNative).Convert(readMagic);
            }
            return readMagic;
        }

        #endregion

        /// <summary>
        /// Initializes the LittleEndianStream memorystream. All reads will write the little endian version to this stream. Used to reverse endian of files read by this reader
        /// </summary>
        public void SetupEndianReverser()
        {
            LittleEndianStream = new MemoryStream();
        }

        public MemoryStream LittleEndianStream { get; private set; }
    }
}
