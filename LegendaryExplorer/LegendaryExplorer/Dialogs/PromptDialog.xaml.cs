using LegendaryExplorer.SharedUI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            Multiline
        }

        private InputType _inputType;

        private Predicate<string> validationFunc;

        private Func<string, string> validationTextFunc;

        public ICommand OkCommand { get; set; }

        private void LoadCommands()
        {
            OkCommand = new GenericCommand(SuccessClose, IsInputValid);
        }

        /// <summary>
        /// Creates a new prompt dialog with the specified question, title, and default value. Ensure yo/su set the owner before showing if this if being called from a WPF window.
        /// </summary>
        /// <param name="question"></param>
        /// <param name="title"></param>
        /// <param name="defaultValue"></param>
        /// <param name="selectText"></param>
        /// <param name="inputType"></param>
        public PromptDialog(string question, string title, string defaultValue = "", bool selectText = false, int selectionStart = -1, int selectionEnd = -1, InputType inputType = InputType.Text)
        {
            LoadCommands();
            InitializeComponent();
            this.Loaded += PromptDialog_Loaded;
            txtQuestion.Text = question;
            Title = title;
            txtResponse.Text = defaultValue;
            if (selectText)
            {
                if (selectionStart == -1)
                {
                    txtResponse.SelectAll();
                }
                else
                {
                    txtResponse.SelectionStart = selectionStart;
                    if (selectionEnd > 0 && selectionEnd > selectionStart)
                    {
                        var maxLen = Math.Abs(selectionStart - defaultValue.Length);
                        txtResponse.SelectionLength = Math.Min(maxLen, Math.Abs(selectionEnd - defaultValue.Length));
                    }
                    else
                    {
                        txtResponse.SelectionLength = defaultValue.Length - selectionStart;
                    }
                }
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

        public static string Prompt(Control owner, string question, string title = "", 
            string defaultValue = "", 
            bool selectText = false, int selectionStart = -1, int selectionEnd = -1, 
            InputType inputType = InputType.Text,
            Predicate<string> validator = null,
            Func<string, string> validationText = null)
        {
            PromptDialog inst = new PromptDialog(question, title, defaultValue, selectText, selectionStart, selectionEnd, inputType);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.validationFunc = validator;
            inst.validationTextFunc = validationText;
            if (validationText is not null)
            {
                inst.txtValidation.Visibility = Visibility.Visible;
                inst.txtValidation.Text = validationText(inst.ResponseText);
            }
            inst.ShowDialog();
            if (inst.DialogResult == true)
                return inst.ResponseText;
            return null;
        }

        public string ResponseText => txtResponse.Text;

        private bool IsInputValid()
        {
            return validationFunc?.Invoke(ResponseText) ?? true;
        }

        private void SuccessClose()
        {
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (validationTextFunc is not null)
            {
                txtValidation.Text = validationTextFunc(ResponseText);
            }
        }
    }
}
