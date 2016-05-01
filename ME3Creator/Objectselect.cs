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
        public Objectselect()
        {
            InitializeComponent();
        }

        public void Init(PCCPackage pcc, int index)
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            for (int i = 0; i < pcc.Header.ExportCount; i++)
                comboBox1.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(i + 1) + pcc.getObjectName(i + 1));
            for (int i = 0; i < pcc.Header.ImportCount; i++)
                comboBox2.Items.Add(i.ToString("d6") + " : " + pcc.GetObjectPath(-i - 1) + pcc.getObjectName(-i - 1));
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
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private int? returnValue()
        {
            int? result = 0;

            if (r2.Checked)
                result = comboBox1.SelectedIndex + 1;
            else if (r3.Checked)
                result = -comboBox2.SelectedIndex - 1;

            return result;
        }

        private void comboBox1_Enter(object sender, EventArgs e)
        {
            r2.Checked = true;
        }

        private void comboBox2_Enter(object sender, EventArgs e)
        {
            r3.Checked = true;
        }
        
        public static int? GetValue(PCCPackage p, int index)
        {
            Objectselect prompt = new Objectselect();
            prompt.Init(p, index);

            return prompt.ShowDialog() == DialogResult.OK ? prompt.returnValue() : null;
        }
    }
}
