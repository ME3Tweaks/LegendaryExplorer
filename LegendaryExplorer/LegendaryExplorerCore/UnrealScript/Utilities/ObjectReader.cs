using System;
using System.Buffers.Binary;
using System.Text;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public class ObjectReader
    {
        protected byte[] _data;
        protected readonly int Size;
        protected int Position;

        /// <summary>
        /// Console platforms (Xenon, PS3, WiiU) store multi-byte values big endian
        /// </summary>
        protected bool BigEndian;

        protected ObjectReader(byte[] data)
        {
            _data = data;
            Size = _data.Length;
        }

        protected void Reset()
        {
            Position = 0;
        }

        private int _position(int delta)
        {
            int tmp = Position;
            Position += delta;
            return tmp;
        }

        protected byte[] ReadRawData(int numBytes)
        {
            if (Position + numBytes > Size || numBytes <= 0)
                return null;

            byte[] raw = new byte[numBytes];
            Buffer.BlockCopy(_data, _position(numBytes), raw, 0, numBytes);
            return raw;
        }

        protected byte ReadByte()
        {
            return Position + 1 > Size ? (byte)0 : _data[_position(1)];
        }

        protected int ReadInt32()
        {
            if (Position + 4 > Size) return 0;
            var span = _data.AsSpan(_position(4), 4);
            return BigEndian ? BinaryPrimitives.ReadInt32BigEndian(span) : BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        protected short ReadInt16()
        {
            if (Position + 2 > Size) return 0;
            var span = _data.AsSpan(_position(2), 2);
            return BigEndian ? BinaryPrimitives.ReadInt16BigEndian(span) : BinaryPrimitives.ReadInt16LittleEndian(span);
        }

        protected long ReadInt64()
        {
            if (Position + 8 > Size) return 0;
            var span = _data.AsSpan(_position(8), 8);
            return BigEndian ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span);
        }

        protected uint ReadUInt32()
        {
            if (Position + 4 > Size) return 0;
            var span = _data.AsSpan(_position(4), 4);
            return BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        protected ushort ReadUInt16()
        {
            if (Position + 2 > Size) return 0;
            var span = _data.AsSpan(_position(2), 2);
            return BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        protected ulong ReadUInt64()
        {
            if (Position + 8 > Size) return 0;
            var span = _data.AsSpan(_position(8), 8);
            return BigEndian ? BinaryPrimitives.ReadUInt64BigEndian(span) : BinaryPrimitives.ReadUInt64LittleEndian(span);
        }

        protected float ReadFloat()
        {
            if (Position + 4 > Size) return 0;
            var span = _data.AsSpan(_position(4), 4);
            return BigEndian ? BinaryPrimitives.ReadSingleBigEndian(span) : BinaryPrimitives.ReadSingleLittleEndian(span);
        }

        protected string ReadString()
        {
            var size = ReadInt32();
            bool unicode = false;
            if (size < 0)
            {
                size = -size;
                unicode = true;
            }
            else if (size == 0)
            {
                return string.Empty;
            }

            if (unicode)
            {
                var bytes = ReadRawData((size * 2));
                return (BigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode).GetString(bytes).Substring(0, size - 1);
            }
            else
            {
                var bytes = ReadRawData(size);
                return Encoding.ASCII.GetString(bytes).Substring(0, size - 1);
            }
        }

        protected unsafe string ReadNullTerminatedString()
        {
            string str;
            fixed (byte* p = _data.AsSpan(Position))
            {
                str = new string((sbyte*)p);
            }
            Position += str.Length + 1;
            return str;
        }
    }
}
