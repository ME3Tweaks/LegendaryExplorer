namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementFunction : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultFunctionClassName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionClassNameFlags = 0;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionNameFlags = 0;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionPackageName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultFunctionPackageNameFlags = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 0;

		/// <summary>
		/// </summary>
		public const int DefaultParameter = 0;

		private int _functionClassName;
		private int _functionClassNameFlags;
		private int _functionName;
		private int _functionNameFlags;
		private int _functionPackageName;
		private int _functionPackageNameFlags;
		private int _parameter;

		/// <summary>
		/// </summary>
		/// <param name="functionPackageName"></param>
		/// <param name="functionPackageNameFlags"></param>
		/// <param name="functionClassName"></param>
		/// <param name="functionClassNameFlags"></param>
		/// <param name="functionName"></param>
		/// <param name="functionNameFlags"></param>
		/// <param name="parameter"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementFunction(int functionPackageName = DefaultFunctionPackageName,
			int functionPackageNameFlags = DefaultFunctionPackageNameFlags, int functionClassName = DefaultFunctionClassName,
			int functionClassNameFlags = DefaultFunctionClassNameFlags, int functionName = DefaultFunctionName,
			int functionNameFlags = DefaultFunctionNameFlags, int parameter = DefaultParameter, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			FunctionClassName = functionClassName;
			FunctionClassNameFlags = functionClassNameFlags;

			FunctionName = functionName;
			FunctionNameFlags = functionNameFlags;

			FunctionPackageName = functionPackageName;
			FunctionPackageNameFlags = functionPackageNameFlags;

			Parameter = parameter;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementFunction(BioStateEventElementFunction other)
			: base(other)
		{
			FunctionClassName = other.FunctionClassName;
			FunctionClassNameFlags = other.FunctionClassNameFlags;

			FunctionName = other.FunctionName;
			FunctionNameFlags = other.FunctionNameFlags;

			FunctionPackageName = other.FunctionPackageName;
			FunctionPackageNameFlags = other.FunctionPackageNameFlags;

			Parameter = other.Parameter;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Function; }
		}

		/// <summary>
		/// </summary>
		public int FunctionClassName
		{
			get { return _functionClassName; }
			set { SetProperty(ref _functionClassName, value); }
		}

		/// <summary>
		/// </summary>
		public int FunctionClassNameFlags
		{
			get { return _functionClassNameFlags; }
			set { SetProperty(ref _functionClassNameFlags, value); }
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
		public int FunctionPackageName
		{
			get { return _functionPackageName; }
			set { SetProperty(ref _functionPackageName, value); }
		}

		/// <summary>
		/// </summary>
		public int FunctionPackageNameFlags
		{
			get { return _functionPackageNameFlags; }
			set { SetProperty(ref _functionPackageNameFlags, value); }
		}

		/// <summary>
		/// </summary>
		public int Parameter
		{
			get { return _parameter; }
			set { SetProperty(ref _parameter, value); }
		}
	}
}
