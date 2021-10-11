using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Dialogs
{
    public partial class DropdownPromptDialog : Window
    {
        public ObservableCollectionExtended<string> Items { get; } = new();
        public string Response => (string)Selection_Combobox.SelectedItem;

        public DropdownPromptDialog(string question, string title, string watermark, IEnumerable<string> items, Window owner)
        {
            Owner = owner;
            InitializeComponent();
            txtInfo.Text = question;
            Title = title;
            Selection_Combobox.Watermark = watermark;
            Items.AddRange(items);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}