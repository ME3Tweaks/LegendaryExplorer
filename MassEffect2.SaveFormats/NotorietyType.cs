namespace MassEffect2.SaveFormats
{
	[OriginalName("ENotorietyType")]
	public enum NotorietyType : byte
	{
		[OriginalName("NotorietyType_None")]
		None = 0,

		[OriginalName("NotorietyType_Survivor")]
		Survivor = 1,

		[OriginalName("NotorietyType_Warhero")]
		Warhero = 2,

		[OriginalName("NotorietyType_Ruthless")]
		Ruthless = 3,
	}
}