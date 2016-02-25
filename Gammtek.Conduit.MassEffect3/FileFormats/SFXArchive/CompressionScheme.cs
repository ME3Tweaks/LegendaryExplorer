namespace MassEffect3.FileFormats.SFXArchive
{
	public enum CompressionScheme : uint
	{
		None = 0x6E6F6E65u, // 'none'
		Lzma = 0x6C7A6D61u, // 'lzma'
		Lzx = 0x6C7A7820u, // 'lzx '
		Invalid = 0xFFFFFFFFu,
	}
}