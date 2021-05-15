namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public abstract class BioStateEventElement : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		/// <param name="instanceVersion"></param>
		protected BioStateEventElement(int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion) {}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		protected BioStateEventElement(BioVersionedNativeObject other)
			: base(other) {}

		/// <summary>
		/// </summary>
		public abstract BioStateEventElementType ElementType { get; }
	}
}
