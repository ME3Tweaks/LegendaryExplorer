namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	/// <summary>
	/// </summary>
	public class BioCodexPage : BioCodexEntry
	{
		/// <summary>
		/// </summary>
		public new const int DefaultCodexSound = BioCodexEntry.DefaultCodexSound;

        /// <summary>
        /// </summary>
        public new const string DefaultCodexSoundString = BioCodexEntry.DefaultCodexSoundString;

        /// <summary>
        /// </summary>
        public new const int DefaultDescription = BioCodexEntry.DefaultDescription;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioCodexEntry.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public new const int DefaultPriority = BioCodexEntry.DefaultPriority;

		/// <summary>
		/// </summary>
		public const int DefaultSection = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultTextureIndex = BioCodexEntry.DefaultTextureIndex;

		/// <summary>
		/// </summary>
		public new const int DefaultTitle = BioCodexEntry.DefaultTitle;

		private int _section;

		public bool IsLE1 { get; set; } = false;

        /// <summary>
        /// </summary>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="section"></param>
        /// <param name="textureIndex"></param>
        /// <param name="priority"></param>
        /// <param name="codexSound">instance version 3/4 only</param>
        /// <param name="codexSoundString">instance version 2 only </param>
        /// <param name="instanceVersion"></param>
        public BioCodexPage(int title = DefaultTitle, int description = DefaultDescription, int section = DefaultSection,
			int textureIndex = DefaultTextureIndex, int priority = DefaultPriority, int codexSound = DefaultCodexSound, string codexSoundString = DefaultCodexSoundString,
            int instanceVersion = DefaultInstanceVersion)
			: base(title, description, textureIndex, priority, codexSound, codexSoundString, instanceVersion)
		{
			Section = section;
		}

		public BioCodexPage(BioCodexPage other)
			: base(other)
		{
			Section = other.Section;
		}

		/// <summary>
		/// </summary>
		public int Section
		{
			get { return _section; }
			set { SetProperty(ref _section, value); }
		}
	}
}
