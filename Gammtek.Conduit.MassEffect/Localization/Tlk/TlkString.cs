using System;
using System.IO;

namespace Gammtek.Conduit.MassEffect.Localization.Tlk
{
	public struct TlkString : IComparable<TlkString>, IComparable
	{
		public TlkString(int id, string value = null)
			: this()
		{
			Id = id;
			Value = value;
		}

		public TlkString(BinaryReader reader)
			: this()
		{
			Id = reader.ReadInt32();
			BitOffset = reader.ReadInt32();
		}

		public int BitOffset { get; set; }
		public int Id { get; set; }

		public bool IsNull
		{
			get { return Id < 0 || Value == null; }
		}

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

			return CompareTo((TlkString) other);
		}

		public int CompareTo(TlkString other)
		{
			return (Id & Int32.MaxValue).CompareTo(other.Id & Int32.MaxValue);
		}
	}
}
