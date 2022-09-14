using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Dialog to select a name in a package file.
    /// </summary>
    /// <remarks>Cannot be used to add a new name, use <see cref="SelectOrAddNamePromptDialog"/> to do that.</remarks>
    public partial class NamePromptDialog : TrackingNotifyPropertyChangedWindowBase
    {
        private List<IndexedName> _nameList;
        public List<IndexedName> NameList
        {
            get => _nameList;
            set
            {
                _nameList = value;
                OnPropertyChanged();
            }
        }

        private int _number;

        public int Number
        {
            get => _number;
            set => SetProperty(ref _number, value);
        }

        public NamePromptDialog(string question, string title, List<IndexedName> NameList, int defaultValue = 0) : base("Name Prompt Dialog", false)
        {
            this.NameList = NameList;
            DataContext = this;
            InitializeComponent();
            txtQuestion.Text = question;
            Title = title;
            answerChoicesCombobox.SelectedIndex = defaultValue;
        }

        public static IndexedName Prompt(Control owner, string question, string title, List<IndexedName> NameList, int defaultValue = 0)
        {
            var inst = new NamePromptDialog(question, title, NameList, defaultValue);
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

        public static bool Prompt(Control owner, string question, string title, IMEPackage pcc, out NameReference result, int defaultValue = 0)
        {
            var inst = new NamePromptDialog(question, title, pcc.Names.Select((nr, i) => new IndexedName(i, nr)).ToList(), defaultValue);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.ShowDialog();
            if (inst.DialogResult == true)
            {
                var name = (IndexedName)inst.answerChoicesCombobox.SelectedItem;
                if (name is not null)
                {
                    result = new NameReference(name.Name, inst.Number);
                    return true;
                }
            }

            result = default;
            return false;
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
