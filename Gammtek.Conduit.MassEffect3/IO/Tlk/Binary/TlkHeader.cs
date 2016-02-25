namespace Gammtek.Conduit.IO.Tlk.Binary
{
	public struct TlkHeader
	{
		public const int ValidFileId = 0x006B6C54;

		/// <summary>
		///     Initializes a new instance of the <see cref="TlkHeader" /> struct.
		/// </summary>
		public TlkHeader(int magic = ValidFileId, int version = 3, int minVersion = 2, int entry1Count = 0, int entry2Count = 0,
			int treeNodeCount = 0, int dataLength = 0)
			: this()
		{
			Magic = magic;
			Version = version;
			MinVersion = minVersion;
			Entry1Count = entry1Count;
			Entry2Count = entry2Count;
			TreeNodeCount = treeNodeCount;
			DataLength = dataLength;
		}

		public int Magic { get; set; }

		public int Version { get; set; }

		public int MinVersion { get; set; }

		public int Entry1Count { get; set; }

		public int Entry2Count { get; set; }

		public int TreeNodeCount { get; set; }

		public int DataLength { get; set; }
	}
}
