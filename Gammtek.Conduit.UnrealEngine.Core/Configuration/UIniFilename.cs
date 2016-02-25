namespace Gammtek.Conduit.UnrealEngine.Configuration
{
	public struct UIniFilename
	{
		public UIniFilename(string filename, bool isRequired)
			: this()
		{
			Filename = filename;
			IsRequired = isRequired;
		}

		public string Filename { get; set; }

		public bool IsRequired { get; set; }
	}
}
