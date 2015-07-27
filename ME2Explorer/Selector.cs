using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME2Explorer
{
    public partial class Selector : Form
    {
        public List<string> selected { get; set; }
        public Selector(List<string> args)
        {
            InitializeComponent();
            //selected = new List<string>(args);
            
            int count = 0;
            foreach (string line in args)
            {
                SelectionBox.Items.Add(line);
            }
        }

        private void SelectionBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            selected = new List<string>();
            foreach (string line in SelectionBox.CheckedItems)
                selected.Add(line);
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            selected = null;
            this.Close();
        }
    }
}
