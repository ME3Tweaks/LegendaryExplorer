using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	public class BioQuestProgress : BioVersionedNativeObject
	{
		private int _activeGoal;
		private int _questAdded;
		private bool _questUpdated;
		private IList<int> _taskHistory;

		public BioQuestProgress(bool questUpdated = false, int questAdded = 0, int activeGoal = 0, IList<int> taskHistory = null,
			int instanceVersion = 0)
			: base(instanceVersion)
		{
			ActiveGoal = activeGoal;
			QuestAdded = questAdded;
			QuestUpdated = questUpdated;
			TaskHistory = taskHistory ?? new List<int>();
		}

		public int ActiveGoal
		{
			get { return _activeGoal; }
			set { SetProperty(ref _activeGoal, value); }
		}

		public int QuestAdded
		{
			get { return _questAdded; }
			set { SetProperty(ref _questAdded, value); }
		}

		public bool QuestUpdated
		{
			get { return _questUpdated; }
			set { SetProperty(ref _questUpdated, value); }
		}

		public IList<int> TaskHistory
		{
			get { return _taskHistory; }
			set { SetProperty(ref _taskHistory, value); }
		}
	}
}
