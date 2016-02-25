namespace MassEffect3.SaveFormats
{
	[OriginalName("EDifficultyOptions")]
	public enum DifficultyOptions : byte
	{
		[OriginalName("DO_Level1")]
		Narritve = 0,

		[OriginalName("DO_Level2")]
		Casual = 1,

		[OriginalName("DO_Level3")]
		Normal = 2,

		[OriginalName("DO_Level4")]
		Hardcore = 3,

		[OriginalName("DO_Level5")]
		Insanity = 4,

		[OriginalName("DO_Level6")]
		//[DisplayName("What is beyond Insanity?")]
		//[Description("(it is a mystery)")]
		WhatIsBeyondInsanity = 5,
	}
}