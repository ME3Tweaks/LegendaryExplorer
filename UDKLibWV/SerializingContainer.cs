using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace UDKLibWV
{
    public struct SerializingContainer
    {
        public bool isLoading;
        private MemoryStream Memory;
        public SerializingContainer(MemoryStream m)
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

        public static Vector3 operator +(SerializingContainer Container, Vector3 v)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 4);
                v.X = BitConverter.ToSingle(buff, 0);
                Container.Memory.Read(buff, 0, 4);
                v.Y = BitConverter.ToSingle(buff, 0);
                Container.Memory.Read(buff, 0, 4);
                v.Z = BitConverter.ToSingle(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(v.X);
                Container.Memory.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(v.Y);
                Container.Memory.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(v.Z);
                Container.Memory.Write(buff, 0, 4);
            }
            return v;
        }

        public static Vector4 operator +(SerializingContainer Container, Vector4 v)
        {
            if (Container.isLoading)
            {
                byte[] buff = new byte[4];
                Container.Memory.Read(buff, 0, 4);
                v.X = BitConverter.ToSingle(buff, 0);
                Container.Memory.Read(buff, 0, 4);
                v.Y = BitConverter.ToSingle(buff, 0);
                Container.Memory.Read(buff, 0, 4);
                v.Z = BitConverter.ToSingle(buff, 0);
                Container.Memory.Read(buff, 0, 4);
                v.W = BitConverter.ToSingle(buff, 0);
            }
            else
            {
                byte[] buff = BitConverter.GetBytes(v.X);
                Container.Memory.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(v.Y);
                Container.Memory.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(v.Z);
                Container.Memory.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(v.W);
                Container.Memory.Write(buff, 0, 4);
            }
            return v;
        }


        public void Seek(int pos, SeekOrigin origin)
        {
            try
            {
                Memory.Seek(pos, origin);
            }
            catch (Exception)
            {
            }
        }

        public int GetPos()
        {
            try
            {
                return (int)Memory.Position;
            }
            catch (Exception)
            {
            }
            return -1;
        }
    }
}
