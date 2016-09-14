namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	public class BioStateEventElementLocalBool : BioStateEventElementLocal
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
		public const bool DefaultNewValue = false;

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

		private bool _newValue;

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
		public BioStateEventElementLocalBool(int functionName = DefaultFunctionName, int functionNameFlags = DefaultFunctionNameFlags,
			int objectType = DefaultObjectTag, bool newValue = DefaultNewValue, int objectTag = DefaultObjectTag, int objectTagFlags = DefaultObjectTagFlags,
			bool useParam = DefaultUseParam, int instanceVersion = DefaultInstanceVersion)
			: base(functionName, functionNameFlags, objectType, objectTag, objectTagFlags, useParam, instanceVersion)
		{
			NewValue = newValue;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementLocalBool(BioStateEventElementLocalBool other)
			: base(other)
		{
			NewValue = other.NewValue;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.LocalBool; }
		}

		/// <summary>
		/// </summary>
		public bool NewValue
		{
			get { return _newValue; }
			set { SetProperty(ref _newValue, value); }
		}
	}
}
