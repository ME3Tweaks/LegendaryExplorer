using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using Be.Windows.Forms;

namespace ME3Explorer
{
    public partial class CloneDialog2 : Form
    {
        public PCCObject pcc;
        public bool isExport;
        public int Index = -1;

        public CloneDialog2()
        {
            InitializeComponent();
            hb1.ByteProvider = new DynamicByteProvider(new byte[0]);
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                ListRefresh();
            }
        }

        public string getExportPath(int i)
        {
            string s = "";
            if (pcc.Exports[i].PackageFullName != "Class" && pcc.Exports[i].PackageFullName != "Package")
                s += pcc.Exports[i].PackageFullName + ".";
            s += pcc.Exports[i].ObjectName;
            return s;
        }

        public string getImportPath(int i)
        {
            string s = "";
            if (pcc.Imports[i].PackageFullName != "Class" && pcc.Imports[i].PackageFullName != "Package")
                s += pcc.Imports[i].PackageFullName + ".";
            s += pcc.Imports[i].ObjectName;
            return s;
        }

        public void ListRefresh()
        {
            int count = 0;
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            listBox3.Items.Clear();
            listBox4.Items.Clear();
            listBox5.Items.Clear();
            listBox6.Items.Clear();
            listBox7.Items.Clear();
            listBox8.Items.Clear();
            listBox9.Items.Clear();
            foreach (PCCObject.ExportEntry e in pcc.Exports)
            {
                listBox1.Items.Add(count.ToString("d6") + " : " + getExportPath(count));
                listBox3.Items.Add(count.ToString("d6") + " : " + getExportPath(count));
                listBox5.Items.Add(count.ToString("d6") + " : " + getExportPath(count++));
            }
            count = 0;
            foreach (PCCObject.ImportEntry i in pcc.Imports)
            {
                listBox2.Items.Add(count.ToString("d6") + " : " + getImportPath(count));
                listBox4.Items.Add(count.ToString("d6") + " : " + getImportPath(count));
                listBox6.Items.Add(count.ToString("d6") + " : " + getImportPath(count++));
            }
            listBox7.Items.Clear();
            listBox7.Items.Add("Class");
            listBox8.Items.Clear();
            listBox8.Items.Add("Root");
            count = 0;
            foreach (string s in pcc.Names)
                listBox9.Items.Add((count++).ToString("d6") + " : " + s);
        }

        public void RefreshFromSelected()
        {
            if (Index == -1)
                return;
            if (isExport)
            {
                PCCObject.ExportEntry e = pcc.Exports[Index];
                if (e.idxClassName == 0)
                    listBox7.SelectedIndex = 0;
                else
                {
                    if (e.idxClassName > 0)
                        listBox3.SelectedIndex = e.idxClassName - 1;
                    else
                        listBox4.SelectedIndex = -e.idxClassName - 1;
                }
                if (e.idxLink == 0)
                    listBox8.SelectedIndex = 0;
                else
                {
                    if (e.idxLink > 0)
                        listBox5.SelectedIndex = e.idxLink - 1;
                    else
                        listBox6.SelectedIndex = -e.idxLink - 1;
                }
                hb1.ByteProvider = new DynamicByteProvider(pcc.Exports[Index].Data);
                listBox9.SelectedIndex = e.idxObjectName;
            }
            else
            {
                PCCObject.ImportEntry i = pcc.Imports[Index];
                //if (i.idxPackageName == 0)
                //    listBox7.SelectedIndex = 0;
                //else
                //{
                //    if (i.idxPackageName > 0)
                //        listBox3.SelectedIndex = i.idxPackageName - 1;
                //    else
                //        listBox4.SelectedIndex = -i.idxPackageName - 1;
                //}
                if (i.idxLink == 0)
                    listBox8.SelectedIndex = 0;
                else
                {
                    if (i.idxLink > 0)
                        listBox5.SelectedIndex = i.idxLink - 1;
                    else
                        listBox6.SelectedIndex = -i.idxLink - 1;
                }
                listBox9.SelectedIndex = i.idxObjectName;
            }
        }

        public void RefreshSummary()
        {
            string res = "";
            if (Index == -1)
                return;
            if (isExport)
                res += "1.Make a clone of export #" + Index + "\n";
            else
                res += "1.Make a clone of import #" + Index + "\n";
            res += "2.Use name : " + pcc.getNameEntry(listBox9.SelectedIndex) + "\n";
            if (isExport)
            {
                res += "3.Use class : ";
                if (listBox3.SelectedIndex != -1)
                    res += pcc.Exports[listBox3.SelectedIndex].ObjectName;
                if (listBox4.SelectedIndex != -1)
                    res += pcc.Imports[listBox4.SelectedIndex].ObjectName;
                if (listBox7.SelectedIndex != -1)
                    res += "Class";            
                res += "\n";
            }
            else
                res += "3.Use class : " + pcc.Imports[Index].ClassName + "\n";
            res += "4.Use data len = " + hb1.ByteProvider.Length + " bytes\n5.Use link : ";
            if (listBox5.SelectedIndex != -1)
                res += getExportPath(listBox5.SelectedIndex);
            if (listBox6.SelectedIndex != -1)
                res += getImportPath(listBox6.SelectedIndex);
            if (listBox8.SelectedIndex != -1)
                res += "Root";
            rtb1.Text = res;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            listBox2.SelectedIndex = -1;
            listBox1.SelectedIndex = n;
            isExport = true;
            Index = n;
            RefreshFromSelected();
            RefreshSummary();
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            listBox1.SelectedIndex = -1;
            listBox2.SelectedIndex = n;
            isExport = false;
            Index = n;
            RefreshFromSelected();
            RefreshSummary();
        }

