using FontAwesome.WPF;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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

namespace ME3Explorer.Pathfinding_Editor
{
    /// <summary>
    /// Interaction logic for ValidationPanel.xaml
    /// </summary>
    public partial class ValidationPanel : UserControl, INotifyPropertyChanged
    {
        private IMEPackage _pcc;
        public IMEPackage Pcc { get => _pcc; private set => SetProperty(ref _pcc, value); }
        BackgroundWorker fixAndValidateWorker;
        public ObservableCollectionExtended<ValidationTask> ValidationTasks { get; } = new ObservableCollectionExtended<ValidationTask>();

        private string _lastRunOnText;
        public string LastRunOnText { get => _lastRunOnText; set => SetProperty(ref _lastRunOnText, value); }

        public ValidationPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public void LoadPackage(IMEPackage pcc)
        {
            Pcc = pcc;
            LastRunOnText = "Not yet run";
        }

        public void UnloadPackage()
        {
            Pcc = null;
        }

        public ICommand FixAndValidateCommand { get; set; }

        private void LoadCommands()
        {
            FixAndValidateCommand = new RelayCommand(FixAndValidate, CanFixAndValidate);
        }

        private bool CanFixAndValidate(object obj)
        {
            return Pcc != null && (fixAndValidateWorker == null || fixAndValidateWorker.IsBusy == false);
        }

        private void FixAndValidate(object obj)
        {
            if (Pcc != null && (fixAndValidateWorker == null || fixAndValidateWorker.IsBusy == false))
            {
                ValidationTasks.ClearEx();
                fixAndValidateWorker = new BackgroundWorker();
                fixAndValidateWorker.DoWork += Background_FixAndValidate;
                fixAndValidateWorker.RunWorkerCompleted += FixAndValidate_Completed;

                fixAndValidateWorker.RunWorkerAsync();
                CommandManager.InvalidateRequerySuggested(); //Recalculate commands.
            }
        }

        private void FixAndValidate_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested(); //Recalculate commands.
        }

        private void Background_FixAndValidate(object sender, DoWorkEventArgs e)
        {
            var task = new ValidationTask("Recalculating reachspecs");
            ValidationTasks.Add(task);
            Thread.Sleep(3000);
            task.Icon = FontAwesomeIcon.CheckSquare;
            task.Foreground = Brushes.Green;
            task.Spinning = false;

            task = new ValidationTask("Fixing stack headers");
            ValidationTasks.Add(task);
            Thread.Sleep(3000);
            task.Icon = FontAwesomeIcon.CheckSquare;
            task.Foreground = Brushes.Green;
            task.Spinning = false;
            task = new ValidationTask("Finding duplicate GUIDs");
            ValidationTasks.Add(task);
            Thread.Sleep(3000);
            task.Icon = FontAwesomeIcon.CheckSquare;
            task.Foreground = Brushes.Green;
            task.Spinning = false;
            task = new ValidationTask("Relinking pathfinding chain");
            ValidationTasks.Add(task);
            Thread.Sleep(3000);
            task.Icon = FontAwesomeIcon.CheckSquare;
            task.Foreground = Brushes.Green;
            task.Spinning = false;
            LastRunOnText = "Last ran at " + DateTime.Now;
        }

        #region PropertyChanged
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

        public class ValidationTask : NotifyPropertyChangedBase
        {
            private string _header;
            public string Header { get => _header; set => SetProperty(ref _header, value); }

            private FontAwesomeIcon _icon = FontAwesomeIcon.Spinner;
            public FontAwesomeIcon Icon { get => _icon; set => SetProperty(ref _icon, value); }

            private Brush _foreground = Brushes.Gray;
            public Brush Foreground { get => _foreground; set => SetProperty(ref _foreground, value); }

            public bool _spinning = true;
            public bool Spinning { get => _spinning; set => SetProperty(ref _spinning, value); }

            public ValidationTask(string header)
            {
                Header = header;
            }
        }
    }
}
