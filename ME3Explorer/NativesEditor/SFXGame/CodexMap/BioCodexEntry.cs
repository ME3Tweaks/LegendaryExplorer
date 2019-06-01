namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	/// <summary>
	/// </summary>
	public abstract class BioCodexEntry : BioVersionedNativeObject
	{
        /// <summary>
        /// </summary>
        public const int DefaultCodexSound = 0;

        /// <summary>
        /// </summary>
        public const string DefaultCodexSoundString = "";

        /// <summary>
        /// </summary>
        public const int DefaultDescription = -1;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultPriority = 0;

		/// <summary>
		/// </summary>
		public const int DefaultTextureIndex = 0;

		/// <summary>
		/// </summary>
		public const int DefaultTitle = -1;

        private string _codexSoundString;
        private int _codexSound;
        private int _description;
		private int _priority;
		private int _textureIndex;
		private int _title;

		/// <summary>
		/// </summary>
		/// <param name="title"></param>
		/// <param name="description"></param>
		/// <param name="textureIndex"></param>
		/// <param name="priority"></param>
		/// <param name="codexSound"></param>
		/// <param name="instanceVersion"></param>
		protected BioCodexEntry(int title = DefaultTitle, int description = DefaultDescription, int textureIndex = DefaultTextureIndex,
			int priority = DefaultPriority, int codexSound = DefaultCodexSound, string codexSoundString = DefaultCodexSoundString, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Description = description;
			Priority = priority;
			TextureIndex = textureIndex;
			Title = title;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		protected BioCodexEntry(BioCodexEntry other)
			: base(other)
		{
			Description = other.Description;
			Priority = other.Priority;
			TextureIndex = other.TextureIndex;
			Title = other.Title;
		}

        /// <summary>
        /// </summary>
        public int CodexSound
        {
            get => _codexSound;
            set => SetProperty(ref _codexSound, value);
        }

        /// <summary>
        /// </summary>
        public string CodexSoundString
        {
            get => _codexSoundString;
            set => SetProperty(ref _codexSoundString, value);
        }

        /// <summary>
        /// </summary>
        public int Description
		{
			get => _description;
		    set => SetProperty(ref _description, value);
		}

		/// <summary>
		/// </summary>
		public int Priority
		{
			get => _priority;
		    set => SetProperty(ref _priority, value);
		}

		/// <summary>
		/// </summary>
		public int TextureIndex
		{
			get => _textureIndex;
		    set => SetProperty(ref _textureIndex, value);
		}

		/// <summary>
		/// </summary>
		public int Title
		{
			get => _title;
		    set => SetProperty(ref _title, value);
		}
	}
}
