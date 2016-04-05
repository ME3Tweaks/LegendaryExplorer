using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.DialogEditor
{
    public partial class InputComboBox : Form
    {
        public InputComboBox()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static string GetValue(string promptText, List<string> items, string defaultValue = "") {
            InputComboBox prompt = new InputComboBox();
            prompt.label1.Text = promptText;
            prompt.comboBox1.Items.AddRange(items.ToArray());
            prompt.comboBox1.SelectedItem = defaultValue;

            return prompt.ShowDialog() == DialogResult.OK ? prompt.comboBox1.Text : "";
        }
    }


}
