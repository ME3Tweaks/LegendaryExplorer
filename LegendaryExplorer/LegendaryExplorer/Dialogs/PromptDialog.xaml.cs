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
        /// Optional validation function that determines if the input is valid and optionally provides textual validation feedback.
        /// </summary>
        private Func<string, (bool, string)> validationFunc;

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
        /// <param name="validator">Optional validation function that returns true if the input is valid,
        /// as well as a string that provides feedback on the value (null is valid). Will be called frequently. Must not cause side effects!</param>
        /// <returns>The user's input text if OK was clicked; null if the dialog was cancelled.</returns>
        public static string Prompt(Control owner, string question, string title = "",
            string defaultValue = "",
            bool selectText = false, int selectionStart = -1, int selectionEnd = -1,
            InputType inputType = InputType.Text,
            Func<string, (bool, string)> validator = null)
        {
            PromptDialog inst = new PromptDialog(question, title, defaultValue, selectText, selectionStart, selectionEnd, inputType);
            if (owner != null)
            {
                inst.Owner = owner as Window ?? GetWindow(owner);
                inst.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inst.validationFunc = validator;
            inst.Validate();
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
        private void Validate()
        {
            if (validationFunc is null) return;
            (bool valid, string feedback) = validationFunc(ResponseText);
            btnOk.IsEnabled = valid;
            if (feedback is null)
            {
                txtValidation.Visibility = Visibility.Hidden;
            }
            else
            {
                txtValidation.Visibility = Visibility.Visible;
                txtValidation.Text = feedback;
            }
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
            Validate();
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            if (validationFunc is null || validationFunc(ResponseText).Item1)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
