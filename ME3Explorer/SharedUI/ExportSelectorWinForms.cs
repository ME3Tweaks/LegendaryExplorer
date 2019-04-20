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
using Gammtek.Conduit.Extensions;

namespace ME3Explorer.SharedUI
{
    public partial class ExportSelectorWinForms : Form
    {
        [Flags]
        public enum SupportedTypes
        {
            Exports = 1,
            Imports = 2,
            ExportsAndImports = 3
        }

        private readonly SupportedTypes SupportedInputTypes = 0;
        public IEntry SelectedEntry;
        public IExportEntry SelectedExport;
        public ImportEntry SelectedImport;
        private readonly IMEPackage pcc;

        public ExportSelectorWinForms(IMEPackage pcc, SupportedTypes inputTypesSupported)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            this.pcc = pcc;
            SupportedInputTypes = inputTypesSupported;
            switch (SupportedInputTypes)
            {
                case SupportedTypes.Exports:
                    instructionsLabel.Text = "Enter an export index.";
                    Text = "Export Selector";
                    break;
                case SupportedTypes.Imports:
                    instructionsLabel.Text = "Enter an import index.";
                    Text = "Import Selector";
                    break;
                case SupportedTypes.ExportsAndImports:
                    instructionsLabel.Text = "Enter an import or export index.";
                    Text = "Import/Export Selector";
                    break;
            }
            acceptButton.Enabled = false;
        }

        private void IndexBox_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(indexField.Text, out int destIndex))
            {
                if (SupportedInputTypes.HasFlag(SupportedTypes.Exports) && pcc.isUExport(destIndex))
                {
                    IExportEntry destExport = pcc.getUExport(destIndex);
                    selectedItemLabel.Text = destExport.GetFullPath + " (EXPORT)"; ;
                    acceptButton.Enabled = true;
                }
                else if (SupportedInputTypes.HasFlag(SupportedTypes.Imports) && pcc.isUImport(destIndex))
                {
                    ImportEntry import = pcc.Imports[-destIndex + 1];
                    selectedItemLabel.Text = import.GetFullPath + " (IMPORT)";
                    acceptButton.Enabled = true;
                }
                else
                {
                    selectedItemLabel.Text = "Invalid index (out of bounds)";
                    acceptButton.Enabled = false;
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
            this.DialogResult = DialogResult.Yes;
            int uIndex = Convert.ToInt32(indexField.Text);
            switch (SupportedInputTypes)
            {
                case SupportedTypes.Exports:
                    SelectedExport = pcc.getUExport(uIndex);
                    break;
                case SupportedTypes.Imports:
                    SelectedImport = pcc.getUImport(uIndex);
                    break;
            }
            SelectedEntry = pcc.getEntry(uIndex);
            Close();
        }

        private void InputField_KeyPressed(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar == Convert.ToChar(Keys.Enter)) && acceptButton.Enabled)
            {
                e.Handled = true;
                acceptButton.PerformClick();
            }
            else if (!SupportedInputTypes.HasFlag(SupportedTypes.Imports))
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
