using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using MassEffect.NativesEditor.Dialogs;
using ME3LibWV;

namespace MassEffect.NativesEditor.ViewModels
{
	public class QuestMapViewModel : PropertyChangedBase
	{
		private StateTaskListsViewModel _boolStateTaskListsViewModel;
		private StateTaskListsViewModel _floatStateTaskListsViewModel;
		private StateTaskListsViewModel _intStateTaskListsViewModel;
		private BindableCollection<KeyValuePair<int, BioQuest>> _quests;
		private KeyValuePair<int, BioQuest> _selectedQuest;
		private BioQuestGoal _selectedQuestGoal;
		private BioQuestPlotItem _selectedQuestPlotItem;
		private BioQuestTask _selectedQuestTask;

		public QuestMapViewModel()
			: this(null) {}

		public QuestMapViewModel(BioQuestMap questMap)
		{
			BoolStateTaskListsViewModel = new StateTaskListsViewModel();
			FloatStateTaskListsViewModel = new StateTaskListsViewModel();
			IntStateTaskListsViewModel = new StateTaskListsViewModel();

			SetFromQuestMap(questMap ?? new BioQuestMap());
		}

		public StateTaskListsViewModel BoolStateTaskListsViewModel
		{
			get { return _boolStateTaskListsViewModel; }
			set
			{
				if (Equals(value, _boolStateTaskListsViewModel))
				{
					return;
				}

				_boolStateTaskListsViewModel = value;

				NotifyOfPropertyChange(() => BoolStateTaskListsViewModel);
			}
		}

		public bool CanAddQuestGoal
		{
			get
			{
				if (Quests == null || !Quests.Any())
				{
					return false;
				}

				return SelectedQuest.Value != null;
			}
		}

		public bool CanAddQuestPlotItem
		{
			get
			{
				if (Quests == null || !Quests.Any())
				{
					return false;
				}

				return SelectedQuest.Value != null;
			}
		}

		public bool CanAddQuestTask
		{
			get
			{
				if (Quests == null || !Quests.Any())
				{
					return false;
				}

				return SelectedQuest.Value != null;
			}
		}

		public bool CanRemoveQuest
		{
			get
			{
				if (Quests == null || !Quests.Any())
				{
					return false;
				}

				return SelectedQuest.Value != null;
			}
		}

		public bool CanRemoveQuestGoal
		{
			get
			{
				if (SelectedQuest.Value == null
					|| SelectedQuest.Value.Goals == null
					|| !SelectedQuest.Value.Goals.Any())
				{
					return false;
				}

				return SelectedQuestGoal != null;
			}
		}

		public bool CanRemoveQuestPlotItem
		{
			get
			{
				if (SelectedQuest.Value == null
					|| SelectedQuest.Value.PlotItems == null
					|| !SelectedQuest.Value.PlotItems.Any())
				{
					return false;
				}

				return SelectedQuestPlotItem != null;
			}
		}

		public bool CanRemoveQuestTask
		{
			get
			{
				if (SelectedQuest.Value == null
					|| SelectedQuest.Value.Tasks == null
					|| !SelectedQuest.Value.Tasks.Any())
				{
					return false;
				}

				return SelectedQuestTask != null;
			}
		}

		public StateTaskListsViewModel FloatStateTaskListsViewModel
		{
			get { return _floatStateTaskListsViewModel; }
			set
			{
				if (Equals(value, _floatStateTaskListsViewModel))
				{
					return;
				}

				_floatStateTaskListsViewModel = value;

				NotifyOfPropertyChange(() => FloatStateTaskListsViewModel);
			}
		}

		public StateTaskListsViewModel IntStateTaskListsViewModel
		{
			get { return _intStateTaskListsViewModel; }
			set
			{
				if (Equals(value, _intStateTaskListsViewModel))
				{
					return;
				}

				_intStateTaskListsViewModel = value;

				NotifyOfPropertyChange(() => IntStateTaskListsViewModel);
			}
		}

		public BindableCollection<KeyValuePair<int, BioQuest>> Quests
		{
			get { return _quests; }
			set
			{
				if (Equals(value, _quests))
				{
					return;
				}

				_quests = value;

				NotifyOfPropertyChange(() => Quests);
				NotifyOfPropertyChange(() => CanAddQuestGoal);
				NotifyOfPropertyChange(() => CanAddQuestPlotItem);
				NotifyOfPropertyChange(() => CanAddQuestTask);
				NotifyOfPropertyChange(() => CanRemoveQuest);
			}
		}

