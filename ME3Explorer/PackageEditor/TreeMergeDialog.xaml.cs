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
using ME3Explorer.SharedUI;

namespace ME3Explorer.PackageEditorWPFControls
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class TreeMergeDialog : NotifyPropertyChangedWindowBase
    {
        public EntryImporter.PortingOption PortingOptionChosen = EntryImporter.PortingOption.Cancel; //Click X, get cancel
        private readonly IEntry sourceEntry;
        private readonly IEntry targetEntry;
        private readonly bool sourceHasChildren;
        private readonly bool targetHasChildren;

        public string TargetEntryObjectName => targetEntry == null ? "Root" : targetEntry.ObjectName.Instanced;
        public string SourceEntryObjectName => sourceEntry.ObjectName.Instanced;

        public ICommand ReplaceDataCommand { get; set; }
        public ICommand AddSingularCommand { get; set; }
        public ICommand MergeTreeCommand { get; set; }
        public ICommand CloneTreeCommand { get; set; }

        public TreeMergeDialog(IEntry sourceEntry, IEntry targetEntry)
        {
            this.sourceEntry = sourceEntry;
            this.targetEntry = targetEntry;

            //target can be null, which means root node
            sourceHasChildren = sourceEntry.FileRef.Exports.Any(x => x.idxLink == sourceEntry.UIndex);
            targetHasChildren = targetEntry == null || targetEntry.FileRef.Exports.Any(x => x.idxLink == targetEntry.UIndex);
            sourceHasChildren |= sourceEntry.FileRef.Imports.Any(x => x.idxLink == sourceEntry.UIndex);
            targetHasChildren |= targetEntry == null || targetEntry.FileRef.Imports.Any(x => x.idxLink == targetEntry.UIndex);

            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            ReplaceDataCommand = new RelayCommand(ReplaceData, CanReplaceData);
            MergeTreeCommand = new RelayCommand(MergeTree, CanMergeTree);
            AddSingularCommand = new RelayCommand(AddSingular, CanAddSingular);
            CloneTreeCommand = new RelayCommand(CloneTree, CanCloneTree);
        }

        private void CloneTree(object obj)
        {
            PortingOptionChosen = EntryImporter.PortingOption.CloneTreeAsChild;
            Close();
        }

        private void AddSingular(object obj)
        {
            PortingOptionChosen = EntryImporter.PortingOption.AddSingularAsChild;
            Close();
        }

        private void MergeTree(object obj)
        {
            PortingOptionChosen = EntryImporter.PortingOption.MergeTreeChildren;
            Close();
        }

        private bool CanMergeTree(object obj)
        {
            return /*EntryTypesMatch() &&*/ sourceHasChildren && targetHasChildren;
        }

        private bool CanAddSingular(object obj)
        {
            return true; //this is always allowed
        }

        private bool CanCloneTree(object obj)
        {
            return sourceHasChildren;
        }

        private bool EntryTypesMatch()
        {
            return (sourceEntry is ExportEntry && targetEntry is ExportEntry) || (sourceEntry is ImportEntry && targetEntry is ImportEntry);
        }

        private void ReplaceData(object obj)
        {
            PortingOptionChosen = EntryImporter.PortingOption.ReplaceSingular;
            Close();
        }

        private bool CanReplaceData(object obj)
        {
            return (sourceEntry is ExportEntry && targetEntry is ExportEntry && sourceEntry.ClassName == targetEntry.ClassName);
        }

        public static EntryImporter.PortingOption GetMergeType(Window w, TreeViewEntry sourceItem, TreeViewEntry targetItem)
        {
            TreeMergeDialog tmd = new TreeMergeDialog(sourceItem.Entry, targetItem.Entry);
            tmd.Owner = w;
            tmd.ShowDialog(); //modal

            return tmd.PortingOptionChosen;
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = EntryImporter.PortingOption.MergeTreeChildren;
            Close();
        }

        private void CloneTreeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = EntryImporter.PortingOption.CloneTreeAsChild;
            Close();

        }

        private void AddSingularButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = EntryImporter.PortingOption.AddSingularAsChild;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = EntryImporter.PortingOption.Cancel;
            Close();
        }
    }
}
