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
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Meshplorer
{
    public partial class MeshDatabase : Form
    {
        public MeshDatabase()
        {
            InitializeComponent();
        }

        public struct ObjInf
        {
            public int Index;
            public int Type; //0=STM,1=SKM
            public string name;
        }

        public struct DBEntry
        {
            public string filename;
            public List<ObjInf> Objects;
        }

        public List<DBEntry> database;
        public Meshplorer MyParent;

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(path, "*.pcc");
            pb1.Maximum = files.Length;
            DebugOutput.Clear();
            database = new List<DBEntry>();
            int count = 0;
            foreach (string file in files)
            {
                pb1.Value = count++;
                DebugOutput.PrintLn("Scanning file : " + Path.GetFileName(file) + " ...");
                PCCObject pcc = new PCCObject(file);
                DBEntry ent = new DBEntry();
                ent.filename = Path.GetFileName(file);
                ent.Objects = new List<ObjInf>();
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    PCCObject.ExportEntry ex = pcc.Exports[i];
                    ObjInf obj;
                    switch (ex.ClassName)
                    {
                        case "StaticMesh":
                            obj = new ObjInf();
                            obj.Index = i;
                            obj.Type = 0;
                            obj.name = ex.ObjectName;
                            ent.Objects.Add(obj);
                            break;
                        case "SkeletalMesh":
                            obj = new ObjInf();
                            obj.Index = i;
                            obj.Type = 1;
                            obj.name = ex.ObjectName;
                            ent.Objects.Add(obj);
                            break;
                    }
                }
                if (ent.Objects.Count != 0)
                {
                    DebugOutput.PrintLn("Found " + ent.Objects.Count + " Objects:", false);
                    //foreach (ObjInf o in ent.Objects)
                    //    DebugOutput.PrintLn("\t" + o.Index + " : " + o.name + " (" + TypeToString(o.Type) + ")", false);
                    //DebugOutput.Update();
                    database.Add(ent);
                }
                else
                {
                    DebugOutput.PrintLn("Nothing...", false);
                }
            }
            RefreshLists();
            pb1.Value = 0;
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            foreach (DBEntry e in database)
                listBox1.Items.Add(e.filename);
        }

        public string TypeToString(int type)
        {
            if (type == 0)
                return "StaticMesh";
            if (type == 1)
                return "SkeletalMesh";
            return "";
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if(n==-1)
                return;
            listBox2.Items.Clear();
            DBEntry en = database[n];
            foreach (ObjInf o in en.Objects)
                listBox2.Items.Add(o.Index + " : " + o.name + " (" + TypeToString(o.Type) + ")");
        }

        private void saveDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                int magic = 0x12345678;
                BitConverter.IsLittleEndian = true;
                fs.Write(BitConverter.GetBytes(magic), 0, 4);
                fs.Write(BitConverter.GetBytes((int)database.Count), 0, 4);
                foreach (DBEntry ent in database)
                {
                    WriteString(fs, ent.filename);
                    fs.Write(BitConverter.GetBytes((int)ent.Objects.Count), 0, 4);
                    foreach (ObjInf o in ent.Objects)
                    {
                        fs.Write(BitConverter.GetBytes((int)o.Index), 0, 4);
                        fs.Write(BitConverter.GetBytes((int)o.Type), 0, 4);
                        WriteString(fs, o.name);
                    }
                }
                fs.Close();
                MessageBox.Show("Done");
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes((int)s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);   
        }

        public string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        public int ReadInt32(FileStream fs)
        {
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            return BitConverter.ToInt32(buff, 0);
        }

        private void loadDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                BitConverter.IsLittleEndian = true;
                int magic = ReadInt32(fs);
                if (magic != 0x12345678)
                {
                    MessageBox.Show("Not a database!");
                    fs.Close();
                    return;
                }
                int count = ReadInt32(fs);
                database = new List<DBEntry>();
                for (int i = 0; i < count; i++)
                {
                    DBEntry en = new DBEntry();
                    en.filename = ReadString(fs);
                    en.Objects = new List<ObjInf>();
                    int count2 = ReadInt32(fs);
                    for (int j = 0; j < count2; j++)
                    {
                        ObjInf o = new ObjInf();
                        o.Index = ReadInt32(fs);
                        o.Type = ReadInt32(fs);
                        o.name = ReadString(fs);
                        en.Objects.Add(o);
                    }
                    database.Add(en);
                }
                fs.Close();
                RefreshLists();
                MessageBox.Show("Done");
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            DBEntry en = database[n];
            MyParent.LoadPCC(ME3Directory.cookedPath + en.filename);
            MyParent.BringToFront();
        }

        private void listBox2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int m = listBox2.SelectedIndex;
            if (m == -1)
                return;
            DBEntry en = database[n];
            MyParent.LoadPCC(ME3Directory.cookedPath + en.filename);
            if (MyParent.listBox1.Items.Count < m)
                MyParent.listBox1.SelectedIndex = m;
            MyParent.BringToFront();
        }
    }
}
