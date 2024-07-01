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
    /// Toolbox that takes generic objects. Uses their .ToString() method for representation. Do not pass null items into this!
    /// </summary>
    public partial class GenericToolBox : NotifyPropertyChangedControlBase
    {
        public ObservableCollectionExtended<object> Items { get; set; } = new();

        private object _selectedItem;

        public object SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public Action<object> DoubleClickCallback;
        public Action<object> ShiftClickCallback;

        public GenericToolBox()
        {
            DataContext = this;
            InitializeComponent();
            Items.CollectionChanged += ItemsCollection_Changed;
        }

        private void SearchBox_OnTextChanged(SearchBox sender, string newText) {
            listView.ItemsSource = Items.Where(obj => obj.ToString().Contains(newText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ItemsCollection_Changed(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            listView.ItemsSource = Items.Where(obj => obj.ToString().Contains(searchBox.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void item_Mousedown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock { DataContext: object obj })
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    ShiftClickCallback?.Invoke(obj);
                }
                else if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 2)
                {
                    DoubleClickCallback?.Invoke(obj);
                }
            }
        }
    }
}
