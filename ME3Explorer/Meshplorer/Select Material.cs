using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class Select_Material : Form
    {
        public bool hasSelected;
        public int SelIndex;
        public List<int> Objects;

        public Select_Material()
        {
            InitializeComponent();
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            SelIndex = Objects[n];
            hasSelected = true;
        }
    }
}
