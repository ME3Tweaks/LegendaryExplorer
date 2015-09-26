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
using ME3Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;
using KFreonLib.Debugging;

namespace ME3Explorer.ScriptDB
{
    public partial class ScriptDB : Form
    {
        public ScriptDB()
        {
            InitializeComponent();
        }

        public struct ScriptEntry
        {
            public string name;
            public string file;
            public string script;
        }

        public List<ScriptEntry> database;

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string pathcook = ME3Directory.cookedPath;
            DebugOutput.StartDebugger("ScriptDB");
            string[] files = Directory.GetFiles(pathcook, "*.pcc");
            int count = 1;
            database = new List<ScriptEntry>();
            foreach (string file in files)
            {
                DebugOutput.PrintLn(count + "\\" + files.Length + " : Scanning " + Path.GetFileName(file) + " ...");
                PCCObject pcc = new PCCObject(file);
                int count2 = 0;
                foreach (PCCObject.ExportEntry ent in pcc.Exports)
                {
                    if (ent.ClassName == "Function")
                    {
                        Function f = new Function(ent.Data, pcc);
                        ScriptEntry n = new ScriptEntry();
                        n.file = Path.GetFileName(file);
                        n.name = ent.PackageFullName + "." + ent.ObjectName;
                        n.script = f.ToRawText(false);
                        database.Add(n);
                        DebugOutput.PrintLn("\tFound \"" + n.name + "\"",false);
                    }
                    count2++;
                }
                {
                    pb1.Maximum = files.Length;
                    pb1.Value = count;
                }
                count++;
            }
            RefreshList();
        }

        public void RefreshList()
        {
            listBox1.Items.Clear();
            foreach (ScriptEntry e in database)
                listBox1.Items.Add(e.file + " : " + e.name);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            rtb1.Text = database[n].script;
        }

        private void saveDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.sdb|*.sdb";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian = true;
                byte[] buff = BitConverter.GetBytes((int)database.Count());
                DebugOutput.PrintLn("Creating file in memory...");
                fs.Write(buff, 0, 4);
                MemoryStream m = new MemoryStream();
                foreach (ScriptEntry ent in database)
                {
                    WriteString(m, ent.file);
                    WriteString(m, ent.name);
                    WriteString(m, ent.script);
                }
                buff = m.ToArray();
                DebugOutput.PrintLn("Saving to disk...");
                fs.Write(buff, 0, buff.Length);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void loadDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.sdb|*.sdb";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                BitConverter.IsLittleEndian = true;
                DebugOutput.PrintLn("Loading file to memory");
                byte[] buff = new byte[fs.Length];
                int cnt;
                int sum = 0;
                while ((cnt = fs.Read(buff, sum, buff.Length - sum)) > 0)
                {
                    sum += cnt;
                    DebugOutput.PrintLn(sum + "\\" + buff.Length);
                }
                fs.Close();
                DebugOutput.PrintLn("Reading data");
                MemoryStream m = new MemoryStream(buff);
                m.Seek(4, SeekOrigin.Begin);
                int count = BitConverter.ToInt32(buff, 0);
                database = new List<ScriptEntry>();
                for (int i = 0; i < count; i++) 
                {
                    ScriptEntry n = new ScriptEntry();
                    n.file = ReadString(m);
                    n.name = ReadString(m);
                    n.script = ReadString(m);
                    database.Add(n);
                }
                DebugOutput.PrintLn("Reading done. Refreshing lists...");
                RefreshList();
                MessageBox.Show("Done.");
            }
        }

        public void WriteString(MemoryStream fs, string s)
        {
            byte[] buff = BitConverter.GetBytes((int)s.Length);
            fs.Write(buff, 0, 4);
            foreach (char c in s)
                fs.WriteByte((byte)c);
        }

        public string ReadString(MemoryStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            int len = BitConverter.ToInt32(buff, 0);
            for (int i = 0; i < len; i++)
                s += (char)fs.ReadByte();
            return s;
        }
    }
}
