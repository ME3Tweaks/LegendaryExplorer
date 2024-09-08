using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    [DebuggerDisplay("SC, {IsLoading ? \"Loading\" : \"Saving\"}, Position @ {ms.Position.ToString(\"X8\")}")]
    public partial class SerializingContainer(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0, PackageCache packageCache = null)
    {
        public readonly EndianReader ms = new(stream) { Endian = pcc?.Endian ?? Endian.Little };
        public readonly bool IsLoading = isLoading;
        public readonly MEGame Game = pcc?.Game ?? MEGame.Unknown;
        public readonly PackageCache PackageCache = packageCache;
        public readonly IMEPackage Pcc = pcc;
        public readonly int startOffset = offset;

        public bool IsSaving => !IsLoading;
        public int FileOffset => startOffset + (int)ms.Position;

        internal DefferedFileOffsetWriter SerializeDefferedFileOffset() => new(this);
    }

    internal readonly ref struct DefferedFileOffsetWriter
    {
        private readonly long WritePos;
        /// <summary>
        /// Offset that was read when this value was loaded
        /// </summary>
        public readonly int SerializedOffset;
        public DefferedFileOffsetWriter(SerializingContainer sc)
        {
            WritePos = sc.ms.Position;
            SerializedOffset = 0;
            sc.Serialize(ref SerializedOffset);
        }

        internal DefferedFileOffsetWriter(long writePos)
        {
            WritePos = writePos;
        }

        public void SetPosition(SerializingContainer sc)
        {
            if (sc.IsLoading) return;
            int fileOffset = sc.FileOffset;
            long pos = sc.ms.Position;
            sc.ms.JumpTo(WritePos);
            sc.ms.Writer.WriteInt32(fileOffset);
            sc.ms.JumpTo(pos);
        }
    }

    public partial class SerializingContainer
    {
        public int SerializeFileOffset()
        {
            int offset = FileOffset + 4;
            Serialize(ref offset);
            return offset;
        }

        public void Serialize(ref int val)
        {
            if (IsLoading)
                val = ms.ReadInt32();
            else
                ms.Writer.WriteInt32(val);
        }

        public void SerializeConstInt(int val)
        {
            Serialize(ref val);
        }

        public void Serialize(ref uint val)
        {
            if (IsLoading)
                val = ms.ReadUInt32();
            else
                ms.Writer.WriteUInt32(val);
        }

        public void Serialize(ref short val)
        {
            if (IsLoading)
                val = ms.ReadInt16();
            else
                ms.Writer.WriteInt16(val);
        }

        public void SerializeConstShort(short val)
        {
            Serialize(ref val);
        }

        public void Serialize(ref ushort val)
        {
            if (IsLoading)
                val = ms.ReadUInt16();
            else
                ms.Writer.WriteUInt16(val);
        }

        public void Serialize(ref long val)
        {
            if (IsLoading)
                val = ms.ReadInt64();
            else
                ms.Writer.WriteInt64(val);
        }

        public void Serialize(ref ulong val)
        {
            if (IsLoading)
                val = ms.ReadUInt64();
            else
                ms.Writer.WriteUInt64(val);
        }

        public void Serialize(ref byte val)
        {
            if (IsLoading)
                val = (byte)ms.ReadByte();
            else
                ms.Writer.WriteByte(val);
        }

        public void SerializeConstByte(byte val)
        {
            Serialize(ref val);
        }

        public void Serialize(ref string val)
        {
            if (IsLoading)
                val = ms.ReadUnrealString();
            else
                ms.Writer.WriteUnrealString(val, Game);
        }

        public void Serialize(ref float val)
        {
            if (IsLoading)
                val = ms.ReadFloat();
            else
                ms.Writer.WriteFloat(val);
        }

        public void Serialize(ref double val)
        {
            if (IsLoading)
                val = ms.ReadDouble();
            else
                ms.Writer.WriteDouble(val);
        }

        /// <summary>
        /// Serializes bool as an int
        /// </summary>
        /// <param name="sc"></param>
        /// <param name="val"></param>
        public void Serialize(ref bool val)
        {
            if (IsLoading)
                val = ms.ReadBoolInt();
            else
                ms.Writer.WriteBoolInt(val);
        }

        public void Serialize(ref byte[] buffer, int size)
        {
            if (IsLoading)
            {
                buffer = ms.ReadBytes(size);
            }
            else
            {
                ms.Writer.WriteFromBuffer(buffer);
            }
        }

        public void Serialize(ref Guid guid)
        {
            if (IsLoading)
                guid = ms.ReadGuid();
            else
                ms.Writer.WriteGuid(guid);
        }

        public delegate void SerializeDelegate<T>(ref T item);

        public void Serialize<T>(ref List<T> arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Count ?? 0;
            Serialize(ref count);
            Serialize(ref arr, count, serialize);
        }

        public void Serialize<T>(ref List<T> arr, int count, SerializeDelegate<T> serialize)
        {
            if (IsLoading)
            {
                arr = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    T tmp = default;
                    serialize(ref tmp);
                    arr.Add(tmp);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    T tmp = arr[i];
                    serialize(ref tmp);
                }
            }
        }

        public void Serialize(ref byte[] arr)
        {
            int count = arr?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                arr = ms.ReadBytes(count);
            }
            else if (count > 0)
            {
                ms.Writer.WriteBytes(arr);
            }
        }

        public void Serialize(ref ushort[] arr)
        {
            if (ms.Endian.IsNative)
            {
                int count = arr?.Length ?? 0;
                Serialize(ref count);
                if (IsLoading)
                {
                    arr = new ushort[count];
                    ms.Read(MemoryMarshal.AsBytes<ushort>(arr));
                }
                else if (count > 0)
                {
                    ms.Writer.Write(MemoryMarshal.AsBytes<ushort>(arr));
                }
            }
            else
            {
                Serialize(ref arr, Serialize);
            }
        }

        public void Serialize(ref Vector3[] arr)
        {
            if (ms.Endian.IsNative)
            {
                int count = arr?.Length ?? 0;
                Serialize(ref count);
                if (IsLoading)
                {
                    arr = new Vector3[count];
                    ms.Read(MemoryMarshal.AsBytes<Vector3>(arr));
                }
                else if (count > 0)
                {
                    ms.Writer.Write(MemoryMarshal.AsBytes<Vector3>(arr));
                }
            }
            else
            {
                Serialize(ref arr, Serialize);
            }
        }

        public void Serialize<T>(ref T[] arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(ref arr[i]);
            }
        }

        //TODO: implement serialization support in UMapBase to make this more efficient
        public void Serialize<TKey, TValue>(ref UMap<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                dict = new UMap<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(ref key);
                    TValue value = default;
                    serializeValue(ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                foreach (var kvp in dict)
                {
                    TKey key = kvp.Key;
                    serializeKey(ref key);
                    TValue value = kvp.Value;
                    serializeValue(ref value);
                }
            }
        }

        //TODO: implement serialization support in UMapBase to make this more efficient
        public void Serialize<TKey, TValue>(ref UMultiMap<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                dict = new UMultiMap<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(ref key);
                    TValue value = default;
                    serializeValue(ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                foreach (var kvp in dict)
                {
                    TKey key = kvp.Key;
                    serializeKey(ref key);
                    TValue value = kvp.Value;
                    serializeValue(ref value);
                }
            }
        }

        public void BulkSerialize(ref byte[] arr, SerializeDelegate<byte> serialize, int elementSize)
        {
            Serialize(ref elementSize);
            int count = arr?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                arr = ms.ReadBytes(count);
            }
            else if (count > 0)
            {
                ms.Writer.WriteBytes(arr);
            }
        }
        public void BulkSerialize<T>(ref T[] arr, SerializeDelegate<T> serialize, int elementSize) //unused param is so it has the same sig as generic method 
        {
            Serialize(ref elementSize);
            int count = arr?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(ref arr[i]);
            }
        }
        public void SerializeBulkData<T>(ref T[] arr, SerializeDelegate<T> serialize)
        {
            SerializeConstInt(0);//bulkdataflags
            int elementCount = arr?.Length ?? 0;
            Serialize(ref elementCount);
            long sizeOnDiskPosition = ms.Position;
            SerializeConstInt(0); //when saving, come back and rewrite this after writing arr
            SerializeFileOffset();
            if (IsLoading)
            {
                arr = new T[elementCount];
            }

            for (int i = 0; i < elementCount; i++)
            {
                serialize(ref arr[i]);
            }

            if (IsSaving)
            {
                long curPos = ms.Position;
                ms.JumpTo(sizeOnDiskPosition);
                ms.Writer.WriteInt32((int)(curPos - (sizeOnDiskPosition + 8)));
                ms.JumpTo(curPos);
            }
        }
        public void SerializeBulkData(ref byte[] arr, SerializeDelegate<byte> serialize = null) //unused param is so it has the same sig as generic method 
        {
            SerializeConstInt(0);//bulkdataflags
            int elementCount = arr?.Length ?? 0;
            Serialize(ref elementCount);
            Serialize(ref elementCount); //sizeondisk, which is equal to elementcount for a byte array
            SerializeFileOffset();
            if (IsLoading)
            {
                arr = ms.ReadBytes(elementCount);
            }
            else if (elementCount > 0)
            {
                ms.Writer.WriteBytes(arr);
            }
        }

        public virtual void Serialize(ref NameReference name)
        {
            if (IsLoading)
            {
                name = ms.ReadNameReference(Pcc);
            }
            else
            {
                ms.Writer.WriteNameReference(name, Pcc);
            }
        }

        public void Serialize(ref SharpDX.Color color)
        {
            if (IsLoading)
            {
                byte b = (byte)ms.ReadByte();
                byte g = (byte)ms.ReadByte();
                byte r = (byte)ms.ReadByte();
                byte a = (byte)ms.ReadByte();
                color = new SharpDX.Color(r, g, b, a);
            }
            else
            {
                ms.Writer.WriteByte(color.B);
                ms.Writer.WriteByte(color.G);
                ms.Writer.WriteByte(color.R);
                ms.Writer.WriteByte(color.A);
            }
        }
    }
}
