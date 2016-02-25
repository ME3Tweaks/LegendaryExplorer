using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using MassEffect.NativesEditor.Dialogs;

namespace MassEffect.NativesEditor.ViewModels
{
	public class StateTaskListsViewModel : PropertyChangedBase
	{
		private KeyValuePair<int, BioStateTaskList> _selectedStateTaskList;
		private BioTaskEval _selectedTaskEval;
		private BindableCollection<KeyValuePair<int, BioStateTaskList>> _stateTaskLists;

		public StateTaskListsViewModel()
			: this(null) {}

		public StateTaskListsViewModel(IEnumerable<KeyValuePair<int, BioStateTaskList>> collection)
		{
			SetStateTaskLists(collection);
		}

		public bool CanAddTaskEval
		{
			get
			{
				if (StateTaskLists == null
					|| !StateTaskLists.Any())
				{
					return false;
				}

				return SelectedStateTaskList.Value != null;
			}
		}

		public bool CanRemoveStateTaskList
		{
			get
			{
				if (StateTaskLists == null
					|| !StateTaskLists.Any())
				{
					return false;
				}

				return SelectedStateTaskList.Value != null;
			}
		}

		public bool CanRemoveTaskEval
		{
			get
			{
				if (StateTaskLists == null
					|| !StateTaskLists.Any())
				{
					return false;
				}

				if (SelectedStateTaskList.Value == null
					|| SelectedStateTaskList.Value.TaskEvals == null
					|| !SelectedStateTaskList.Value.TaskEvals.Any())
				{
					return false;
				}

				return SelectedTaskEval != null;
			}
		}

		public KeyValuePair<int, BioStateTaskList> SelectedStateTaskList
		{
			get { return _selectedStateTaskList; }
			set
			{
				if (value.Equals(_selectedStateTaskList))
				{
					return;
				}

				_selectedStateTaskList = value;

				NotifyOfPropertyChange(() => SelectedStateTaskList);
				NotifyOfPropertyChange(() => CanAddTaskEval);
				NotifyOfPropertyChange(() => CanRemoveStateTaskList);
				NotifyOfPropertyChange(() => CanRemoveTaskEval);
			}
		}

		public BioTaskEval SelectedTaskEval
		{
			get { return _selectedTaskEval; }
			set
			{
				if (Equals(value, _selectedTaskEval))
				{
					return;
				}

				_selectedTaskEval = value;

				NotifyOfPropertyChange(() => SelectedTaskEval);
				NotifyOfPropertyChange(() => CanRemoveTaskEval);
			}
		}

		public BindableCollection<KeyValuePair<int, BioStateTaskList>> StateTaskLists
		{
			get { return _stateTaskLists; }
			set
			{
				if (Equals(value, _stateTaskLists))
				{
					return;
				}

				_stateTaskLists = value;

				NotifyOfPropertyChange(() => StateTaskLists);
				NotifyOfPropertyChange(() => CanAddTaskEval);
				NotifyOfPropertyChange(() => CanRemoveStateTaskList);
				NotifyOfPropertyChange(() => CanRemoveTaskEval);
			}
		}

