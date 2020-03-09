using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gammtek.Conduit.IO;

namespace ME3Explorer
{
    public struct SerializingContainer
    {
        public bool isLoading;
        public EndianReader Memory;
        public SerializingContainer(EndianReader m)
        {
            this.Memory = m;
            this.isLoading = true;
        }
        public static int operator +(SerializingContainer Container, int i)
        {
            if (Container.isLoading)
            {
                return Container.Memory.ReadInt32();
            }
            else
            {
                Container.Memory.Writer.WriteInt32(i);
            }
            return i;
        }

        public static uint operator +(SerializingContainer Container, uint i)
        {
            if (Container.isLoading)
            {
                return Container.Memory.ReadUInt32();
            }
            else
            {
                Container.Memory.Writer.WriteUInt32(i);
            }
            return i;
        }

        public static short operator +(SerializingContainer Container, short i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadInt16();
            }
            else
            {
                Container.Memory.Writer.WriteInt16(i);
            }
            return i;
        }

        public static ushort operator +(SerializingContainer Container, ushort i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadUInt16();
            }
            else
            {
                Container.Memory.Writer.WriteUInt16(i);
            }
            return i;
        }

        public static byte operator +(SerializingContainer Container, byte i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadByte();
            }
            else
            {
                Container.Memory.Writer.WriteByte(i);
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
                Container.Memory.Writer.WriteByte((byte)c);
            }
            return c;
        }

        public static float operator +(SerializingContainer Container, float f)
        {
            if (Container.isLoading)
            {
                f = Container.Memory.ReadSingle();
            }
            else
            {
                Container.Memory.Writer.WriteFloat(f);
            }
            return f;
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

    public struct SerializingFile
    {
        public bool isLoading;
        public EndianReader Memory;
        public SerializingFile(EndianReader m)
        {
            this.Memory = m;
            this.isLoading = true;
        }
        public static int operator +(SerializingFile Container, int i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadInt32();
            }
            else
            {
                Container.Memory.Writer.WriteInt32(i);
            }
            return i;
        }

        public static uint operator +(SerializingFile Container, uint i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadUInt32();
            }
            else
            {
                Container.Memory.Writer.WriteUInt32(i);
            }
            return i;
        }

        public static short operator +(SerializingFile Container, short i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadInt16();
            }
            else
            {
                Container.Memory.Writer.WriteInt16(i);
            }

            return i;
        }

        public static ushort operator +(SerializingFile Container, ushort i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadUInt16();
            }
            else
            {
                Container.Memory.Writer.WriteUInt16(i);
            }

            return i;
        }

        public static byte operator +(SerializingFile Container, byte i)
        {
            if (Container.isLoading)
            {
                i = Container.Memory.ReadByte();
            }
            else
            {
                Container.Memory.Writer.WriteByte(i);
            }

            return i;
        }

        public static char operator +(SerializingFile Container, char c)
        {
            if (Container.isLoading)
            {
                c = Container.Memory.ReadChar();
            }
            else
            {
                Container.Memory.Writer.WriteByte((byte)c);
            }

            return c;
        }

        public static float operator +(SerializingFile Container, float f)
        {
            if (Container.isLoading)
            {
                f = Container.Memory.ReadFloat();
            }
            else
            {
                Container.Memory.Writer.WriteFloat(f);
            }
            return f;
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
            return 0;
        }
    }
}
