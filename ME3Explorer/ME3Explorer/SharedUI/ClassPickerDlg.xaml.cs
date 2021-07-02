using System;
using System.Collections.Generic;
using System.Linq;
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
using ME3ExplorerCore.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ClassPickerDlg.xaml
    /// </summary>
    public partial class ClassPickerDlg : NotifyPropertyChangedWindowBase
    {

        private ClassInfo ChosenClass;

        private string _okButtonText = "Add";
        public string OkButtonText
        {
            get => _okButtonText;
            set => SetProperty(ref _okButtonText, value);
        }

        public static ClassInfo GetClass(Control control, List<ClassInfo> classes, string windowTitle = null, string okButtonText = null)
        {
            var dlg = new ClassPickerDlg(control, classes, windowTitle, okButtonText);
            dlg.ShowDialog();
            return dlg.ChosenClass;
        }

        private ClassPickerDlg(Control owner, List<ClassInfo> classes, string windowTitle, string okButtonText)
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            if (windowTitle is not null)
            {
                Title = windowTitle;
            }
            if (okButtonText is not null)
            {
                OkButtonText = okButtonText;
            }
            if (owner != null)
            {
                Owner = owner as Window ?? GetWindow(owner);
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            ClassToolBox.Classes = classes;
            ClassToolBox.DoubleClickCallback = DoubleClickCallback;

            ClassToolBox.searchBox.searchBox.Focus();
        }

        private void DoubleClickCallback(ClassInfo info)
        {
            ChosenClass = info;
            DialogResult = true;
        }

        public ICommand OKCommand { get; set; }
        private void LoadCommands()
        {
            OKCommand = new GenericCommand(AcceptSelection, CanAcceptSelection);
        }

        private bool CanAcceptSelection()
        {
            return ClassToolBox.SelectedItem is not null;
        }

        private void AcceptSelection()
        {
            ChosenClass = ClassToolBox.SelectedItem;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ChosenClass = null;
            DialogResult = false;
        }
    }
}
