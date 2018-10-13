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

namespace ME3Explorer.PackageEditorWPFControls
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class TreeMergeDialog : Window
    {
        public enum PortingOption
        {
            AddTreeAsChild,
            AddSingularAsChild,
            ReplaceSingular,
            MergeTreeChildren,
            Cancel
        }
        public PortingOption PortingOptionChosen;

        public TreeMergeDialog()
        {
            InitializeComponent();
        }

        public static object GetMergeType(Window w, TreeViewEntry sourceItem, TreeViewEntry targetItem)
        {
            TreeMergeDialog tmd = new TreeMergeDialog();
            tmd.Owner = w;
            tmd.ShowDialog(); //modal

            return tmd.PortingOptionChosen;
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.MergeTreeChildren;
            Close();
        }

        private void CloneTreeButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.AddTreeAsChild;
            Close();

        }

        private void AddSingularButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.AddSingularAsChild;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.Cancel;
            Close();
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            PortingOptionChosen = PortingOption.ReplaceSingular;
            Close();
        }
    }
}
