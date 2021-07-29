using System.Windows;
using System.Windows.Controls;

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
