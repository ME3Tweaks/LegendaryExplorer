/*
 * C# Stream Helpers
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski <aquadran at users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.IO;
using System.Text;

namespace StreamHelpers
{
    public static class StreamHelpers
    {
        public static byte[] ReadToBuffer(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new Exception("Stream read error!");
            return buffer;
        }

        public static byte[] ReadToBuffer(this Stream stream, uint count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static byte[] ReadToBuffer(this Stream stream, long count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static void WriteFromBuffer(this Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, int count)
        {
            byte[] buffer = new byte[0x10000];
            do
            {
                int readed = inputStream.Read(buffer, 0, Math.Min(buffer.Length, count));
                if (readed > 0)
                    stream.Write(buffer, 0, readed);
                else
                    break;
                count -= readed;
            } while (count != 0);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, uint count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, long count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static string ReadStringASCII(this Stream stream, int count)
        {
            byte[] buffer = stream.ReadToBuffer(count);
            return Encoding.ASCII.GetString(buffer);
        }

        public static string ReadStringASCIINull(this Stream stream)
        {
            string str = "";
            for (;;)
            {
                char c = (char)stream.ReadByte();
                if (c == 0)
                    break;
                str += c;
            }
            return str;
        }

        public static string ReadStringUnicode(this Stream stream, int count)
        {
            byte[] buffer = stream.ReadToBuffer(count);
            return Encoding.Unicode.GetString(buffer);
        }

        public static string ReadStringUnicodeNull(this Stream stream, int count)
        {
            return stream.ReadStringUnicode(count).Trim('\0');
        }

        public static void WriteStringASCII(this Stream stream, string str)
        {
            stream.Write(Encoding.ASCII.GetBytes(str), 0, Encoding.ASCII.GetByteCount(str));
        }

        public static void WriteStringASCIINull(this Stream stream, string str)
        {
            stream.WriteStringASCII(str + "\0");
        }

        public static void WriteStringUnicode(this Stream stream, string str)
        {
            stream.Write(Encoding.Unicode.GetBytes(str), 0, Encoding.Unicode.GetByteCount(str));
        }

        public static void WriteStringUnicodeNull(this Stream stream, string str)
        {
            stream.WriteStringUnicode(str + "\0");
        }

        public static ulong ReadUInt64(this Stream stream)
        {
            byte[] buffer = new byte[sizeof(ulong)];
            if (stream.Read(buffer, 0, sizeof(ulong)) != sizeof(ulong))
                throw new Exception();
            return BitConverter.ToUInt64(buffer, 0);
        }

        public static long ReadInt64(this Stream stream)
        {
            return (long)stream.ReadUInt64();
        }

        public static void WriteUInt64(this Stream stream, ulong data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ulong));
        }

        public static void WriteInt64(this Stream stream, long data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(long));
        }

        public static uint ReadUInt32(this Stream stream)
        {
            byte[] buffer = new byte[sizeof(uint)];
            if (stream.Read(buffer, 0, sizeof(uint)) != sizeof(uint))
                throw new Exception();
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static int ReadInt32(this Stream stream)
        {
            return (int)stream.ReadUInt32();
        }

        public static void WriteUInt32(this Stream stream, uint data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(uint));
        }

        public static void WriteInt32(this Stream stream, int data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(int));
        }

        public static ushort ReadUInt16(this Stream stream)
        {
            byte[] buffer = new byte[sizeof(ushort)];
            if (stream.Read(buffer, 0, sizeof(ushort)) != sizeof(ushort))
                throw new Exception();
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static short ReadInt16(this Stream stream)
        {
            return (short)stream.ReadUInt16();
        }

        public static void WriteUInt16(this Stream stream, ushort data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ushort));
        }

        public static void WriteInt16(this Stream stream, short data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(short));
        }

        public static void WriteZeros(this Stream stream, uint count)
        {
            for (int i = 0; i < count; i++)
                stream.WriteByte(0);
        }

        public static void WriteZeros(this Stream stream, int count)
        {
            WriteZeros(stream, (uint)count);
        }

        public static void SeekBegin(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        public static void SeekEnd(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        public static void JumpTo(this Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }

        public static void JumpTo(this Stream stream, uint offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }

        public static void JumpTo(this Stream stream, long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }

        public static void Skip(this Stream stream, int count)
        {
            stream.Seek(count, SeekOrigin.Current);
        }

        public static void Skip(this Stream stream, uint count)
        {
            stream.Seek(count, SeekOrigin.Current);
        }

        public static void Skip(this Stream stream, long count)
        {
            stream.Seek(count, SeekOrigin.Current);
        }

        public static void SkipByte(this Stream stream)
        {
            stream.Seek(1, SeekOrigin.Current);
        }

        public static void SkipInt16(this Stream stream)
        {
            stream.Seek(2, SeekOrigin.Current);
        }

        public static void SkipInt32(this Stream stream)
        {
            stream.Seek(4, SeekOrigin.Current);
        }

        public static void SkipInt64(this Stream stream)
        {
            stream.Seek(8, SeekOrigin.Current);
        }
    }
}
