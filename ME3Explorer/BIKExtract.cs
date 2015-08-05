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
    public partial class BIKExtract : Form
    {
        public struct Entry
        {
            public int off;
            public int size;
        }

        byte[] memory = new byte[0];
        int memsize = 0;
        public List<Entry> entr;

        public BIKExtract()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "Movies.tfc|Movies.tfc";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog1.FileName;
                FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                memsize = (int)fileStream.Length;
                memory = new byte[memsize];
                entr = new List<Entry>();
                for (int i = 0; i < memsize; i++)
                {
                    memory[i] = (byte)fileStream.ReadByte();
                }
                for (int i = 0; i < memsize -3; i++)
                {
                    if (memory[i] == 0x42)
                        if (memory[i + 1] == 0x49
                        && memory[i + 2] == 0x4B
                        && memory[i + 3] == 0x69)
                        {
                            int n = entr.Count;
                            Entry temp = new Entry();
                            if (n == 0)
                            {
                                temp.off = i;
                                entr.Add(temp);
                            }
                            else
                            {
                                temp.off = i;
                                Entry temp2 = entr[n - 1];
                                temp2.size = temp.off - temp2.off;
                                entr[n - 1] = temp2;
                                entr.Add(temp);
                            }
                        }
                }

                if (entr.Count > 0)
                {
                    int n = entr.Count;
                    Entry temp = entr[n - 1];
                    temp.size = memsize - temp.off;
                    entr[n - 1] = temp;
                }
                listBox1.Items.Clear();
                for (int i = 0; i < entr.Count; i++)
                    listBox1.Items.Add("#" + i.ToString() + " Offset:" + entr[i].off.ToString("X") + " Size:" + entr[i].size.ToString("X"));
                listBox2.Items.Clear();
                listBox2.Items.Add("Loaded " + Path.GetFileName(path) + "\nCount: " + entr.Count);
            }
        }

        private void selectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1) return;
            string path = string.Empty;
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "BIK files (*.bik)|*.bik";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                Entry t = entr[n];
                for (int i = 0; i < t.size; i++)
                    fileStream.WriteByte(memory[t.off + i]);
                fileStream.Close();
            }
        }

        private void allToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog m = new System.Windows.Forms.FolderBrowserDialog();
            m.ShowDialog();
            if (m.SelectedPath != "")
            {
                string dir = m.SelectedPath + "\\";
                for (int i = 0; i < entr.Count; i++)
                {
                    Entry t = entr[i];
                    FileStream fileStream = new FileStream(dir + t.off.ToString("X") + ".bik", FileMode.Create, FileAccess.Write);
                    listBox2.Items.Add("Extracting " + dir + t.off.ToString("X") + ".bik");
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                    Application.DoEvents(); 
                    for (int j = 0; j < t.size; j++)
                        fileStream.WriteByte(memory[t.off + j]);
                    fileStream.Close();
                }
            }
            MessageBox.Show("Done.");
        }

        private void BIKExtract_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }
    }
}
