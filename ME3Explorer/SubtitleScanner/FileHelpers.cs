using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer.SubtitleScanner
{
    public static class FH//Filehelper
    {
        public static void WriteString(FileStream fs, string s)
        {
            if (s == null)
                s = "";
            fs.Write(BitConverter.GetBytes((int)s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);
        }

        public static string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public static string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        public static int GetInt(FileStream fs)
        {
            byte[] buff = new byte[4];
            if (fs.Position < fs.Length - 4)
                fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        public static void WriteInt(FileStream fs, int i)
        {
            fs.Write(BitConverter.GetBytes(i), 0, 4);
        }

        public static ushort GetInt16(FileStream fs)
        {
            byte[] buff = new byte[2];
            if (fs.Position < fs.Length - 2)
                fs.Read(buff, 0, 2);
            return BitConverter.ToUInt16(buff, 0);
        }

        public static void WriteUShort(FileStream fs, ushort i)
        {
            fs.Write(BitConverter.GetBytes(i), 0, 2);
        }
    }
}
