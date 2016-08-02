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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPaneInfoPanel.xaml
    /// </summary>
    public partial class TaskPaneInfoPanel : UserControl
    {
        public TaskPaneInfoPanel()
        {
            InitializeComponent();
        }

        GenericWindow current;

        public void setTool(GenericWindow gen)
        {
            this.DataContext = current = gen;
            screenShot.Source = gen.GetImage();
        }

        private void viewButton_Click(object sender, RoutedEventArgs e)
        {
            current?.BringToFront();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            current?.Close();
        }
    }
}
