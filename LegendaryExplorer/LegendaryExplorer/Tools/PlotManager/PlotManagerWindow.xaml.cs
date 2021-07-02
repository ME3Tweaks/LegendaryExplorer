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

        public ObservableCollectionExtended<PlotElement> Elements { get; } = new();
        public ObservableCollectionExtended<PlotElement> RootNodes { get; } = new();

        private PlotElement _selectedNode;
        public PlotElement SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public PlotManagerWindow()
        {

            LoadCommands();
            InitializeComponent();

            var dictionary = PlotDatabases.GetMasterDictionaryForGame(MEGame.LE3);
            RootNodes.Add(dictionary[1]);

            Focus();
        }


        private void LoadCommands()
        {

        }

    }

  
}
