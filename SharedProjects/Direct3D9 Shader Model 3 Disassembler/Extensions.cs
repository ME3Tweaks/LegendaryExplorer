using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct3D9_Shader_Model_3_Disassembler
{
    internal static class Extensions
    {
        public static float ReadFloat(this Stream stream)
        {
            var bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToSingle(bytes, 0);
        }
        public static int ReadInt32(this Stream stream)
        {
            var bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToInt32(bytes, 0);
        }
        public static uint ReadUInt32(this Stream stream)
        {
            var bytes = new byte[4];
            stream.Read(bytes, 0, 4);
            return BitConverter.ToUInt32(bytes, 0);
        }
        public static ushort ReadUInt16(this Stream stream)
        {
            var bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static string ReadString(this Stream stream)
        {

            long startPos = stream.Position;
            int length = 0;
            while (stream.ReadByte() != 0)
            {
                length++;
            }

            stream.Seek(startPos, SeekOrigin.Begin);
            byte[] buff = new byte[length];
            stream.Read(buff, 0, length);
            return Encoding.ASCII.GetString(buff);
        }

        public static uint bits(this uint word, byte from, byte to)
        {
            Contract.Assert(from < 32);
            Contract.Assert(to < 32);
            Contract.Assert(to < from);

            return (word << (31 - from)) >> (31 - from + to);
        }

        public static bool bit(this uint word, byte index)
        {
            Contract.Assert(index < 32);

            return (word << (31 - index)) >> 31 == 1;
        }

        public static int NumDigits(this int i)
        {
            return i > 0 ? (int)Math.Log10(i) + 1 : 1;
        }
    }
}