		public KeyValuePair<int, BioQuest> SelectedQuest
		{
			get { return _selectedQuest; }
			set
			{
				if (value.Equals(_selectedQuest))
				{
					return;
				}

				_selectedQuest = value;

				NotifyOfPropertyChange(() => SelectedQuest);
				NotifyOfPropertyChange(() => CanAddQuestGoal);
				NotifyOfPropertyChange(() => CanAddQuestPlotItem);
				NotifyOfPropertyChange(() => CanAddQuestTask);
				NotifyOfPropertyChange(() => CanRemoveQuest);
				NotifyOfPropertyChange(() => CanRemoveQuestGoal);
				NotifyOfPropertyChange(() => CanRemoveQuestPlotItem);
				NotifyOfPropertyChange(() => CanRemoveQuestTask);
			}
		}

		public BioQuestGoal SelectedQuestGoal
		{
			get { return _selectedQuestGoal; }
			set
			{
				if (Equals(value, _selectedQuestGoal))
				{
					return;
				}

				_selectedQuestGoal = value;

				NotifyOfPropertyChange(() => SelectedQuestGoal);
				NotifyOfPropertyChange(() => CanRemoveQuestGoal);
			}
		}

		public BioQuestPlotItem SelectedQuestPlotItem
		{
			get { return _selectedQuestPlotItem; }
			set
			{
				if (Equals(value, _selectedQuestPlotItem))
				{
					return;
				}

				_selectedQuestPlotItem = value;

				NotifyOfPropertyChange(() => SelectedQuestPlotItem);
				NotifyOfPropertyChange(() => CanRemoveQuestPlotItem);
			}
		}

		public BioQuestTask SelectedQuestTask
		{
			get { return _selectedQuestTask; }
			set
			{
				if (Equals(value, _selectedQuestTask))
				{
					return;
				}

				_selectedQuestTask = value;

				NotifyOfPropertyChange(() => SelectedQuestTask);
				NotifyOfPropertyChange(() => CanRemoveQuestTask);
			}
		}

		public void AddQuest()
		{
			if (Quests == null)
			{
				return;
			}

			var dlg = new NewObjectDialog
			{
				ContentText = "New codex section",
				ObjectId = GetMaxQuestId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			AddQuest(dlg.ObjectId);
		}

		public void AddQuest(int id, BioQuest quest = null)
		{
			if (Quests == null)
			{
				Quests = InitCollection<KeyValuePair<int, BioQuest>>();
			}

			if (Quests.Any(pair => pair.Key == id))
			{
				return;
			}

			if (quest == null)
			{
				quest = new BioQuest();
			}

			quest.Goals = InitCollection(quest.Goals);
			quest.PlotItems = InitCollection(quest.PlotItems);
			quest.Tasks = InitCollection(quest.Tasks);

			var questPair = new KeyValuePair<int, BioQuest>(id, quest);

			Quests.Add(questPair);

			SelectedQuest = questPair;
		}

		public void AddQuestGoal()
		{
			AddQuestGoal(null);
		}

		public void AddQuestGoal(BioQuestGoal questGoal)
		{
			if (Quests == null || SelectedQuest.Value == null)
			{
				return;
			}

			if (SelectedQuest.Value.Goals == null)
			{
				SelectedQuest.Value.Goals = InitCollection<BioQuestGoal>();
			}

			if (questGoal == null)
			{
				questGoal = new BioQuestGoal();
			}

			SelectedQuest.Value.Goals.Add(questGoal);

			SelectedQuestGoal = questGoal;
		}

		public void AddQuestPlotItem()
		{
			AddQuestPlotItem(null);
		}

		public void AddQuestPlotItem(BioQuestPlotItem questPlotItem)
		{
			if (Quests == null || SelectedQuest.Value == null)
			{
				return;
			}

			if (SelectedQuest.Value.PlotItems == null)
			{
				SelectedQuest.Value.PlotItems = InitCollection<BioQuestPlotItem>();
			}

			if (questPlotItem == null)
			{
				questPlotItem = new BioQuestPlotItem();
			}

			SelectedQuest.Value.PlotItems.Add(questPlotItem);

			SelectedQuestPlotItem = questPlotItem;
		}

		public void AddQuestTask()
		{
			AddQuestTask(null);
		}

		public void AddQuestTask(BioQuestTask questTask)
		{
			if (Quests == null || SelectedQuest.Value == null)
			{
				return;
			}

			if (SelectedQuest.Value.Tasks == null)
			{
				SelectedQuest.Value.Tasks = InitCollection<BioQuestTask>();
			}

			if (questTask == null)
			{
				questTask = new BioQuestTask();
			}

			questTask.PlotItemIndices = questTask.PlotItemIndices != null 
				? InitCollection(questTask.PlotItemIndices) 
				: InitCollection<int>();

			SelectedQuest.Value.Tasks.Add(questTask);

			SelectedQuestTask = questTask;
		}

		public void AddQuestTaskPlotItemIndex()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuestTask == null)
			{
				return;
			}

