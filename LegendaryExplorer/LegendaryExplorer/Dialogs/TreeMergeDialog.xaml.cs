using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class TreeMergeDialog : NotifyPropertyChangedWindowBase
    {
        public PortingOptions PortingOption = new PortingOptions() { PortingOptionChosen = EntryImporter.PortingOption.Cancel };//Click X, get cancel
        private readonly IEntry sourceEntry;
        private readonly IEntry targetEntry;
        private readonly PackageEditorWindow sourceWindow;
        private readonly PackageEditorWindow destWindow;
        // private readonly IMEPackage targetPackage;
        private readonly bool sourceHasChildren;
        private readonly bool targetHasChildren;

        public string TargetEntryObjectName => targetEntry == null ? "Root" : targetEntry.ObjectName.Instanced;
        public string SourceEntryObjectName => sourceEntry.ObjectName.Instanced;

        public ICommand ReplaceDataCommand { get; set; }
        public ICommand ReplaceDataWithRelinkCommand { get; set; }
        public ICommand AddSingularCommand { get; set; }
        public ICommand MergeTreeCommand { get; set; }
        public ICommand CloneTreeCommand { get; set; }
        public ICommand CloneAllReferencesCommand { get; set; }
        public ICommand SeeAllReferencesCommand { get; set; }

        // For crossgen, try to use donors in dest game instead
        private bool _portUsingDonors;
        public bool PortUsingDonors { get => _portUsingDonors; set => SetProperty(ref _portUsingDonors, value); }

        /// <summary>
        /// Is the source file a globally loaded file?
        /// </summary>
        public bool IsGlobalFile => EntryImporter.IsSafeToImportFrom(sourceEntry.FileRef.FilePath, sourceEntry.FileRef); // This is full global check. Do not do checking for non-global    

        private bool _portGlobalsAsImports = true;
        public bool PortGlobalsAsImports { get => _portGlobalsAsImports; set => SetProperty(ref _portGlobalsAsImports, value); }

        public bool PortExportsMemorySafe { get; set; }
        public bool PortExportsAsImportsWhenPossible { get; set; }

        public TreeMergeDialog(IEntry sourceEntry, IEntry targetEntry, MEGame targetGame, PackageEditorWindow sourceWindow = null, PackageEditorWindow destWindow = null)
        {
            this.sourceEntry = sourceEntry;
            this.targetEntry = targetEntry;
            this.sourceWindow = sourceWindow;
            this.destWindow = destWindow;

            Owner = destWindow;

            IsCrossGamePort = sourceEntry.Game != targetGame;
            if (IsCrossGamePort)
                PortUsingDonors = true;

            //target can be null, which means root node
            sourceHasChildren = sourceEntry.FileRef.Exports.Any(x => x.idxLink == sourceEntry.UIndex);
            targetHasChildren = targetEntry == null || targetEntry.FileRef.Exports.Any(x => x.idxLink == targetEntry.UIndex);
            sourceHasChildren |= sourceEntry.FileRef.Imports.Any(x => x.idxLink == sourceEntry.UIndex);
            targetHasChildren |= targetEntry == null || targetEntry.FileRef.Imports.Any(x => x.idxLink == targetEntry.UIndex);

            LoadCommands();
            InitializeComponent();

            //            if (sourceEntry.Game != targetGame
            //#if DEBUG
            //                && (sourceEntry.Game != MEGame.ME3 || !targetGame.IsLEGame())
            //#endif
            //            )
            //            {
            //                cloneAllRefsButton.IsEnabled = false;
            //                cloneAllRefsText.Text = "Cannot do this when cross-game porting";
            //            }
        }

        public bool IsCrossGamePort { get; }

        private void LoadCommands()
        {
            ReplaceDataCommand = new GenericCommand(ReplaceData, CanReplaceData);
            ReplaceDataWithRelinkCommand = new GenericCommand(ReplaceDataWithRelink, CanReplaceData);
            MergeTreeCommand = new GenericCommand(MergeTree, CanMergeTree);
            AddSingularCommand = new GenericCommand(AddSingular, CanAddSingular);
            CloneTreeCommand = new GenericCommand(CloneTree, CanCloneTree);
            CloneAllReferencesCommand = new GenericCommand(CloneAllReferences);
            SeeAllReferencesCommand = new GenericCommand(SeeAllReferences);
        }

        private void SeeAllReferences()
        {
            Task.Run(() =>
            {
                SortedSet<int> referencedObjects = new SortedSet<int>();
                List<IEntry> portingObjects = new List<IEntry>();
                portingObjects.Add(sourceEntry);
                if (sourceEntry.ClassName == "Package")
                {
                    portingObjects.AddRange(sourceEntry.GetChildren());
                }

                foreach (var v in portingObjects)
                {
                    if (v is ExportEntry exp)
                    {
                        //if (exp.Parent != null)
                        //    referencedObjects.Add(exp.Parent);
                        referencedObjects.AddRange(EntryImporter.GetAllReferencesOfExport(exp).Select(x => x.UIndex));
                    }
                    else if (v is ImportEntry imp)
                    {
                        //if (imp.Parent != null)
                        //    referencedObjects.Add(imp.Parent);
                        // ? This would require resolution since it has no object refs
                    }
                }
                return referencedObjects.OrderBy(x => x).Select(x => sourceEntry.FileRef.GetEntry(x)).ToList();
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                {
                    MessageBox.Show(x.Exception.FlattenException());
                }
                else if (x.Result != null)
                {
                    ListDialog ld = new ListDialog(x.Result.Select(x => new EntryStringPair(x)),
                        $"Referenced objects from {sourceEntry.InstancedFullPath}",
                        "The following objects are referenced by the source object and will be ported if using clone all dependencies.",
                        this)
                    {
                        DoubleClickEntryHandler = sourceWindow?.GetEntryDoubleClickAction()
                    };
                    ld.Show();
                }
            });
        }

        private void CloneAllReferences()
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.CloneAllDependencies;
            Close();
        }

        private void CloneTree()
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.CloneTreeAsChild;
            Close();
        }

        private void AddSingular()
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.AddSingularAsChild;
            Close();
        }

        private void MergeTree()
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.MergeTreeChildren;
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
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.ReplaceSingular;
            Close();
        }

        private void ReplaceDataWithRelink()
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.ReplaceSingularWithRelink;
            Close();
        }

        private bool CanReplaceData()
        {
            return (sourceEntry is ExportEntry && targetEntry is ExportEntry && sourceEntry.ClassName == targetEntry.ClassName);
        }

        public static PortingOptions GetMergeType(PackageEditorWindow sourceWindow, PackageEditorWindow destWindow, TreeViewEntry sourceItem, TreeViewEntry targetItem, MEGame targetGame)
        {
            TreeMergeDialog tmd = new TreeMergeDialog(sourceItem.Entry, targetItem.Entry, targetGame, sourceWindow: sourceWindow, destWindow: destWindow);
            tmd.ShowDialog(); //modal

            tmd.PortingOption.PortUsingDonors = tmd.PortUsingDonors;
            tmd.PortingOption.PortGlobalsAsImports = tmd.PortGlobalsAsImports;
            tmd.PortingOption.PortExportsAsImportsWhenPossible = tmd.PortExportsAsImportsWhenPossible;
            tmd.PortingOption.PortExportsMemorySafe = tmd.PortExportsMemorySafe;
            return tmd.PortingOption;
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.MergeTreeChildren;
            Close();
        }

        private void CloneTreeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.CloneTreeAsChild;
            Close();
        }

        private void AddSingularButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.AddSingularAsChild;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOption.PortingOptionChosen = EntryImporter.PortingOption.Cancel;
            Close();
        }
    }
}
