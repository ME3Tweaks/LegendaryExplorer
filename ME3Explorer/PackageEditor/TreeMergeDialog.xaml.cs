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
using ME3ExplorerCore.Packages;

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
        public ICommand CloneAllReferencesCommand { get; set; }

        public TreeMergeDialog(IEntry sourceEntry, IEntry targetEntry, MEGame targetGame)
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

            if (sourceEntry.Game != targetGame)
            {
                cloneAllRefsButton.IsEnabled = false;
                cloneAllRefsText.Text = "Cannot do this when cross-game porting";
            }
        }

        private void LoadCommands()
        {
            ReplaceDataCommand = new GenericCommand(ReplaceData, CanReplaceData);
            MergeTreeCommand = new GenericCommand(MergeTree, CanMergeTree);
            AddSingularCommand = new GenericCommand(AddSingular, CanAddSingular);
            CloneTreeCommand = new GenericCommand(CloneTree, CanCloneTree);
            CloneAllReferencesCommand = new GenericCommand(CloneAllReferences);
        }

        private void CloneAllReferences()
        {
            PortingOptionChosen = EntryImporter.PortingOption.CloneAllDependencies;
            Close();
        }

        private void CloneTree()
        {
            PortingOptionChosen = EntryImporter.PortingOption.CloneTreeAsChild;
            Close();
        }

        private void AddSingular()
        {
            PortingOptionChosen = EntryImporter.PortingOption.AddSingularAsChild;
            Close();
        }

        private void MergeTree()
        {
            PortingOptionChosen = EntryImporter.PortingOption.MergeTreeChildren;
            Close();
        }

        private bool CanMergeTree()
        {
            return /*EntryTypesMatch() &&*/ sourceHasChildren && targetHasChildren;
        }

        private bool CanAddSingular()
        {
            return true; //this is always allowed
        }

        private bool CanCloneTree()
        {
            return sourceHasChildren;
        }

        private bool EntryTypesMatch()
        {
            return (sourceEntry is ExportEntry && targetEntry is ExportEntry) || (sourceEntry is ImportEntry && targetEntry is ImportEntry);
        }

        private void ReplaceData()
        {
            PortingOptionChosen = EntryImporter.PortingOption.ReplaceSingular;
            Close();
        }

        private bool CanReplaceData()
        {
            return (sourceEntry is ExportEntry && targetEntry is ExportEntry && sourceEntry.ClassName == targetEntry.ClassName);
        }

        public static EntryImporter.PortingOption GetMergeType(Window w, TreeViewEntry sourceItem, TreeViewEntry targetItem, MEGame targetGame)
        {
            TreeMergeDialog tmd = new TreeMergeDialog(sourceItem.Entry, targetItem.Entry, targetGame)
            {
                Owner = w
            };
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
