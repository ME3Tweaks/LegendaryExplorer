using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        public void Decompress(string path)
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            rtb1.Text = "Load: "  + path + "\n";
            RunShell(loc + "\\exec\\Decompress.exe", "\"" + path + "\"");
            rtb1.Text += "\nDone.";;
        }

        private void fileOpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "pcc files (*.pcc)|*.pcc";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog1.FileName;
                Decompress(path);
            }
        }

        private void RunShell(string cmd, string args)
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }
    }
}
