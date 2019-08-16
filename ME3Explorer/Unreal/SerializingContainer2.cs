using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using StreamHelpers;

namespace ME3Explorer
{
    public class SerializingContainer2
    {
        public Stream ms;
        public bool IsLoading;

        public SerializingContainer2(Stream stream, bool isLoading = false)
        {
            ms = stream;
            IsLoading = isLoading;
        }

        public void Serialize(ref int val)
        {
            if (IsLoading)
                val = ms.ReadInt32();
            else
                ms.WriteInt32(val);
        }

        public void Serialize(ref uint val)
        {
            if (IsLoading)
                val = ms.ReadUInt32();
            else
                ms.WriteUInt32(val);
        }

        public void Serialize(ref short val)
        {
            if (IsLoading)
                val = ms.ReadInt16();
            else
                ms.WriteInt16(val);
        }

        public void Serialize(ref ushort val)
        {
            if (IsLoading)
                val = ms.ReadUInt16();
            else
                ms.WriteUInt16(val);
        }

        public void Serialize(ref long val)
        {
            if (IsLoading)
                val = ms.ReadInt64();
            else
                ms.WriteInt64(val);
        }

        public void Serialize(ref ulong val)
        {
            if (IsLoading)
                val = ms.ReadUInt64();
            else
                ms.WriteUInt64(val);
        }

        public void Serialize(ref byte val)
        {
            if (IsLoading)
                val = (byte)ms.ReadByte();
            else
                ms.WriteByte(val);
        }

        public void Serialize(ref string val, MEGame game)
        {
            if (IsLoading)
                val = ms.ReadUnrealString();
            else
                ms.WriteUnrealString(val, game);
        }

        public void Serialize(ref float val)
        {
            if (IsLoading)
                val = ms.ReadFloat();
            else
                ms.WriteFloat(val);
        }

        public void Serialize(ref double val)
        {
            if (IsLoading)
                val = ms.ReadDouble();
            else
                ms.WriteDouble(val);
        }

        public void Serialize(ref bool val, bool isInt = false)
        {
            if (IsLoading)
                val = isInt ? ms.ReadBoolInt() : ms.ReadBoolByte();
            else if (isInt)
                ms.WriteBoolInt(val);
            else
                ms.WriteBoolByte(val);
        }

        public void Serialize(ref byte[] buffer, int size)
        {
            if (IsLoading)
            {
                buffer = ms.ReadToBuffer(size);
            }
            else
            {
                ms.WriteFromBuffer(buffer);
            }
        }

        public void Serialize(ref Guid guid)
        {
            if (IsLoading)
                guid = ms.ReadGuid();
            else
                ms.WriteGuid(guid);
        }

        public void Serialize(ref int[] arr)
        {
            int count = arr?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                arr = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                Serialize(ref arr[i]);
            }

        }
    }

    public static class SerializingContainerExtensions
    {
        public static void Serialize(this SerializingContainer2 sc, ref NameReference name, IMEPackage pcc)
        {
            if (sc.IsLoading)
            {
                name = sc.ms.ReadNameReference(pcc);
            }
            else
            {
                sc.ms.WriteNameReference(name, pcc);
            }
        }
    }
}
