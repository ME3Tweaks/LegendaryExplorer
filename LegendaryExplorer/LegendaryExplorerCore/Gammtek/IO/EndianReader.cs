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

//#define LITTLEENDIANSTREAM

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Gammtek.IO
{
    [DebuggerDisplay("EndianReader @ {Position.ToString(\"X8\")}, endian is native to platform: {Endian.IsNative}")]

    /// <summary>
    ///     BinaryReader that supports reading and writing individual bits from
    ///     the stream and reordering values based on Endian settings between
    ///     the system and the stream.
    /// You can write data through the Writer member.
    /// </summary>
    public sealed class EndianReader : BinaryReader
    {
        public readonly EndianWriter Writer;
        private bool NoConvert;
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
                NoConvert = _endian.IsNative;
                if (Writer != null)
                {
                    Writer.Endian = Endian;
                }
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianReader" /> class.
        /// </summary>
        public EndianReader(Stream input, Encoding encoding)
            : base(input, encoding)
        {
            if (input.CanWrite)
            {
                Writer = new EndianWriter(input, encoding);
            }
            Endian = Endian.Native;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EndianReader" /> class
        ///     using stream <paramref name="input" />.
        /// </summary>
        public EndianReader(Stream input)
            : this(input, Encoding.UTF8)
        {
        }/// <summary>
         ///     Initializes a new instance of the <see cref="EndianReader" /> class
         ///     using a MemoryStream.
         /// </summary>
        public EndianReader() : this(MemoryManager.GetMemoryStream(), Encoding.UTF8)
        {
        }

        /// <summary>
        ///     Initializes a new read-only instance of the <see cref="EndianReader" /> class
        ///     using byte array <paramref name="input" />.
        /// </summary>
        public EndianReader(byte[] input)
            : this(new MemoryStream(input, false), Encoding.UTF8)
        {
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

        public string ReadStringASCII(int count)
        {
            return new string(ReadChars(count));
        }

        public string ReadStringASCIINull()
        {
            string str = "";
            for (; ; )
            {
                char c = (char)ReadByte();
                if (c == 0)
                    break;
                str += c;
            }
            return str;
        }

        /// <summary>
        /// Reads an unreal-style prefixed string from the underlying stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string ReadUnrealString(ReadOnlySpan<byte> data, int position, Endian endian)
        {
            int length = ToInt32(data, position, endian);
            if (length == 0)
            {
                return "";
            }

            if (length > 0)
            {
                return Encoding.Latin1.GetString(data.Slice(position + 4, length));
            }
            else
            {
                return Encoding.Unicode.GetString(data.Slice(position + 4, length * -2));
            }
        }

        /// <summary>
        /// Reads an unreal-style prefixed string from the underlying stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string ReadUnrealString(byte[] data, int position, Endian endian)
        {
            int length = ToInt32(data, position, endian);
            if (length == 0)
            {
                return "";
            }

            if (length > 0)
            {
                return Encoding.Latin1.GetString(data, position + 4, length);
            }
            else
            {
                return Encoding.Unicode.GetString(data, position + 4, length * -2);
            }
        }

        /// <summary>
        /// Reads an unreal-style prefixed string from the underlying stream.
        /// </summary>
        /// <returns></returns>
        public string ReadUnrealString()
        {
            int length = ReadInt32();
            if (length == 0)
            {
                return "";
            }
            return length < 0 ? BaseStream.ReadStringUnicodeNull(length * -2) : BaseStream.ReadStringLatin1Null(length);
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

#if LITTLEENDIANSTREAM
        /// <summary>
        ///     Reads the next byte from the current stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>
        ///     The next byte read from the current stream.
        /// </returns>
        public override byte ReadByte()
        {
            var b = base.ReadByte();
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
            var bytes = base.ReadBytes(count);
            LittleEndianStream?.WriteFromBuffer(bytes);
            return bytes;
        }
#endif

        /// <summary>
        ///     Reads an 8-byte floating point value from the current stream and advances the current position of the stream by
        ///     eight bytes.
        /// </summary>
        /// <returns>
        ///     An 8-byte floating point value read from the current stream.
        /// </returns>
        public override double ReadDouble()
        {
            var val = base.ReadDouble();
            if (!NoConvert)
            {
                val = val.Swap();
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteDouble(val);
#endif
                return val;
            }

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
            var val = base.ReadInt16();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteInt16(val);
#endif
                return val;
            }

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
            var val = base.ReadInt32();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
                LittleEndianStream?.WriteInt32(val);
#endif
                return val;
            }

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
            var val = base.ReadInt64();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteInt64(val);
#endif
                return val;
            }

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
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteByte((byte)val);
#endif
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
            var val = base.ReadSingle();
            if (!NoConvert)
            {
                val = val.Swap();
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteFloat(val);
#endif
                return val;
            }

            return val;
        }

        public float ReadFloat() => ReadSingle();

        /// <summary>
        ///     Reads a 2-byte unsigned integer from the current stream using little-endian encoding and advances the position of
        ///     the stream by two bytes.
        /// </summary>
        /// <returns>
        ///     A 2-byte unsigned integer read from this stream.
        /// </returns>
        public override ushort ReadUInt16()
        {
            var val = base.ReadUInt16();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteUInt16(val);
#endif
                return val;
            }

            return val;
        }

        /// <summary>
        ///     Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
        /// </summary>
        /// <returns>
        ///     A 4-byte unsigned integer read from this stream.
        /// </returns>
        public override uint ReadUInt32()
        {
            var val = base.ReadUInt32();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteUInt32(val);
#endif
                return val;
            }

            return val;
        }

        /// <summary>
        ///     Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
        /// </summary>
        /// <returns>
        ///     An 8-byte unsigned integer read from this stream.
        /// </returns>
        public override ulong ReadUInt64()
        {
            var val = base.ReadUInt64();
            if (!NoConvert)
            {
                val = BinaryPrimitives.ReverseEndianness(val);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.WriteUInt64(val);
#endif
                return val;
            }

            return val;
        }
        public Guid ReadGuid()
        {
            Span<byte> span = stackalloc byte[16];
            Read(span);
            if (!NoConvert)
            {
                return new Guid(
                    BinaryPrimitives.ReadInt32BigEndian(span),
                    BinaryPrimitives.ReadInt16BigEndian(span[4..]),
                    BinaryPrimitives.ReadInt16BigEndian(span[6..]),
                    span[8],
                    span[9],
                    span[10],
                    span[11],
                    span[12],
                    span[13],
                    span[14],
                    span[15]);
            }
            return MemoryMarshal.Read<Guid>(span);
        }

        /// <summary>
        ///     Skips 4 bytes in the stream.
        /// </summary>
        public void SkipInt32()
        {
            Seek(4, SeekOrigin.Current);
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            BaseStream.Seek(offset, origin);
#if LITTLEENDIANSTREAM
            LittleEndianStream?.Seek(offset, origin);
#endif
        }

        public long Position
        {
            get => BaseStream.Position;
            set
            {
                BaseStream.Position = value;
#if LITTLEENDIANSTREAM
                if (LittleEndianStream != null)
                    LittleEndianStream.Position = value;
#endif
            }
        }

        public long Length => BaseStream.Length;

        /// <summary>
        /// Generates an EndianReader object that takes a stream that uses the specified LITTLE ENDIAN (PC) magic number to set the endianness. The stream is advanced by 4 and the out variable is set to what the value would be if it was read in the correct endianness.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static EndianReader SetupForReading(Stream input, int magic, out int readvalue)
        {
            var er = new EndianReader(input);
            var readMagic = er.ReadUInt32();
            if (readMagic == magic)
            {
                er.Endian = Endian.Native;
                readvalue = magic;
            }
            else
            {
                //cast to int to ensure we have some comparisons.
                var reversed = (int)BinaryPrimitives.ReverseEndianness(readMagic);
                if (reversed != magic)
                {
                    throw new Exception($"Magic number {readMagic:X8} does not match either big or little endianness for expected value 0x{magic:X8}");
                }
                else
                {
                    er.Endian = Endian.NonNative;
                    readvalue = reversed;
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
            var er = new EndianReader(input);
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

        /// <summary>
        /// Reads a FaceFX string
        /// </summary>
        /// <param name="er"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public string ReadFaceFXString(MEGame game, bool extended = false)
        {
            if (game == MEGame.ME2)
            {
                // ME2 strings appear to have a Int16 before the actual string.
                // This appears to be the 'version' of the object.
                if (extended) ReadInt16(); //It's 4 bytes
                var objectVersion = ReadInt16(); // This seems like it's always 1. Older versions set it to zero. Looks like some sort of parser flag but it's on every string
                if (objectVersion != 1)
                {
                    Debug.WriteLine($@"Expected pre-string value was not 1! Value was: {objectVersion}, position 0x{(Position - 2):X8}");
                }
            }

            return ReadUnrealString();
        }

        public float ReadFloat16()
        {
            var buffer = ReadBytes(sizeof(ushort));
            if (buffer.Length != sizeof(ushort))
                throw new Exception("Could not read float16");
            ushort u = ToUInt16(buffer, 0, Endian);

#if LITTLEENDIANSTREAM
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
#endif
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
            return BitConverter.Int32BitsToSingle(i);
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
            Seek(count, SeekOrigin.Current);
            return this;
        }
        public EndianReader Skip(long count)
        {
            //Must use seek or we will desync the little endian stream
            //Seek will update the LE stream
            Seek(count, SeekOrigin.Current);
            return this;
        }

        public byte[] ReadToBuffer(int size)
        {
            return ReadBytes(size);
        }

        /// <summary>
        /// Copies stream to a new array. Consider using a more performant method if at all possible.
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            var pos = Position;
            Position = 0;
            var data = ReadBytes((int)Length);
            Position = pos;
            return data;
        }

        #region BITCONVERTER STATIC METHODS

        public static float ToSingle(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            if (!endianness.IsNative)
            {
                return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(MemoryMarshal.Read<int>(buffer.Slice(offset))));
            }
            return MemoryMarshal.Read<float>(buffer.Slice(offset));
        }

        public static ushort ToUInt16(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<ushort>(buffer.Slice(offset));
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        public static ushort ToUInt16(ReadOnlySpan<byte> buffer, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<ushort>(buffer);
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an int32 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static int ToInt32(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<int>(buffer.Slice(offset));
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an int32 from the span with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static int ToInt32(ReadOnlySpan<byte> buffer, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<int>(buffer);
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an int16 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static short ToInt16(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<short>(buffer.Slice(offset));
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }

            return readMagic;
        }

        /// <summary>
        /// Reads an int16 from the span with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static short ToInt16(ReadOnlySpan<byte> buffer, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<short>(buffer);
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }

            return readMagic;
        }

        /// <summary>
        /// Reads an uint32 from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static uint ToUInt32(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<uint>(buffer.Slice(offset));
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads an uint32 from the span with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static uint ToUInt32(ReadOnlySpan<byte> buffer, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<uint>(buffer);
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads a ulong from the buffer at the specified position with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static ulong ToUInt64(ReadOnlySpan<byte> buffer, int offset, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<ulong>(buffer.Slice(offset));
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        /// <summary>
        /// Reads a ulong from the span with the specified endianness.
        /// </summary>
        /// <returns></returns>
        public static ulong ToUInt64(ReadOnlySpan<byte> buffer, Endian endianness)
        {
            var readMagic = MemoryMarshal.Read<ulong>(buffer);
            if (!endianness.IsNative)
            {
                //swap
                return BinaryPrimitives.ReverseEndianness(readMagic);
            }
            return readMagic;
        }

        public static Guid ToGuid(ReadOnlySpan<byte> span, Endian endianness)
        {
            if (endianness.IsNative)
            {
                return new Guid(span);
            }
            else
            {
                return new Guid(
                    BinaryPrimitives.ReadInt32BigEndian(span),
                    BinaryPrimitives.ReadInt16BigEndian(span.Slice(4)),
                    BinaryPrimitives.ReadInt16BigEndian(span.Slice(6)),
                    span[8],
                    span[9],
                    span[10],
                    span[11],
                    span[12],
                    span[13],
                    span[14],
                    span[15]);
            }
        }

        #endregion

#if LITTLEENDIANSTREAM
        /// <summary>
        /// Initializes the LittleEndianStream memorystream. All reads will write the little endian version to this stream. Used to reverse endian of files read by this reader
        /// </summary>
        public void SetupEndianReverser()
        {
            LittleEndianStream = MemoryManager.GetMemoryStream();
        }

        /// <summary>
        /// Stream that will write out little endian values of data read in. If this item is not set, the stream is never initialized or written to.
        /// Only use this if you are trying to write-out a little endian version of big endian data. ADVANCED USE ONLY!
        /// </summary>
        public MemoryStream LittleEndianStream { get; private set; }
#endif
    }
}
