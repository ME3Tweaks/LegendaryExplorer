using System;

namespace Gammtek.Conduit.UnrealEngine3.Core
{
	public struct UBool
	{
		public UBool(bool value)
			: this()
		{
			BoolValue = value;
			IntValue = Convert.ToInt32(value);
		}

		public UBool(int value)
			: this()
		{
			BoolValue = Convert.ToBoolean(value);
			IntValue = value;
		}

		public bool BoolValue { get; set; }

		public int IntValue { get; set; }

		public static implicit operator UBool(bool value)
		{
			return new UBool(value);
		}

		public static implicit operator UBool(int value)
		{
			return new UBool(value);
		}

		public static implicit operator bool(UBool value)
		{
			return value.BoolValue;
		}

		public static implicit operator int(UBool value)
		{
			return value.IntValue;
		}
	}
}
