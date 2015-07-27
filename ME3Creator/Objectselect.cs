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
    public partial class Objectselect : Form
    {
        public bool Aborted = false;
        public bool PressedOK = false;
        public int Result;
        public PCCPackage pcc;

        public Objectselect()
        {
            InitializeComponent();
        }

        public void Init(PCCPackage p, int index = 0)
        {
            pcc = p;
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            for (int i = 0; i < pcc.Header.ExportCount; i++)
                comboBox1.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(i + 1) + pcc.GetObject(i + 1));
            for (int i = 0; i < pcc.Header.ImportCount; i++)
                comboBox2.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(-i - 1) + pcc.GetObject(-i - 1));
            comboBox1.SelectedIndex = comboBox2.SelectedIndex = 0;
            if (index == 0)
                r1.Checked = true;
            if (index > 0)
            {
                comboBox1.SelectedIndex = index - 1;
                r2.Checked = true;
            }
            if (index < 0)
            {
                comboBox2.SelectedIndex = -index - 1;
                r3.Checked = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Aborted = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (r1.Checked)
                Result = 0;
            if (r2.Checked)
                Result = comboBox1.SelectedIndex + 1;
            if (r3.Checked)
                Result = -comboBox2.SelectedIndex - 1;
            PressedOK = true;
        }

        private void Objectselect_Shown(object sender, EventArgs e)
        {
            this.Activate();
            this.WindowState = FormWindowState.Normal;
            this.TopMost = true;
            this.Focus();
        }

        private void Objectselect_FormClosing(object sender, FormClosingEventArgs e)
        {
            Aborted = true;
        }
    }
}
