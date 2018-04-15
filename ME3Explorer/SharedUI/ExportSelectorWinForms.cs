using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.SharedUI
{
    public partial class ExportSelectorWinForms : Form
    {
        public const int SUPPORTS_EXPORTS_ONLY = 1;
        public const int SUPPORTS_IMPORTS_ONLY = 2;
        public const int SUPPORTS_BOTH_IMPORTS_EXPORTS = 3;
        private int SupportedInputTypes = 0;
        public int SelectedItemIndex = int.MinValue;
        private IMEPackage pcc;

        public ExportSelectorWinForms(IMEPackage pcc, int inputTypesSupported)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            this.pcc = pcc;
            SupportedInputTypes = inputTypesSupported;
            switch (SupportedInputTypes)
            {
                case SUPPORTS_EXPORTS_ONLY:
                    instructionsLabel.Text = "Enter an export index.";
                    Text = "Export Selector";
                    break;
                case SUPPORTS_IMPORTS_ONLY:
                    instructionsLabel.Text = "Enter an import index.";
                    Text = "Import Selector";
                    break;
                case SUPPORTS_BOTH_IMPORTS_EXPORTS:
                    instructionsLabel.Text = "Enter an import or export index.";
                    Text = "Import/Export Selector";
                    break;
            }
        }

        private void IndexBox_TextChanged(object sender, EventArgs e)
        {
            int destIndex = -1;
            bool succeeded = Int32.TryParse(indexField.Text, out destIndex);

            if (succeeded)
            {
                if (((SupportedInputTypes & SUPPORTS_EXPORTS_ONLY) != 0))
                {
                    if (destIndex >= 0 && destIndex < pcc.Exports.Count)
                    {
                        //Parse
                        IExportEntry destExport = pcc.Exports[destIndex];
                        selectedItemLabel.Text = destExport.GetFullPath + " (EXPORT)"; ;
                        acceptButton.Enabled = true;
                    } else
                    {
                        selectedItemLabel.Text = "Invalid export index (out of bounds)";
                        acceptButton.Enabled = false;
                    }
                }
                else if (((SupportedInputTypes & SUPPORTS_IMPORTS_ONLY) != 0))
                {
                    if (destIndex < 0 && -destIndex + 1 < pcc.Imports.Count)
                    {
                        //Parse
                        ImportEntry import = pcc.Imports[-destIndex + 1];
                        selectedItemLabel.Text = import.GetFullPath + " (IMPORT)";
                        acceptButton.Enabled = true;
                    } else
                    {
                        selectedItemLabel.Text = "Invalid import index (out of bounds)";
                        acceptButton.Enabled = false;
                    }
                }
            }
            else
            {
                selectedItemLabel.Text = "Invalid input";
                acceptButton.Enabled = false;
            }
        }

        private void acceptButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Yes;
            SelectedItemIndex = Convert.ToInt32(indexField.Text);
            Close();
        }

        private void InputField_KeyPressed(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar == Convert.ToChar(Keys.Enter)) && acceptButton.Enabled)
            {
                e.Handled = true;
                acceptButton.PerformClick();
                return;
            }
            if ((SupportedInputTypes & SUPPORTS_EXPORTS_ONLY) != 0)
            {
                e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar); //prevent non digit entry
            }
            else
            {
                e.Handled = !char.IsDigit(e.KeyChar) && e.KeyChar != '-' && !char.IsControl(e.KeyChar); //prevent non digit entry but allow -
            }
        }
    }
}
