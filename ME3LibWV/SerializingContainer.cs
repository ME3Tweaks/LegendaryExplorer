using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3LibWV
{
    public struct SerializingContainer
    {
        public bool isLoading;
        public Stream Memory;
        public SerializingContainer(Stream m)
        {
            this.Memory = m;
            this.isLoading = true;
        }
        public static int operator +(SerializingContainer Container, int i)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 4);
                i = BitConverter.ToInt32(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(i);
                Container.Memory.Write(buff, 0, 4);
            }
            return i;
        }
        public static uint operator +(SerializingContainer Container, uint i)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 4);
                i = BitConverter.ToUInt32(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(i);
                Container.Memory.Write(buff, 0, 4);
            }
            return i;
        }
        public static short operator +(SerializingContainer Container, short i)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 2);
                i = BitConverter.ToInt16(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(i);
                Container.Memory.Write(buff, 0, 2);
            }
            return i;
        }
        public static ushort operator +(SerializingContainer Container, ushort i)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 2);
                i = BitConverter.ToUInt16(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(i);
                Container.Memory.Write(buff, 0, 2);
            }
            return i;
        }
        public static byte operator +(SerializingContainer Container, byte i)
        {
            if (Container.isLoading)
            {
                i = (byte)Container.Memory.ReadByte();
            }
            else
            {
                Container.Memory.WriteByte(i);
            }
            return i;
        }
        public static char operator +(SerializingContainer Container, char c)
        {
            if (Container.isLoading)
            {
                c = (char)Container.Memory.ReadByte();
            }
            else
            {
                Container.Memory.WriteByte((byte)c);
            }
            return c;
        }
        public static float operator +(SerializingContainer Container, float f)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 4);
                f = BitConverter.ToSingle(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(f);
                Container.Memory.Write(buff, 0, 4);
            }
            return f;
        }
        public void Seek(long pos, SeekOrigin origin)
        {
            try
            {
                Memory.Seek(pos, origin);
            }
            catch (Exception)
            {
            }
        }
        public long GetPos()
        {
            try
            {
                return Memory.Position;
            }
            catch (Exception)
            {
            }
            return -1;
        }
    }
}
