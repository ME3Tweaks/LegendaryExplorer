namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementConsequence : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultConsequence = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 0;

		private int _consequence;

		/// <summary>
		/// </summary>
		/// <param name="consequence"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementConsequence(int consequence = DefaultConsequence, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Consequence = consequence;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementConsequence(BioStateEventElementConsequence other)
			: base(other)
		{
			Consequence = other.Consequence;
		}

		/// <summary>
		/// </summary>
		public int Consequence
		{
			get { return _consequence; }
			set { SetProperty(ref _consequence, value); }
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Consequence; }
		}
	}
}
