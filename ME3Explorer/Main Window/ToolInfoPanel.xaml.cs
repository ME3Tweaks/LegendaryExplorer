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
    /// Interaction logic for ToolInfoPanel.xaml
    /// </summary>
    public partial class ToolInfoPanel : UserControl
    {
        public ToolInfoPanel()
        {
            InitializeComponent();
        }

        public void setTool(Tool tool)
        {
            this.DataContext = tool;
            if (tool.description?.Length > 315)
            {
                scrollIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                scrollIndicator.Visibility = Visibility.Hidden;
            }
        }
    }
}
