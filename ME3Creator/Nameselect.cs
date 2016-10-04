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
        public PCCPackage pcc;

        public Nameselect()
        {
            InitializeComponent();
        }

        public void Init(PCCPackage p, int index)
        {
            pcc = p;
            comboBox1.Items.Clear();
            for (int i = 0; i < pcc.Header.NameCount; i++)
                comboBox1.Items.Add(i.ToString("d6") + " : " + pcc.Names[i]);
            comboBox1.SelectedIndex = index;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private int returnValue()
        {
            int result = -1;
            if (r1.Checked)
                result = comboBox1.SelectedIndex;
            if (r2.Checked)
            {
                
                result = pcc.FindNameOrAdd(textBox1.Text);
            }
            return result;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            r2.Checked = true;
        }

        private void comboBox1_Enter(object sender, EventArgs e)
        {
            r1.Checked = true;
        }

        public static int GetValue(PCCPackage p, int index)
        {
            Nameselect prompt = new Nameselect();
            prompt.Init(p, index);

            return prompt.ShowDialog() == DialogResult.OK ? prompt.returnValue() : -1;
        }
    }
}
