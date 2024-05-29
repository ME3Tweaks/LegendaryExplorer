using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.SharedUI.Controls
{
    /// <summary>
    /// Interaction logic for InputComboBoxWPF.xaml
    /// </summary>
    public partial class InputComboBoxWPF : NotifyPropertyChangedWindowBase
    {
        private InputComboBoxWPF(Control owner, string promptText, string titleText, IEnumerable<object> items, string defaultValue = "", bool topMost = false)
        {
            DirectionsText = promptText;
            Topmost = topMost;
            Title = titleText;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            if (owner != null)
            {
                Owner = owner as Window ?? GetWindow(owner);
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            EntrySelector_ComboBox.ItemsSource = items;
            EntrySelector_ComboBox.SelectedItem = defaultValue;
            EntrySelector_ComboBox.Focus();
        }

        //private InputComboBoxWPF(Window owner, string promptText, string titleText, IEnumerable<PackageEditorWPF.IndexedName> items, string defaultValue = "", bool topMost = false)
        //{
        //    DirectionsText = promptText;
        //    Topmost = topMost;
        //    Owner = owner;
        //    Title = titleText;
        //    DataContext = this;
        //    LoadCommands();
        //    InitializeComponent();
        //    if (owner == null)
        //    {
        //        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        //    }
        //    EntrySelector_ComboBox.ItemsSource = items;
        //    EntrySelector_ComboBox.SelectedItem = defaultValue;
        //    EntrySelector_ComboBox.Focus();
        //}

        public static string GetValue(Control owner, string promptText, string titleText, IEnumerable<object> items, string defaultValue = "", bool topMost = false, Func<string> getDefaultValueFunc = null)
        {
            var dlg = new InputComboBoxWPF(owner, promptText, titleText, items, getDefaultValueFunc?.Invoke() ?? defaultValue, topMost);
            return dlg.ShowDialog() == true ? dlg.ChosenItem.ToString() : "";
        }

        //public static string GetValue(Window owner, string promptText, string titleText, IEnumerable<PackageEditorWPF.IndexedName> items, string defaultValue = "", bool topMost = false)
        //{
        //    var dlg = new InputComboBoxWPF(owner, promptText, titleText, items, defaultValue, topMost);
        //    return dlg.ShowDialog() == true ? dlg.ChosenItem : "";
        //}

        public ICommand OKCommand { get; set; }
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        private bool CanAcceptSelection()
        {
            return EntrySelector_ComboBox.SelectedItem != null;
        }

        private void AcceptSelection()
        {
            DialogResult = true;
            ChosenItem = EntrySelector_ComboBox.SelectedItem;
        }

        private object ChosenItem;
        public string DirectionsText { get; }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void EntrySelector_ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OKCommand.CanExecute(null))
            {
                OKCommand.Execute(null);
            }
        }
    }
}
