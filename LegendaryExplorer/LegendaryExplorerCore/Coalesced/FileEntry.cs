using System.Collections.Generic;

namespace LegendaryExplorerCore.Coalesced
{
	public class FileEntry
	{
		public FileEntry(string name = "")
		{
			Name = name ?? "";
			Sections = new Dictionary<string, Dictionary<string, List<PropertyValue>>>();
		}

		public string Name { get; set; }

		public Dictionary<string, Dictionary<string, List<PropertyValue>>> Sections { get; set; }
	}

	public struct PropertyValue
	{
		public PropertyValue(int type, string value)
			: this()
		{
			Type = type;
			Value = value;
		}

		public int Type { get; set; }

		public string Value { get; set; }
	}
}