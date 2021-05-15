namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	/// <summary>
	/// </summary>
	public class BioCodexSection : BioCodexEntry
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
		public const bool DefaultIsPrimary = false;

		/// <summary>
		/// </summary>
		public new const int DefaultPriority = BioCodexEntry.DefaultPriority;

		/// <summary>
		/// </summary>
		public new const int DefaultTextureIndex = BioCodexEntry.DefaultTextureIndex;

		/// <summary>
		/// </summary>
		public new const int DefaultTitle = BioCodexEntry.DefaultTitle;

		private bool _isPrimary;

        /// <summary>
        /// </summary>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="isPrimary"></param>
        /// <param name="textureIndex"></param>
        /// <param name="priority"></param>
        /// <param name="codexSound">instance version 3/4</param>
        /// <param name="codexSoundString">instance version 2 only </param>
        /// <param name="instanceVersion"></param>
        public BioCodexSection(int title = DefaultTitle, int description = DefaultDescription, bool isPrimary = DefaultIsPrimary,
			int textureIndex = DefaultTextureIndex, int priority = DefaultPriority, int codexSound = DefaultCodexSound, string codexSoundString = DefaultCodexSoundString,
            int instanceVersion = DefaultInstanceVersion)
			: base(title, description, textureIndex, priority, codexSound, codexSoundString, instanceVersion)
		{
			IsPrimary = isPrimary;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioCodexSection(BioCodexSection other)
			: base(other)
		{
			IsPrimary = other.IsPrimary;
		}

		/// <summary>
		/// </summary>
		public bool IsPrimary
		{
			get { return _isPrimary; }
			set { SetProperty(ref _isPrimary, value); }
		}
	}
}