        private void listBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox3.SelectedIndex;
            listBox4.SelectedIndex = -1;
            listBox7.SelectedIndex = -1;
            listBox3.SelectedIndex = n;
            RefreshSummary();
        }

        private void listBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox4.SelectedIndex;
            listBox3.SelectedIndex = -1;
            listBox7.SelectedIndex = -1;
            listBox4.SelectedIndex = n;
            RefreshSummary();
        }

        private void listBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox5.SelectedIndex;
            listBox6.SelectedIndex = -1;
            listBox8.SelectedIndex = -1;
            listBox5.SelectedIndex = n;
            RefreshSummary();
        }

        private void listBox6_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox6.SelectedIndex;
            listBox5.SelectedIndex = -1;
            listBox8.SelectedIndex = -1;
            listBox6.SelectedIndex = n;
            RefreshSummary();
        }

        private void listBox7_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox7.SelectedIndex;
            listBox4.SelectedIndex = -1;
            listBox3.SelectedIndex = -1;
            listBox7.SelectedIndex = n;
            RefreshSummary();
        }

        private void listBox8_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox8.SelectedIndex;
            listBox6.SelectedIndex = -1;
            listBox5.SelectedIndex = -1;
            listBox8.SelectedIndex = n;
            RefreshSummary();
        }

        private void savePccFile(string path)
        {
            if (path == null)
                return;

            BitConverter.IsLittleEndian = true;
            if (pcc.bCompressed)
            {
                Print("\nFile is compressed, saving uncompressed...");
                pcc.altSaveToFile(path, true);
                Print("Done.Reloading...");
                pcc = new PCCObject(path);
                Print("Done.");
            }
            Print("\nCreating Header...");
            FileStream fs;
            if (File.Exists(path))
                fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            else
                fs = new FileStream(pcc.pccFileName, FileMode.Open, FileAccess.Read);
            FileStream fsout = new FileStream(path + "_tmp", FileMode.Create, FileAccess.Write);
            fs.Seek(0xC, 0);
            int GroupNameLen = GetInt(fs);
            Print("Group Name Length: " + GroupNameLen);
            int testpos = 0x14 + (-GroupNameLen) * 2;
            fs.Seek(testpos, 0);
            int test = GetInt(fs);
            Print("Test Value: " + test);
            int start;
            if (test == 0)
                start = testpos + 4;
            else
                start = testpos;
            int end = start + 0x6C;
            Print("Start Position: 0x" + start.ToString("X8"));
            Print("End Position: 0x" + end.ToString("X8"));
            Print("Writing Header...");
            fs.Seek(0, 0);
            byte[] buff = new byte[end];
            fs.Read(buff, 0, end);
            fsout.Write(buff, 0, end);
            int pos = end;
            Print("Writing Names...");
            fsout.Seek(pos, 0);
            foreach (string s in pcc.Names)
                pos += WriteName(fsout, s);
            Print("Done. Pos = 0x" + pos.ToString("X8"));
            int NewImportStart = pos;
            Print("Writing Import Table...");
            foreach (PCCObject.ImportEntry i in pcc.Imports)
            {
                fsout.Write(i.data, 0, i.data.Length);
                pos += i.data.Length;
            }
            if (!isExport)
            {
                int len = pcc.Imports[Index].data.Length;
                fsout.Write(pcc.Imports[Index].data, 0, len);
                pos += len;
            }
            Print("Done. Pos = 0x" + pos.ToString("X8"));
            int NewExportStart = pos;
            Print("Writing Export Table...");
            int count = 0;
            foreach (PCCObject.ExportEntry ex in pcc.Exports)
            {
                fsout.Write(ex.info, 0, ex.info.Length);
                pcc.Exports[count++].offset = (uint)pos;
                pos += ex.info.Length;
            }
            if (isExport)
            {
                int len = pcc.Exports[Index].info.Length;
                fsout.Write(pcc.Exports[Index].info, 0, len);
                pos += len;
            }
            Print("Done. Pos = 0x" + pos.ToString("X8"));
            Print("Writing Exports Data...");
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                pcc.Exports[i].DataOffsetTmp = pos;
                fsout.Write(pcc.Exports[i].Data, 0, pcc.Exports[i].Data.Length);
                pos += pcc.Exports[i].Data.Length;
            }
            if (isExport)
            {
                DynamicByteProvider b = (DynamicByteProvider)hb1.ByteProvider;
                for (int i = 0; i < b.Length; i++)
                    fsout.WriteByte(b.ReadByte(i));
            }
            Print("Fixing Header...");
            if (isExport)
            {
                fsout.Seek(start + 8, 0);
                fsout.Write(BitConverter.GetBytes(pcc.Exports.Count + 1), 0, 4);
            }
            else
            {
                fsout.Seek(start + 16, 0);
                fsout.Write(BitConverter.GetBytes(pcc.Imports.Count + 1), 0, 4);
            }
            fsout.Seek(start, 0);
            fsout.Write(BitConverter.GetBytes(pcc.Names.Count), 0, 4);
            fsout.Write(BitConverter.GetBytes(end), 0, 4);
            fsout.Seek(start + 0xC, 0);
            fsout.Write(BitConverter.GetBytes(NewExportStart), 0, 4);
            fsout.Seek(start + 0x14, 0);
            fsout.Write(BitConverter.GetBytes(NewImportStart), 0, 4);
            int datastart = pcc.Exports[0].DataOffset;
            fsout.Write(BitConverter.GetBytes(datastart), 0, 4);//Fix zero page (start)
            fsout.Write(BitConverter.GetBytes(datastart), 0, 4);//make it zero :P(end)
            Print("Fixing Export Table...");
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                fsout.Seek(pcc.Exports[i].offset + 0x24, 0);
                fsout.Write(BitConverter.GetBytes(pcc.Exports[i].DataOffsetTmp), 0, 4);
            }
            if (isExport)
            {
                Print("Fixing Clone...");
                int clonestart = (int)pcc.Exports[pcc.Exports.Count - 1].offset + (int)pcc.Exports[pcc.Exports.Count - 1].info.Length;
                fsout.Seek(clonestart, 0);//Class
                if (listBox3.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(listBox3.SelectedIndex + 1), 0, 4);
                if (listBox4.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(-listBox4.SelectedIndex - 1), 0, 4);
                if (listBox7.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(0), 0, 4);
                fsout.Seek(clonestart + 0x8, 0);//Link
                if (listBox5.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(listBox5.SelectedIndex + 1), 0, 4);
                if (listBox6.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(-listBox6.SelectedIndex - 1), 0, 4);
                if (listBox8.SelectedIndex != -1)
                    fsout.Write(BitConverter.GetBytes(0), 0, 4);
                fsout.Write(BitConverter.GetBytes(listBox9.SelectedIndex), 0, 4);//Name
                fsout.Seek(clonestart + 0x24, 0);//DataOffset
                fsout.Write(BitConverter.GetBytes(pcc.Exports[pcc.Exports.Count - 1].DataOffsetTmp + pcc.Exports[pcc.Exports.Count - 1].Data.Length), 0, 4);
            }

            fsout.Close();
            fs.Close();
            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + "_tmp", path);


        }

		private void saveCloneToolStripMenuItem_Click(object sender, EventArgs e) {
            if (pcc == null || Index == -1)
                return;
            if (pcc.pccFileName != null)
            {
                string path = pcc.pccFileName;
                savePccFile(path);

                // Reload File
                Print("Reloading File...");
                pcc = new PCCObject(path);

                Print("Checking for texture...");
                if (path.StartsWith(Path.Combine(Properties.Settings.Default.TexplorerME3Path, "CookedPCConsole")) && pcc.Exports[pcc.Exports.Count - 1].ClassName == "Texture2D" || pcc.Exports[pcc.Exports.Count - 1].ClassName == "LightMapTexture2D" || pcc.Exports[pcc.Exports.Count - 1].ClassName == "TextureFlipBook")
                {
                    Print("PCC in Game Directory. Texture Found. Adding to DB...");
                    int exportID = (pcc.Exports.Count - 1);
                    Texplorer2 tp = new Texplorer2(true);
                    tp.AddToTree(path, exportID);
                    MessageBox.Show("Texture added to DB.");
                }
                else
                {
                    Print("No Texture Found or File not in Game Directory.");
                }

                Print("Refreshing Screen...");
                ListRefresh();

                MessageBox.Show("Done.");

            }
		}

        private void savePccWithCloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Index == -1)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = d.FileName;
                savePccFile(path);

                MessageBox.Show("Done.");
            }
        }

        public int WriteName(FileStream fs, string name)
        {
            int len = 6 + name.Length * 2;
            int ilen = -name.Length;
            fs.Write(BitConverter.GetBytes(ilen - 1), 0, 4);
            foreach (char c in name)
                fs.Write(BitConverter.GetBytes((byte)c), 0, 2);
            fs.WriteByte(0);
            fs.WriteByte(0);
            return len;
        }

        public int GetInt(FileStream fs)
        {
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        public void Print(string s)
        {
            rtb1.AppendText(s + "\n");
        }

        private void listBox9_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshSummary();
        }
    }
}
