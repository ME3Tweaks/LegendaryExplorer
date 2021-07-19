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

        public ICommand FilterCommand { get; set; }
        public ICommand CopyToClipboardCommand { get; set; }

        #endregion

        #region PDBInitialization
        public PlotManagerWindow()
        {

            LoadCommands();
            InitializeComponent();

            var dictionary3 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3);
            RootNodes3.Add(dictionary3[1]);

            var dictionary2 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE2);
            RootNodes2.Add(dictionary2[1]);
            var dictionary1 = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE1);
            RootNodes1.Add(dictionary1[1]);
            Focus();
           
        }


        private void LoadCommands()
        {
            FilterCommand = new GenericCommand(Filter);
            CopyToClipboardCommand = new GenericCommand(CopyToClipboard);
        }

        private void UpdateSelection()
        {
            ElementsTable.ClearEx();
            var temptable = new List<PlotElement>();
            AddPlotsToList(SelectedNode, temptable);
            ElementsTable.AddRange(temptable);
        }

        private void AddPlotsToList(PlotElement plotElement, List<PlotElement> elementList)
        {
            switch (plotElement.Type)
            {
                case PlotElementType.Plot:
                case PlotElementType.Region:
                case PlotElementType.FlagGroup:
                case PlotElementType.None:
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

        private void Tree_BW3_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = Tree_BW3.SelectedItem as PlotElement;
            
        }

        private void Tree_BW2_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = Tree_BW2.SelectedItem as PlotElement;
        }

        private void Tree_BW1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = Tree_BW1.SelectedItem as PlotElement;
        }

        private void NewTab_Selected(object sender, SelectionChangedEventArgs e)
        {
            switch(CurrentView)
            {
                case 1:
                    CurrentGame = MEGame.LE2;
                    break;
                case 2:
                    CurrentGame = MEGame.LE1;
                    break;
                default:
                    CurrentGame = MEGame.LE3;
                    break;
            }
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
            if(showthis && !ShowBoolStates)
            {
                showthis = e.Type != PlotElementType.State && e.Type != PlotElementType.SubState;
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
            return showthis;
        }

        #endregion

        #region UserCommands
        private void CopyToClipboard()
        {
            var elmnt = (PlotElement)LV_Plots.SelectedItem;
            if(elmnt != null)
            {
                Clipboard.SetText(elmnt.PlotId.ToString());
            }
        }
        #endregion

    }


}
