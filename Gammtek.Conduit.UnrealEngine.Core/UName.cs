namespace Gammtek.Conduit.UnrealEngine
{
	public class UName
	{
		public const string None = "None";

		public UName(UNameEntry nameEntry)
		{
			NameEntry = nameEntry;
		}

		public int Index
		{
			get { return NameEntry.Index; }
		}

		public UNameEntry NameEntry { get; set; }

		public string Value
		{
			get { return NameEntry.Value; }
		}

		public static implicit operator UName(string value)
		{
			return new UName(value);
		}

		public static implicit operator string(UName value)
		{
			return value.Value;
		}

		public static implicit operator UName(UNameEntry value)
		{
			return new UName(value);
		}

		public static implicit operator UNameEntry(UName value)
		{
			return value.NameEntry;
		}
	}
}
