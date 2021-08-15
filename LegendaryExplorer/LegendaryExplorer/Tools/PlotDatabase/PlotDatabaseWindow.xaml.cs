using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;
using System.Threading;
using Newtonsoft.Json;
using System.Globalization;
using System.ComponentModel;
using Microsoft.Win32;
using ClosedXML.Excel;
using LegendaryExplorer.Dialogs;

namespace LegendaryExplorer.Tools.PlotManager
{
    /// <summary>
    /// Interaction logic for PlotManagerWindow.xaml
    /// </summary>
    public partial class PlotManagerWindow : NotifyPropertyChangedWindowBase
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
        public PlotDatabase modDB = new PlotDatabase();
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

        public bool IsModRoot() => SelectedNode?.ElementId == 100000;
        public bool IsMod() => SelectedNode?.Type == PlotElementType.Mod;
        public bool CanAddCategory() => SelectedNode?.Type == PlotElementType.Mod || SelectedNode?.Type == PlotElementType.Category;
        public bool CanEditItem() => SelectedNode?.ElementId > 100000;
        public bool CanDeleteItem() => SelectedNode?.ElementId > 100000 && SelectedNode.Children.IsEmpty();
        public bool CanAddItem() => SelectedNode?.Type == PlotElementType.Category;
        public bool CanExportTable() => SelectedNode != null;
        #endregion

