using System;
using System.Text;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public class ObjectReader
    {
        protected byte[] _data;
        protected readonly int Size;
        protected int Position;

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
            return Position + 4 > Size ? 0 : BitConverter.ToInt32(_data, _position(4));
        }

        protected short ReadInt16()
        {
            return Position + 2 > Size ? (short)0 : BitConverter.ToInt16(_data, _position(2));
        }

        protected long ReadInt64()
        {
            return Position + 8 > Size ? 0 : BitConverter.ToInt64(_data, _position(8));
        }

        protected uint ReadUInt32()
        {
            return Position + 4 > Size ? 0 : BitConverter.ToUInt32(_data, _position(4));
        }

        protected ushort ReadUInt16()
        {
            return Position + 2 > Size ? (ushort)0 : BitConverter.ToUInt16(_data, _position(2));
        }

        protected ulong ReadUInt64()
        {
            return Position + 8 > Size ? 0 : BitConverter.ToUInt64(_data, _position(8));
        }

        protected float ReadFloat()
        {
            return Position + 4 > Size ? 0 : BitConverter.ToSingle(_data, _position(4));
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
                return Encoding.Unicode.GetString(bytes).Substring(0, size - 1);
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