		public void AddStateTaskList()
		{
			if (StateTaskLists == null)
			{
				StateTaskLists = InitCollection<KeyValuePair<int, BioStateTaskList>>();
			}

			var dlg = new NewObjectDialog
			{
				ContentText =
					string.Format("New {0}", "StateTaskList"),
				ObjectId = (GetMaxStateTaskListId() + 1)
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			AddStateTaskList(dlg.ObjectId);
		}

		public void AddStateTaskList(int id, BioStateTaskList taskList = null)
		{
			if (StateTaskLists == null)
			{
				StateTaskLists = InitCollection<KeyValuePair<int, BioStateTaskList>>();
			}

			if (id < 0)
			{
				return;
			}

			if (taskList == null)
			{
				taskList = new BioStateTaskList();
			}

			taskList.TaskEvals = taskList.TaskEvals != null
				? InitCollection(taskList.TaskEvals)
				: InitCollection<BioTaskEval>();

			var stateTaskList = new KeyValuePair<int, BioStateTaskList>(id, taskList);

			StateTaskLists.Add(stateTaskList);

			SelectedStateTaskList = stateTaskList;
		}

		public void AddTaskEval()
		{
			AddTaskEval(null);
		}

		public void AddTaskEval(BioTaskEval taskEval)
		{
			if (StateTaskLists == null || SelectedStateTaskList.Value == null)
			{
				return;
			}

			if (taskEval == null)
			{
				taskEval = new BioTaskEval();
			}

			SelectedStateTaskList.Value.TaskEvals.Add(taskEval);

			SelectedTaskEval = taskEval;
		}

		public void ChangeStateTaskListId()
		{
			if (SelectedStateTaskList.Value == null)
			{
				return;
			}

			var dlg = new ChangeObjectIdDialog
			{
				ContentText =
					string.Format("Change id of {0} #{1}", "StateTaskList", SelectedStateTaskList.Key),
				ObjectId = SelectedStateTaskList.Key
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			var stateTaskList = SelectedStateTaskList.Value;

			StateTaskLists.Remove(SelectedStateTaskList);

			AddStateTaskList(dlg.ObjectId, stateTaskList);
		}

		public void CopyStateTaskList()
		{
			if (SelectedStateTaskList.Value == null)
			{
				return;
			}

			var dlg = new CopyObjectDialog
			{
				ContentText =
					string.Format("Copy {0} {1}", "StateTaskList", SelectedStateTaskList.Key),
				ObjectId = SelectedStateTaskList.Key
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedStateTaskList.Key == dlg.ObjectId)
			{
				return;
			}

			AddStateTaskList(dlg.ObjectId, new BioStateTaskList(SelectedStateTaskList.Value));
		}

		public void CopyTaskEval()
		{
			if (StateTaskLists == null || SelectedStateTaskList.Value == null || SelectedTaskEval == null)
			{
				return;
			}

			AddTaskEval(new BioTaskEval(SelectedTaskEval));
		}

		public void RemoveStateTaskList()
		{
			if (StateTaskLists == null || SelectedStateTaskList.Value == null)
			{
				return;
			}

			var index = StateTaskLists.IndexOf(SelectedStateTaskList);

			if (!StateTaskLists.Remove(SelectedStateTaskList))
			{
				return;
			}

			if (StateTaskLists.Any())
			{
				SelectedStateTaskList = ((index - 1) >= 0)
					? StateTaskLists[index - 1]
					: StateTaskLists.First();
			}
		}

		public void RemoveTaskEval()
		{
			if (StateTaskLists == null || SelectedStateTaskList.Value == null || SelectedTaskEval == null)
			{
				return;
			}

			var index = SelectedStateTaskList.Value.TaskEvals.IndexOf(SelectedTaskEval);

			if (!SelectedStateTaskList.Value.TaskEvals.Remove(SelectedTaskEval))
			{
				return;
			}

			if (SelectedStateTaskList.Value.TaskEvals.Any())
			{
				SelectedTaskEval = ((index - 1) >= 0)
					? SelectedStateTaskList.Value.TaskEvals[index - 1]
					: SelectedStateTaskList.Value.TaskEvals.First();
			}
		}

		public void SetStateTaskLists(IEnumerable<KeyValuePair<int, BioStateTaskList>> collection)
		{
			if (collection == null)
			{
				StateTaskLists = new BindableCollection<KeyValuePair<int, BioStateTaskList>>();
			}
			else
			{
				StateTaskLists = InitCollection(collection);

				foreach (var taskEval in StateTaskLists)
				{
					taskEval.Value.TaskEvals = InitCollection(taskEval.Value.TaskEvals);
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

		private int GetMaxStateTaskListId()
		{
			return StateTaskLists.Any() ? StateTaskLists.Max(b => b.Key) : -1;
		}
	}
}
