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
    public partial class AFCExtract : Form
    {
        public struct Entry
        {
            public int off;
            public int size;
        }

        byte[] memory = new byte[0];
        int memsize = 0;
        public List<Entry> entr;

        
        public AFCExtract()
        {
            InitializeComponent();
        }


        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = string.Empty;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "AFC files (*.afc)|*.afc";
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
                    if(i>3 && memory[i] == 0x46)
                        if (memory[i - 3] == 0x52
                        && memory[i - 2] == 0x49
                        && memory[i - 1] == 0x46)
                        {
                            int n = entr.Count;
                            Entry temp = new Entry();
                            if (n == 0)
                            {
                                temp.off = 0;
                                temp.size = i - 3;
                                entr.Add(temp);
                                temp.size = 0;
                                temp.off = i - 3;
                                entr.Add(temp);
                            }
                            else
                            {
                                temp.off = i - 3;
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
            FileDialog1.Filter = "Wave files (*.wav)|*.wav";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = FileDialog1.FileName;
                listBox2.Items.Add("\nExtract Wwise");
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                FileStream fileStream = new FileStream(loc + "\\exec\\out.dat", FileMode.Create, FileAccess.Write);
                Entry t = entr[n];
                for (int i = 0; i < t.size; i++)
                    fileStream.WriteByte(memory[t.off + i]);
                fileStream.Close();
                listBox2.Items.Add("\nConvert to Ogg");
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\ww2ogg.exe", "out.dat");
                procStartInfo.WorkingDirectory = loc + "\\exec";
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                proc.WaitForExit();
                proc.Close();
                listBox2.Items.Add("\nConvert to Wav");
                procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\oggdec.exe", "out.ogg");
                procStartInfo.WorkingDirectory = loc + "\\exec";
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                proc.WaitForExit();
                File.Copy(loc + "\\exec\\out.wav", path);
                File.Delete(loc + "\\exec\\out.ogg");
                File.Delete(loc + "\\exec\\out.dat");
                listBox2.Items.Add("\n Clean up. \nDone.");
                MessageBox.Show("Done.");
            }
        }

        private void allToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog m = new System.Windows.Forms.FolderBrowserDialog();
            m.ShowDialog();
            if (m.SelectedPath != "")
            {
                string dir = m.SelectedPath +"\\";
                for (int i = 0; i < entr.Count; i++)
                {
                    listBox2.Items.Add("\n#" + i.ToString() + "/" + (entr.Count-1).ToString() + " Extracting " + entr[i].off.ToString("X"));
                    listBox2.Items.Add("\n#" + i.ToString() + "/" + (entr.Count - 1).ToString() + " Extract Wwise");
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                    string loc = Path.GetDirectoryName(Application.ExecutablePath);
                    FileStream fileStream = new FileStream(loc + "\\exec\\out.dat", FileMode.Create, FileAccess.Write);
                    Entry t = entr[i];
                    for (int j = 0; j < t.size; j++)
                        fileStream.WriteByte(memory[t.off + j]);
                    fileStream.Close();
                    listBox2.Items.Add("\n#" + i.ToString() + "/" + (entr.Count - 1).ToString() + " Convert to Ogg");
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                    System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\ww2ogg.exe", "out.dat");
                    procStartInfo.WorkingDirectory = loc + "\\exec";
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo = procStartInfo;
                    proc.Start();
                    proc.WaitForExit();
                    proc.Close();
                    listBox2.Items.Add("\n#" + i.ToString() + "/" + (entr.Count - 1).ToString() + " Convert to Wav");
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                    procStartInfo = new System.Diagnostics.ProcessStartInfo(loc + "\\exec\\oggdec.exe", "out.ogg");
                    procStartInfo.WorkingDirectory = loc + "\\exec";
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    proc = new System.Diagnostics.Process();
                    proc.StartInfo = procStartInfo;
                    proc.Start();
                    proc.WaitForExit();
                    File.Copy(loc + "\\exec\\out.wav", dir + entr[i].off.ToString("X") + ".wav");
                    File.Delete(loc + "\\exec\\out.ogg");
                    File.Delete(loc + "\\exec\\out.dat");
                    listBox2.Items.Add("\n#" + i.ToString() + "/" + (entr.Count - 1).ToString() + " Clean up. \nDone.");
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                    Application.DoEvents();
                }
                MessageBox.Show("Done.");
            }
        }
    }
}
