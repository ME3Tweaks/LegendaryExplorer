namespace MassEffect3.FileFormats.SFXArchive
{
	public class Entry
	{
		public int BlockSizeIndex;
		public FileNameHash NameHash;
		public long Offset;
		public long UncompressedSize;
	}
}