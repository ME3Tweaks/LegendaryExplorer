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

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for PromptDialog.xaml
    /// </summary>
    public partial class PromptDialog : Window
    {
        public enum InputType
        {
            Text,
            Password
        }

        private InputType _inputType = InputType.Text;

        public PromptDialog(string question, string title, string defaultValue = "", InputType inputType = InputType.Text)
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(PromptDialog_Loaded);
            txtQuestion.Text = question;
            Title = title;
            txtResponse.Text = defaultValue;
            _inputType = inputType;
        }

        void PromptDialog_Loaded(object sender, RoutedEventArgs e)
        {
            txtResponse.Focus();
        }

        public static string Prompt(Window owner, string question, string title, string defaultValue = "", InputType inputType = InputType.Text)
        {
            PromptDialog inst = new PromptDialog(question, title, defaultValue, inputType);
            inst.Owner = owner;
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return inst.ResponseText;
            return null;
        }

        public string ResponseText
        {
            get
            {
                return txtResponse.Text;
            }
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
