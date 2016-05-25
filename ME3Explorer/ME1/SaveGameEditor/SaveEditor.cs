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
using Ionic.Zip;

namespace ME1Explorer.SaveGameEditor
{
    public partial class SaveEditor : Form
    {
        public struct SaveFileEntry
        {
            public MemoryStream memory;
            public string FileName;
        }

        public struct SaveGame
        {
            public List<SaveFileEntry> Files;
            public MemoryStream complete;
            public MemoryStream zipfile;
            public ZipFile zip;
        }
        public SaveGame Save;

        public SaveEditor()
        {
            InitializeComponent();
        }

        private void openSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.MassEffectSave|*.MassEffectSave";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MemoryStream m = new MemoryStream();
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                int len = (int)fs.Length;
                byte[] buff = new byte[len];
                fs.Read(buff, 0, len);
                m.Write(buff, 0, len);
                fs.Close();
                Save = new SaveGame();
                Save.complete = m;
                BitConverter.IsLittleEndian = true;                
                m.Seek(8, SeekOrigin.Begin);
                int off = ReadInt(m);
                int len2 = len - off;
                m.Seek(off, SeekOrigin.Begin);
                buff = new byte[len2];
                m.Read(buff, 0, len2);
                Save.zipfile = new MemoryStream(buff);
                Save.zipfile.Seek(0, SeekOrigin.Begin);
                ZipFile zip = ZipFile.Read(Save.zipfile);            
                Save.Files = new List<SaveFileEntry>();
                foreach (ZipEntry file in zip)
                {
                    SaveFileEntry ent = new SaveFileEntry();
                    ent.FileName = file.FileName;
                    ent.memory = new MemoryStream();
                    file.Extract(ent.memory);                    
                    Save.Files.Add(ent);
                }
                Save.zip = zip;
                RefreshList();
            }
        }

        public void RefreshList()
        {
            if (Save.Files == null)
                return;
            listBox1.Items.Clear();
            foreach (SaveFileEntry e in Save.Files)
                listBox1.Items.Add(e.FileName);
        }

        private int ReadInt(MemoryStream m)
        {
            byte[] buff = new byte[4];
            m.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || Save.Files == null)
                return;
            hb1.ByteProvider = new DynamicByteProvider(Save.Files[n].memory.ToArray());
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || Save.Files == null)
                return;
            int len = (int)hb1.ByteProvider.Length;
            SaveFileEntry en = Save.Files[n];
            en.memory = new MemoryStream();
            for (int i = 0; i < len; i++)
                en.memory.WriteByte(hb1.ByteProvider.ReadByte(i));
            Save.Files[n] = en;
        }

        private void saveSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Save.Files == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.MassEffectSave|*.MassEffectSave";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff;
                ZipFile zip = new ZipFile();
                foreach (SaveFileEntry ent in Save.Files)
                {
                    FileStream fs2 = new FileStream(ent.FileName, FileMode.Create, FileAccess.Write);
                    fs2.Write(ent.memory.ToArray(), 0, (int)ent.memory.Length);
                    fs2.Close();
                    if (File.Exists(ent.FileName))
                    {
                        zip.AddFile(ent.FileName);
                        zip.Save("temp.zip");
                    }
                    File.Delete(ent.FileName);
                }
                Save.zip = zip;
                FileStream fs3 = new FileStream("temp.zip", FileMode.Open, FileAccess.Read);
                int len = (int)fs3.Length;
                buff = new byte[len];
                fs3.Read(buff, 0, len);
                fs3.Close();
                Save.zipfile = new MemoryStream(buff);
                File.Delete("temp.zip");
                BitConverter.IsLittleEndian = true;
                MemoryStream m = new MemoryStream();
                Save.complete.Seek(8, SeekOrigin.Begin);
                int off = ReadInt(Save.complete);
                Save.complete.Seek(0, SeekOrigin.Begin);
                buff = new byte[off];
                Save.complete.Read(buff, 0, off);
                m.Write(buff, 0, off);
                m.Write(Save.zipfile.ToArray(), 0, (int)Save.zipfile.Length);
                fs.Write(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                MessageBox.Show("Done");
            }
        }

        private void rawDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || Save.Files == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            string filename = Save.Files[n].FileName;
            d.Filter = filename + "|" + filename;
            d.FileName = filename;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(Save.Files[n].memory.ToArray(), 0, (int)Save.Files[n].memory.Length);
                fs.Close();
                MessageBox.Show("Done");
            }
        }

        private void extractAsME1PackageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || Save.Files == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            string filename = Save.Files[n].FileName;
            if (filename == "state.sav")
            {
                MessageBox.Show("This file is not a ME1 Package, please select one of the others");
                return;
            }
            filename = Path.GetFileNameWithoutExtension(filename);
            filename += ".upk";
            d.Filter = filename + "|" + filename;
            d.FileName = filename;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MemoryStream m = Save.Files[n].memory;
                m.Seek(8, SeekOrigin.Begin);
                int off = ReadInt(m);
                m.Seek(off, SeekOrigin.Begin);
                MemoryStream m2 = new MemoryStream();
                m2.Write(Save.Files[n].memory.ToArray(), off, (int)Save.Files[n].memory.Length - off);
                m2.Seek(0x1D, SeekOrigin.Begin);
                int realoff = ReadInt(m2);
                m = new MemoryStream();
                m.Write(m2.ToArray(), 0, 0x89);
                int len = realoff - 0x89;
                for (int i = 0; i < len; i++)
                    m.WriteByte(0);
                m.Write(m2.ToArray(), 0x89, (int)m2.Length - 0x89);
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void importFromME1PackageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || Save.Files == null)
                return;
            OpenFileDialog d = new OpenFileDialog();
            string filename = Save.Files[n].FileName;
            if (filename == "state.sav")
            {
                MessageBox.Show("This file is not a ME1 Package, please select one of the others");
                return;
            }
            filename = Path.GetFileNameWithoutExtension(filename);
            filename += ".upk";
            d.Filter = filename + "|" + filename;
            d.FileName = filename;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {                
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                byte[] buff = new byte[(int)fs.Length];
                fs.Read(buff, 0, (int)fs.Length);
                fs.Close();
                MemoryStream m = new MemoryStream(buff);
                MemoryStream m2 = new MemoryStream();
                m2.Write(m.ToArray(), 0, 0x89);
                m.Seek(0x1D, SeekOrigin.Begin);
                int off = ReadInt(m);
                m2.Write(m.ToArray(), off, (int)m.Length - off);
                m = new MemoryStream();
                Save.Files[n].memory.Seek(8, SeekOrigin.Begin);
                int off2 = ReadInt(Save.Files[n].memory);
                m.Write(Save.Files[n].memory.ToArray(), 0, off2);
                m.Write(m2.ToArray(), 0, (int)m2.Length);
                SaveFileEntry ent = Save.Files[n];
                ent.memory = m;
                Save.Files[n] = ent;
                MessageBox.Show("Done.");
            }
        }
    }    
}
