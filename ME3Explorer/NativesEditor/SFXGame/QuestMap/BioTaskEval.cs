namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioTaskEval : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public const int DefaultConditional = -1;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 2;

		/// <summary>
		/// </summary>
		public const int DefaultQuest = -1;

		/// <summary>
		/// </summary>
		public const int DefaultState = -1;

		/// <summary>
		/// </summary>
		public const int DefaultTask = 0;

		private int _conditional;
		private int _quest;
		private int _state;
		private int _task;

		/// <summary>
		/// </summary>
		/// <param name="quest"></param>
		/// <param name="conditional"></param>
		/// <param name="state"></param>
		/// <param name="task"></param>
		/// <param name="instanceVersion"></param>
		public BioTaskEval(int quest = DefaultQuest, int conditional = DefaultConditional, int state = DefaultState, int task = DefaultTask,
			int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Conditional = conditional;
			Quest = quest;
			State = state;
			Task = task;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioTaskEval(BioTaskEval other)
			: base(other)
		{
			Conditional = other.Conditional;
			Quest = other.Quest;
			State = other.State;
			Task = other.Task;
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
		public int Quest
		{
			get { return _quest; }
			set { SetProperty(ref _quest, value); }
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
		public int Task
		{
			get { return _task; }
			set { SetProperty(ref _task, value); }
		}
	}
}
