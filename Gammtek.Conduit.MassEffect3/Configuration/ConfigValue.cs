using System;

namespace Gammtek.Conduit.MassEffect3.Configuration
{
	public struct ConfigValue : IEquatable<ConfigValue>
	{
		public ConfigValue(string value = null, ConfigParseAction parseAction = ConfigParseAction.None,
			StringComparison comparisonType = ConfigFile.DefaultStringComparison)
			: this()
		{
			ComparisonType = comparisonType;
			ParseAction = parseAction;
			Value = value;
		}

		public StringComparison ComparisonType { get; private set; }

		public ConfigParseAction ParseAction { get; set; }

		[CanBeNull]
		public string Value { get; set; }

		public static bool operator ==(ConfigValue value1, ConfigValue value2)
		{
			return value1.Equals(value2);
		}

		public static bool operator !=(ConfigValue value1, ConfigValue value2)
		{
			return !(value1 == value2);
		}

		public bool Equals(ConfigValue other)
		{
			return ParseAction == other.ParseAction && string.Equals(Value, other.Value);
		}

		public bool Equals(string value)
		{
			return string.Equals(Value, value, ComparisonType);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}
			return obj is ConfigValue && Equals((ConfigValue) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((int) ParseAction * 397) ^ (Value != null ? Value.GetHashCode() : 0);
			}
		}
	}
}
