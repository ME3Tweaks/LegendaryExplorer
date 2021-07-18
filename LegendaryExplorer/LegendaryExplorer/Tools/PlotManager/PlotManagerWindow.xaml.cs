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

        public ObservableCollectionExtended<PlotElement> Elements3 { get; } = new();
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

        private int tempTreeCount; //Temporary stop large table generation.
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

        }


        private void UpdateSelection()
        {
            tempTreeCount = 0;
            Elements3.ClearEx();
            tempTreeCount++;
            AddPlotsToList(SelectedNode, Elements3);
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
    }

  
}
