using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using MassEffect.NativesEditor.Dialogs;
using ME3Explorer;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	/// Interaction logic for StateTaskListsView.xaml
	/// </summary>
	public partial class StateTaskListsView : NotifyPropertyChangedControlBase
    {
		public StateTaskListsView()
		{
			InitializeComponent();
            SetStateTaskLists(null);
        }
        private KeyValuePair<int, BioStateTaskList> _selectedStateTaskList;
        private BioTaskEval _selectedTaskEval;
        private ObservableCollection<KeyValuePair<int, BioStateTaskList>> _stateTaskLists;

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

                if (SelectedStateTaskList.Value?.TaskEvals == null || !SelectedStateTaskList.Value.TaskEvals.Any())
                {
                    return false;
                }

                return SelectedTaskEval != null;
            }
        }

        public KeyValuePair<int, BioStateTaskList> SelectedStateTaskList
        {
            get => _selectedStateTaskList;
            set
            {
                SetProperty(ref _selectedStateTaskList, value);
                OnPropertyChanged(nameof(CanAddTaskEval));
                OnPropertyChanged(nameof(CanRemoveStateTaskList));
                OnPropertyChanged(nameof(CanRemoveTaskEval));
            }
        }

        public BioTaskEval SelectedTaskEval
        {
            get => _selectedTaskEval;
            set
            {
                SetProperty(ref _selectedTaskEval, value);
                OnPropertyChanged(nameof(CanRemoveTaskEval));
            }
        }

        public ObservableCollection<KeyValuePair<int, BioStateTaskList>> StateTaskLists
        {
            get => _stateTaskLists;
            set
            {
                SetProperty(ref _stateTaskLists, value);
                OnPropertyChanged(nameof(CanAddTaskEval));
                OnPropertyChanged(nameof(CanRemoveStateTaskList));
                OnPropertyChanged(nameof(CanRemoveTaskEval));
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
                ContentText = "New StateTaskList",
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
                ContentText = $"Change id of StateTaskList #{SelectedStateTaskList.Key}",
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
                ContentText = $"Copy StateTaskList {SelectedStateTaskList.Key}",
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
                StateTaskLists = new ObservableCollection<KeyValuePair<int, BioStateTaskList>>();
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

        
        private static ObservableCollection<T> InitCollection<T>()
        {
            return new ObservableCollection<T>();
        }

        
        private static ObservableCollection<T> InitCollection<T>(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collection));
            }

            return new ObservableCollection<T>(collection);
        }

        private int GetMaxStateTaskListId()
        {
            return StateTaskLists.Any() ? StateTaskLists.Max(b => b.Key) : -1;
        }

        private void ChangeStateTaskListId_Click(object sender, RoutedEventArgs e)
        {
            ChangeStateTaskListId();
        }

        private void CopyStateTaskList_Click(object sender, RoutedEventArgs e)
        {
            CopyStateTaskList();
        }

        private void RemoveStateTaskList_Click(object sender, RoutedEventArgs e)
        {
            RemoveStateTaskList();
        }

        private void AddStateTaskList_Click(object sender, RoutedEventArgs e)
        {
            AddStateTaskList();
        }

        private void CopyTaskEval_Click(object sender, RoutedEventArgs e)
        {
            CopyTaskEval();
        }

        private void RemoveTaskEval_Click(object sender, RoutedEventArgs e)
        {
            RemoveTaskEval();
        }

        private void AddTaskEval_Click(object sender, RoutedEventArgs e)
        {
            AddTaskEval();
        }
    }
}
