namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioQuestPlotItem : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public const int DefaultConditional = -1;

		/// <summary>
		/// </summary>
		public const int DefaultIconIndex = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultState = -1;

		/// <summary>
		/// </summary>
		public const int DefaultTargetItems = 0;

		private int _conditional;
		private int _iconIndex;
		private int _name;
		private int _state;
		private int _targetItems;

		/// <summary>
		/// </summary>
		/// <param name="name"></param>
		/// <param name="conditional"></param>
		/// <param name="state"></param>
		/// <param name="iconIndex"></param>
		/// <param name="targetItems"></param>
		/// <param name="instanceVersion"></param>
		public BioQuestPlotItem(int name = DefaultName, int conditional = DefaultConditional, int state = DefaultState, int iconIndex = DefaultIconIndex,
			int targetItems = DefaultTargetItems, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Conditional = conditional;
			IconIndex = iconIndex;
			Name = name;
			State = state;
			TargetItems = targetItems;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioQuestPlotItem(BioQuestPlotItem other)
			: base(other)
		{
			Conditional = other.Conditional;
			IconIndex = other.IconIndex;
			Name = other.Name;
			State = other.State;
			TargetItems = other.TargetItems;
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
		public int IconIndex
		{
			get { return _iconIndex; }
			set { SetProperty(ref _iconIndex, value); }
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

		/// <summary>
		/// </summary>
		public int TargetItems
		{
			get { return _targetItems; }
			set { SetProperty(ref _targetItems, value); }
		}
	}
}
