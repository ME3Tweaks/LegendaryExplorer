using System;
using System.IO;

namespace MassEffect3.TlkEditor
{
	public struct TlkString : IComparable<TlkString>, IComparable
	{
		public const string EmptyText = "-1";

		public TlkString(int id, string value = EmptyText, int position = 0)
			: this()
		{
			Id = id;
			Position = position;
			Value = value;
		}

		public TlkString(BinaryReader reader, int position = 0)
			: this()
		{
			Id = reader.ReadInt32();
			BitOffset = reader.ReadInt32();
			Position = position;
		}

		public int BitOffset { get; set; }

		public int Id { get; set; }

		public int Position { get; set; }

		public int StringStart { get; set; }

		public string Value { get; set; }

		public int CompareTo(object other)
		{
			if (other == null)
			{
				return 1;
			}

			if (!(other is TlkString))
			{
				throw new ArgumentException("Argument must be a TlkString.");
			}

			var entry = (TlkString) other;

			return Position.CompareTo(entry.Position);
		}

		public int CompareTo(TlkString other)
		{
			return Position.CompareTo(other.Position);
		}
	}
}