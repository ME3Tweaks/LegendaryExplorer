namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementLocalInt : BioStateEventElementLocal
	{
		/// <summary>
		/// </summary>
		public new const int DefaultFunctionName = BioStateEventElementLocal.DefaultFunctionName;

		/// <summary>
		/// </summary>
		public new const int DefaultFunctionNameFlags = BioStateEventElementLocal.DefaultFunctionNameFlags;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioStateEventElementLocal.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultNewValue = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultObjectTag = BioStateEventElementLocal.DefaultObjectTag;

		/// <summary>
		/// </summary>
		public new const int DefaultObjectTagFlags = BioStateEventElementLocal.DefaultObjectTagFlags;

		/// <summary>
		/// </summary>
		public new const int DefaultObjectType = BioStateEventElementLocal.DefaultObjectType;

		/// <summary>
		/// </summary>
		public new const bool DefaultUseParam = BioStateEventElementLocal.DefaultUseParam;

		private int _newValue;

		/// <summary>
		/// </summary>
		/// <param name="functionName"></param>
		/// <param name="functionNameFlags"></param>
		/// <param name="objectType"></param>
		/// <param name="newValue"></param>
		/// <param name="objectTag"></param>
		/// <param name="objectTagFlags"></param>
		/// <param name="useParam"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementLocalInt(int functionName = DefaultFunctionName, int functionNameFlags = DefaultFunctionNameFlags,
			int objectType = DefaultObjectTag, int newValue = DefaultNewValue, int objectTag = DefaultObjectTag, int objectTagFlags = DefaultObjectTagFlags,
			bool useParam = DefaultUseParam, int instanceVersion = DefaultInstanceVersion)
			: base(functionName, functionNameFlags, objectType, objectTag, objectTagFlags, useParam, instanceVersion)
		{
			NewValue = newValue;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementLocalInt(BioStateEventElementLocalInt other)
			: base(other)
		{
			NewValue = other.NewValue;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.LocalInt; }
		}

		/// <summary>
		/// </summary>
		public int NewValue
		{
			get { return _newValue; }
			set { SetProperty(ref _newValue, value); }
		}
	}
}
