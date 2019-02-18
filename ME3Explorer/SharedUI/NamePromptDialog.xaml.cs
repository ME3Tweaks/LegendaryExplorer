using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static ME3Explorer.PackageEditorWPF;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for NamePromptDialogPromptDialog.xaml
    /// </summary>
    public partial class NamePromptDialog : NotifyPropertyChangedWindowBase
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
        public NamePromptDialog(string question, string title, List<IndexedName> NameList, int defaultValue = 0)
        {
            this.NameList = NameList;
            DataContext = this;
            InitializeComponent();
            txtQuestion.Text = question;
            Title = title;
            answerChoicesCombobox.SelectedIndex = defaultValue;
        }

        public static IndexedName Prompt(Window owner, string question, string title, List<IndexedName> NameList, int defaultValue = 0)
        {
            NamePromptDialog inst = new NamePromptDialog(question, title, NameList, defaultValue);
            inst.Owner = owner;
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return (IndexedName)inst.answerChoicesCombobox.SelectedItem;
            return null;
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
