using System.Collections.Generic;
using ME3ExplorerCore.Gammtek.ComponentModel;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioQuestMap : BindableBase
	{
		private IDictionary<int, BioStateTaskList> _boolTaskEvals;
		private IDictionary<int, BioStateTaskList> _floatTaskEvals;
		private IDictionary<int, BioStateTaskList> _intTaskEvals;
		private IDictionary<int, BioQuest> _quests;

		/// <summary>
		/// </summary>
		/// <param name="quests"></param>
		/// <param name="taskEvals"></param>
		/// <param name="intTaskEvals"></param>
		/// <param name="floatTaskEvals"></param>
		public BioQuestMap(IDictionary<int, BioQuest> quests = null,
			IDictionary<int, BioStateTaskList> taskEvals = null,
			IDictionary<int, BioStateTaskList> intTaskEvals = null,
			IDictionary<int, BioStateTaskList> floatTaskEvals = null)
		{
			BoolTaskEvals = taskEvals ?? new Dictionary<int, BioStateTaskList>();
			FloatTaskEvals = floatTaskEvals ?? new Dictionary<int, BioStateTaskList>();
			IntTaskEvals = intTaskEvals ?? new Dictionary<int, BioStateTaskList>();
			Quests = quests ?? new Dictionary<int, BioQuest>();
		}

		/// <summary>
		/// </summary>
		public IDictionary<int, BioStateTaskList> BoolTaskEvals
		{
			get { return _boolTaskEvals; }
			set { SetProperty(ref _boolTaskEvals, value); }
		}

		/// <summary>
		/// </summary>
		public IDictionary<int, BioStateTaskList> FloatTaskEvals
		{
			get { return _floatTaskEvals; }
			set { SetProperty(ref _floatTaskEvals, value); }
		}

		/// <summary>
		/// </summary>
		public IDictionary<int, BioStateTaskList> IntTaskEvals
		{
			get { return _intTaskEvals; }
			set { SetProperty(ref _intTaskEvals, value); }
		}

		/// <summary>
		/// </summary>
		public IDictionary<int, BioQuest> Quests
		{
			get { return _quests; }
			set { SetProperty(ref _quests, value); }
		}
	}
}
