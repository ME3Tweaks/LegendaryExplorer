using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3LibWV;

namespace ME3Creator
{
    public partial class Nameselect : Form
    {
        public int Result = -1;
        public PCCPackage pcc;

        public Nameselect()
        {
            InitializeComponent();
        }

        public void Init(PCCPackage p, int index = 0)
        {
            pcc = p;
            comboBox1.Items.Clear();
            for (int i = 0; i < pcc.Header.NameCount; i++)
                comboBox1.Items.Add(i.ToString("d6") + " : " + pcc.Names[i]);
            comboBox1.SelectedIndex = index;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (r1.Checked)
                Result = comboBox1.SelectedIndex;
            if (r2.Checked)
            {
                pcc.Names.Add(textBox1.Text);
                Result = (int)pcc.Header.NameCount++;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Result = -2;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.TopMost = true;
        }

        private void Nameselect_Shown(object sender, EventArgs e)
        {
            this.Activate();
            this.WindowState = FormWindowState.Normal;
            this.TopMost = true;
            this.Focus();
        }

        private void Nameselect_FormClosing(object sender, FormClosingEventArgs e)
        {
            Result = -2;
        }
    }
}
