using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;

namespace ME3Explorer
{
    public partial class Soundplorer : Form
    {
        PCCObject pcc;
        public string CurrentFile;
        public List<int> ObjectIndexes;
        WwiseStream w;
        WwiseBank wb;
        public bool isDLC = false;

        public Soundplorer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                CurrentFile = d.FileName;
                LoadObjects();
                Status.Text = "Ready";
                saveToolStripMenuItem.Enabled = true;
            }
        }

        public void LoadObjects()
        {
            listBox1.Items.Clear();
            ObjectIndexes = new List<int>();
            for(int i=0;i<pcc.Exports.Count;i++)            
            {
                PCCObject.ExportEntry e = pcc.Exports[i];
                Status.Text = "Scan object " + i + " / " + pcc.Exports.Count;
                if (e.ClassName == "WwiseBank" || e.ClassName == "WwiseStream")
                {
                    string s = i.ToString("d6") + " : " + e.ClassName + " : \"" + e.ObjectName + "\"";
                    listBox1.Items.Add(s);
                    ObjectIndexes.Add(i);
                }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            rtb1.Text = "";
            rtb1.Visible = true;
            hb1.Visible = false;
            int index = ObjectIndexes[n];
            PCCObject.ExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                w = new WwiseStream(pcc, ex.Data);                
                string s = "#" + index + " WwiseStream : " + ex.ObjectName + "\n\n";
                s += "Filename : \"" + w.FileName + "\"\n";
                s += "Data size: " + w.DataSize + " bytes\n";                    
                s += "Data offset: 0x" + w.DataOffset.ToString("X8") + "\n";
                s += "ID: 0x" + w.Id.ToString("X8") + " = " + w.Id +"\n";
                rtb1.Text = s;
            }
            if (ex.ClassName == "WwiseBank")
            {
                rtb1.Visible = false;
                hb1.Visible = true;
                wb = new WwiseBank(pcc, index);
                hb1.ByteProvider = new DynamicByteProvider(wb.getBinary());
            }
        }

        private void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            PCCObject.ExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                Stop();
                w = new WwiseStream(pcc, ex.Data);
                string path = ME3Directory.cookedPath;
                Status.Text = "Loading...";
                w.Play(path);
                Status.Text = "Ready";
            }
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Status.Text = "Stopping...";
            Stop();
            Status.Text = "Ready";
        }

        public void Stop()
        {
            if (w != null)
                if (w.sp != null)
                    w.sp.Stop();
        }

        private void exportToWAVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            PCCObject.ExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.wav|*.wav";
                if(ex.ObjectName.Length > 4)
                    d.FileName = ex.ObjectName.Substring(0, ex.ObjectName.Length - 4);
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = ME3Directory.cookedPath;
                    Status.Text = "Exporting...";
                    w.ExtractToFile(path, d.FileName,false);
                    Status.Text = "Ready";
                    MessageBox.Show("Done");
                }
            }
        }

        private void importFromWAVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            PCCObject.ExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.wav|*.wav";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string pathcook = ME3Directory.cookedPath;
                    string pathbio = Path.GetDirectoryName(Path.GetDirectoryName(pathcook)) + "\\";
                    Status.Text = "Importing...";
                    if(!isDLC)
                        w.ImportFromFile(d.FileName,pathbio,pathcook);
                    else
                        w.ImportFromFile(d.FileName, pathbio, "" ,false);
                    byte[] buff = new byte[w.memsize];
                    for (int i = 0; i < w.memsize; i++)
                        buff[i] = w.memory[i];
                    ex.Data = buff;
                    Status.Text = "Saving...";
                    pcc.altSaveToFile(CurrentFile, true);
                    Status.Text = "Ready";
                    MessageBox.Show("Done");
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
                if (CurrentFile != "")
                    pcc.altSaveToFile(CurrentFile, true);
        }

        private void openDLCPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                CurrentFile = d.FileName;
                isDLC = true;
                LoadObjects();
                Status.Text = "Ready";
                saveToolStripMenuItem.Enabled = true;
            }
        }

        private void directAFCReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DirectReplace dr = new DirectReplace();
            dr.MdiParent = this.MdiParent;
            dr.Show();
            dr.WindowState = FormWindowState.Maximized;
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            PCCObject.ExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                dr.textBox3.Text = w.DataOffset.ToString();
                dr.textBox2.Text = w.FileName + ".afc";
            }
        }
    }
}
