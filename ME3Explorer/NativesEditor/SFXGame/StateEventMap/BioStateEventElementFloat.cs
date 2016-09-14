namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementFloat : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultGlobalFloat = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultIncrement = false;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 0;

		/// <summary>
		/// </summary>
		public const float DefaultNewValue = 0f;

		/// <summary>
		/// </summary>
		public const bool DefaultUseParam = false;

		private int _globalFloat;
		private bool _increment;
		private float _newValue;
		private bool _useParam;

		/// <summary>
		/// </summary>
		/// <param name="globalFloat"></param>
		/// <param name="newValue"></param>
		/// <param name="increment"></param>
		/// <param name="useParam"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementFloat(int globalFloat = DefaultGlobalFloat, float newValue = DefaultNewValue, bool increment = DefaultIncrement,
			bool useParam = DefaultUseParam, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			GlobalFloat = globalFloat;
			Increment = increment;
			NewValue = newValue;
			UseParam = useParam;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementFloat(BioStateEventElementFloat other)
			: base(other)
		{
			GlobalFloat = other.GlobalFloat;
			Increment = other.Increment;
			NewValue = other.NewValue;
			UseParam = other.UseParam;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Float; }
		}

		/// <summary>
		/// </summary>
		public int GlobalFloat
		{
			get { return _globalFloat; }
			set { SetProperty(ref _globalFloat, value); }
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
		public float NewValue
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
