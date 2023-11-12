using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioQuest : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const bool DefaultIsMission = false;

		private IList<BioQuestGoal> _goals;
		private bool _isMission;
		private IList<BioQuestPlotItem> _plotItems;
		private IList<BioQuestTask> _tasks;

		/// <summary>
		/// </summary>
		/// <param name="isMission"></param>
		/// <param name="goals"></param>
		/// <param name="tasks"></param>
		/// <param name="plotItems"></param>
		/// <param name="instanceVersion"></param>
		public BioQuest(bool isMission = DefaultIsMission, IList<BioQuestGoal> goals = null, IList<BioQuestTask> tasks = null,
			IList<BioQuestPlotItem> plotItems = null, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Goals = goals ?? new List<BioQuestGoal>();
			IsMission = isMission;
			PlotItems = plotItems ?? new List<BioQuestPlotItem>();
			Tasks = tasks ?? new List<BioQuestTask>();
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioQuest(BioQuest other)
			: base(other)
		{
			IsMission = other.IsMission;

			Goals = other.Goals != null ? other.Goals.Select(goal => new BioQuestGoal(goal)).ToList() : new List<BioQuestGoal>();
			PlotItems = other.PlotItems != null ? other.PlotItems.Select(item => new BioQuestPlotItem(item)).ToList() : new List<BioQuestPlotItem>();
			Tasks = other.Tasks != null ? other.Tasks.Select(task => new BioQuestTask(task)).ToList() : new List<BioQuestTask>();
		}

		/// <summary>
		/// </summary>
		public IList<BioQuestGoal> Goals
		{
			get { return _goals; }
			set { SetProperty(ref _goals, value); }
		}

		/// <summary>
		/// </summary>
		public bool IsMission
		{
			get { return _isMission; }
			set { SetProperty(ref _isMission, value); }
		}

		/// <summary>
		/// </summary>
		public IList<BioQuestPlotItem> PlotItems
		{
			get { return _plotItems; }
			set { SetProperty(ref _plotItems, value); }
		}

		/// <summary>
		/// </summary>
		public IList<BioQuestTask> Tasks
		{
			get { return _tasks; }
			set { SetProperty(ref _tasks, value); }
		}
		
		private string _questName;

		public string QuestName
		{
			get => _questName;
			set => SetProperty(ref _questName, value);
		}

		public int QuestNameTlkId => _goals.FirstOrDefault()?.Name ?? default;
		public override string ToString() { return "BioQuest"; }
	}
}
