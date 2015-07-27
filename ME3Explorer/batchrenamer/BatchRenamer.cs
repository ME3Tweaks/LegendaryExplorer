using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using KFreonLib.Debugging;

namespace ME3Explorer.batchrenamer
{
    public partial class BatchRenamer : Form
    {
        public BatchRenamer()
        {
            InitializeComponent();
        }

        public List<string> AllFiles = new List<string>();
        public List<string> Rules = new List<string>();

        private void addFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.*|*.*";
            d.Multiselect = true;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string s in d.FileNames)
                    AllFiles.Add(s);
                RefreshLists();
            }
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (string s in AllFiles)
                listBox1.Items.Add(Path.GetFileName(s));
            listBox2.Items.Clear();
            foreach (string s in Rules)
            {
                string[] s2 = s.Split(':');
                if (s2.Length != 2)
                    continue;
                listBox2.Items.Add("Replace \"" + s2[0] + "\" with \"" + s2[1] + "\"");
            }
        }

        private void removeFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            AllFiles.RemoveAt(n);
            RefreshLists();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string s1 = toolStripTextBox1.Text;
            string s2 = toolStripTextBox2.Text;
            if (s1.Contains(":") || s2.Contains(":"))
                return;
            Rules.Add(s1 + ":" + s2);
            RefreshLists();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            if (n == -1)
                return;
            Rules.RemoveAt(n);
            RefreshLists();
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AllFiles.Count == 0 || Rules.Count == 0)
                return;
            DebugOutput.StartDebugger("Batch Renamer");
            foreach (string file in AllFiles)                
            {
                string s = Path.GetFileName(file);
                foreach (string rule in Rules)
                {
                    string[] s2 = rule.Split(':');
                    if (s2.Length != 2)
                        continue;
                    s = s.Replace(s2[0], s2[1]);
                }
                s = Path.GetDirectoryName(file) + "\\" + s;
                try
                {
                    DebugOutput.PrintLn("Trying to move \nfrom:\t" + file + "\nto:\t" + s);
                    File.Move(file, s);
                    DebugOutput.PrintLn("Success");
                }
                catch (Exception ex)
                {
                    DebugOutput.PrintLn("Error : " + ex.ToString());
                }
            }
            MessageBox.Show("Done.");
        }
    }
}
