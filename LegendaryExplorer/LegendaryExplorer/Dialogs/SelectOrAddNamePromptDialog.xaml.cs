using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Dialog to select an existing or new name in a package file
    /// </summary>
    public partial class SelectOrAddNamePromptDialog : TrackingNotifyPropertyChangedWindowBase
    {
        private IMEPackage _pcc;

        public ObservableCollectionExtended<IndexedName> NameList { get; } = new();

        private int _number;
        public int Number
        {
            get => _number;
            set => SetProperty(ref _number, value);
        }

        public SelectOrAddNamePromptDialog(string question, string title, IMEPackage pcc, int defaultValue = 0) : base("Select/Add Name Prompt Dialog", false)
        {
            _pcc = pcc;
            NameList.AddRange(pcc.Names.Select((nr, i) => new IndexedName(i, nr)));
            DataContext = this;
            InitializeComponent();
            txtQuestion.Text = question;
            Title = title;
            answerChoicesCombobox.SelectedIndex = defaultValue;
        }

        public SelectOrAddNamePromptDialog(string question, string title, IMEPackage pcc, NameReference defaultValue) : this(question, title, pcc)
        {
            answerChoicesCombobox.SelectedIndex = NameList.FirstOrDefault(name => name.Name == defaultValue.Name)?.Index ?? 0;
            Number = defaultValue.Number;
        }

        public static IndexedName Prompt(Control owner, string question, string title, IMEPackage pcc, int defaultValue = 0)
        {
            SelectOrAddNamePromptDialog inst = new SelectOrAddNamePromptDialog(question, title, pcc, defaultValue);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.numberColumn.Width = new GridLength(0);
            inst.ShowDialog();
            if (inst.DialogResult == true)
            {
                return (IndexedName)inst.answerChoicesCombobox.SelectedItem;
            }

            return null;
        }

        public static bool Prompt(Control owner, string question, string title, IMEPackage pcc, out NameReference result, NameReference defaultValue = default)
        {
            SelectOrAddNamePromptDialog inst = new SelectOrAddNamePromptDialog(question, title, pcc, defaultValue);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.ShowDialog();
            if (inst.DialogResult == true)
            {
                IndexedName name = (IndexedName)inst.answerChoicesCombobox.SelectedItem;
                if (name is not null)
                {
                    result = new NameReference(name.Name.Name, inst.Number);
                    return true;
                }
            }

            result = default;
            return false;
        }

        private void Name_ComboBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if(!CheckName())
                {
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Checks if the typed name is in the package, prompts the user to add it if it is not
        /// </summary>
        /// <returns>True if name was added</returns>
        private bool CheckName()
        {
            // Check name - code copied from EntryMetadataExportLoader
            var text = answerChoicesCombobox.Text;
            int index = _pcc.findName(text);
            if (index < 0 && !string.IsNullOrEmpty(text))
            {
                Keyboard.ClearFocus();
                string input = $"The name \"{text}\" does not exist in the current loaded package.\nIf you'd like to add this name, press enter below, or change the name to what you would like it to be.";
                string result = PromptDialog.Prompt(this, input, "Enter new name", text);
                if (!string.IsNullOrEmpty(result))
                {
                    int idx = _pcc.FindNameOrAdd(result);
                    if (idx != _pcc.Names.Count - 1)
                    {
                        //not the last
                        MessageBox.Show($"{result} already exists in this package file.\nName index: {idx} (0x{idx:X8})", "Name already exists");
                    }
                    else
                    {
                        var newName = new IndexedName(idx, result);
                        NameList.Add(newName);
                        answerChoicesCombobox.SelectedItem = newName;
                        return true;
                    }
                }
            }
            return false;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            CheckName();
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
