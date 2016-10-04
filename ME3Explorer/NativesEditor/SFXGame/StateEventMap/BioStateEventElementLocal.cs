namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public abstract class BioStateEventElementLocal : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultFunctionName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionNameFlags = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioStateEventElement.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultObjectTag = -1;

		/// <summary>
		/// </summary>
		public const int DefaultObjectTagFlags = 0;

		/// <summary>
		/// </summary>
		public const int DefaultObjectType = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultUseParam = false;

		private int _functionName;
		private int _functionNameFlags;
		private int _objectTag;
		private int _objectTagFlags;
		private int _objectType;
		private bool _useParam;

		/// <summary>
		/// </summary>
		/// <param name="functionName"></param>
		/// <param name="functionNameFlags"></param>
		/// <param name="objectType"></param>
		/// <param name="objectTag"></param>
		/// <param name="objectTagFlags"></param>
		/// <param name="useParam"></param>
		/// <param name="instanceVersion"></param>
		protected BioStateEventElementLocal(int functionName = DefaultFunctionName, int functionNameFlags = DefaultFunctionNameFlags,
			int objectType = DefaultObjectType, int objectTag = DefaultObjectTag, int objectTagFlags = DefaultObjectTagFlags, bool useParam = DefaultUseParam,
			int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			FunctionName = functionName;
			FunctionNameFlags = functionNameFlags;
			ObjectTag = objectTag;
			ObjectTagFlags = objectTagFlags;
			ObjectType = objectType;
			UseParam = useParam;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		protected BioStateEventElementLocal(BioStateEventElementLocal other)
			: base(other)
		{
			FunctionName = other.FunctionName;
			FunctionNameFlags = other.FunctionNameFlags;
			ObjectTag = other.ObjectTag;
			ObjectTagFlags = other.ObjectTagFlags;
			ObjectType = other.ObjectType;
			UseParam = other.UseParam;
		}

		/// <summary>
		/// </summary>
		public int FunctionName
		{
			get { return _functionName; }
			set { SetProperty(ref _functionName, value); }
		}

		/// <summary>
		/// </summary>
		public int FunctionNameFlags
		{
			get { return _functionNameFlags; }
			set { SetProperty(ref _functionNameFlags, value); }
		}

		/// <summary>
		/// </summary>
		public int ObjectTag
		{
			get { return _objectTag; }
			set { SetProperty(ref _objectTag, value); }
		}

		/// <summary>
		/// </summary>
		public int ObjectTagFlags
		{
			get { return _objectTagFlags; }
			set { SetProperty(ref _objectTagFlags, value); }
		}

		/// <summary>
		/// </summary>
		public int ObjectType
		{
			get { return _objectType; }
			set { SetProperty(ref _objectType, value); }
		}

		/// <summary>
		/// </summary>
		public bool UseParam
		{
			get { return _useParam; }
			set { SetProperty(ref _useParam, value); }
		}
	}
}
