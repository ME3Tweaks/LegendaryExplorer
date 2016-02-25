namespace Gammtek.Conduit.UnrealEngine
{
	public struct UNameEntry
	{
		public const int DefaultIndex = -1;
		public const string DefaultValue = null;

		public UNameEntry(int index = DefaultIndex, string value = DefaultValue)
			: this()
		{
			Value = value ?? UName.None;
			Index = index;
		}

		public int Index { get; set; }

		public string Value { get; set; }

		public static implicit operator UNameEntry(string value)
		{
			return new UNameEntry(DefaultIndex, value);
		}

		public static implicit operator string(UNameEntry value)
		{
			return value.Value;
		}
	}
}
