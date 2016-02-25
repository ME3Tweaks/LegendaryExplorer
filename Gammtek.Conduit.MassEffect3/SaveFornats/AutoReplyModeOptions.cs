namespace MassEffect3.SaveFormats
{
	[OriginalName("EAutoReplyModeOptions")]
	public enum AutoReplyModeOptions : byte
	{
		[OriginalName("ARMO_All_Decisions")]
		//[DisplayName("All Decisions")]
		AllDecisions = 0,

		[OriginalName("ARMO_Major_Decisions")]
		//[DisplayName("Major Decisions")]
		MajorDecisions = 1,

		[OriginalName("ARMO_No_Decisions")]
		//[DisplayName("No Decisions")]
		NoDecisions = 2,
	}
}