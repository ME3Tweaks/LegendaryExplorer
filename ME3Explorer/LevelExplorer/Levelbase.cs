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

namespace ME3Explorer.LevelExplorer
{
    public partial class Levelbase : Form
    {
        public string DataBaseFile;
        public string location;

        public struct DBEntry
        {            
            public string filepath;
            public int index;
            public int count;
        }

        public List<DBEntry> database = new List<DBEntry>();

        public Levelbase()
        {
            InitializeComponent();
        }

        public void LoadDataBase()
        {
            FileStream fs = new FileStream(DataBaseFile, FileMode.Open, FileAccess.Read);
            BitConverter.IsLittleEndian = true;
            database = new List<DBEntry>();
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            int count = BitConverter.ToInt32(buff,0);
            for (int i = 0; i < count; i++) 
            {
                DBEntry e = new DBEntry();
                fs.Read(buff, 0, 4);
                e.index = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                e.count = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                int len = BitConverter.ToInt32(buff, 0);
                e.filepath = "";
                for (int j = 0; j < len; j++)
                    e.filepath += (char)fs.ReadByte();
                database.Add(e);
            }
            fs.Close();            
        }

        public void RefreshList()
        {
            listBox1.Items.Clear();
            foreach (DBEntry e in database)
                listBox1.Items.Add(Path.GetFileName(e.filepath) + " #" + e.index + " Objects (" + e.count + ")");            
        }

        public void CreateDataBase()
        {
            FileStream fs = new FileStream(DataBaseFile, FileMode.Create, FileAccess.Write);
            string pathcook = ME3Directory.cookedPath;
            DebugOutput.Clear();
            DebugOutput.PrintLn("Levelbase.cs: Loading files from :" + pathcook);
            string[] files = Directory.GetFiles(pathcook, "*.pcc");
            for (int i = 0; i < files.Length; i++) 
            {
                string file = files[i];
                DebugOutput.PrintLn(i + "/" + (files.Length - 1) + " Scanning : " + Path.GetFileName(file));
                PCCObject pcc = new PCCObject(file);
                for (int j = 0; j < pcc.Exports.Count(); j++)
                {
                    PCCObject.ExportEntry e = pcc.Exports[j];
                    if (e.ClassName == "Level")
                    {
                        Level l = new Level(pcc, j, true);
                        DBEntry entry = new DBEntry();
                        entry.filepath = file;
                        entry.index = j;
                        entry.count = l.Objects.Count();
                        database.Add(entry);
                        //foreach(int idx in l.Objects)
                        //    if (pcc.isExport(idx) && pcc.Exports[idx].ClassName == "BioPlaypenVolumeAdditive")
                        //        DebugOutput.PrintLn("#############################found");
                        DebugOutput.PrintLn("\tfound Level with " + entry.count + " Objects");
                    }
                }
            }
            database.Sort((a,b) => a.filepath.CompareTo(b.filepath));
            BitConverter.IsLittleEndian = true;
            byte[] buff = BitConverter.GetBytes(database.Count());
            fs.Write(buff, 0, 4);
            foreach (DBEntry e in database)
            {
                buff = BitConverter.GetBytes(e.index);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(e.count);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(e.filepath.Length);
                fs.Write(buff, 0, 4);
                foreach (char c in e.filepath)
                    fs.WriteByte((byte)c);
            }
            fs.Close();
        }

        private void Levelbase_Activated(object sender, EventArgs e)
        {
            location = Path.GetDirectoryName(Application.ExecutablePath);
            DataBaseFile = location + "\\exec\\levelz.dbs";
            if (File.Exists(DataBaseFile))
                LoadDataBase();
            else
            {
                DialogResult res = MessageBox.Show("No database found. Do you want to start a scan and create one?\n (this may take a while, so have debug window open)", "No database found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == System.Windows.Forms.DialogResult.Yes)
                    CreateDataBase();
            }
            RefreshList();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            DBEntry l = database[n];
            if (File.Exists(l.filepath))
            {

                PCCObject pcc = new PCCObject(l.filepath);
                Level lev = new Level(pcc, l.index, true);
                string s = "";
                s += "Loading Level from : " + Path.GetFileName(l.filepath) + "\n";
                s += "Object count : " + lev.Objects.Count + "\n==============\n\n";
                for (int i = 0; i < lev.Objects.Count(); i++)
                {
                    int index = lev.Objects[i];
                    s += "(" + i + "/" + (lev.Objects.Count() - 1) + ") ";
                    s += "#" + index + " : \"" + pcc.Exports[index].ObjectName + "\" Class : \"" + pcc.Exports[index].ClassName + "\"\n";
                }
                rtb1.Text = s;
            }
        }

        private void createToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateDataBase();
            RefreshList();
        }

        private void openInLeveleditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            DBEntry d = database[n];
            LevelEditor.Leveleditor led = new LevelEditor.Leveleditor();
            led.MdiParent = this.MdiParent;
            led.Show();
            led.WindowState = FormWindowState.Maximized;
            led.LoadPCC(d.filepath);
        }
    }
}