			if (SelectedQuestTask.PlotItemIndices == null)
			{
				SelectedQuestTask.PlotItemIndices = InitCollection<int>();
			}

			var dlg = new NewObjectDialog
			{
				ContentText = "New quest task plot item index",
				ObjectId = SelectedQuestTask.PlotItemIndices.Any() ? SelectedQuestTask.PlotItemIndices.Max(i => i) + 1 : 0
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			SelectedQuestTask.PlotItemIndices.Add(dlg.ObjectId);
		}

		public void CopyQuest()
		{
			if (Quests == null || SelectedQuest.Value == null)
			{
				return;
			}

			var dlg = new CopyObjectDialog
			{
				ContentText = string.Format("Copy quest #{0}", SelectedQuest.Key),
				ObjectId = GetMaxQuestId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedQuest.Key == dlg.ObjectId)
			{
				return;
			}

			AddQuest(dlg.ObjectId, new BioQuest(SelectedQuest.Value));
		}

		public void CopyQuestGoal()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.Goals == null || SelectedQuestGoal == null)
			{
				return;
			}

			AddQuestGoal(new BioQuestGoal(SelectedQuestGoal));
		}

		public void CopyQuestPlotItem()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.PlotItems == null || SelectedQuestPlotItem == null)
			{
				return;
			}

			AddQuestPlotItem(new BioQuestPlotItem(SelectedQuestPlotItem));
		}

		public void CopyQuestTask()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.Tasks == null || SelectedQuestTask == null)
			{
				return;
			}

			AddQuestTask(new BioQuestTask(SelectedQuestTask));
		}

		public bool TryFindQuestMap(PCCPackage pcc, out int exportIndex, out int dataOffset)
		{
			var index = pcc.FindClass("BioQuestMap");

			exportIndex = -1;
			dataOffset = -1;

			if (index == 0)
			{
				return false;
			}

			exportIndex = pcc.Exports.FindIndex(entry => entry.idxClass == index);

			if (exportIndex < 0)
			{
				return false;
			}

			var mapData = pcc.Exports[exportIndex].Data;
			var mapProperties = PropertyReader.getPropList(pcc, mapData);

			if (mapProperties.Count <= 0)
			{
				return false;
			}

			var mapProperty = mapProperties.Find(property => property.TypeVal == PropertyReader.Type.None);
			dataOffset = mapProperty.offend;

			return true;
		}

		public void Open(PCCPackage pcc)
		{
			int exportIndex;
			int dataOffset;

			if (!TryFindQuestMap(pcc, out exportIndex, out dataOffset))
			{
				return;
			}

			using (var stream = new MemoryStream(pcc.Exports[exportIndex].Data))
			{
				stream.Seek(dataOffset, SeekOrigin.Begin);

				var questMap = BinaryBioQuestMap.Load(stream);

				SetFromQuestMap(questMap);
			}
		}

		[NotNull]
		public BioQuestMap ToQuestMap()
		{
			var questMap = new BioQuestMap
			{
				BoolTaskEvals = BoolStateTaskListsViewModel.StateTaskLists.ToDictionary(pair => pair.Key, pair => pair.Value),
				FloatTaskEvals = FloatStateTaskListsViewModel.StateTaskLists.ToDictionary(pair => pair.Key, pair => pair.Value),
				IntTaskEvals = IntStateTaskListsViewModel.StateTaskLists.ToDictionary(pair => pair.Key, pair => pair.Value),
				Quests = Quests.ToDictionary(pair => pair.Key, pair => pair.Value)
			};

			return questMap;
		}

		public void RemoveQuest()
		{
			if (Quests == null || SelectedQuest.Value == null)
			{
				return;
			}

			var index = Quests.IndexOf(SelectedQuest);

			if (!Quests.Remove(SelectedQuest))
			{
				return;
			}

			if (Quests.Any())
			{
				SelectedQuest = ((index - 1) >= 0)
					? Quests[index - 1]
					: Quests.First();
			}
		}

		public void RemoveQuestGoal()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.PlotItems == null || SelectedQuestGoal == null)
			{
				return;
			}

			var index = SelectedQuest.Value.Goals.IndexOf(SelectedQuestGoal);

			if (!SelectedQuest.Value.Goals.Remove(SelectedQuestGoal))
			{
				return;
			}

			if (SelectedQuest.Value.Goals.Any())
			{
				SelectedQuestGoal = ((index - 1) >= 0)
					? SelectedQuest.Value.Goals[index - 1]
					: SelectedQuest.Value.Goals.First();
			}
		}

		public void RemoveQuestPlotItem()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.PlotItems == null || SelectedQuestPlotItem == null)
			{
				return;
			}

			var index = SelectedQuest.Value.PlotItems.IndexOf(SelectedQuestPlotItem);

			if (!SelectedQuest.Value.PlotItems.Remove(SelectedQuestPlotItem))
			{
				return;
			}

			if (SelectedQuest.Value.PlotItems.Any())
			{
				SelectedQuestPlotItem = ((index - 1) >= 0)
					? SelectedQuest.Value.PlotItems[index - 1]
					: SelectedQuest.Value.PlotItems.First();
			}
		}

		public void RemoveQuestTask()
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuest.Value.Tasks == null || SelectedQuestTask == null)
			{
				return;
			}

			var index = SelectedQuest.Value.Tasks.IndexOf(SelectedQuestTask);

			if (!SelectedQuest.Value.Tasks.Remove(SelectedQuestTask))
			{
				return;
			}

			if (SelectedQuest.Value.Goals.Any())
			{
				SelectedQuestTask = ((index - 1) >= 0)
					? SelectedQuest.Value.Tasks[index - 1]
					: SelectedQuest.Value.Tasks.First();
			}
		}

		public void RemoveQuestTaskPlotItemIndex(int index)
		{
			if (Quests == null || SelectedQuest.Value == null || SelectedQuestTask == null)
			{
				return;
			}

			if (SelectedQuestTask.PlotItemIndices == null)
			{
				return;
			}

			if (index < 0 || index > SelectedQuestTask.PlotItemIndices.Count)
			{
				return;
			}

			SelectedQuestTask.PlotItemIndices.RemoveAt(index);
		}

		protected void SetFromQuestMap(BioQuestMap questMap)
		{
			if (questMap == null)
			{
				return;
			}

			BoolStateTaskListsViewModel.SetStateTaskLists(questMap.BoolTaskEvals.OrderBy(pair => pair.Key));
			FloatStateTaskListsViewModel.SetStateTaskLists(questMap.FloatTaskEvals.OrderBy(pair => pair.Key));
			IntStateTaskListsViewModel.SetStateTaskLists(questMap.IntTaskEvals.OrderBy(pair => pair.Key));
			Quests = InitCollection(questMap.Quests.OrderBy(quest => quest.Key));

			foreach (var quest in Quests)
			{
				quest.Value.Goals = InitCollection(quest.Value.Goals);
				quest.Value.PlotItems = InitCollection(quest.Value.PlotItems);
				quest.Value.Tasks = InitCollection(quest.Value.Tasks);

				foreach (var questTask in quest.Value.Tasks)
				{
					questTask.PlotItemIndices = InitCollection(questTask.PlotItemIndices);
				}
			}
		}

		[NotNull]
		private static BindableCollection<T> InitCollection<T>()
		{
			return new BindableCollection<T>();
		}

		[NotNull]
		private static BindableCollection<T> InitCollection<T>(IEnumerable<T> collection)
		{
			if (collection == null)
			{
				ThrowHelper.ThrowArgumentNullException("collection");
			}

			return new BindableCollection<T>(collection);
		}

		private int GetMaxQuestId()
		{
			return Quests.Any() ? Quests.Max(page => page.Key) : -1;
		}
	}
}
