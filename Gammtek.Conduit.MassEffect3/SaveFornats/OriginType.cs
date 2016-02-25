namespace MassEffect3.SaveFormats
{
	[OriginalName("EOriginType")]
	public enum OriginType : byte
	{
		[OriginalName("OriginType_None")]
		None = 0,

		[OriginalName("OriginType_Spacer")]
		Spacer = 1,

		[OriginalName("OriginType_Colony")]
		Colony = 2,

		[OriginalName("OriginType_Earthborn")]
		Earthborn = 3,
	}
}