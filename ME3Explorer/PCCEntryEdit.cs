using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.UnrealHelper;

namespace ME3Explorer
{
    public partial class PCCEntryEdit : Form
    {
        public PCCFile pcc;
        public int name;
        public int classname;
        public int link;
        public int datasize;
        public int dataoff;

        public PCCEntryEdit(PCCFile Pcc, int entry)
        {
            InitializeComponent();
            pcc = Pcc;            
            classname = pcc.Export[entry].Class;
            name = (int)pcc.Export[entry].Name;
            link = pcc.Export[entry].Link;
            dataoff = (int)pcc.Export[entry].DataOffset;
            datasize = (int)pcc.Export[entry].DataSize;
            int max =(int)pcc.Header.ExportCount;
            int min = (int)pcc.Header.ImportCount * -1;
            ud1.Maximum = (int)pcc.Header.NameCount-1;
            ud1.Minimum = 0;
            ud2.Maximum = max;
            ud2.Minimum = min;
            ud3.Maximum = max;
            ud3.Minimum = min;
            ud1.Value = name;
            ud2.Value = link;
            ud3.Value = classname;
            RefreshDisplay();
        }

        private void PCCEntryEdit_Activated(object sender, EventArgs e)
        {
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            label6.Text = pcc.names[name];
            label7.Text = pcc.getClassName(link);
            label8.Text = pcc.getClassName(classname);
            textBox1.Text = datasize.ToString();
            textBox2.Text = dataoff.ToString();
        }

        private void ud1_ValueChanged(object sender, EventArgs e)
        {
            name = (int)ud1.Value;
            RefreshDisplay();
        }

        private void ud2_ValueChanged(object sender, EventArgs e)
        {
            link = (int)ud2.Value;
            RefreshDisplay();
        }

        private void ud3_ValueChanged(object sender, EventArgs e)
        {
            classname = (int)ud3.Value;
            RefreshDisplay();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            datasize = Convert.ToInt32(textBox1.Text);
            RefreshDisplay();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            dataoff = Convert.ToInt32(textBox2.Text);
            RefreshDisplay();
        }
    }
}
