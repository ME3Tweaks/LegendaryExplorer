using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Helpers;

namespace LegendaryExplorer.SharedUI.Controls
{
    /// <summary>
    /// Interaction logic for ToolBox.xaml
    /// </summary>
    public partial class ToolBox : NotifyPropertyChangedControlBase
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

        private ClassInfo _selectedItem;

        public ClassInfo SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public Action<ClassInfo> DoubleClickCallback;

        public ToolBox()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void classInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 2 && sender is TextBlock {DataContext: ClassInfo info})
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
