namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioQuestGoal : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public const int DefaultConditional = -1;

		/// <summary>
		/// </summary>
		public const int DefaultDescription = -1;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultState = -1;

		private int _conditional;
		private int _description;
		private int _name;
		private int _state;

		public BioQuestGoal(int name = DefaultName, int description = DefaultDescription, int conditional = DefaultConditional, int state = DefaultState,
			int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Conditional = conditional;
			Description = description;
			Name = name;
			State = state;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioQuestGoal(BioQuestGoal other)
			: base(other)
		{
			Conditional = other.Conditional;
			Description = other.Description;
			Name = other.Name;
			State = other.State;
		}

		/// <summary>
		/// </summary>
		public int Conditional
		{
			get { return _conditional; }
			set { SetProperty(ref _conditional, value); }
		}

		/// <summary>
		/// </summary>
		public int Description
		{
			get { return _description; }
			set { SetProperty(ref _description, value); }
		}

		/// <summary>
		/// </summary>
		public int Name
		{
			get { return _name; }
			set { SetProperty(ref _name, value); }
		}

		/// <summary>
		/// </summary>
		public int State
		{
			get { return _state; }
			set { SetProperty(ref _state, value); }
		}
	}
}
