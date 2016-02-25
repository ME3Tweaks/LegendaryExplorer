namespace MassEffect3.PackageDecompress
{
	internal struct CompressedBlockInfo
	{
		public uint CompressedOffset;
		public uint CompressedSize;
		public uint UncompressedOffset;
		public uint UncompressedSize;
	}
}