        #region PDBInitialization
        public PlotManagerWindow()
        {

            LoadCommands();
            InitializeComponent();

            var rootlist3 = new List<PlotElement>();
            var dictionary3 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3);
            rootlist3.Add(dictionary3[1]);
            if (PlotDatabases.LoadDatabase(MEGame.LE3, false, AppDirectories.AppDataFolder))
            {
                var mods3 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3, false);
                rootlist3.Add(mods3[100000]);
            }
            dictionary3.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 3 Plots", PlotElementType.None, -1, rootlist3, null));
            dictionary3[0].Children[0].Parent = dictionary3[0];
            dictionary3[0].Children[1].Parent = dictionary3[0];
            RootNodes3.Add(dictionary3[0]);

            RootNodes2.ClearEx();
            var rootlist2 = new List<PlotElement>();
            var dictionary2 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE2);
            rootlist2.Add(dictionary2[1]);
            if (PlotDatabases.LoadDatabase(MEGame.LE2, false, AppDirectories.AppDataFolder))
            {
                var mods2 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE2, false);
                rootlist2.Add(mods2[100000]);
            }
            dictionary2.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 2 Plots", PlotElementType.None, 0, rootlist2, null));
            dictionary2[0].Children[0].Parent = dictionary2[0];
            dictionary2[0].Children[1].Parent = dictionary2[0];
            RootNodes2.Add(dictionary2[0]);

            RootNodes1.ClearEx();
            var rootlist1 = new List<PlotElement>();
            var dictionary1 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE1);
            rootlist1.Add(dictionary1[1]);
            if (PlotDatabases.LoadDatabase(MEGame.LE1, false, AppDirectories.AppDataFolder))
            {
                var mods1 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE1, false);
                rootlist1.Add(mods1[100000]);
            }
            dictionary1.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 1 Plots", PlotElementType.None, -1, rootlist1, null));
            dictionary3[0].Children[0].Parent = dictionary3[0];
            dictionary3[0].Children[1].Parent = dictionary3[0];
            RootNodes1.Add(dictionary1[0]);
            Focus();
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
        }

        private void PlotDB_Loaded(object sender, RoutedEventArgs e)
        {
            var plotenum = Enum.GetNames(typeof(PlotElementType)).ToList();
            newItem_subtype.ItemsSource = plotenum;
            CurrentGame = MEGame.LE3;
            modDB = PlotDatabases.Le3ModDatabase;
            SelectedNode = RootNodes3[0];
            SetFocusByPlotElement(RootNodes3[0]);
        }

        private void PlotDB_Closing(object sender, CancelEventArgs e)
        {
            if (NeedsSave)
            {
                var dlg = MessageBox.Show("Changes have been made to the modding database. Save now?", "Plot Database", MessageBoxButton.YesNo);
                if (dlg == MessageBoxResult.Yes)
                {
                    NeedsSave = false;
                    CurrentGame = MEGame.LE3;
                    SaveModDB();
                    CurrentGame = MEGame.LE2;
                    SaveModDB();
                    CurrentGame = MEGame.LE1;
                    SaveModDB();
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
            switch(CurrentGame)
            {
                case MEGame.LE3:
                    RootNodes3.ClearEx();
                    var rootlist3 = new List<PlotElement>();
                    var dictionary3 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3);
                    rootlist3.Add(dictionary3[1]);
                    var mods3 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3, false);
                    rootlist3.Add(mods3[100000]);
                    dictionary3.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 3 Plots", PlotElementType.None, -1, rootlist3));
                    RootNodes3.Add(dictionary3[0]);
                    break;
                case MEGame.LE2:
                    RootNodes2.ClearEx();
                    var rootlist2 = new List<PlotElement>();
                    var dictionary2 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE2);
                    rootlist2.Add(dictionary2[1]);
                    var mods2 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE2, false);
                    rootlist2.Add(mods2[100000]);
                    dictionary2.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 2 Plots", PlotElementType.None, -1, rootlist2));
                    RootNodes2.Add(dictionary2[0]);
                    break;
                case MEGame.LE1:
                    RootNodes1.ClearEx();
                    var rootlist1 = new List<PlotElement>();
                    var dictionary1 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE1);
                    rootlist1.Add(dictionary1[1]);
                    var mods1 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE1, false);
                    rootlist1.Add(mods1[100000]);
                    dictionary1.Add(0, new PlotElement(0, 0, "Legendary Edition - Mass Effect 1 Plots", PlotElementType.None, -1, rootlist1));
                    RootNodes1.Add(dictionary1[0]);
                    break;
                default:
                    break;
            }
        }

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
                    AddPlotsToList(c, elementList);
                }
            }
        }

        private void Tree_BW3_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            updateTable = true;
            SelectedNode = Tree_BW3.SelectedItem as PlotElement;
        }

        private void Tree_BW2_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            updateTable = true;
            SelectedNode = Tree_BW2.SelectedItem as PlotElement;
        }

        private void Tree_BW1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            updateTable = true;
            SelectedNode = Tree_BW1.SelectedItem as PlotElement;
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
                switch (CurrentView)
                {
                    case 1:
                        CurrentGame = MEGame.LE2;
                        modDB = PlotDatabases.Le2ModDatabase;
                        if (Tree_BW2.SelectedItem == null)
                        {
                            SelectedNode = RootNodes2[0];
                            //Tree_BW2.SelectItem(RootNodes2[0]);  //Not a treeviewItem
                            SetFocusByPlotElement(RootNodes2[0]);
                            //SetFocusByElementId(0); Won't work because scan is from zero element up
                        }
                        break;
                    case 2:
                        CurrentGame = MEGame.LE1;
                        modDB = PlotDatabases.Le1ModDatabase;
                        //if (Tree_BW1.SelectedItem == null)
                        //SetFocusByPlotElement(RootNodes1[0]);
                        break;
                    default:
                        CurrentGame = MEGame.LE3;
                        modDB = PlotDatabases.Le3ModDatabase;
                        //if (Tree_BW3.SelectedItem == null)
                        //SetFocusByPlotElement(RootNodes3[0]);
                        break;
                }
            }
        }

        #endregion

        #region TreeView

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
            var tree = new TreeView();
            switch (CurrentGame)
            {
                case MEGame.LE2:
                    tree = Tree_BW2;
                    break;
                case MEGame.LE1:
                    tree = Tree_BW1;
                    break;
                default:
                    tree = Tree_BW3;
                    break;
            }

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

                    string primarySort;
                    ICollectionView linedataView = CollectionViewSource.GetDefaultView(LV_Plots.ItemsSource);
                    primarySort = headerClicked.Column.Header.ToString();
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

            modDB.LoadPlotsFromJSON(CurrentGame, false);
            RefreshTrees();
        }

        private async void SaveModDB()
        {
            bwLink.Visibility = Visibility.Collapsed;
            var statustxt = CurrentOverallOperationText.ToString();
            CurrentOverallOperationText = "Saving...";
            switch (CurrentGame)
            {
                case MEGame.LE3:
                    PlotDatabases.Le3ModDatabase.SaveDatabaseToFile(AppDirectories.AppDataFolder);
                    CurrentOverallOperationText = "Saved LE3 Mod Database Locally...";
                    break;
                case MEGame.LE2:
                    PlotDatabases.Le2ModDatabase.SaveDatabaseToFile(AppDirectories.AppDataFolder);
                    CurrentOverallOperationText = "Saved LE2 Mod Database Locally...";
                    break;
                case MEGame.LE1:
                    PlotDatabases.Le1ModDatabase.SaveDatabaseToFile(AppDirectories.AppDataFolder);
                    CurrentOverallOperationText = "Saved LE1 Mod Database Locally...";
                    break;
            }
            if (NeedsSave) //don't do this on exit
            {
                await Task.Delay(TimeSpan.FromSeconds(1.0));
            }
            CurrentOverallOperationText = statustxt;
            bwLink.Visibility = Visibility.Visible;
            NeedsSave = false;
        }

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
                var mdb = new PlotDatabase();
                switch (CurrentGame)
                {
                    case MEGame.LE1:
                        mdb = PlotDatabases.Le1ModDatabase;
                        break;
                    case MEGame.LE2:
                        mdb = PlotDatabases.Le2ModDatabase;
                        break;
                    default:
                        mdb = PlotDatabases.Le3ModDatabase;
                        break;
                }
                int newElementId = SelectedNode.ElementId;
                if (!isEditing)
                {
                    newElementId = mdb.GetNextElementId();
                }
                var parent = SelectedNode;
                var childlist = new List<PlotElement>();
                PlotElement target = null;
                if (isEditing)
                {
                    parent = SelectedNode.Parent;
                    childlist.AddRange(SelectedNode.Children);
                    target = SelectedNode;
                }
                switch (command)
                {
                    case "NewMod":
                        var modname = newMod_Name.Text;
                        if (modname == null || modname.Contains(" "))
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
                            int parentId = 100000;
                            parent = mdb.Organizational[parentId];
                            var mods = parent.Children.ToList();
                            foreach (var mod in mods)
                            {
                                if (mod.Label == modname)
                                {
                                    MessageBox.Show($"Mod '{modname}' already exists in the database.  Please use another name.", "Invalid Name");
                                    CancelAddData(command);
                                    return;
                                }
                            }
                            var newModPE = new PlotElement(-1, newElementId, modname, PlotElementType.Mod, parentId, childlist, parent);
                            mdb.Organizational.Add(newElementId, newModPE);
                            parent.Children.Add(newModPE);
                        }
                        else
                        {
                            mdb.Organizational[SelectedNode.ElementId].Label = newMod_Name.Text;
                        }

                        NeedsSave = true;
                        break;
                    case "NewCategory":
                        if (newCat_Name.Text == null || newCat_Name.Text.Contains(" "))
                        {
                            MessageBox.Show($"Label is empty or contains a space.\nPlease add a valid label, using underscore '_' for spaces.", "Invalid Label");
                            return;
                        }
                        if(!isEditing)
                        {
                            var newModCatPE = new PlotElement(-1, newElementId, newCat_Name.Text, PlotElementType.Category, parent.ElementId, new List<PlotElement>(), parent);
                            parent.Children.Add(newModCatPE);
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
                        if (nameItem == null || nameItem.Contains(" "))
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
                        var newModItemPE = new PlotElement(newPlotId, newElementId, nameItem, type, parent.ElementId, newchildren, parent);
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
                                if(newItem_subtype.SelectedItem != null)
                                    newST = (PlotElementType)newItem_subtype.SelectedItem;
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
                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target, newST, newAchID, newGVarID, newGAWID);
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
                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target);
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

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target);
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

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target, PlotElementType.None, -1, -1, -1, newItem_Code.Text);
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

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target, PlotElementType.None, -1, -1, -1, null, newItem_Argument.Text);
                                break;
                            default:   //PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask, PlotElementType.Consequence, PlotElementType.Flag

                                AddVerifiedPlotState(type, parent, newPlotId, newElementId, nameItem, childlist, isEditing, target);
                                break;
                        }
                        NeedsSave = true;
                        break;
                    default:
                        break;
                }
            }

            RevertPanelsToDefault();
            RefreshTrees();
        }

        private bool AddVerifiedPlotState(PlotElementType type, PlotElement parent, int newPlotId, int newElementId, string label, List<PlotElement> newchildren, bool update = false, 
            PlotElement target = null, PlotElementType subtype = PlotElementType.None, int achievementId = -1, int gv = -1, int gaw = -1, string code = null, string argu = null)  //Game state must be verified
        {
            if (update && target == null)
                return false;
            var newModItemPE = new PlotElement(newPlotId, newElementId, label, type, parent.ElementId, newchildren, parent);
            try
            {
                switch (type)
                {
                    case PlotElementType.State:
                    case PlotElementType.SubState:
                        if (update)
                        {
                            parent.Children.Remove(target);
                            modDB.Bools.Remove(target.PlotId);
                        }
                        var newModBool = new PlotBool(newPlotId, newElementId, label, type, parent.ElementId, newchildren, parent);
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
                        modDB.Bools.Add(newPlotId, newModBool);
                        parent.Children.Add(newModBool);
                        break;
                    case PlotElementType.Integer:
                        if (update)
                        {
                            parent.Children.Remove(target);
                            modDB.Ints.Remove(target.PlotId);
                        }
                        modDB.Ints.Add(newPlotId, newModItemPE);
                        parent.Children.Add(newModItemPE);
                        break;
                    case PlotElementType.Float:
                        if (update)
                        {
                            parent.Children.Remove(target);
                            modDB.Floats.Remove(target.PlotId);
                        }
                        modDB.Floats.Add(newPlotId, newModItemPE);
                        parent.Children.Add(newModItemPE);
                        break;
                    case PlotElementType.Conditional:
                        if (update)
                        {
                            parent.Children.Remove(target);
                            modDB.Conditionals.Remove(target.PlotId);
                        }

                        var newModCnd = new PlotConditional(newPlotId, newElementId, label, type, parent.ElementId, new List<PlotElement>(), parent);
                        if(code != null)
                        {
                            newModCnd.Code = code;
                        }
                        modDB.Conditionals.Add(newPlotId, newModCnd);
                        parent.Children.Add(newModCnd);
                        break;
                    case PlotElementType.Transition:
                        if (update)
                        {
                            parent.Children.Remove(SelectedNode);
                            modDB.Transitions.Remove(SelectedNode.PlotId);
                        }
                        var newModTrans = new PlotTransition(newPlotId, newElementId, label, type, parent.ElementId, new List<PlotElement>(), parent);
                        if(argu != null)
                        {
                            newModTrans.Argument = argu;
                        }
                        modDB.Transitions.Add(newPlotId, newModTrans);
                        parent.Children.Add(newModTrans);
                        break;
                    default:   //PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask, PlotElementType.Consequence, PlotElementType.Flag
                        if (update)
                        {
                            parent.Children.Remove(target);
                            modDB.Organizational.Remove(target.ElementId);
                        }
                        modDB.Organizational.Add(newElementId, newModItemPE);
                        parent.Children.Add(newModItemPE);
                        break;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DeleteNewModData()
        {
            if (SelectedNode.ElementId <= 100000)
            {
                MessageBox.Show("Cannot Delete Bioware plot states.");
                return;
            }
            if (!SelectedNode.Children.IsEmpty())
            {
                MessageBox.Show("This item has subitems.  Delete all the sub-items before deleting this.");
                return;
            }
            var dlg = MessageBox.Show($"Are you sure you wish to delete this item?\nType: {SelectedNode.Type}\nPlotId: {SelectedNode.PlotId}\nPath: {SelectedNode.Path}", "Plot Database", MessageBoxButton.OKCancel);
            if (dlg == MessageBoxResult.Cancel)
                return;
            var deleteId = SelectedNode.ElementId;
            var mdb = new PlotDatabase();
            switch (CurrentGame)
            {
                case MEGame.LE1:
                    mdb = PlotDatabases.Le1ModDatabase;
                    break;
                case MEGame.LE2:
                    mdb = PlotDatabases.Le2ModDatabase;
                    break;
                default:
                    mdb = PlotDatabases.Le3ModDatabase;
                    break;
            }
            mdb.RemoveFromParent(SelectedNode);
            switch (SelectedNode.Type)
            {
                case PlotElementType.State:
                case PlotElementType.SubState:
                    mdb.Bools.Remove(SelectedNode.PlotId);
                    break;
                case PlotElementType.Integer:
                    mdb.Ints.Remove(SelectedNode.PlotId);
                    break;
                case PlotElementType.Float:
                    mdb.Floats.Remove(SelectedNode.PlotId);
                    break;
                case PlotElementType.Conditional:
                    mdb.Conditionals.Remove(SelectedNode.PlotId);
                    break;
                case PlotElementType.Transition:
                    mdb.Transitions.Remove(SelectedNode.PlotId);
                    break;
                default:
                    mdb.Organizational.Remove(SelectedNode.ElementId);
                    break;
            }
            NeedsSave = true;
            RefreshTrees();
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
                Title = "Import Excel table"
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
            var modList = modDB.Organizational[100000].Children;
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
                    xlPlot.Mod = modPE;
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
                    var newModCatPE = new PlotElement(-1, modDB.GetNextElementId(), xlPlot.Label, PlotElementType.Category, parent.ElementId, new List<PlotElement>(), parent);
                    parent.Children.Add(newModCatPE);
                    modDB.Organizational.Add(newModCatPE.ElementId, newModCatPE);
                    continue;
                }

                //Update category labels
                if (xlPlot.Type == PlotElementType.Category && xlPlot.Action == xlImportAction.Update)
                {
                    IXLCell cellM = iWorksheet.Cell(row, 13);
                    string contentsM = cellM.Value.ToString();
                    if (contentsM != null)
                    {
                        modDB.Organizational[target.ElementId].Label = contentsM;
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
                        parent.Children.Remove(target);
                        switch (target.Type)
                        {
                            case PlotElementType.State:
                            case PlotElementType.SubState:
                                modDB.Bools.Remove(xlPlot.PlotID);
                                break;
                            case PlotElementType.Integer:
                                modDB.Ints.Remove(xlPlot.PlotID);
                                break;
                            case PlotElementType.Float:
                                modDB.Floats.Remove(xlPlot.PlotID);
                                break;
                            case PlotElementType.Conditional:
                                modDB.Conditionals.Remove(xlPlot.PlotID);
                                break;
                            case PlotElementType.Transition:
                                modDB.Transitions.Remove(xlPlot.PlotID);
                                break;
                            default:
                                modDB.Organizational.Remove(target.ElementId);
                                break;
                        }
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
                        int newElementId = modDB.GetNextElementId();
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
            public PlotElement Mod { get; set; }
            public PlotElementType Type { get; set; }
            public string? ParentPath { get; set; }
            public string Label { get; set; }
            public int PlotID { get; set; }
            public string? Code { get; set; }
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
                    "#",
                    "PlotId",
                    "Type",
                    "Label",
                    "Path",
                    "SubType",
                    "Code",
                    "Argument",
                    "GamerVariable",
                    "AchievementID",
                    "GalaxyAtWar"
                };
                for (int colindex = 0; colindex < headers.Count; colindex++)
                {
                    worksheet.Cell(1, colindex + 1).Value = headers[colindex];
                }

                //Write rows
                var plotlist = new List<PlotElement>();
                AddPlotsToList(SelectedNode, plotlist);
                for (int r = 0; r < plotlist.Count; r++)
                {
                    worksheet.Cell(r + 2, 1).Value = r.ToString();
                    worksheet.Cell(r + 2, 2).Value = plotlist[r].PlotId.ToString();
                    worksheet.Cell(r + 2, 3).Value = plotlist[r].Type.ToString();
                    worksheet.Cell(r + 2, 4).Value = plotlist[r].Label;
                    worksheet.Cell(r + 2, 5).Value = plotlist[r].Path;
                    switch(plotlist[r].Type)
                    {
                        case PlotElementType.State:
                        case PlotElementType.SubState:
                            var plotBool = plotlist[r] as PlotBool;
                            worksheet.Cell(r + 2, 6).Value = plotBool.SubType.ToString();
                            worksheet.Cell(r + 2, 9).Value = plotBool.GamerVariable.ToString();
                            worksheet.Cell(r + 2, 10).Value = plotBool.AchievementID.ToString();
                            worksheet.Cell(r + 2, 11).Value = plotBool.GalaxyAtWar.ToString();
                            break;
                        case PlotElementType.Conditional:
                            var plotCnd = plotlist[r] as PlotConditional;
                            worksheet.Cell(r + 2, 7).Value = plotCnd.Code;
                            break;
                        case PlotElementType.Transition:
                            var plotTrans = plotlist[r] as PlotTransition;
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
