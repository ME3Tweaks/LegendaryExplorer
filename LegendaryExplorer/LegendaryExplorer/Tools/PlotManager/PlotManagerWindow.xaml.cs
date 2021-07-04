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
        public Dictionary<int, PlotElement> BW_ME3_Plots { get; } = new();
        

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
            var sortedPlots = BW_me3db.GetMasterDictionary();
            PlotElement root = sortedPlots[1];
            //Create Tree
            var rootNode = new TreeViewItem();
            rootNode.Header = root.Label;

            foreach(var e in root.Children)
            {
                var node = CreateChildTree(e);
                rootNode.Items.Add(node);
            }

            ME3_Tree_BW.Items.Add(rootNode);

        }

        private TreeViewItem CreateChildTree(PlotElement childelement)
        {
            var childItem = new TreeViewItem();

            switch (childelement.Type)
            {
                case PlotElementType.Plot:
                case PlotElementType.Region:
                    childItem.Header = childelement.Label;
                    break;
                default:
                    childItem.Header = $"{childelement.Label} {childelement.Type} {childelement.PlotID}";
                    break;
            }
            foreach (var e in childelement.Children)
            {
                var node = CreateChildTree(e);
                childItem.Items.Add(node);
            }
            return childItem;
        }
    }

  
}
