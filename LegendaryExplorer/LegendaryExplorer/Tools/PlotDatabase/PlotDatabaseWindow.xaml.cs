using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ClosedXML.Excel;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace LegendaryExplorer.Tools.PlotDatabase
{
    /// <summary>
    /// Interaction logic for PlotManagerWindow.xaml
    /// </summary>
    public partial class PlotManagerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        #region Declarations
        public ObservableCollectionExtended<PlotElement> ElementsTable { get; } = new();
        public ObservableCollectionExtended<PlotElement> RootNodes3 { get; } = new();
        public ObservableCollectionExtended<PlotElement> RootNodes2 { get; } = new();
        public ObservableCollectionExtended<PlotElement> RootNodes1 { get; } = new();

        private PlotElement _selectedNode;
        public PlotElement SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    UpdateSelection();
                }
            }
        }

        private string _currentOverallOperationText = "Plot Databases courtesy of Bioware.";
        public string CurrentOverallOperationText { get => _currentOverallOperationText; set => SetProperty(ref _currentOverallOperationText, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private string _busyText;
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }
        private string _busyHeader;
        public string BusyHeader { get => _busyHeader; set => SetProperty(ref _busyHeader, value); }
        private bool _BusyBarInd;
        public bool BusyBarInd
        {
            get => _BusyBarInd; set => SetProperty(ref _BusyBarInd, value);
        }

        private MEGame _currentGame;
        public MEGame CurrentGame { get => _currentGame; set => SetProperty(ref _currentGame, value); }
        private int previousView { get; set; }
        private int _currentView;
        public int CurrentView { get => _currentView; set { previousView = _currentView; SetProperty(ref _currentView, value); } }
        private bool _needsSave;
        public bool NeedsSave { get => _needsSave; set => SetProperty(ref _needsSave, value); }
        private bool _ShowBoolStates = true;
        public bool ShowBoolStates { get => _ShowBoolStates; set => SetProperty(ref _ShowBoolStates, value); }
        private bool _ShowInts = true;
        public bool ShowInts { get => _ShowInts; set => SetProperty(ref _ShowInts, value); }
        private bool _ShowFloats = true;
        public bool ShowFloats { get => _ShowFloats; set => SetProperty(ref _ShowFloats, value); }
        private bool _ShowConditionals = true;
        public bool ShowConditionals { get => _ShowConditionals; set => SetProperty(ref _ShowConditionals, value); }
        private bool _ShowTransitions = true;
        public bool ShowTransitions { get => _ShowTransitions; set => SetProperty(ref _ShowTransitions, value); }
        private bool _ShowJournal = true;
        public bool ShowJournal { get => _ShowJournal; set => SetProperty(ref _ShowJournal, value); }
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        public ObservableCollectionExtended<PlotElementType> newItemTypes { get; } = new()
        {
            PlotElementType.State,
            PlotElementType.SubState,
            PlotElementType.Integer,
            PlotElementType.Float,
            PlotElementType.Conditional,
            PlotElementType.Transition,
            PlotElementType.JournalGoal,
            PlotElementType.JournalItem,
            PlotElementType.JournalTask,
            PlotElementType.Consequence,
            PlotElementType.Flag
        };
        private bool updateTable = true;
        private bool isEditing;
        private (MEGame game, PlotElement pe) initialElement;
        public ICommand FilterCommand { get; set; }
        public ICommand CopyToClipboardCommand { get; set; }
        public ICommand RefreshLocalCommand { get; set; }
        public ICommand LoadLocalCommand { get; set; }
        public ICommand SaveLocalCommand { get; set; }
        public ICommand ExitCommand { get; set; }
        public ICommand AddNewModCommand { get; set; }
        public ICommand ClickOkCommand { get; set; }
        public ICommand ClickCancelCommand { get; set; }
        public ICommand AddModCategoryCommand { get; set; }
        public ICommand AddModItemCommand { get; set; }
        public ICommand EditModItemCommand { get; set; }
        public ICommand DeleteModItemCommand { get; set; }
        public ICommand XLImportCommand { get; set; }
        public ICommand XLExportCommand { get; set; }
        public ICommand MigrateCommand { get; set; }
        public ICommand OpenModPlotFolderInExplorerCommand { get; set; }

        public bool IsModRoot() => SelectedNode?.ElementId == ModPlotContainer.StartingModId;
        public bool IsMod() => SelectedNode?.Type == PlotElementType.Mod;
        public bool CanAddCategory() => SelectedNode?.Type == PlotElementType.Mod || SelectedNode?.Type == PlotElementType.Category;
        public bool CanEditItem() => !PlotDatabases.GetDatabaseContainingElement(SelectedNode, CurrentGame).IsBioware;
        public bool CanDeleteItem() => !PlotDatabases.GetDatabaseContainingElement(SelectedNode, CurrentGame).IsBioware;
        public bool CanAddItem() => SelectedNode?.Type == PlotElementType.Category;
        public bool CanExportTable() => SelectedNode != null;
        #endregion

        #region PDBInitialization
        public PlotManagerWindow() : base("Plot Database", true)
        {
            LoadCommands();
            InitializeComponent();

            RootNodes3.ClearEx();
            RootNodes3.Add(PlotDatabases.BridgeBasegameAndModDatabases(MEGame.LE3, AppDirectories.AppDataFolder));

            RootNodes2.ClearEx();
            RootNodes2.Add(PlotDatabases.BridgeBasegameAndModDatabases(MEGame.LE2, AppDirectories.AppDataFolder));

            RootNodes1.ClearEx();
            RootNodes1.Add(PlotDatabases.BridgeBasegameAndModDatabases(MEGame.LE1, AppDirectories.AppDataFolder));
            Focus();
        }

        public PlotManagerWindow(MEGame game, PlotElement elementToOpen) : this()
        {
            initialElement = (game, elementToOpen);
        }

        private void LoadCommands()
        {
            FilterCommand = new GenericCommand(Filter);
            CopyToClipboardCommand = new GenericCommand(CopyToClipboard);
            RefreshLocalCommand = new GenericCommand(RefreshTrees);
            LoadLocalCommand = new GenericCommand(LoadModDB);
            SaveLocalCommand = new GenericCommand(SaveModDB);
            ExitCommand = new GenericCommand(Close);
            ClickOkCommand = new RelayCommand(AddDataToModDatabase);
            ClickCancelCommand = new RelayCommand(CancelAddData);
            AddNewModCommand = new GenericCommand(AddNewModData, IsModRoot);
            AddModCategoryCommand = new GenericCommand(AddNewModCatData, CanAddCategory);
            AddModItemCommand = new GenericCommand(AddNewModItemData, CanAddItem);
            DeleteModItemCommand = new GenericCommand(DeleteNewModData, CanDeleteItem);
            EditModItemCommand = new GenericCommand(EditNewModData, CanEditItem);
            XLImportCommand = new GenericCommand(ImportModDataFromExcel);
            XLExportCommand = new GenericCommand(ExportDataToExcel, CanExportTable);
            MigrateCommand = new GenericCommand(MigrateFromOldFormat);
            OpenModPlotFolderInExplorerCommand = new GenericCommand(OpenModPlotFolder);
        }

        private void PlotDB_Loaded(object sender, RoutedEventArgs e)
        {
            var plotenum = Enum.GetNames(typeof(PlotElementType)).ToList();
            newItem_subtype.ItemsSource = plotenum;
            CurrentGame = initialElement.pe is null ? MEGame.LE3 : initialElement.game;
            CurrentView = CurrentGame switch
            {
                MEGame.LE1 => 2,
                MEGame.LE2 => 1,
                _ => 0
            };
        }

        private void PlotDB_Closing(object sender, CancelEventArgs e)
        {
            if (NeedsSave)
            {
                //var dlg = MessageBox.Show("Changes have been made to the modding database. Save now?", "Plot Database", MessageBoxButton.YesNo);
                //if (dlg == MessageBoxResult.Yes)
                //{
                //    NeedsSave = false;
                //    CurrentGame = MEGame.LE3;
                //    SaveModDB();
                //    CurrentGame = MEGame.LE2;
                //    SaveModDB();
                //    CurrentGame = MEGame.LE1;
                //    SaveModDB();
                //}
                //Above is bugged but saves database? Why?
                var dlg = MessageBox.Show("Changes have been made to the modding database. Do you want to exit?", "Plot Database", MessageBoxButton.OKCancel);
                if (dlg == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void UpdateSelection()
        {
            if(updateTable)
            {
                ElementsTable.ClearEx();
                var temptable = new List<PlotElement>();
                AddPlotsToList(SelectedNode, temptable);
                ElementsTable.AddRange(temptable);
                Filter();
            }
        }

        private void RefreshTrees()
        {
            var rootNodes = GetRootNodes();

            // Create parent object for both basegame and mod
            var rootList = new List<PlotElement>();
            rootList.Add(PlotDatabases.GetBasegamePlotDatabaseForGame(CurrentGame).Root);
            rootList.Add(PlotDatabases.GetModPlotContainerForGame(CurrentGame).GameHeader);

            var plotParent = PlotDatabases.GetNewRootPlotElement(CurrentGame, rootList);
            rootNodes.ClearEx();
            rootNodes.Add(plotParent);
        }

        private ObservableCollectionExtended<PlotElement> GetRootNodes() => CurrentGame switch
        {
            MEGame.LE3 => RootNodes3,
            MEGame.LE2 => RootNodes2,
            MEGame.LE1 => RootNodes1,
            _ => throw new Exception($"Cannot get root nodes for game {CurrentGame}")
        };

        private TreeView GetTreeView() => CurrentGame switch
        {
            MEGame.LE3 => Tree_BW3,
            MEGame.LE2 => Tree_BW2,
            MEGame.LE1 => Tree_BW1,
            _ => throw new Exception($"Cannot get TreeView for game {CurrentGame}")
        };

        private void AddPlotsToList(PlotElement plotElement, List<PlotElement> elementList, bool addFolders = false)
        {
            if (plotElement != null)
            {
                switch (plotElement.Type)
                {
                    case PlotElementType.Plot:
                    case PlotElementType.Region:
                    case PlotElementType.FlagGroup:
                    case PlotElementType.Category:
                    case PlotElementType.Mod:
                    case PlotElementType.None:
                        if(addFolders)
                            elementList.Add(plotElement);
                        break;
                    default:
                        elementList.Add(plotElement);
                        break;
                }

                foreach (var c in plotElement.Children)
                {
                    AddPlotsToList(c, elementList, addFolders);
                }
            }
        }

        private void LV_Plots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateTable = false;
            SelectedNode = LV_Plots.SelectedItem as PlotElement;
        }
        private void NewTab_Selected(object sender, SelectionChangedEventArgs e)
        {
            if(e.RemovedItems.Count != 0)
            {
                CurrentGame = CurrentView switch
                {
                    2 => MEGame.LE1,
                    1 => MEGame.LE2,
                    _ => MEGame.LE3
                };

                var tv = GetTreeView();
                if (tv.SelectedItem is null)
                {
                    if (initialElement.pe is not null && CurrentGame == initialElement.game)
                    {
                        SetFocusByPlotElement(initialElement.pe);
                    }
                    else SetFocusByPlotElement(GetRootNodes()[0]);
                }
                SelectedNode = tv.SelectedItem as PlotElement;
            }
        }


        #endregion

        #region TreeView
        private void TreeView_OnLoaded(object sender, RoutedEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == GetTreeView())
            {
                if (initialElement.pe is not null && CurrentGame == initialElement.game)
                {
                    SetFocusByPlotElement(initialElement.pe);
                    initialElement = default;
                }
                else SetFocusByPlotElement(GetRootNodes()[0]);
            }
        }


        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            updateTable = true;
            var tv = sender as TreeView;
            SelectedNode = tv.SelectedItem as PlotElement;
        }

        public void SelectPlotElement(PlotElement pe, MEGame game)
        {
            CurrentGame = game.ToLEVersion();
            switch (CurrentGame)
            {
                case MEGame.LE1:
                    CurrentView = 2;
                    break;
                case MEGame.LE2:
                    CurrentView = 1;
                    break;
                case MEGame.LE3:
                    CurrentView = 0;
                    break;
            }
            SelectedNode = pe;
            SetFocusByPlotElement(pe);
        }

        private void SetFocusByPlotElement(PlotElement pe)
        {
            var tree = GetTreeView();

            var tvi = tree.ItemContainerGenerator.ContainerFromItemRecursive(pe); 

            if (tvi != null && tvi is TreeViewItem)
            {
                tvi.IsSelected = true;
                tvi.IsExpanded = true;
            }
            else //temp test
            {
                tvi = FindTviFromObjectRecursive(tree, pe); //This does the same as above but can look inside to debug
                if (tvi != null && tvi is TreeViewItem)
                {
                    tvi.IsSelected = true;
                    tvi.IsExpanded = true;
                }
            }
        }

        private static TreeViewItem FindTviFromObjectRecursive(ItemsControl ic, object o)
        {

            //Search for the object model in first level children (recursively)
            TreeViewItem tvi = ic.ItemContainerGenerator.ContainerFromItem(o) as TreeViewItem;
            if (tvi != null) return tvi;
            //Loop through user object models
            if (ic != null)
            {
                foreach (object i in ic.Items)
                {
                    //Get the TreeViewItem associated with the iterated object model
                    TreeViewItem tvi2 = ic.ItemContainerGenerator.ContainerFromItem(i) as TreeViewItem;
                    if (tvi2 == null)
                    {
                        var a = i;
                        Console.WriteLine($"tvi not found. {a.ToString()}");
                    }
                    else
                    {
                        tvi = FindTviFromObjectRecursive(tvi2, o);

                    }
                    if (tvi != null) return tvi;
                }
            }

            return null;
        }


        #endregion

        #region Filter
        private void FilterBox_KeyUp(object sender, KeyEventArgs e)
        {
            Filter();
        }

        private void Filter()
        {
            LV_Plots.Items.Filter = PlotFilter;
        }

        private bool PlotFilter(object p)
        {
            bool showthis = true;
            var e = (PlotElement)p;
            var t = FilterBox.Text;
            if (!string.IsNullOrEmpty(t))
            {
                showthis = e.Path.ToLower().Contains(t.ToLower());
                if (!showthis)
                {
                    showthis = e.PlotId.ToString().Contains(t);
                }
            }
            if (showthis && !ShowBoolStates)
            {
                showthis = e.Type != PlotElementType.State && e.Type != PlotElementType.SubState && e.Type != PlotElementType.Flag && e.Type != PlotElementType.Consequence;
            }
            if (showthis && !ShowInts)
            {
                showthis = e.Type != PlotElementType.Integer;
            }
            if (showthis && !ShowFloats)
            {
                showthis = e.Type != PlotElementType.Float;
            }
            if (showthis && !ShowConditionals)
            {
                showthis = e.Type != PlotElementType.Conditional;
            }
            if (showthis && !ShowTransitions)
            {
                showthis = e.Type != PlotElementType.Transition;
            }
            if (showthis && !ShowJournal)
            {
                showthis = e.Type != PlotElementType.JournalGoal && e.Type != PlotElementType.JournalItem && e.Type != PlotElementType.JournalTask;
            }

            return showthis;
        }

        #endregion

        #region UserCommands
        private void CopyToClipboard()
        {
            var elmnt = (PlotElement)LV_Plots.SelectedItem;
            if (elmnt != null)
            {
                Clipboard.SetText(elmnt.PlotId.ToString());
            }
        }

        private void OpenModPlotFolder()
        {
            var mpc = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
            var path = Path.Combine(AppDirectories.AppDataFolder, mpc.LocalModFolderName);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
        }

        private void list_ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    ListSortDirection direction;
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    ICollectionView linedataView = CollectionViewSource.GetDefaultView(LV_Plots.ItemsSource);
                    string primarySort = headerClicked.Column.Header.ToString();
                    linedataView.SortDescriptions.Clear();
                    switch (primarySort)
                    {
                        case "Plot":
                            primarySort = "PlotId";
                            break;
                        case "Icon":
                        case "Type":
                            primarySort = "Type"; //Want to sort on alphabetical not Enum
                            break;
                        default:

                            break;
                    }
                    linedataView.SortDescriptions.Add(new SortDescription(primarySort, direction));
                    linedataView.Refresh();
                    LV_Plots.ItemsSource = linedataView;

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
            e.Handled = true;
        }

        private void LoadModDB()
        {
            if(NeedsSave)
            {
                var w = MessageBox.Show("There are unsaved changes in modding databases. Please confirm you wish to discard changes and reload?", "Warning", MessageBoxButton.OKCancel);
                if (w == MessageBoxResult.Cancel)
                    return;
                NeedsSave = false;
            }

            var modContainer = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
            modContainer.LoadModsFromDisk(AppDirectories.AppDataFolder);
        }

        private async void SaveModDB()
        {
            bwLink.Visibility = Visibility.Collapsed;
            var statustxt = CurrentOverallOperationText;
            CurrentOverallOperationText = "Saving...";
            ModPlotContainer mdb = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
            mdb.SaveModsToDisk(AppDirectories.AppDataFolder);
            CurrentOverallOperationText = $"Saved {CurrentGame} Mod Database Locally...";

            if (NeedsSave) //don't do this on exit
            {
                await Task.Delay(TimeSpan.FromSeconds(1.0));
            }
            CurrentOverallOperationText = statustxt;
            bwLink.Visibility = Visibility.Visible;
            NeedsSave = false;
        }

        #endregion

        #region Mod DB Editing

        private void AddNewModData()
        {
            newMod_Title.Text = "Add a New Mod to the Database";
            GameTab.IsEnabled = false;
            NewModForm.Visibility = Visibility.Visible;
            newMod_Name.Focus();
        }
        private void AddNewModCatData()
        {
            newCat_Title.Text = "Add a New Category to a Mod";
            GameTab.IsEnabled = false;
            NewCategoryForm.Visibility = Visibility.Visible;
            newCat_Name.Focus();
        }

        private void AddNewModItemData()
        {
            newItem_Title.Text = "Add a New Game State to the Mod Database";
            GameTab.IsEnabled = false;
            NewItemForm.Visibility = Visibility.Visible;
            newItem_Name.Focus();
        }

        private void EditNewModData()
        {
            if(SelectedNode == null || SelectedNode.ElementId <= 100000)
            {
                MessageBox.Show("Cannot edit Bioware plots.", "Plot Database");
                return;
            }
            GameTab.IsEnabled = false;
            isEditing = true;
            switch(SelectedNode.Type)
            {
                case PlotElementType.Mod:
                    newMod_Title.Text = "Edit Mod Title";
                    newMod_Name.Text = SelectedNode.Label;
                    NewModForm.Visibility = Visibility.Visible;
                    newMod_Name.Focus();
                    break;
                case PlotElementType.Category:
                    newCat_Title.Text = "Edit Category Title";
                    var label = SelectedNode.Label;
                    newCat_Name.Text = label;
                    NewCategoryForm.Visibility = Visibility.Visible;
                    newCat_Name.Focus();
                    break;
                default:
                    newItem_Title.Text = "Edit Game State";
                    newItem_Name.Text = SelectedNode.Label;
                    newItem_Plot.Text = SelectedNode.PlotId.ToString();
                    newItem_Type.SelectedIndex = newItemTypes.IndexOf(SelectedNode.Type);
                    switch (SelectedNode.Type)
                    {
                        case PlotElementType.State:
                        case PlotElementType.SubState:
                            var state = SelectedNode as PlotBool;
                            newItem_subtype.SelectedItem = state.SubType;
                            newItem_achievementid.Text = state.AchievementID.ToString();
                            newItem_galaxyatwar.Text = state.GalaxyAtWar.ToString();
                            newItem_gamervariable.Text = state.GamerVariable.ToString();
                            break;
                        case PlotElementType.Conditional:
                            var cnd = SelectedNode as PlotConditional;
                            newItem_Code.Text = cnd.Code;
                            break;
                        case PlotElementType.Transition:
                            var trans = SelectedNode as PlotTransition;
                            newItem_Argument.Text = trans.Argument;
                            break;
                    }
                    NewItemForm.Visibility = Visibility.Visible;
                    newItem_Name.Focus();
                    break;
            }

        }

        private void RevertPanelsToDefault()
        {
            isEditing = false;
            GameTab.IsEnabled = true;
            NewModForm.Visibility = Visibility.Collapsed;
            NewCategoryForm.Visibility = Visibility.Collapsed;
            NewItemForm.Visibility = Visibility.Collapsed;
            newMod_Name.Clear();
            newCat_Name.Clear();
            newItem_Name.Clear();
            newItem_Plot.Clear();
            newItem_Type.SelectedIndex = 0;
            newItem_Code.Clear();
            newItem_Argument.Clear();
            newItem_achievementid.Clear();
            newItem_galaxyatwar.Clear();
            newItem_gamervariable.Clear();
            newItem_subtype.SelectedIndex = -1;
        }
        private void CancelAddData(object obj)
        {
            RevertPanelsToDefault();
        }

        private void AddDataToModDatabase(object obj)
        {
            if (SelectedNode != null)
            {
                var command = obj.ToString();
                var modContainer = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
                var mdb = PlotDatabases.GetDatabaseContainingElement(SelectedNode, CurrentGame) as ModPlotDatabase;

                int newElementId = SelectedNode.ElementId;
                var newItemSelectedIndex = newItem_Type.SelectedIndex == -1 ? 0 : newItem_Type.SelectedIndex;
                if (!isEditing || SelectedNode.Type != newItemTypes[newItemSelectedIndex])
                {
                    newElementId = modContainer.GetNextElementId();
                }
                var parent = SelectedNode;
                var childlist = new List<PlotElement>();
                PlotElement editTarget = null;
                if (isEditing)
                {
                    parent = SelectedNode.Parent;
                    childlist.AddRange(SelectedNode.Children);
                    editTarget = SelectedNode;
                }
                switch (command)
                {
                    case "NewMod":
                        var modname = newMod_Name.Text;
                        if (string.IsNullOrEmpty(modname) || modname.Contains(" "))
                        {
                            MessageBox.Show($"Label is empty or contains a space.\nPlease add a valid label, using underscore '_' for spaces.", "Invalid Label");
                            return;
                        }

                        if (!isEditing)
                        {
                            var dlg = MessageBox.Show($"Do you want to add mod '{modname}' to the database?", "Modding Plots Database", MessageBoxButton.OKCancel);
                            if (dlg == MessageBoxResult.Cancel)
                            {
                                CancelAddData(command);
                                return;
                            }

                            var modsContainer = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
                            foreach (var mod in modsContainer.Mods)
                            {
                                if (mod.Root.Label == modname)
                                {
                                    MessageBox.Show($"Mod '{modname}' already exists in the database.  Please use another name.", "Invalid Name");
                                    CancelAddData(command);
                                    return;
                                }
                            }

                            mdb = new ModPlotDatabase(modname, newElementId);
                            modsContainer.AddMod(mdb);
                        }
                        else
                        {
                            // This renames the file on disk
                            if (mdb != null)
                            {
                                modContainer.RemoveMod(mdb, true, AppDirectories.AppDataFolder);
                                mdb.Root.Label = newMod_Name.Text;
                                modContainer.AddMod(mdb);
                            }
                        }

                        NeedsSave = true;
                        break;
                    case "NewCategory":
                        if (string.IsNullOrEmpty(newCat_Name.Text) || newCat_Name.Text.Contains(" "))
                        {
                            MessageBox.Show($"Label is empty or contains a space.\nPlease add a valid label, using underscore '_' for spaces.", "Invalid Label");
                            return;
                        }
                        if(!isEditing)
                        {
                            var newModCatPE = new PlotElement(-1, newElementId, newCat_Name.Text, PlotElementType.Category, parent, new List<PlotElement>());
                            mdb.Organizational.Add(newElementId, newModCatPE);
                        }
                        else
                        {
                            mdb.Organizational[SelectedNode.ElementId].Label = newCat_Name.Text;
                        }
                        NeedsSave = true;
                        break;
                    case "NewItem":
                        var nameItem = newItem_Name.Text;
                        if (newItem_Type.SelectedIndex < 0)
                        {
                            MessageBox.Show($"A type needs to be selected.  Please review.", "Invalid Type");
                            return;
                        }
                        var type = newItemTypes[newItem_Type.SelectedIndex];
                        var newPlotId_txt = newItem_Plot.Text;
                        if (string.IsNullOrEmpty(nameItem) || nameItem.Contains(" "))
                        {
                            MessageBox.Show($"Label is empty or contains a space.\nPlease add a valid label, using underscore '_' for spaces.", "Invalid Label");
                            return;
                        }
                        if (!(int.TryParse(newPlotId_txt, out int newPlotId) && newPlotId > 0))
                        {
                            MessageBox.Show($"Plot Id needs to be a positive integer.  Please review.", "Invalid Plot Id");
                            return;
                        }
                        var newchildren = new List<PlotElement>();
                        switch (type)
                        {
                            case PlotElementType.State:
                            case PlotElementType.SubState:
                                if (!isEditing || SelectedNode.PlotId != newPlotId)
                                {
                                    if (PlotDatabases.FindPlotBoolByID(newPlotId, CurrentGame) != null)
                                    {
                                        MessageBox.Show($"State '{newPlotId}' already exists in the database.  Please use another id.", "Invalid Id");
                                        return;
                                    }
                                }
                                var newST = PlotElementType.None;
                                if (newItem_subtype.SelectedItem != null &&
                                    Enum.TryParse<PlotElementType>((string) newItem_subtype.SelectedItem,
                                        out var elementType))
                                    newST = elementType;
                                int newAchID = -1;
                                if (!newItem_achievementid.Text.IsEmpty())
                                {
                                    if (!int.TryParse(newItem_achievementid.Text, out newAchID))
                                    {
                                        MessageBox.Show($"Achievement Id needs to be an integer.  Please review.", "Invalid Achievement Id");
                                        return;
                                    }
                                }
                                int newGAWID = -1;
                                if (!newItem_galaxyatwar.Text.IsEmpty())
                                {
                                    if (!(int.TryParse(newItem_galaxyatwar.Text, out newGAWID) && newGAWID > 0))
                                    {
                                        MessageBox.Show($"Galaxy at War Id needs to be a positive integer.  Please review.", "Invalid War Asset Id");
                                        return;
                                    }
                                }
                                int newGVarID = -1;
                                if (!newItem_galaxyatwar.Text.IsEmpty())
                                {
                                    if (!(int.TryParse(newItem_gamervariable.Text, out newGVarID) && newGVarID > 0))
                                    {
                                        MessageBox.Show($"Gamer Variable Id needs to be a positive integer.  Please review.", "Invalid Gamer Variable Id");
                                        return;
                                    }
                                }
                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget, newST, newAchID, newGVarID, newGAWID);
                                break;
                            case PlotElementType.Integer:
                                if (!isEditing || SelectedNode.PlotId != newPlotId)
                                {
                                    if (PlotDatabases.FindPlotIntByID(newPlotId, CurrentGame) != null)
                                    {
                                        MessageBox.Show($"Integer '{newPlotId}' already exists in the database.  Please use another id.", "Invalid Id");
                                        return;
                                    }
                                }
                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget);
                                break;
                            case PlotElementType.Float:
                                if (!isEditing || SelectedNode.PlotId != newPlotId)
                                {
                                    if (PlotDatabases.FindPlotFloatByID(newPlotId, CurrentGame) != null)
                                    {
                                        MessageBox.Show($"Float '{newPlotId}' already exists in the database.  Please use another id.", "Invalid Id");
                                        return;
                                    }
                                }

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget);
                                break;
                            case PlotElementType.Conditional:
                                if (!isEditing || SelectedNode.PlotId != newPlotId)
                                {
                                    if (PlotDatabases.FindPlotConditionalByID(newPlotId, CurrentGame) != null)
                                    {
                                        MessageBox.Show($"Conditional '{newPlotId}' already exists in the database.  Please use another id.", "Invalid Id");
                                        return;
                                    }
                                }

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget, PlotElementType.None, -1, -1, -1, newItem_Code.Text);
                                break;
                            case PlotElementType.Transition:
                                if (!isEditing || SelectedNode.PlotId != newPlotId)
                                {
                                    if (PlotDatabases.FindPlotTransitionByID(newPlotId, CurrentGame) != null)
                                    {
                                        MessageBox.Show($"Transition '{newPlotId}' already exists in the database.  Please use another id.", "Invalid Id");
                                        return;
                                    }
                                }

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget, PlotElementType.None, -1, -1, -1, null, newItem_Argument.Text);
                                break;
                            default:   //PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask, PlotElementType.Consequence, PlotElementType.Flag

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, editTarget);
                                break;
                        }
                        NeedsSave = true;
                        break;
                    default:
                        break;
                }
            }
            RevertPanelsToDefault();
        }

        private bool AddVerifiedPlotState(PlotElementType type, PlotElement parent, int newPlotId, int newElementId, string label, List<PlotElement> newChildren, bool update = false,
            PlotElement updateTarget = null, PlotElementType subtype = PlotElementType.None, int achievementId = -1, int gv = -1, int gaw = -1, string code = null, string argu = null)  //Game state must be verified
        {
            if (update && updateTarget == null)
                return false;
            try
            {
                var modDb = PlotDatabases.GetDatabaseContainingElement(parent, CurrentGame);
                if (update)
                {
                    modDb.RemoveElement(updateTarget);
                }
                PlotElement newElement;

                switch (type)
                {
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                        var newModBool = new PlotBool(newPlotId, newElementId, label, type, parent, newChildren);
                        //subtype, gamervariable, achievementid, galaxyatwar
                        if (subtype != PlotElementType.None)
                            newModBool.SubType = subtype;
                        if (achievementId >= 0)
                        {
                            newModBool.AchievementID = achievementId;
                        }
                        if (gaw >= 0)
                        {
                            newModBool.GalaxyAtWar = gaw;
                        }
                        if (gv >= 0)
                        {
                            newModBool.GamerVariable = gv;
                        }
                        newElement = newModBool;
                        break;
                    case PlotElementType.Conditional:
                        var newModCnd = new PlotConditional(newPlotId, newElementId, label, type, parent, new List<PlotElement>());
                        if(code != null)
                        {
                            newModCnd.Code = code;
                        }
                        newElement = newModCnd;
                        break;
                    case PlotElementType.Transition:
                        var newModTrans = new PlotTransition(newPlotId, newElementId, label, type, parent, new List<PlotElement>());
                        if(argu != null)
                        {
                            newModTrans.Argument = argu;
                        }
                        newElement = newModTrans;
                        break;
                    case PlotElementType.Integer:
                    case PlotElementType.Float:
                    default:   //PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask, PlotElementType.Consequence, PlotElementType.Flag
                        newElement = new PlotElement(newPlotId, newElementId, label, type, parent, newChildren);
                        break;
                }
                modDb.AddElement(newElement, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DeleteNewModData()
        {
            var node = SelectedNode;
            var mdb = PlotDatabases.GetDatabaseContainingElement(node, CurrentGame);
            if (node.ElementId <= 100000 || mdb.IsBioware)
            {
                MessageBox.Show("Cannot Delete Bioware plot states.");
                return;
            }

            if (node == mdb.Root && mdb is ModPlotDatabase mpdb)
            {
                var mpc = PlotDatabases.GetModPlotContainerForGame(CurrentGame);
                var modDelDlg =
                    MessageBox.Show($"Are you sure you wish to delete mod {node.Label}? This is irreversible.");
                if (modDelDlg == MessageBoxResult.Cancel) return;

                mpc.RemoveMod(mpdb, true, AppDirectories.AppDataFolder);
                return;
            }

            var dlg = MessageBox.Show($"Are you sure you wish to delete this item?\nType: {node.Type}\nPlotId: {node.PlotId}\nPath: {node.Path}", "Plot Database", MessageBoxButton.OKCancel);
            if (dlg == MessageBoxResult.Cancel)
                return;

            if (node.Children.Any())
            {
                var dlg2 = MessageBox.Show(
                    $"This item has {node.Children.Count} subitems! They will also be deleted.\nConfirm multiple item deletion.");
                if (dlg2 == MessageBoxResult.Cancel) return;
            }

            mdb.RemoveElement(node, true);
            NeedsSave = true;
        }

        private void MigrateFromOldFormat()
        {
            OpenFileDialog jsonFileDialog = new () {
                Title = "Select PlotDBMods file to migrate",
                Filter = "*.json|*.json",
                InitialDirectory = AppDirectories.AppDataFolder
            };
            var result = jsonFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value) return;
            StreamReader sr = new StreamReader(jsonFileDialog.FileName);
            var db = JsonConvert.DeserializeObject<SerializedPlotDatabase>(sr.ReadToEnd(), new JsonSerializerSettings(){NullValueHandling = NullValueHandling.Ignore});
            if (db is null)
            {
                MessageBox.Show("Unable to deserialize PlotDBMods file!");
                return;
            }
            db.BuildTree();
            var mods = (db.Organizational.FirstOrDefault(d => d.ElementId == 100000)?.Children ?? new()).ToList();
            var dlg = MessageBox.Show($"Migrating {mods.Count} mods to new format for game {CurrentGame}. Proceed?");
            if (dlg == MessageBoxResult.Cancel) return;
            var mpc = PlotDatabases.GetModPlotContainerForGame(CurrentGame);

            foreach (var oldModRoot in mods)
            {
                if (oldModRoot.Type != PlotElementType.Mod) continue;
                var mod = new ModPlotDatabase(oldModRoot.Label, oldModRoot.ElementId);
                oldModRoot.RemoveFromParent();

                Queue<PlotElement> elQueue = new();
                foreach(var ch in oldModRoot.Children) elQueue.Enqueue(ch);
                while (elQueue.TryDequeue(out var pe))
                {
                    foreach(var ch in pe.Children) elQueue.Enqueue(ch);
                    var parent = pe.Parent == oldModRoot ? mod.ModRoot : pe.Parent;
                    mod.AddElement(pe, parent);
                }

                foreach (var m in mpc.Mods)
                {
                    if (m.ModRoot.Label == mod.ModRoot.Label)
                    {
                        mpc.RemoveMod(m);
                    }
                }
                mpc.AddMod(mod);
            }
            mpc.SaveModsToDisk(AppDirectories.AppDataFolder, true);
        }

        private void form_KeyUp(object sender, KeyEventArgs e)
        {
            var form = sender as TextBox;
            if (form == null)
                return;
            if(e.Key == Key.Enter)
            {
                switch(form.Name)
                {
                    case "newMod_Name":
                        AddDataToModDatabase("NewMod");
                        break;
                    case "newCat_Name":
                        AddDataToModDatabase("NewCategory");
                        break;
                    case "newItem_Name":
                    case "newItem_Plot":
                    case "newItem_Code":
                    case "newItem_Argument":
                    case "newItem_achievementid":
                    case "newItem_Type":
                    case "newItem_subtype":
                    case "newItem_galaxyatwar":
                    case "newItem_gamervariable":
                        AddDataToModDatabase("NewItem");
                        break;
                    default:
                        break;
                }
                return;
            }
            if (e.Key == Key.Escape)
            {
                switch (form.Name)
                {
                    default:
                        CancelAddData("generic");
                        break;
                }
            }
        }
        private void newItem_Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = e.AddedItems;
            if(selection != null)
            {
                newItem_Plotlbl.Text = "Plot Id: ";
                newItem_CndPnl.Visibility = Visibility.Collapsed;
                newItem_ArgPnl.Visibility = Visibility.Collapsed;
                newItem_BoolPnl.Visibility = Visibility.Collapsed;
                switch ((PlotElementType)newItem_Type.SelectedItem)
                {
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                        newItem_BoolPnl.Visibility = Visibility.Visible;
                        break;
                    case PlotElementType.Conditional:
                        newItem_Plotlbl.Text = "Id: ";
                        newItem_CndPnl.Visibility = Visibility.Visible;
                        break;
                    case PlotElementType.Transition:
                        newItem_Plotlbl.Text = "Id: ";
                        newItem_ArgPnl.Visibility = Visibility.Visible;
                        break;
                    default:
                        break;
                }
            }
        }

        #endregion

        #region Excel Import/Export
        private void ImportModDataFromExcel()
        {
            var w = MessageBox.Show($"Do you want to import mod plot data into {CurrentGame}?\nNote the import must be laid out exactly as in the sample file.", "Plot Database", MessageBoxButton.OKCancel);
            if (w == MessageBoxResult.Cancel)
                return;
            BusyHeader = "Plot Database";
            BusyText = "Importing from excel.";
            IsBusy = true;

            OpenFileDialog oDlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Import Excel table",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };

            if (oDlg.ShowDialog() != true)
                return;

            var Workbook = new XLWorkbook();
            try
            {
                Workbook = new XLWorkbook(oDlg.FileName);
            }
            catch
            {
                IsBusy = false;
                MessageBox.Show("XLS is open in another application. Close before import.", "Plot Database", MessageBoxButton.OK);
                return;
            }
            IXLWorksheet iWorksheet;
            if (Workbook.Worksheets.Count() > 1)
            {
                try
                {
                    iWorksheet = Workbook.Worksheet("Import");
                }
                catch
                {
                    MessageBox.Show("Import Sheet not found");
                    return;
                }
            }
            else
            {
                iWorksheet = Workbook.Worksheet(1);
            }

            //STEP 1 Check Column Headers
            var headers = new List<string>()
                {
                    "Action",
                    "Mod",
                    "Type",
                    "ParentPath",
                    "Label",
                    "PlotID",
                    "Code",
                    "Argument",
                    "SubType",
                    "GamerVariable",
                    "AchievementID",
                    "GalaxyAtWar",
                    "NewLabel"
                };

            for (int h = 0; h < headers.Count; h++)
            {
                string xlHead = iWorksheet.Cell(1, h + 1).Value.ToString();
                if (xlHead != headers[h])
                {
                    MessageBox.Show("XLS has wrong header format.");
                    IsBusy = false;
                    return;
                }
            }

            //STEP 2 Read and Validate rows into a list
            var changes = new Queue<xlPlotImport>();
            var faillist = new List<xlPlotImport>();
            var modList = PlotDatabases.GetModPlotContainerForGame(CurrentGame).Mods.Select(m => m.Root);
            var validtypes = new List<PlotElementType> { PlotElementType.Category, PlotElementType.State, PlotElementType.SubState, PlotElementType.Integer, PlotElementType.Float, PlotElementType.Conditional,
                PlotElementType.Transition, PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask};

            var lastrow = iWorksheet.LastRowUsed().RowNumber();
            for (int row = 2; row <= lastrow; row++)
            {
                var xlPlot = new xlPlotImport(row);
                //Label
                IXLCell cellE = iWorksheet.Cell(row, 5);
                string contentsE = cellE.Value.ToString();
                if (contentsE != null && !contentsE.Contains(" "))
                {
                    xlPlot.Label = contentsE;
                }
                else
                {
                    xlPlot.ErrorReason = "Label invalid.  It must not contain spaces.";
                    faillist.Add(xlPlot);
                    continue;
                }

                //Mod
                IXLCell cellB = iWorksheet.Cell(row, 2);
                string contentsB = cellB.Value.ToString();
                var modPE = modList.FirstOrDefault(d => d.Label == contentsB);
                if (contentsB != null && modPE != null)
                {
                    xlPlot.ModDb = PlotDatabases.GetDatabaseContainingElement(modPE, CurrentGame) as ModPlotDatabase;
                }
                else
                {
                    xlPlot.ErrorReason = "Mod not found in database.";
                    faillist.Add(xlPlot);
                    continue;
                }

                //Action
                IXLCell cellA = iWorksheet.Cell(row, 1);
                string contentsA = cellA.Value.ToString();
                if(Enum.TryParse(contentsA, out xlImportAction action))
                {
                    xlPlot.Action = action;
                }
                else
                {
                    xlPlot.ErrorReason = "Invalid action";
                    faillist.Add(xlPlot);
                    continue;
                }

                //Type
                IXLCell cellC = iWorksheet.Cell(row, 3);
                string contentsC = cellC.Value.ToString();
                if (Enum.TryParse(contentsC, out PlotElementType type) &&  validtypes.Contains(type))
                {
                    xlPlot.Type = type;
                }
                else
                {
                    xlPlot.ErrorReason = "Invalid Type";
                    faillist.Add(xlPlot);
                    continue;
                }

                //Path
                IXLCell cellD = iWorksheet.Cell(row, 4);
                string contentsD = cellD.Value.ToString();
                var pathArray = contentsD.Split(".").ToList();
                PlotElement parent = null;
                if (pathArray != null && IsModPathValid(modPE, pathArray, out parent))
                {
                    xlPlot.ParentPath = contentsD;
                    xlPlot.Parent = parent;
                }
                else
                {
                    xlPlot.ErrorReason = "Invalid ParentPath.  It must start with the mod and consist of categories that have either been previously created or are higher up on the sheet, seperated by periods.";
                    faillist.Add(xlPlot);
                    continue;
                }
                var target = parent.Children.FirstOrDefault(p => p.Label == xlPlot.Label);
                if(target == null && xlPlot.Action != xlImportAction.Add)
                {
                    xlPlot.ErrorReason = "Target not found. Update and Deletion actions must point to an existing plot state.";
                    faillist.Add(xlPlot);
                    continue;
                }

                //PlotId
                IXLCell cellF = iWorksheet.Cell(row, 6);
                string contentsF = cellF.Value.ToString();
                bool invalidPlot = true;
                string ploterror = "Invalid Plot.";
                if (contentsF != null && int.TryParse(contentsF, out int plotid))
                {
                    switch(xlPlot.Type)
                    {
                        case PlotElementType.State:
                        case PlotElementType.SubState:
                            var b = PlotDatabases.FindPlotBoolByID(plotid, CurrentGame);
                            if (b != null && xlPlot.Action == xlImportAction.Add)
                                ploterror = "State already exists. Cannot be duplicated";
                            else
                            {
                                xlPlot.PlotID = plotid;
                                invalidPlot = false;
                            }
                            break;
                        case PlotElementType.Integer:
                            var i = PlotDatabases.FindPlotIntByID(plotid, CurrentGame);
                            if (i != null && xlPlot.Action == xlImportAction.Add)
                                ploterror = "Integer already exists. Cannot be duplicated";
                            else
                            {
                                xlPlot.PlotID = plotid;
                                invalidPlot = false;
                            }
                            break;
                        case PlotElementType.Float:
                            var f = PlotDatabases.FindPlotFloatByID(plotid, CurrentGame);
                            if (f != null && xlPlot.Action == xlImportAction.Add)
                                ploterror = "Float already exists. Cannot be duplicated";
                            else
                            {
                                xlPlot.PlotID = plotid;
                                invalidPlot = false;
                            }
                            break;
                        case PlotElementType.Conditional:
                            var c = PlotDatabases.FindPlotConditionalByID(plotid, CurrentGame);
                            if (c != null && xlPlot.Action == xlImportAction.Add)
                                ploterror = "Conditional already exists. Cannot be duplicated";
                            else
                            {
                                xlPlot.PlotID = plotid;
                                invalidPlot = false;
                            }
                            break;
                        case PlotElementType.Transition:
                            var t = PlotDatabases.FindPlotConditionalByID(plotid, CurrentGame);
                            if (t != null && xlPlot.Action == xlImportAction.Add)
                                ploterror = "Transition already exists. Cannot be duplicated";
                            else
                            {
                                xlPlot.PlotID = plotid;
                                invalidPlot = false;
                            }
                            break;
                        default: //ignore organisational always -1
                            xlPlot.PlotID = -1;
                            invalidPlot = false;
                            break;
                    }
                }

                if(invalidPlot)
                {
                    xlPlot.ErrorReason = ploterror;
                    faillist.Add(xlPlot);
                    continue;
                }
            
                //Code
                if(xlPlot.Type == PlotElementType.Conditional)
                {
                    IXLCell cellG = iWorksheet.Cell(row, 7);
                    string contentsG = cellG.Value.ToString();
                    if (contentsG != null && contentsG.Length > 350)
                        contentsG.Substring(0, 350);
                    xlPlot.Code = contentsG;
                }

                //Argument
                if (xlPlot.Type == PlotElementType.Transition)
                {
                    IXLCell cellH = iWorksheet.Cell(row, 8);
                    string contentsH = cellH.Value.ToString();
                    if (contentsH != null)
                    {
                        xlPlot.Argument = contentsH;
                    }
                }

                //Bool states
                if (xlPlot.Type == PlotElementType.State || xlPlot.Type == PlotElementType.SubState)
                {
                    IXLCell cellI = iWorksheet.Cell(row, 9);
                    string contentsI = cellI.Value.ToString();
                    if (contentsI != null && Enum.TryParse(contentsI, out PlotElementType st))
                    {
                        xlPlot.SubType = st;
                    }
                    IXLCell cellJ = iWorksheet.Cell(row, 10);
                    string contentsJ = cellJ.Value.ToString();
                    if (contentsJ != null && int.TryParse(contentsJ, out int gv))
                    {
                        xlPlot.GamerVariable = gv;
                    }
                    IXLCell cellK = iWorksheet.Cell(row, 11);
                    string contentsK = cellK.Value.ToString();
                    if (contentsK != null && int.TryParse(contentsK, out int achid))
                    {
                        xlPlot.AchievementID = achid;
                    }
                    IXLCell cellL = iWorksheet.Cell(row, 12);
                    string contentsL = cellL.Value.ToString();
                    if (contentsL != null && int.TryParse(contentsL, out int gaw))
                    {
                        xlPlot.GalaxyAtWar = gaw;
                    }
                }

                //STEP 3 - immediately action category changes and all deletions
                //If add category then do immediately
                if(xlPlot.Type == PlotElementType.Category && xlPlot.Action == xlImportAction.Add)
                {
                    if(parent.Children.FirstOrDefault(c => c.Label == xlPlot.Label) != null)
                    {
                        xlPlot.ErrorReason = "Update failed. Duplicate Category exists.";
                        faillist.Add(xlPlot);
                        continue;
                    }
                    var newModCatPE = new PlotElement(-1, xlPlot.ModDb.GetNextElementId(), xlPlot.Label, PlotElementType.Category, parent, new List<PlotElement>());
                    xlPlot.ModDb.Organizational.Add(newModCatPE.ElementId, newModCatPE);
                    continue;
                }

                //Update category labels
                if (xlPlot.Type == PlotElementType.Category && xlPlot.Action == xlImportAction.Update)
                {
                    IXLCell cellM = iWorksheet.Cell(row, 13);
                    string contentsM = cellM.Value.ToString();
                    if (contentsM != null && !contentsM.IsEmpty())
                    {
                        xlPlot.ModDb.Organizational[target.ElementId].Label = contentsM;
                    }
                    else
                    {
                        xlPlot.ErrorReason = "Update failed. NewLabel was empty";
                        faillist.Add(xlPlot);
                    }
                    continue;
                }

                //check valid action - Delete must have matching Mod, Type, Path, Label and plotID
                if (xlPlot.Action == xlImportAction.Delete)
                {
                    if(!target.Children.IsEmpty())
                    {
                        xlPlot.ErrorReason = "Deletion failed. Target has dependent plot states.";
                        faillist.Add(xlPlot);
                        continue;
                    }
                    if(target != null && target.Label == xlPlot.Label && parent.Path.Substring(13) == xlPlot.ParentPath && xlPlot.PlotID == target.PlotId)
                    {
                        xlPlot.ModDb.RemoveElement(target);
                    }
                    else
                    {
                        xlPlot.ErrorReason = "Deletion failed. Could not find existing element to remove.";
                        faillist.Add(xlPlot);
                    }
                    continue;
                }

                //If reached here then xlPlot is valid and should be queued up for when all deletions and category events have occured
                changes.Enqueue(xlPlot);
            }
            //Step 4: In order update plots
            while (!changes.IsEmpty())
            {
                var import = changes.Dequeue();
                bool success = false;
                switch (import.Action)
                {
                    case xlImportAction.Add:
                        int newElementId = import.ModDb.GetNextElementId();
                        success = AddVerifiedPlotState(import.Type, import.Parent, import.PlotID, newElementId, import.Label, new List<PlotElement>(), false, null, import.SubType, import.AchievementID, import.GamerVariable,
                            import.GalaxyAtWar, import.Code, import.Argument);
                        break;
                    case xlImportAction.Update:
                        var target = import.Parent.Children.FirstOrDefault(p => p.Label == import.Label);
                        success = AddVerifiedPlotState(import.Type, import.Parent, import.PlotID, target.ElementId, import.Label, new List<PlotElement>(), true, target, import.SubType, import.AchievementID, import.GamerVariable,
                            import.GalaxyAtWar, import.Code, import.Argument);
                        break;
                    case xlImportAction.Delete: //should not happen as valid deletions should already have occurred.
                        break;
                }
                if (!success)
                {
                    import.ErrorReason = "Failed during database addition.";
                    faillist.Add(import);
                }
                else
                {
                    NeedsSave = true;
                }
            }

            var errors = new List<string>();
            foreach(var f in faillist)
            {
                string plt = null;
                if (f.PlotID > 0)
                    plt = " - " + f.PlotID.ToString() + " ";
                string s = "Row: " + f.Row.ToString() + " - " + f.Label + " - " + f.Type.ToString() + plt + "\n" + f.ErrorReason;
                errors.Add(s);
            }
            RefreshTrees();
            IsBusy = false;
            if (Enumerable.Any(errors))
            {
                ListDialog ld = new ListDialog(errors, "Plot import failures",
                        "The following items failed to import:", this);
                ld.Show();
            }
            else
            {
                MessageBox.Show("All plot states were successfully imported.", "Plot Database");
            }
        }

        private bool IsModPathValid(PlotElement element, List<string> path, out PlotElement target)
        {
            target = null;
            if (element.Label != path[0])
                return false;
            path.RemoveAt(0);
            if (path.IsEmpty())
            {
                target = element;
                return true;
            }
            var nextPE = element.Children.FirstOrDefault(c => c.Label == path[0]);
            if (nextPE == null)
                return false;
            return IsModPathValid(nextPE, path, out target);
        }

        private class xlPlotImport
        {
            public int Row { get; set; }
            public xlImportAction Action { get; set; }

            public ModPlotDatabase ModDb { get; set; }
            public PlotElementType Type { get; set; }
            public string ParentPath { get; set; }
            public string Label { get; set; }
            public int PlotID { get; set; }
            public string Code { get; set; }
            public string Argument { get; set; }
            public PlotElementType SubType { get; set; } = PlotElementType.None;
            public int GamerVariable { get; set; } = -1;
            public int AchievementID { get; set; } = -1;
            public int GalaxyAtWar { get; set; } = -1;
            public string ErrorReason { get; set; }
            public PlotElement Parent { get; set; }
            public xlPlotImport(int row) { Row = row; }
        }
        public enum xlImportAction
        {
            Add = 0,
            Update = 1,
            Delete = 2
        }

        private void ExportDataToExcel()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "Excel spreadsheet|*.xlsx"
            };
            if (d.ShowDialog() == true)
            {
                var msg = MessageBox.Show($"Do you want to write the current branch to\n{d.FileName}?", "Plot Database", MessageBoxButton.OKCancel);
                if (msg == MessageBoxResult.Cancel)
                    return;

                var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add($"Plots_{CurrentGame}");

                //write labels
                var headers = new List<string>()
                {
                    "Action", //1
                    "Mod",
                    "Type",
                    "ParentPath", //4
                    "Label",
                    "PlotID",
                    "Code", 
                    "Argument",  //8
                    "SubType",
                    "GamerVariable",
                    "AchievementID",
                    "GalaxyAtWar",
                    "NewLabel"

                };
                for (int colindex = 0; colindex < headers.Count; colindex++)
                {
                    worksheet.Cell(1, colindex + 1).Value = headers[colindex];
                }

                //Write rows
                var rawlist = new List<PlotElement>();
                var catlist = new List<PlotElement>();
                var plotlist = new List<PlotElement>();
                AddPlotsToList(SelectedNode, rawlist, true);
                foreach (PlotElement pe in rawlist)
                {

                    if (pe.ElementId <= 100000 || pe.Type == PlotElementType.Mod)
                        continue;
                    if (pe.Type == PlotElementType.Category)
                    {
                        catlist.Add(pe);
                    }
                    else
                    {
                        plotlist.Add(pe);
                    }
                }

                var exportlist = new List<PlotElement>();
                exportlist.AddRange(catlist);
                exportlist.AddRange(plotlist);

                for (int r = 0; r < exportlist.Count; r++)
                {
                    string modname = GetCurrentMod(exportlist[r]);
                    if (modname == "N/A") //Export is not import
                        continue;
                    worksheet.Cell(r + 2, 1).Value = "";
                    worksheet.Cell(r + 2, 2).Value = modname;
                    worksheet.Cell(r + 2, 3).Value = exportlist[r].Type.ToString();
                    worksheet.Cell(r + 2, 4).Value = exportlist[r].Parent.Path.Length > 13 ? exportlist[r].Parent.Path.Remove(0, 13) : "";
                    worksheet.Cell(r + 2, 5).Value = exportlist[r].Label;
                    worksheet.Cell(r + 2, 6).Value = exportlist[r].PlotId;
                    switch (exportlist[r].Type)
                    {
                        case PlotElementType.State:
                        case PlotElementType.SubState:
                            var plotBool = exportlist[r] as PlotBool;
                            worksheet.Cell(r + 2, 9).Value = plotBool.SubType.ToString();
                            worksheet.Cell(r + 2, 10).Value = plotBool.GamerVariable.ToString();
                            worksheet.Cell(r + 2, 11).Value = plotBool.AchievementID.ToString();
                            worksheet.Cell(r + 2, 12).Value = plotBool.GalaxyAtWar.ToString();
                            break;
                        case PlotElementType.Conditional:
                            var plotCnd = exportlist[r] as PlotConditional;
                            worksheet.Cell(r + 2, 7).Value = plotCnd.Code;
                            break;
                        case PlotElementType.Transition:
                            var plotTrans = exportlist[r] as PlotTransition;
                            worksheet.Cell(r + 2, 8).Value = plotTrans.Argument;
                            break;
                        default:
                            break;
                    }
                }

                workbook.SaveAs(d.FileName);
                MessageBox.Show("Done");


            }
        }


        private string GetCurrentMod(PlotElement pe)
        {
            if (pe == null)
                return "N/A";
            string modname;
            if(pe.Type == PlotElementType.Mod)
            {
                 modname = pe.Label;
            }
            else
            {
                modname = GetCurrentMod(pe.Parent);
                
            }
            if(modname == null)
            {
                modname = "N/A";
            }
            return modname;
        }
        #endregion
    }

    [ValueConversion(typeof(IEntry), typeof(string))]
    public class PlotElementTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlotElementType elementType)
            {
                if(parameter == null)
                {
                    switch (elementType)
                    {
                        case PlotElementType.Conditional:
                            return "/Tools/PlotDatabase/PlotTypeIcons/icon_cnd.png";
                        case PlotElementType.Consequence:
                        case PlotElementType.Flag:
                        case PlotElementType.State:
                        case PlotElementType.SubState:
                            return "/Tools/PlotDatabase/PlotTypeIcons/icon_bool.png";
                        case PlotElementType.Float:
                            return "/Tools/PlotDatabase/PlotTypeIcons/icon_float.png";
                        case PlotElementType.Integer:
                            return "/Tools/PlotDatabase/PlotTypeIcons/icon_int.png";
                        case PlotElementType.JournalGoal:
                        case PlotElementType.JournalItem:
                        case PlotElementType.JournalTask:
                            return "/Tools/PackageEditor/ExportIcons/icon_world.png";
                        case PlotElementType.FlagGroup:
                        case PlotElementType.None:
                        case PlotElementType.Plot:
                        case PlotElementType.Region:
                        case PlotElementType.Category:
                            return "/Tools/PackageEditor/ExportIcons/icon_package.png";
                        case PlotElementType.Transition:
                            return "/Tools/PackageEditor/ExportIcons/icon_function.png";
                        case PlotElementType.Mod:
                            return "/Tools/PackageEditor/ExportIcons/icon_package_fileroot.png";
                        default:
                            break;
                    }
                }
                else if (parameter.ToString() == "Description")
                {
                    return PlotElementTypeExtensions.GetDescription(elementType);
                }

            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }

    }
}
