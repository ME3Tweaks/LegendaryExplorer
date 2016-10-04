namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementBool : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultGlobalBool = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultNewState = false;

		/// <summary>
		/// </summary>
		public const bool DefaultUseParam = false;

		private int _globalBool;
		private bool _newState;
		private bool _useParam;

		/// <summary>
		/// </summary>
		/// <param name="globalBool"></param>
		/// <param name="newState"></param>
		/// <param name="useParam"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementBool(int globalBool = DefaultGlobalBool, bool newState = DefaultNewState, bool useParam = DefaultUseParam,
			int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			GlobalBool = globalBool;
			NewState = newState;
			UseParam = useParam;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementBool(BioStateEventElementBool other)
			: base(other)
		{
			GlobalBool = other.GlobalBool;
			NewState = other.NewState;
			UseParam = other.UseParam;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Bool; }
		}

		/// <summary>
		/// </summary>
		public int GlobalBool
		{
			get { return _globalBool; }
			set { SetProperty(ref _globalBool, value); }
		}

		/// <summary>
		/// </summary>
		public bool NewState
		{
			get { return _newState; }
			set { SetProperty(ref _newState, value); }
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
