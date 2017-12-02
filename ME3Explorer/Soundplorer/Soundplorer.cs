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
using ME3Explorer.Packages;
using KFreonLib.MEDirectories;

namespace ME3Explorer
{
    public partial class Soundplorer : WinFormsBase
    {
        public string CurrentFile;
        public List<int> ObjectIndexes;
        WwiseStream w;
        WwiseBank wb;
        public string afcPath = "";

        public Soundplorer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    LoadME3Package(d.FileName);
                    CurrentFile = d.FileName;
                    afcPath = "";
                    LoadObjects();
                    Status.Text = "Ready";
                    saveToolStripMenuItem.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                }
            }
        }

        public void LoadObjects()
        {
            listBox1.Items.Clear();
            ObjectIndexes = new List<int>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            for (int i=0; i < Exports.Count; i++)            
            {
                IExportEntry e = Exports[i];
                Status.Text = "Scan object " + i + " / " + Exports.Count;
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
            IExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                w = new WwiseStream(pcc as ME3Package, index);                
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
                wb = new WwiseBank(pcc as ME3Package, index);
                hb1.ByteProvider = new DynamicByteProvider(wb.getBinary());
            }
        }

        private void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            PlaySound(n);
        }

        private void PlaySound(int n)
        {
            int index = ObjectIndexes[n];
            IExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                Stop();
                w = new WwiseStream(pcc as ME3Package, index);
                string path;
                if (w.IsPCCStored)
                {
                    path = pcc.FileName;
                }
                else
                {
                    path = getPathToAFC();
                }
                if (path != "")
                {
                    Status.Text = "Loading...";
                    w.Play(path);
                    Status.Text = "Ready";
                }
            }
        }

        private string getPathToAFC()
        {
            string path = ME3Directory.cookedPath;
            if (!File.Exists(path + w.FileName + ".afc"))
            {
                if (!File.Exists(afcPath + w.FileName + ".afc"))
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = w.FileName + ".afc|" + w.FileName + ".afc";
                    if (d.ShowDialog() == DialogResult.OK)
                    {
                        afcPath = Path.GetDirectoryName(d.FileName) + '\\';
                    }
                    else
                    {
                        return "";
                    }
                }
                return afcPath;
            }
            return path;
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
            IExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.wav|*.wav";
                if(ex.ObjectName.Length > 4)
                    d.FileName = ex.ObjectName.Substring(0, ex.ObjectName.Length - 4);
                if (d.ShowDialog() == DialogResult.OK)
                {
                    string path;
                    if (w.IsPCCStored)
                    {
                        path = pcc.FileName;
                    }
                    else
                    {
                        path = getPathToAFC();
                    }
                    if (path != "")
                    {
                        Status.Text = "Exporting...";
                        w.ExtractToFile(path, d.FileName,false);
                        Status.Text = "Ready";
                        MessageBox.Show("Done");
                    }
                }
            }
        }

        private void importFromWAVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            IExportEntry ex = pcc.Exports[index];
            if (w.IsPCCStored)
            {
                //TODO: enable replacing of PCC-stored sounds
                MessageBox.Show("Cannot replace pcc-stored sounds.");
                return;
            }
            if (ex.ClassName == "WwiseStream")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.wav|*.wav";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    string path;
                    if (w.IsPCCStored)
                    {
                        path = pcc.FileName;
                    }
                    else
                    {
                        path = getPathToAFC();
                    }
                    if (path != "")
                    {
                        Status.Text = "Importing...";
                        w.ImportFromFile(d.FileName, path);
                        ex.Data = w.memory.TypedClone();
                        Status.Text = "Ready";
                        MessageBox.Show("Done");
                    }
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
                pcc.save();
        }

        private void directAFCReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (w.IsPCCStored)
            {
                MessageBox.Show("Cannot do an afc replace on a pcc-stored sound.");
                return;
            }
            DirectReplace dr = new DirectReplace();
            dr.MdiParent = this.MdiParent;
            dr.Show();
            dr.WindowState = FormWindowState.Maximized;
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int index = ObjectIndexes[n];
            IExportEntry ex = pcc.Exports[index];
            if (ex.ClassName == "WwiseStream")
            {
                dr.textBox3.Text = w.DataOffset.ToString();
                dr.textBox2.Text = w.FileName + ".afc";
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            
            if (updatedExports.Intersect(ObjectIndexes).Count() > 0)
            {
                LoadObjects();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (pcc.getExport(i).ClassName == "WwiseStream")
                    {
                        LoadObjects();
                        break;
                    }
                }
            }
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                int n = listBox1.SelectedIndex;
                if (n == -1)
                    return;
                PlaySound(n);
            }
        }
    }
}
