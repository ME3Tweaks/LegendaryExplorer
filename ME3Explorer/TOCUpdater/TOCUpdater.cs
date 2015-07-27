using KFreonLib.MEDirectories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.TOCUpdater
{
    public partial class TOCUpdater : Form
    {
        public TOCUpdater()
        {
            InitializeComponent();
        }

        private void checkPCConsoleTOCbinToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "PCConsoleTOC.bin|PCConsoleTOC.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TOCbinUpdater.UpdateTocBin(d.FileName, ME3Directory.gamePath, listBox1,pb1);
            }
        }

        public void EasyUpdate()
        {
            TOCbinUpdater.UpdateTocBin(ME3Directory.tocFile, ME3Directory.gamePath, listBox1, pb1);
        }

        public void InvolvedUpdate(string toc, string path)
        {
            TOCbinUpdater.UpdateTocBin(toc, path, listBox1, pb1);
        }
    }
}
