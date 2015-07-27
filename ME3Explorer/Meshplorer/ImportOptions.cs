using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.Meshplorer
{
    public partial class ImportOptions : Form
    {
        public ImportOptions()
        {
            InitializeComponent();
        }

        private void ImportOptions_Activated(object sender, EventArgs e)
        {
            checkBox1.Checked = MPOpt.SKM_cullfaces;
            checkBox2.Checked = MPOpt.SKM_tnflipX;
            checkBox3.Checked = MPOpt.SKM_tnflipY;
            checkBox4.Checked = MPOpt.SKM_tnflipZ;
            checkBox5.Checked = MPOpt.SKM_tnflipW;
            checkBox6.Checked = MPOpt.SKM_biflipX;
            checkBox7.Checked = MPOpt.SKM_biflipY;
            checkBox8.Checked = MPOpt.SKM_biflipZ;
            checkBox9.Checked = MPOpt.SKM_biflipW;
            checkBox10.Checked = MPOpt.SKM_swaptangents;
            checkBox11.Checked = MPOpt.SKM_normalize;
            checkBox12.Checked = MPOpt.SKM_importbones;
            checkBox13.Checked = MPOpt.SKM_fixtexcoord;
            checkBox14.Checked = MPOpt.SKM_tnW100;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_cullfaces = checkBox1.Checked;
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_swaptangents = checkBox10.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_tnflipX = checkBox2.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_tnflipY = checkBox3.Checked;
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_tnflipZ = checkBox4.Checked;
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_tnflipW = checkBox5.Checked;
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_biflipX = checkBox6.Checked;
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_biflipY = checkBox7.Checked;
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_biflipZ = checkBox8.Checked;
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_biflipW = checkBox9.Checked;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ImportOptions_Resize(object sender, EventArgs e)
        {
            button1.Left = groupBox2.Width - button1.Width - 10;
            button1.Top = groupBox1.Height - button1.Height - 10;
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_importbones = checkBox12.Checked;
        }

        private void checkBox13_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_fixtexcoord = checkBox13.Checked;
        }

        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            MPOpt.SKM_tnW100 = checkBox14.Checked;
        }
    }
}
