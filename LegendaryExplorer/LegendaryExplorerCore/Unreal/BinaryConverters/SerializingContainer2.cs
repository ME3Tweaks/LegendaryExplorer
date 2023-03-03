using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    [DebuggerDisplay("SC2, IsLoading: {IsLoading}, IsSaving: {IsSaving}, Position @ {ms.Position.ToString(\"X8\")}")]
    public class SerializingContainer2
    {
        public readonly PackageCache packageCache;
        public readonly EndianReader ms;
        public readonly bool IsLoading;
        public readonly IMEPackage Pcc;
        public readonly int startOffset;
        public readonly MEGame Game;

        public bool IsSaving => !IsLoading;
        public int FileOffset => startOffset + (int)ms.Position;

        public SerializingContainer2(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0, PackageCache packageCache = null)
        {
            Game = pcc?.Game ?? MEGame.Unknown;
            ms = new EndianReader(stream) { Endian = pcc?.Endian ?? Endian.Little };
            IsLoading = isLoading;
            Pcc = pcc;
            startOffset = offset;
            this.packageCache = packageCache;
        }
    }

    public static partial class SCExt
    {
        public static int SerializeFileOffset(this SerializingContainer2 sc)
        {
            int offset = sc.FileOffset + 4;
            sc.Serialize(ref offset);
            return offset;
        }

        public static void Serialize(this SerializingContainer2 sc, ref int val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt32();
            else
                sc.ms.Writer.WriteInt32(val);
        }

        public static void SerializeConstInt(this SerializingContainer2 sc, int val)
        {
            sc.Serialize(ref val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref uint val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt32();
            else
                sc.ms.Writer.WriteUInt32(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref short val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt16();
            else
                sc.ms.Writer.WriteInt16(val);
        }

        public static void SerializeConstShort(this SerializingContainer2 sc, short val)
        {
            sc.Serialize(ref val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ushort val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt16();
            else
                sc.ms.Writer.WriteUInt16(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref long val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt64();
            else
                sc.ms.Writer.WriteInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ulong val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt64();
            else
                sc.ms.Writer.WriteUInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte val)
        {
            if (sc.IsLoading)
                val = (byte)sc.ms.ReadByte();
            else
                sc.ms.Writer.WriteByte(val);
        }

        public static void SerializeConstByte(this SerializingContainer2 sc, byte val)
        {
            sc.Serialize(ref val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref string val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUnrealString();
            else
                sc.ms.Writer.WriteUnrealString(val, sc.Game);
        }

        public static void Serialize(this SerializingContainer2 sc, ref float val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadFloat();
            else
                sc.ms.Writer.WriteFloat(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref double val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadDouble();
            else
                sc.ms.Writer.WriteDouble(val);
        }

        /// <summary>
        /// Serializes bool as an int
        /// </summary>
        /// <param name="sc"></param>
        /// <param name="val"></param>
        public static void Serialize(this SerializingContainer2 sc, ref bool val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadBoolInt();
            else
                sc.ms.Writer.WriteBoolInt(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte[] buffer, int size)
        {
            if (sc.IsLoading)
            {
                buffer = sc.ms.ReadBytes(size);
            }
            else
            {
                sc.ms.Writer.WriteFromBuffer(buffer);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref Guid guid)
        {
            if (sc.IsLoading)
                guid = sc.ms.ReadGuid();
            else
                sc.ms.Writer.WriteGuid(guid);
        }

        public delegate void SerializeDelegate<T>(SerializingContainer2 sc, ref T item);

        public static void Serialize<T>(this SerializingContainer2 sc, ref List<T> arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Count ?? 0;
            sc.Serialize(ref count);
            sc.Serialize(ref arr, count, serialize);
        }

        public static void Serialize<T>(this SerializingContainer2 sc, ref List<T> arr, int count, SerializeDelegate<T> serialize)
        {
            if (sc.IsLoading)
            {
                arr = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    T tmp = default;
                    serialize(sc, ref tmp);
                    arr.Add(tmp);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    T tmp = arr[i];
                    serialize(sc, ref tmp);
                }
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte[] arr)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = sc.ms.ReadBytes(count);
            }
            else if (count > 0)
            {
                sc.ms.Writer.WriteBytes(arr);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref ushort[] arr)
        {
            if (sc.ms.Endian.IsNative)
            {
                int count = arr?.Length ?? 0;
                sc.Serialize(ref count);
                if (sc.IsLoading)
                {
                    arr = new ushort[count];
                    sc.ms.Read(MemoryMarshal.AsBytes<ushort>(arr));
                }
                else if (count > 0)
                {
                    sc.ms.Writer.Write(MemoryMarshal.AsBytes<ushort>(arr));
                }
            }
            else
            {
                Serialize(sc, ref arr, Serialize);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref Vector3[] arr)
        {
            if (sc.ms.Endian.IsNative)
            {
                int count = arr?.Length ?? 0;
                sc.Serialize(ref count);
                if (sc.IsLoading)
                {
                    arr = new Vector3[count];
                    sc.ms.Read(MemoryMarshal.AsBytes<Vector3>(arr));
                }
                else if (count > 0)
                {
                    sc.ms.Writer.Write(MemoryMarshal.AsBytes<Vector3>(arr));
                }
            }
            else
            {
                Serialize(sc, ref arr, Serialize);
            }
        }

        public static void Serialize<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(sc, ref arr[i]);
            }
        }

        //TODO: implement serialization support in UMapBase to make this more efficient
        public static void Serialize<TKey, TValue>(this SerializingContainer2 sc, ref UMap<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                dict = new UMap<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(sc, ref key);
                    TValue value = default;
                    serializeValue(sc, ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                foreach (var kvp in dict)
                {
                    TKey key = kvp.Key;
                    serializeKey(sc, ref key);
                    TValue value = kvp.Value;
                    serializeValue(sc, ref value);
                }
            }
        }

        //TODO: implement serialization support in UMapBase to make this more efficient
        public static void Serialize<TKey, TValue>(this SerializingContainer2 sc, ref UMultiMap<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                dict = new UMultiMap<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(sc, ref key);
                    TValue value = default;
                    serializeValue(sc, ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                foreach (var kvp in dict)
                {
                    TKey key = kvp.Key;
                    serializeKey(sc, ref key);
                    TValue value = kvp.Value;
                    serializeValue(sc, ref value);
                }
            }
        }

        public static void BulkSerialize(this SerializingContainer2 sc, ref byte[] arr, SerializeDelegate<byte> serialize, int elementSize)
        {
            sc.Serialize(ref elementSize);
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = sc.ms.ReadBytes(count);
            }
            else if (count > 0)
            {
                sc.ms.Writer.WriteBytes(arr);
            }
        }
        public static void BulkSerialize<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize, int elementSize)
        {
            sc.Serialize(ref elementSize);
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(sc, ref arr[i]);
            }
        }
        public static void SerializeBulkData<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize)
        {
            sc.SerializeConstInt(0);//bulkdataflags
            int elementCount = arr?.Length ?? 0;
            sc.Serialize(ref elementCount);
            long sizeOnDiskPosition = sc.ms.Position;
            sc.SerializeConstInt(0); //when saving, come back and rewrite this after writing arr
            sc.SerializeFileOffset();
            if (sc.IsLoading)
            {
                arr = new T[elementCount];
            }

            for (int i = 0; i < elementCount; i++)
            {
                serialize(sc, ref arr[i]);
            }

            if (sc.IsSaving)
            {
                long curPos = sc.ms.Position;
                sc.ms.JumpTo(sizeOnDiskPosition);
                sc.ms.Writer.WriteInt32((int)(curPos - (sizeOnDiskPosition + 8)));
                sc.ms.JumpTo(curPos);
            }
        }
        public static void SerializeBulkData(this SerializingContainer2 sc, ref byte[] arr, SerializeDelegate<byte> serialize = null)
        {
            sc.SerializeConstInt(0);//bulkdataflags
            int elementCount = arr?.Length ?? 0;
            sc.Serialize(ref elementCount);
            sc.Serialize(ref elementCount); //sizeondisk, which is equal to elementcount for a byte array
            sc.SerializeFileOffset();
            if (sc.IsLoading)
            {
                arr = sc.ms.ReadBytes(elementCount);
            }
            else if (elementCount > 0)
            {
                sc.ms.Writer.WriteBytes(arr);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref NameReference name)
        {
            if (sc.IsLoading)
            {
                name = sc.ms.ReadNameReference(sc.Pcc);
            }
            else
            {
                sc.ms.Writer.WriteNameReference(name, sc.Pcc);
            }
        }


        public static void Serialize(this SerializingContainer2 sc, ref SharpDX.Color color)
        {
            if (sc.IsLoading)
            {
                byte b = (byte)sc.ms.ReadByte();
                byte g = (byte)sc.ms.ReadByte();
                byte r = (byte)sc.ms.ReadByte();
                byte a = (byte)sc.ms.ReadByte();
                color = new SharpDX.Color(r, g, b, a);
            }
            else
            {
                sc.ms.Writer.WriteByte(color.B);
                sc.ms.Writer.WriteByte(color.G);
                sc.ms.Writer.WriteByte(color.R);
                sc.ms.Writer.WriteByte(color.A);
            }
        }
    }
}
