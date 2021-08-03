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
        public ICommand FilterCommand { get; set; }
        public ICommand CopyToClipboardCommand { get; set; }
        public ICommand RefreshLocalCommand { get; set; }
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
                        //if (Tree_BW1.SelectedItem == null)
                            //SetFocusByPlotElement(RootNodes1[0]);
                        break;
                    default:
                        CurrentGame = MEGame.LE3;
                        //if (Tree_BW3.SelectedItem == null)
                            //SetFocusByPlotElement(RootNodes3[0]);
                        break;
                }
            }
        }

        #endregion

        #region TreeView

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
                if(isEditing)
                {
                    parent = SelectedNode.Parent;
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
                            var newModPE = new PlotElement(-1, newElementId, modname, PlotElementType.Mod, parentId, new List<PlotElement>(), parent);
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
                                else
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Bools.Remove(SelectedNode.PlotId);
                                }
                                var newModBool = new PlotBool(newPlotId, newElementId, nameItem, type, parent.ElementId, newchildren, parent);
                                //subtype, gamervariable, achievementid, galaxyatwar
                                if(newItem_subtype.SelectedItem != null)
                                    newModBool.SubType = (PlotElementType)newItem_subtype.SelectedItem;

                                if (!newItem_achievementid.Text.IsEmpty())
                                {
                                    if (!int.TryParse(newItem_achievementid.Text, out int newAchID))
                                    {
                                        MessageBox.Show($"Achievement Id needs to be an integer.  Please review.", "Invalid Achievement Id");
                                        return;
                                    }
                                    newModBool.AchievementID = newAchID;
                                }

                                if (!newItem_galaxyatwar.Text.IsEmpty())
                                {
                                    if (!(int.TryParse(newItem_galaxyatwar.Text, out int newGAWID) && newGAWID > 0))
                                    {
                                        MessageBox.Show($"Galaxy at War Id needs to be a positive integer.  Please review.", "Invalid War Asset Id");
                                        return;
                                    }
                                    newModBool.GalaxyAtWar = newGAWID;
                                }
                                if (!newItem_galaxyatwar.Text.IsEmpty())
                                {
                                    if (!(int.TryParse(newItem_gamervariable.Text, out int newGVarID) && newGVarID > 0))
                                    {
                                        MessageBox.Show($"Gamer Variable Id needs to be a positive integer.  Please review.", "Invalid Gamer Variable Id");
                                        return;
                                    }
                                    newModBool.GamerVariable = newGVarID;
                                }
                                mdb.Bools.Add(newPlotId, newModBool);
                                parent.Children.Add(newModBool);
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
                                if(isEditing)
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Ints.Remove(SelectedNode.PlotId);
                                }
                                mdb.Ints.Add(newPlotId, newModItemPE);
                                parent.Children.Add(newModItemPE);
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
                                if(isEditing)
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Floats.Remove(SelectedNode.PlotId);
                                }
                                mdb.Floats.Add(newPlotId, newModItemPE);
                                parent.Children.Add(newModItemPE);
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
                                if (isEditing)
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Conditionals.Remove(SelectedNode.PlotId);
                                }

                                var newModCnd = new PlotConditional(newPlotId, newElementId, nameItem, type, parent.ElementId, new List<PlotElement>(), parent);
                                newModCnd.Code = newItem_Code.Text;
                                mdb.Conditionals.Add(newPlotId, newModCnd);
                                parent.Children.Add(newModCnd);
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
                                if (isEditing)
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Transitions.Remove(SelectedNode.PlotId);
                                }

                                var newModTrans = new PlotTransition(newPlotId, newElementId, nameItem, type, parent.ElementId, new List<PlotElement>(), parent);
                                newModTrans.Argument = newItem_Argument.Text;
                                mdb.Transitions.Add(newPlotId, newModTrans);
                                parent.Children.Add(newModTrans);
                                break;
                            default:   //PlotElementType.JournalGoal, PlotElementType.JournalItem, PlotElementType.JournalTask, PlotElementType.Consequence, PlotElementType.Flag
                                if (isEditing)
                                {
                                    newModItemPE.Children.AddRange(SelectedNode.Children);
                                    parent.Children.Remove(SelectedNode);
                                    mdb.Organizational.Remove(SelectedNode.ElementId);
                                }
                                mdb.Organizational.Add(newElementId, newModItemPE);
                                parent.Children.Add(newModItemPE);
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
            //Not yet implemented
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
