using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.FileFormats.Unreal
{
	public class FileWriter : ISerializer
	{
		private readonly Stream _Output;

		public ByteOrder Endian;

		public FileWriter(Stream output, uint version)
			: this(output, version, ByteOrder.LittleEndian)
		{}

		public FileWriter(Stream output, uint version, ByteOrder endian)
		{
			_Output = output;
			Version = version;
			Endian = endian;
		}

		public uint Version { get; private set; }

		public SerializeMode Mode
		{
			get { return SerializeMode.Writing; }
		}

		public void Serialize(ref bool value)
		{
			_Output.WriteBooleanInt(value, Endian);
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
		}

		public void Serialize(ref byte value)
		{
			_Output.WriteByte(value);
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
		}

		public void Serialize(ref int value)
		{
			_Output.WriteInt32(value, Endian);
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
		}

		public void Serialize(ref uint value)
		{
			_Output.WriteUInt32(value, Endian);
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
		}

		public void Serialize(ref float value)
		{
			_Output.WriteSingle(value, Endian);
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
		}

		public void Serialize(ref string value)
		{
			WriteString(value);
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
		}

		public void Serialize(ref Guid value)
		{
			_Output.WriteGuid(value, Endian);
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
		}

		public void SerializeEnum<TEnum>(ref TEnum value)
		{
			_Output.WriteEnum<TEnum>(value, Endian);
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
		}

		public void Serialize<TType>(ref TType value)
			where TType : class, ISerializable, new()
		{
			if (value == null)
			{
				throw new ArgumentException("value cannot be null", nameof(value));
			}

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
		}

		public void Serialize(ref BitArray list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list));
			}

			var count = ((uint) list.Count + 31) / 32;
			_Output.WriteUInt32(count, Endian);

			for (uint i = 0; i < count; i++)
			{
				var offset = i * 32;
				var bits = 0;

				for (var bit = 0; bit < 32 && offset + bit < list.Count; bit++)
				{
					bits |= (list.Get((int) (offset + bit)) ? 1 : 0) << bit;
				}

				_Output.WriteInt32(bits, Endian);
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
		}

		public void Serialize(ref List<byte> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w._Output.WriteByte(v));
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
		}

		public void Serialize(ref List<int> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w._Output.WriteInt32(v, w.Endian));
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
		}

		public void Serialize(ref List<float> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w._Output.WriteSingle(v, w.Endian));
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
		}

		public void Serialize(ref List<string> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w.WriteString(v));
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
		}

		public void Serialize(ref List<Guid> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w._Output.WriteGuid(v, w.Endian));
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
		}

		public void SerializeEnum<TEnum>(ref List<TEnum> list)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			WriteBasicList(list, (w, v) => w._Output.WriteEnum<TEnum>(v, w.Endian));
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
		}

		public void Serialize<TType>(ref List<TType> list)
			where TType : class, ISerializable, new()
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			_Output.WriteInt32(list.Count, Endian);
			foreach (var item in list)
			{
				if (item == null)
				{
					throw new ArgumentException("items in list cannot be null", nameof(list));
				}
				item.Serialize(this);
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
		}

		public void Serialize<TType>(ref BindingList<TType> list) where TType : class, ISerializable, new()
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "serializable list should not be null");
			}

			_Output.WriteInt32(list.Count, Endian);
			foreach (var item in list)
			{
				if (item == null)
				{
					throw new ArgumentException("items in list cannot be null", nameof(list));
				}
				item.Serialize(this);
			}
		}

		public void Serialize<TType>(ref BindingList<TType> list, Func<ISerializer, bool> condition,
			Func<BindingList<TType>> defaultList) where TType : class, ISerializable, new()
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
		}

		private void WriteString(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				_Output.WriteInt32(0, Endian);
				return;
			}

			// detect unicode
			if (value.Any(c => c > 0xFF))
			{
				_Output.WriteInt32(-(value.Length + 1), Endian);
				_Output.WriteString(value,
					Endian == ByteOrder.LittleEndian
						? Encoding.Unicode
						: Encoding.BigEndianUnicode);
				_Output.WriteUInt16(0, Endian);
			}
			else
			{
				var bytes = new byte[value.Length];
				for (var i = 0; i < value.Length; i++)
				{
					bytes[i] = (byte) value[i];
				}

				_Output.WriteInt32(bytes.Length + 1, Endian);
				_Output.WriteBytes(bytes);
				_Output.WriteByte(0);
			}
		}

		private void WriteBasicList<TType>(List<TType> list, Action<FileWriter, TType> writeValue)
		{
			if (list == null)
			{
				throw new ArgumentNullException(nameof(list), "list should not be null");
			}

			_Output.WriteInt32(list.Count, Endian);
			foreach (var item in list)
			{
				writeValue(this, item);
			}
		}
	}
}