using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class InputComboBox : Form
    {
        public InputComboBox()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static string GetValue(string promptText, IEnumerable<string> items, string defaultValue = "", bool topMost = false) {
            InputComboBox prompt = new InputComboBox();
            prompt.TopMost = topMost;
            prompt.label1.Text = promptText;
            prompt.comboBox1.Items.AddRange(items.Cast<object>().ToArray());
            prompt.comboBox1.SelectedItem = defaultValue;

            return prompt.ShowDialog() == DialogResult.OK ? prompt.comboBox1.Text : "";
        }
    }


}
