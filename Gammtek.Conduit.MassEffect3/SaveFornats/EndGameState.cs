namespace MassEffect3.SaveFormats
{
	[OriginalName("EEndGameState")]
	public enum EndGameState
	{
		[OriginalName("EGS_NotFinished")]
		//[DisplayName("Not Finished")]
		NotFinished = 0,

		[OriginalName("EGS_OutInABlazeOfGlory")]
		//[DisplayName("Out In A Blaze Of Glory")]
		OutInABlazeOfGlory = 1,

		[OriginalName("EGS_LivedToFightAgain")]
		//[DisplayName("Lived To Fight Again")]
		LivedToFightAgain = 2,
	}
}