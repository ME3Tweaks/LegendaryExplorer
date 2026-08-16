using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.ToolsetDev.ShaderComparer
{
    public partial class ShaderDifferencesWindow : Window
    {
        private readonly Action<string> _onSelect;

        public ShaderDifferencesWindow(IEnumerable<string> items, Action<string> onSelect)
        {
            InitializeComponent();
            _onSelect = onSelect;
            ListBoxShaders.ItemsSource = items;
        }

        private void ListBoxShaders_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListBoxShaders.SelectedItem is string s)
            {
                _onSelect?.Invoke(s);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
