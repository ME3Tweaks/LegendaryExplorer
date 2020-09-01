using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for InputComboBoxWPF.xaml
    /// </summary>
    public partial class InputComboBoxWPF : NotifyPropertyChangedWindowBase
    {
        private InputComboBoxWPF(Window owner, string promptText, string titleText, IEnumerable<string> items, string defaultValue = "", bool topMost = false)
        {
            DirectionsText = promptText;
            Topmost = topMost;
            Owner = owner;
            Title = titleText;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            if (owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            EntrySelector_ComboBox.ItemsSource = items;
            EntrySelector_ComboBox.SelectedItem = defaultValue;
            EntrySelector_ComboBox.Focus();
        }

        public static string GetValue(Window owner, string promptText, string titleText, IEnumerable<string> items, string defaultValue = "", bool topMost = false)
        {
            var dlg = new InputComboBoxWPF(owner, promptText, titleText, items, defaultValue, topMost);
            return dlg.ShowDialog() == true ? dlg.ChosenEntry : "";
        }

        public ICommand OKCommand { get; set; }
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        private bool CanAcceptSelection()
        {
            return EntrySelector_ComboBox.SelectedItem is string;
        }

        private void AcceptSelection()
        {
            DialogResult = true;
            ChosenEntry = EntrySelector_ComboBox.SelectedItem as string;
        }

        private string ChosenEntry;
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
