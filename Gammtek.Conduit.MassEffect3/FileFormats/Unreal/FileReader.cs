using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.FileFormats.Unreal
{
	public class FileReader : ISerializer
	{
		private readonly Stream _Input;

		public ByteOrder ByteOrder;

		public FileReader(Stream input, uint version)
			: this(input, version, ByteOrder.LittleEndian)
		{}

		public FileReader(Stream input, uint version, ByteOrder byteOrder)
		{
			_Input = input;
			Version = version;
			ByteOrder = byteOrder;
		}

		public uint Version { get; private set; }

		public SerializeMode Mode
		{
			get { return SerializeMode.Reading; }
		}

		// ReSharper disable RedundantAssignment
		public void Serialize(ref bool value)
			// ReSharper restore RedundantAssignment
		{
			value = _Input.ReadBooleanInt(ByteOrder);
		}

		public void Serialize(ref bool value, Func<ISerializer, bool> condition, Func<bool> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = (byte) _Input.ReadByte();
		}

		public void Serialize(ref byte value, Func<ISerializer, bool> condition, Func<byte> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = _Input.ReadInt32(ByteOrder);
		}

		public void Serialize(ref int value, Func<ISerializer, bool> condition, Func<int> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = _Input.ReadUInt32(ByteOrder);
		}

		public void Serialize(ref uint value, Func<ISerializer, bool> condition, Func<uint> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = _Input.ReadSingle(ByteOrder);
		}

		public void Serialize(ref float value, Func<ISerializer, bool> condition, Func<float> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = ReadString();
		}

		public void Serialize(ref string value, Func<ISerializer, bool> condition, Func<string> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = _Input.ReadGuid(ByteOrder);
		}

		public void Serialize(ref Guid value, Func<ISerializer, bool> condition, Func<Guid> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
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
			value = _Input.ReadEnum<TEnum>(ByteOrder);
		}

		public void SerializeEnum<TEnum>(ref TEnum value, Func<ISerializer, bool> condition, Func<TEnum> defaultValue)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				SerializeEnum(ref value);
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
				throw new ArgumentNullException(nameof(value), "serializable class value should not be null");
			}

			//value = new TType();
			value.Serialize(this);
		}

		public void Serialize<TType>(ref TType value, Func<ISerializer, bool> condition, Func<TType> defaultValue)
			where TType : class, ISerializable, new()
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultValue == null)
			{
				throw new ArgumentNullException(nameof(defaultValue));
			}

			if (condition(this) == false)
			{
				Serialize(ref value);
			}
			else
			{
				value = defaultValue();
				if (value == null)
				{
					throw new ArgumentException("evaluated default value cannot be null", nameof(defaultValue));
				}
			}
		}

		public void Serialize(ref BitArray list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			var count = _Input.ReadUInt32(ByteOrder);
			if (count >= 0x7FFFFF)
			{
				throw new FormatException("too many items in list");
			}

			list.Length = (int) (count*32);
			list.SetAll(false);

			for (uint i = 0; i < count; i++)
			{
				var offset = i*32;
				var bits = _Input.ReadUInt32(ByteOrder);
				for (var bit = 0; bit < 32; bit++)
				{
					list.Set((int) (offset + bit), (bits & (1 << bit)) != 0);
				}
			}
		}

		public void Serialize(ref BitArray list, Func<ISerializer, bool> condition, Func<BitArray> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize(ref List<byte> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadUInt8());
		}

		public void Serialize(ref List<byte> list, Func<ISerializer, bool> condition, Func<List<byte>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize(ref List<int> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadInt32(r.ByteOrder));
		}

		public void Serialize(ref List<int> list, Func<ISerializer, bool> condition, Func<List<int>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize(ref List<float> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadSingle(r.ByteOrder));
		}

		public void Serialize(ref List<float> list, Func<ISerializer, bool> condition, Func<List<float>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize(ref List<string> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r.ReadString());
		}

		public void Serialize(ref List<string> list, Func<ISerializer, bool> condition, Func<List<string>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize(ref List<Guid> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadGuid(r.ByteOrder));
		}

		public void Serialize(ref List<Guid> list, Func<ISerializer, bool> condition, Func<List<Guid>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void SerializeEnum<TEnum>(ref List<TEnum> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadEnum<TEnum>(r.ByteOrder));
		}

		public void SerializeEnum<TEnum>(ref List<TEnum> list,
			Func<ISerializer, bool> condition,
			Func<List<TEnum>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				SerializeEnum(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize<TType>(ref List<TType> list)
			where TType : class, ISerializable, new()
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			var count = _Input.ReadUInt32(ByteOrder);
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
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		public void Serialize<TType>(ref BindingList<TType> list) where TType : class, ISerializable, new()
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			var count = _Input.ReadUInt32(ByteOrder);
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
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}

		private string ReadString()
		{
			var length = _Input.ReadInt32(ByteOrder);
			if (length == 0)
			{
				return "";
			}

			var isUnicode = false;
			if (length < 0)
			{
				length = Math.Abs(length);
				isUnicode = true;
			}

			if (length >= 1024*1024)
			{
				throw new FormatException("somehow I doubt there is a >1MB string to be read");
			}

			if (isUnicode)
			{
				return _Input.ReadString((uint) (length*2),
					true,
					ByteOrder == ByteOrder.LittleEndian
						? Encoding.Unicode
						: Encoding.BigEndianUnicode);
			}

			var bytes = _Input.ReadBytes(length);
			var sb = new StringBuilder();
			foreach (var t in bytes)
			{
				if (t == 0)
				{
					break;
				}
				sb.Append((char) t);
			}
			return sb.ToString();
		}

		private void ReadBasicList<TType>(List<TType> list, Func<FileReader, TType> readValue)
		{
			var count = _Input.ReadUInt32(ByteOrder);
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

		public void Serialize(ref List<uint> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			ReadBasicList(list, r => r._Input.ReadUInt32(r.ByteOrder));
		}

		public void Serialize(ref List<uint> list, Func<ISerializer, bool> condition, Func<List<uint>> defaultList)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			if (defaultList == null)
			{
				throw new ArgumentNullException(nameof(defaultList));
			}

			if (condition(this) == false)
			{
				Serialize(ref list);
			}
			else
			{
				list = defaultList();
				if (list == null)
				{
					throw new ArgumentException("evaluated default list cannot be null", nameof(defaultList));
				}
			}
		}
	}
}