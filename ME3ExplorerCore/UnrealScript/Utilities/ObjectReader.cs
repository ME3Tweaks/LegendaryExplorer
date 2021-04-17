using System;
using System.Text;

namespace ME3ExplorerCore.UnrealScript.Utilities
{
    public class ObjectReader
    {
        protected byte[] _data;
        public int Size;
        public int Position;

        public ObjectReader(byte[] data)
        {
            _data = data;
            Size = _data.Length;
        }

        public void Reset()
        {
            Position = 0;
        }

        private int _position(int delta)
        {
            int tmp = Position;
            Position += delta;
            return tmp;
        }

        public byte[] ReadRawData(int numBytes)
        {
            if (Position + numBytes > Size || numBytes <= 0)
                return null;

            byte[] raw = new byte[numBytes];
            Buffer.BlockCopy(_data, _position(numBytes), raw, 0, numBytes);
            return raw;
        }

        public byte ReadByte()
        {
            return Position + 1 > Size ? (byte)0 : _data[_position(1)];
        }

        public int ReadInt32()
        {
            return Position + 4 > Size ? 0 : BitConverter.ToInt32(_data, _position(4));
        }

        public short ReadInt16()
        {
            return Position + 2 > Size ? (short)0 : BitConverter.ToInt16(_data, _position(2));
        }

        public long ReadInt64()
        {
            return Position + 8 > Size ? 0 : BitConverter.ToInt64(_data, _position(8));
        }

        public uint ReadUInt32()
        {
            return Position + 4 > Size ? 0 : BitConverter.ToUInt32(_data, _position(4));
        }

        public ushort ReadUInt16()
        {
            return Position + 2 > Size ? (ushort)0 : BitConverter.ToUInt16(_data, _position(2));
        }

        public ulong ReadUInt64()
        {
            return Position + 8 > Size ? 0 : BitConverter.ToUInt64(_data, _position(8));
        }

        public float ReadFloat()
        {
            return Position + 4 > Size ? 0 : BitConverter.ToSingle(_data, _position(4));
        }

        public string ReadString()
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

        public string ReadNullTerminatedString()
        {
            var str = "";
            var curr = ReadByte();
            while (curr != 0)
            {
                str += (char)curr;
                curr = ReadByte();
            }
            return str;
        }
    }
}
