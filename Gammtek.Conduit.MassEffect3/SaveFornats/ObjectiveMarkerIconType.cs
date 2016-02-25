namespace MassEffect3.SaveFormats
{
	[OriginalName("EObjectiveMarkerIconType")]
	public enum ObjectiveMarkerIconType : byte
	{
		[OriginalName("EOMIT_None")]
		None = 0,

		[OriginalName("EOMIT_Attack")]
		Attack = 1,

		[OriginalName("EOMIT_Supply")]
		Supply = 2,

		[OriginalName("EOMIT_Alert")]
		Alert = 3,
	}
}