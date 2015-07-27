/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Gibbed.IO;

namespace Gibbed.MassEffect3.FileFormats.Unreal
{
    public class FileReader : ISerializer
    {
        private readonly Stream _Input;

        public Endian Endian;
        public uint Version { get; private set; }

        public SerializeMode Mode
        {
            get { return SerializeMode.Reading; }
        }

        public FileReader(Stream input, uint version)
            : this(input, version, Endian.Little)
        {
        }

        public FileReader(Stream input, uint version, Endian endian)
        {
            this._Input = input;
            this.Version = version;
            this.Endian = endian;
        }

        private string ReadString()
        {
            var length = this._Input.ReadValueS32(this.Endian);
            if (length == 0)
            {
                return "";
            }

            bool isUnicode = false;
            if (length < 0)
            {
                length = Math.Abs(length);
                isUnicode = true;
            }

            if (length >= 1024 * 1024)
            {
                throw new FormatException("somehow I doubt there is a >1MB string to be read");
            }

            if (isUnicode == true)
            {
                return this._Input.ReadString((uint)(length * 2),
                                              true,
                                              this.Endian == Endian.Little
                                                  ? Encoding.Unicode
                                                  : Encoding.BigEndianUnicode);
            }

            var bytes = this._Input.ReadBytes(length);
            var sb = new StringBuilder();
            foreach (var t in bytes)
            {
                if (t == 0)
                {
                    break;
                }
                sb.Append((char)t);
            }
            return sb.ToString();
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref bool value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueB32(this.Endian);
        }

        public void Serialize(ref bool value, Func<ISerializer, bool> condition, Func<bool> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref byte value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueU8();
        }

        public void Serialize(ref byte value, Func<ISerializer, bool> condition, Func<byte> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref int value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueS32(this.Endian);
        }

        public void Serialize(ref int value, Func<ISerializer, bool> condition, Func<int> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref uint value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueU32(this.Endian);
        }

        public void Serialize(ref uint value, Func<ISerializer, bool> condition, Func<uint> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref float value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueF32(this.Endian);
        }

        public void Serialize(ref float value, Func<ISerializer, bool> condition, Func<float> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref string value)
            // ReSharper restore RedundantAssignment
        {
            value = this.ReadString();
        }

        public void Serialize(ref string value, Func<ISerializer, bool> condition, Func<string> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize(ref Guid value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueGuid(this.Endian);
        }

        public void Serialize(ref Guid value, Func<ISerializer, bool> condition, Func<Guid> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void SerializeEnum<TEnum>(ref TEnum value)
            // ReSharper restore RedundantAssignment
        {
            value = this._Input.ReadValueEnum<TEnum>(this.Endian);
        }

        public void SerializeEnum<TEnum>(ref TEnum value, Func<ISerializer, bool> condition, Func<TEnum> defaultValue)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.SerializeEnum(ref value);
            }
            else
            {
                value = defaultValue();
            }
        }

        // ReSharper disable RedundantAssignment
        public void Serialize<TType>(ref TType value)
            // ReSharper restore RedundantAssignment
            where TType : class, ISerializable, new()
        {
            if (value == null)
            {
                throw new ArgumentNullException("value", "serializable class value should not be null");
            }

            //value = new TType();
            value.Serialize(this);
        }

        public void Serialize<TType>(ref TType value, Func<ISerializer, bool> condition, Func<TType> defaultValue)
            where TType : class, ISerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref value);
            }
            else
            {
                value = defaultValue();
                if (value == null)
                {
                    throw new ArgumentException("evaluated default value cannot be null", "defaultValue");
                }
            }
        }

        public void Serialize(ref BitArray list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            var count = this._Input.ReadValueU32(this.Endian);
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in list");
            }

            list.Length = (int)(count * 32);
            list.SetAll(false);

            for (uint i = 0; i < count; i++)
            {
                uint offset = i * 32;
                var bits = this._Input.ReadValueU32(this.Endian);
                for (int bit = 0; bit < 32; bit++)
                {
                    list.Set((int)(offset + bit), (bits & (1 << bit)) != 0);
                }
            }
        }

        public void Serialize(ref BitArray list, Func<ISerializer, bool> condition, Func<BitArray> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        private void ReadBasicList<TType>(List<TType> list, Func<FileReader, TType> readValue)
        {
            var count = this._Input.ReadValueU32(this.Endian);
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in list");
            }

            list.Clear();
            for (uint i = 0; i < count; i++)
            {
                list.Add(readValue(this));
            }
        }

        public void Serialize(ref List<byte> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueU8());
        }

        public void Serialize(ref List<byte> list, Func<ISerializer, bool> condition, Func<List<byte>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<int> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueS32(r.Endian));
        }

        public void Serialize(ref List<int> list, Func<ISerializer, bool> condition, Func<List<int>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<uint> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueU32(r.Endian));
        }

        public void Serialize(ref List<uint> list, Func<ISerializer, bool> condition, Func<List<uint>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<float> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueF32(r.Endian));
        }

        public void Serialize(ref List<float> list, Func<ISerializer, bool> condition, Func<List<float>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<string> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r.ReadString());
        }

        public void Serialize(ref List<string> list, Func<ISerializer, bool> condition, Func<List<string>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize(ref List<Guid> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueGuid(r.Endian));
        }

        public void Serialize(ref List<Guid> list, Func<ISerializer, bool> condition, Func<List<Guid>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void SerializeEnum<TEnum>(ref List<TEnum> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            this.ReadBasicList(list, r => r._Input.ReadValueEnum<TEnum>(r.Endian));
        }

        public void SerializeEnum<TEnum>(ref List<TEnum> list,
                                         Func<ISerializer, bool> condition,
                                         Func<List<TEnum>> defaultList)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.SerializeEnum(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize<TType>(ref List<TType> list)
            where TType : class, ISerializable, new()
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            var count = this._Input.ReadValueU32(this.Endian);
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in list");
            }

            list.Clear();
            for (uint i = 0; i < count; i++)
            {
                var item = new TType();
                item.Serialize(this);
                list.Add(item);
            }
        }

        public void Serialize<TType>(ref List<TType> list,
                                     Func<ISerializer, bool> condition,
                                     Func<List<TType>> defaultList)
            where TType : class, ISerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }

        public void Serialize<TType>(ref BindingList<TType> list) where TType : class, ISerializable, new()
        {
            if (list == null)
            {
                throw new ArgumentNullException("list", "serializable list should not be null");
            }

            var count = this._Input.ReadValueU32(this.Endian);
            if (count >= 0x7FFFFF)
            {
                throw new FormatException("too many items in list");
            }

            list.Clear();
            for (uint i = 0; i < count; i++)
            {
                var item = new TType();
                item.Serialize(this);
                list.Add(item);
            }
        }

        public void Serialize<TType>(ref BindingList<TType> list,
                                     Func<ISerializer, bool> condition,
                                     Func<BindingList<TType>> defaultList)
            where TType : class, ISerializable, new()
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (defaultList == null)
            {
                throw new ArgumentNullException("defaultList");
            }

            if (condition(this) == false)
            {
                this.Serialize(ref list);
            }
            else
            {
                list = defaultList();
                if (list == null)
                {
                    throw new ArgumentException("evaluated default list cannot be null", "defaultList");
                }
            }
        }
    }
}
