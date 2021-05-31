namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	public class BioStateEventElementLocalFloat : BioStateEventElementLocal
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
		public const float DefaultNewValue = 0f;

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

		private float _newValue;

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
		public BioStateEventElementLocalFloat(int functionName = DefaultFunctionName, int functionNameFlags = DefaultFunctionNameFlags,
			int objectType = DefaultObjectTag, float newValue = DefaultNewValue, int objectTag = DefaultObjectTag, int objectTagFlags = DefaultObjectTagFlags,
			bool useParam = DefaultUseParam, int instanceVersion = DefaultInstanceVersion)
			: base(functionName, functionNameFlags, objectType, objectTag, objectTagFlags, useParam, instanceVersion)
		{
			NewValue = newValue;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementLocalFloat(BioStateEventElementLocalFloat other)
			: base(other)
		{
			NewValue = other.NewValue;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.LocalFloat; }
		}

		/// <summary>
		/// </summary>
		public float NewValue
		{
			get { return _newValue; }
			set { SetProperty(ref _newValue, value); }
		}
	}
}
