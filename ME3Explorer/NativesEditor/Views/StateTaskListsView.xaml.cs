using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using MassEffect.NativesEditor.Dialogs;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	/// Interaction logic for StateTaskListsView.xaml
	/// </summary>
	public partial class StateTaskListsView : INotifyPropertyChanged
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
                SetProperty(ref _selectedStateTaskList, value);
                OnPropertyChanged(nameof(CanAddTaskEval));
                OnPropertyChanged(nameof(CanRemoveStateTaskList));
                OnPropertyChanged(nameof(CanRemoveTaskEval));
            }
        }

        public BioTaskEval SelectedTaskEval
        {
            get { return _selectedTaskEval; }
            set
            {
                SetProperty(ref _selectedTaskEval, value);
                OnPropertyChanged(nameof(CanRemoveTaskEval));
            }
        }

        public ObservableCollection<KeyValuePair<int, BioStateTaskList>> StateTaskLists
        {
            get { return _stateTaskLists; }
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
                ThrowHelper.ThrowArgumentNullException("collection");
            }

            return new ObservableCollection<T>(collection);
        }

        private int GetMaxStateTaskListId()
        {
            return StateTaskLists.Any() ? StateTaskLists.Max(b => b.Key) : -1;
        }

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

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
