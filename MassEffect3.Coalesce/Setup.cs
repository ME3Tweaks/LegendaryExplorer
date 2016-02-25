using System.Collections.Generic;
using Gammtek.Conduit.IO;

namespace MassEffect3.Coalesce
{
	public class Setup
	{
		public Setup()
		{
			Endian = ByteOrder.LittleEndian;
			Files = new List<string>();
			Settings = new Dictionary<string, string>();
			Version = 0;
		}

		public ByteOrder Endian { get; set; }

		public List<string> Files { get; set; }

		public Dictionary<string, string> Settings { get; set; }

		public uint Version { get; set; }
	}
}