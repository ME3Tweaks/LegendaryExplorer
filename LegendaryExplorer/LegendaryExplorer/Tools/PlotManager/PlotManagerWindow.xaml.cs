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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        private int previousView { get; set; }
        private int _currentView;
        public int currentView { get => _currentView; set { previousView = _currentView; SetProperty(ref _currentView, value); } }
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
        

        private int tempTreeCount; //Temporary stop large table generation.
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
        }


        private void UpdateSelection()
        {
            tempTreeCount = 0;
            ElementsTable.ClearEx();
            tempTreeCount++;
            AddPlotsToList(SelectedNode, ElementsTable);
        }

        private void AddPlotsToList(PlotElement plotElement, ObservableCollectionExtended<PlotElement> elementList)
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
                    tempTreeCount++;
                    break;
            }

            foreach (var c in plotElement.Children)
            {
                if(tempTreeCount < 100)
                {
                    AddPlotsToList(c, elementList);
                }
                
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


    }


}
