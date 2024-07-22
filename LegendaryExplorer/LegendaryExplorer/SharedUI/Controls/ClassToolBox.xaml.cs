using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.SharedUI.Controls
{
    /// <summary>
    /// UI toolbox for selecting a uclass
    /// </summary>
    public partial class ClassToolBox : NotifyPropertyChangedControlBase
    {
        public ObservableCollectionExtended<ClassInfo> Classes { get; set; } = new();

        private ClassInfo _selectedItem;

        public ClassInfo SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public Action<ClassInfo> DoubleClickCallback;
        public Action<ClassInfo> ShiftClickCallback;

        public ClassToolBox()
        {
            DataContext = this;
            InitializeComponent();
            Classes.CollectionChanged += ClassCollection_Changed;
        }

        private void classInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock {DataContext: ClassInfo info})
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    ShiftClickCallback?.Invoke(info);
                }
                else if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 2)
                {
                    DoubleClickCallback?.Invoke(info);
                }
            }
        }

        private void SearchBox_OnTextChanged(SearchBox sender, string newText) {
            listView.ItemsSource = Classes.Where(classInfo =>
                classInfo.ClassName.Contains(newText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ClassCollection_Changed(object sender,
            NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            listView.ItemsSource = Classes.Where(classInfo =>
                classInfo.ClassName.Contains(searchBox.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
