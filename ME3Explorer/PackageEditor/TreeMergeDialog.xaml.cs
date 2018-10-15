using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace ME3Explorer.PackageEditorWPFControls
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class TreeMergeDialog : Window, INotifyPropertyChanged
    {
        public enum PortingOption
        {
            AddTreeAsChild,
            AddSingularAsChild,
            ReplaceSingular,
            MergeTreeChildren,
            Cancel
        }
        public PortingOption PortingOptionChosen;
        private IEntry sourceEntry;
        private IEntry targetEntry;

        #region propertychangedhandling
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;

            if (propertyName != null)
            {
                OnPropertyChanged(propertyName);
            }

            return true;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public string TargetEntryObjectName { get { return targetEntry.ObjectName; } }
        public string SourceEntryObjectName { get { return sourceEntry.ObjectName; } }

        public ICommand ReplaceDataCommand { get; set; }
        public TreeMergeDialog(IEntry sourceEntry, IEntry targetEntry)
        {
            this.sourceEntry = sourceEntry;
            this.targetEntry = targetEntry;
            InitializeComponent();
            LoadCommands();
        }

        private void LoadCommands()
        {
            ReplaceDataCommand = new RelayCommand(ReplaceData, CanReplaceData);
        }

        private void ReplaceData(object obj)
        {
            PortingOptionChosen = PortingOption.ReplaceSingular;
            Close();
        }

        private bool CanReplaceData(object obj)
        {
            return (sourceEntry is IExportEntry && targetEntry is IExportEntry && sourceEntry.ClassName == targetEntry.ClassName);
        }

        public static PortingOption GetMergeType(Window w, TreeViewEntry sourceItem, TreeViewEntry targetItem)
        {
            TreeMergeDialog tmd = new TreeMergeDialog(sourceItem.Entry, targetItem.Entry);
            tmd.Owner = w;
            tmd.ShowDialog(); //modal

            return tmd.PortingOptionChosen;
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.MergeTreeChildren;
            Close();
        }

        private void CloneTreeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.AddTreeAsChild;
            Close();

        }

        private void AddSingularButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.AddSingularAsChild;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.Cancel;
            Close();
        }
    }
}
