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

        public PlotDatabase BW_me3db { get; } = new();



        public PlotManagerWindow()
        {

            LoadCommands();
            InitializeComponent();

            //me1BWdb.LoadBiowarePlotsFromJSON(MEGame.LE1);
            //me2BWdb.LoadBiowarePlotsFromJSON(MEGame.LE2);
            BW_me3db.LoadPlotsFromJSON(MEGame.LE3);
            InitializeTreeView();
            Focus();
        }


        private void LoadCommands()
        {

        }




        private void InitializeTreeView()
        {
            //Create Tree
            var rootNode = new TreeViewItem();
             

        }

        private TreeViewItem CreateChildTree(int childelementId)
        {
            var childItem = new TreeViewItem();

            return childItem;
        }
    }

  
}
