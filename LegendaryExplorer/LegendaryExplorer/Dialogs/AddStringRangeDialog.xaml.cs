using LegendaryExplorer.SharedUI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for AddStringRangeDialog.xaml
    /// A dialog window that prompts the user to enter a range of String IDs to add in batch.
    /// </summary>
    public partial class AddStringRangeDialog : Window
    {
        /// <summary>
        /// Validation function that checks if the range is valid.
        /// </summary>
        private Func<int, int, HashSet<int>, string> validationFunc;

        /// <summary>
        /// Set of existing string IDs to check for conflicts.
        /// </summary>
        private HashSet<int> existingStringIDs;

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
        /// Creates a new AddStringRangeDialog with existing string IDs for validation.
        /// </summary>
        /// <param name="existingIDs">Set of existing string IDs to check for conflicts.</param>
        public AddStringRangeDialog(HashSet<int> existingIDs)
        {
            LoadCommands();
            InitializeComponent();
            this.Loaded += AddStringRangeDialog_Loaded;
            existingStringIDs = existingIDs;
            validationFunc = ValidateRange;
        }

        /// <summary>
        /// Handles the Loaded event to set focus on the start ID text box.
        /// </summary>
        void AddStringRangeDialog_Loaded(object sender, RoutedEventArgs e)
        {
            txtStartID.Focus();
        }

        /// <summary>
        /// Gets the start String ID entered by the user.
        /// </summary>
        public int StartStringID { get; private set; }

        /// <summary>
        /// Gets the end String ID entered by the user.
        /// </summary>
        public int EndStringID { get; private set; }

        /// <summary>
        /// Checks if the current input is valid.
        /// </summary>
        /// <returns>True if the input is valid; otherwise false.</returns>
        private bool IsInputValid()
        {
            if (!int.TryParse(txtStartID.Text, out int startID) || startID <= 0)
            {
                return false;
            }

            if (!int.TryParse(txtEndID.Text, out int endID) || endID <= 0)
            {
                return false;
            }

            if (validationFunc != null)
            {
                string validationMessage = validationFunc(startID, endID, existingStringIDs);
                return string.IsNullOrEmpty(validationMessage);
            }

            return true;
        }

        /// <summary>
        /// Validates the range of String IDs.
        /// </summary>
        /// <param name="startID">The start String ID.</param>
        /// <param name="endID">The end String ID.</param>
        /// <param name="existingIDs">Set of existing String IDs.</param>
        /// <returns>Error message if validation fails; null or empty if valid.</returns>
        private string ValidateRange(int startID, int endID, HashSet<int> existingIDs)
        {
            if (startID > endID)
            {
                return "Start String ID must be less than or equal to End String ID.";
            }

            if (endID - startID + 1 > 1000)
            {
                return "Range is too large. Maximum of 1000 strings can be added at once.";
            }

            // Check for conflicts with existing IDs
            for (int id = startID; id <= endID; id++)
            {
                if (existingIDs.Contains(id))
                {
                    return $"String ID {id} already exists in the TLK file.";
                }
            }

            return null;
        }

        /// <summary>
        /// Closes the dialog with a successful result (DialogResult = true).
        /// </summary>
        private void SuccessClose()
        {
            if (int.TryParse(txtStartID.Text, out int startID) && int.TryParse(txtEndID.Text, out int endID))
            {
                StartStringID = startID;
                EndStringID = endID;
                DialogResult = true;
                Close();
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
        /// Handles text changes in the text boxes and updates validation feedback.
        /// </summary>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtStartID.Text, out int startID) && int.TryParse(txtEndID.Text, out int endID))
            {
                if (validationFunc != null)
                {
                    string validationMessage = validationFunc(startID, endID, existingStringIDs);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        txtValidation.Text = validationMessage;
                        txtValidation.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        txtValidation.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                txtValidation.Text = "Please enter valid positive integer values.";
                txtValidation.Visibility = Visibility.Visible;
            }
        }
    }
}
