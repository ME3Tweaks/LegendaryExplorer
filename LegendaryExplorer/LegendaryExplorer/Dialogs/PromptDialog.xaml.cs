using LegendaryExplorer.SharedUI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for PromptDialog.xaml
    /// A dialog window that prompts the user for text input with optional validation.
    /// </summary>
    public partial class PromptDialog : Window
    {
        /// <summary>
        /// Defines the type of input control to display in the dialog.
        /// </summary>
        public enum InputType
        {
            /// <summary>
            /// Single-line text input.
            /// </summary>
            Text,
            /// <summary>
            /// Multi-line text input with return key support.
            /// </summary>
            Multiline
        }

        private InputType _inputType;

        /// <summary>
        /// Optional validation function that determines if the input is valid.
        /// </summary>
        private Predicate<string> validationFunc;

        /// <summary>
        /// Optional function that returns validation feedback text based on the current input.
        /// </summary>
        private Func<string, string> validationTextFunc;

        /// <summary>
        /// Command executed when the OK button is clicked.
        /// </summary>
        public ICommand OkCommand { get; set; }

        /// <summary>
        /// Initializes the commands used by the dialog.
        /// </summary>
        private void LoadCommands()
        {
            OkCommand = new GenericCommand(SuccessClose, IsInputValid);
        }

        /// <summary>
        /// Creates a new prompt dialog with the specified question, title, and default value. Ensure you set the owner before showing if this if being called from a WPF window.
        /// </summary>
        /// <param name="question">The question or prompt text to display to the user.</param>
        /// <param name="title">The title of the dialog window.</param>
        /// <param name="defaultValue">The default text value to populate in the input field.</param>
        /// <param name="selectText">If true, selects text in the input field when the dialog opens.</param>
        /// <param name="selectionStart">The starting position of the text selection. Use -1 to select all text.</param>
        /// <param name="selectionEnd">The ending position of the text selection. Use -1 or 0 to select to the end.</param>
        /// <param name="inputType">The type of input control to display (single-line or multi-line).</param>
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

        /// <summary>
        /// Handles the Loaded event to set focus on the response text box.
        /// </summary>
        void PromptDialog_Loaded(object sender, RoutedEventArgs e)
        {
            txtResponse.Focus();
        }

        /// <summary>
        /// Displays a modal prompt dialog and returns the user's input.
        /// </summary>
        /// <param name="owner">The owner control or window for centering the dialog.</param>
        /// <param name="question">The question or prompt text to display to the user.</param>
        /// <param name="title">The title of the dialog window.</param>
        /// <param name="defaultValue">The default text value to populate in the input field.</param>
        /// <param name="selectText">If true, selects text in the input field when the dialog opens.</param>
        /// <param name="selectionStart">The starting position of the text selection. Use -1 to select all text.</param>
        /// <param name="selectionEnd">The ending position of the text selection. Use -1 or 0 to select to the end.</param>
        /// <param name="inputType">The type of input control to display (single-line or multi-line).</param>
        /// <param name="validator">Optional validation function that returns true if the input is valid.</param>
        /// <param name="validationText">Optional function that returns validation feedback text based on the current input.</param>
        /// <returns>The user's input text if OK was clicked; null if the dialog was cancelled.</returns>
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

        /// <summary>
        /// Gets the text entered by the user in the response text box.
        /// </summary>
        public string ResponseText => txtResponse.Text;

        /// <summary>
        /// Checks if the current input is valid using the validation function if provided.
        /// </summary>
        /// <returns>True if the input is valid or no validation function is set; otherwise false.</returns>
        private bool IsInputValid()
        {
            return validationFunc?.Invoke(ResponseText) ?? true;
        }

        /// <summary>
        /// Closes the dialog with a successful result (DialogResult = true).
        /// </summary>
        private void SuccessClose()
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles text changes in the response text box and updates validation feedback.
        /// </summary>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (validationTextFunc is not null)
            {
                txtValidation.Text = validationTextFunc(ResponseText);
            }
        }
    }
}
