namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementInt : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultGlobalInt = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultIncrement = false;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioStateEventElement.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultNewValue = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultUseParam = false;

		private int _globalInt;
		private bool _increment;
		private int _newValue;
		private bool _useParam;

		/// <summary>
		/// </summary>
		/// <param name="globalInt"></param>
		/// <param name="newValue"></param>
		/// <param name="increment"></param>
		/// <param name="useParam"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementInt(int globalInt = DefaultGlobalInt, int newValue = DefaultNewValue, bool increment = DefaultIncrement,
			bool useParam = DefaultUseParam, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			GlobalInt = globalInt;
			Increment = increment;
			NewValue = newValue;
			UseParam = useParam;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementInt(BioStateEventElementInt other)
			: base(other)
		{
			GlobalInt = other.GlobalInt;
			Increment = other.Increment;
			NewValue = other.NewValue;
			UseParam = other.UseParam;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Int; }
		}

		/// <summary>
		/// </summary>
		public int GlobalInt
		{
			get { return _globalInt; }
			set { SetProperty(ref _globalInt, value); }
		}

		/// <summary>
		/// </summary>
		public bool Increment
		{
			get { return _increment; }
			set { SetProperty(ref _increment, value); }
		}

		/// <summary>
		/// </summary>
		public int NewValue
		{
			get { return _newValue; }
			set { SetProperty(ref _newValue, value); }
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
