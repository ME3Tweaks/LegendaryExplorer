using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.Conditionals
{
	public class OldConditionalsFile
	{
		private readonly Dictionary<uint, byte[]> _Buffers
			= new Dictionary<uint, byte[]>();

		private readonly Dictionary<int, uint> _Conditionals
			= new Dictionary<int, uint>();

		public ByteOrder ByteOrder;
		public uint Version;

		public ReadOnlyCollection<int> Ids
		{
			get { return new ReadOnlyCollection<int>(_Conditionals.Keys.ToArray()); }
		}

		public void Serialize(Stream output)
		{
			throw new NotSupportedException();
		}

		public void Deserialize(Stream input)
		{
			var magic = input.ReadUInt32();

			if (magic != 0x434F4E44 && magic.Swap() != 0x434F4E44)
			{
				throw new FormatException();
			}

			var endian = magic == 0x434F4E44 ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

			var version = input.ReadUInt32(endian);

			if (version != 1)
			{
				throw new FormatException();
			}

			Version = version;

			var unknown08 = input.ReadUInt16(endian);
			var count = input.ReadUInt16(endian);

			var ids = new int[count];
			var offsets = new uint[count];

			for (ushort i = 0; i < count; i++)
			{
				ids[i] = input.ReadInt32(endian);
				offsets[i] = input.ReadUInt32(endian);
			}

			var sortedOffsets = offsets
				.OrderBy(o => o)
				.Distinct()
				.ToArray();

			_Buffers.Clear();

			for (var i = 0; i < sortedOffsets.Length; i++)
			{
				var offset = sortedOffsets[i];

				if (offset == 0)
				{
					continue;
				}

				var nextOffset = i + 1 < sortedOffsets.Length
					? sortedOffsets[i + 1]
					: input.Length;

				input.Seek(offset, SeekOrigin.Begin);

				var length = (int) (nextOffset - offset);

				var bytes = input.ReadBytes(length);

				_Buffers.Add(offset, bytes);
			}

			_Conditionals.Clear();

			for (var i = 0; i < count; i++)
			{
				_Conditionals.Add(ids[i], offsets[i]);
			}

			ByteOrder = endian;
		}

		public byte[] GetConditional(int id)
		{
			if (_Conditionals.ContainsKey(id) == false)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			var offset = _Conditionals[id];

			if (_Buffers.ContainsKey(offset) == false)
			{
				throw new InvalidOperationException();
			}

			return (byte[]) _Buffers[offset].Clone();
		}
	}
}