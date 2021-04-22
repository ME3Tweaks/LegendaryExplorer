using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for PromptDialog.xaml
    /// </summary>
    public partial class PromptDialog : Window
    {
        public enum InputType
        {
            Text,
            Password,
            Multiline
        }

        private InputType _inputType;

        /// <summary>
        /// Creates a new prompt dialog with the specified question, title, and default value. Ensure yo/su set the owner before showing if this if being called from a WPF window.
        /// </summary>
        /// <param name="question"></param>
        /// <param name="title"></param>
        /// <param name="defaultValue"></param>
        /// <param name="selectText"></param>
        /// <param name="inputType"></param>
        public PromptDialog(string question, string title, string defaultValue = "", bool selectText = false, InputType inputType = InputType.Text)
        {
            InitializeComponent();
            this.Loaded += PromptDialog_Loaded;
            txtQuestion.Text = question;
            Title = title;
            txtResponse.Text = defaultValue;
            if (selectText)
            {
                txtResponse.SelectAll();
            }
            _inputType = inputType;
            if (inputType == InputType.Multiline)
            {
                txtResponse.AcceptsReturn = true;
                txtResponse.Height = 100;
            }
            else
            {
                txtResponse.AcceptsReturn = false;
                txtResponse.MaxLines = 1;
            }
        }

        void PromptDialog_Loaded(object sender, RoutedEventArgs e)
        {
            txtResponse.Focus();
        }

        public static string Prompt(Control owner, string question, string title = "", string defaultValue = "", bool selectText = false, InputType inputType = InputType.Text)
        {
            PromptDialog inst = new PromptDialog(question, title, defaultValue, selectText, inputType);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return inst.ResponseText;
            return null;
        }

        public string ResponseText => txtResponse.Text;

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
