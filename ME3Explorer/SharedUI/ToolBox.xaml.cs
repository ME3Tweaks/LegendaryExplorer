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
using ME3ExplorerCore.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ToolBox.xaml
    /// </summary>
    public partial class ToolBox : UserControl
    {
        private List<ClassInfo> _classes;
        public List<ClassInfo> Classes
        {
            get => _classes;
            set
            {
                _classes = value;
                searchBox.Clear();
                listView.ItemsSource = _classes;
            }
        }

        public Action<ClassInfo> DoubleClickCallback;

        public ToolBox()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void classInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 2 && sender is TextBlock tb && tb.DataContext is ClassInfo info)
            {
                DoubleClickCallback?.Invoke(info);
            }
        }

        private void SearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            listView.ItemsSource = Classes.Where(classInfo => classInfo.ClassName.Contains(newtext, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
