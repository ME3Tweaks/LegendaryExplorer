using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.PlotVarDB
{
    public partial class PlotVarDB : Form
    {
        public int MEVersion = 2; //ME1 = 0,ME2 = 1,ME3 = 2
        public int SortStyle = 0; //0 = by id, 1 = by type, 2 = by desc

        public struct PlotVarEntry
        {
            public int ID;
            public int type; // 0 = bool, 1 = int, 2 = float
            public string Desc;
        }

        public struct DataBaseType
        {
            public List<PlotVarEntry> ME1;
            public List<PlotVarEntry> ME2;
            public List<PlotVarEntry> ME3;
        }

        public DataBaseType database;

        public PlotVarDB()
        {
            InitializeComponent();
        }


        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            SetVersion(0);
        }

        public void SetVersion(int n)
        {
            if (n < 0 || n > 2)
                return;
            MEVersion = n;
            toolStripButton2.Checked = (n == 0);
            toolStripButton3.Checked = (n == 1);
            toolStripButton4.Checked = (n == 2);
            RefreshLists();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            SetVersion(1);
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            SetVersion(2);
        }

        private void PlotVarDB_Load(object sender, EventArgs e)
        {
            database = new DataBaseType();
            database.ME1 = new List<PlotVarEntry>();
            database.ME2 = new List<PlotVarEntry>();
            database.ME3 = new List<PlotVarEntry>();
        }

        private void addPlotVarDescriptionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PlotVarEditor ed = new PlotVarEditor();
            ed.MdiParent = this.MdiParent;
            ed.Show();
            ed.WindowState = FormWindowState.Maximized;
            ed.parent = this;
            ed.version = MEVersion;
        }

        public void SetSortStyle(int n)
        {
            if (n < 0 || n > 2)
                return;
            SortStyle = n;
            RefreshLists();
        }

        private void byIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortStyle(0);
        }

        private void byTypeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortStyle(1);
        }

        private void byDescriptionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSortStyle(2);
        }

        public string TypeToString(int type)
        {
            string res = "";
            switch (type)
            {
                case 0:
                    res = "bool";
                    break;
                case 1:
                    res = "int";
                    break;
                case 2:
                    res = "float";
                    break;
            }
            return res;
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            List<PlotVarEntry> temp = new List<PlotVarEntry>();
            if (MEVersion == 0)
                temp = database.ME1;
            if (MEVersion == 1)
                temp = database.ME2;
            if (MEVersion == 2)
                temp = database.ME3;
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < temp.Count - 1; i++)
                {
                    switch (SortStyle)
                    {
                        case 0:
                            if (temp[i].ID > temp[i + 1].ID)
                            {
                                run = true;
                                PlotVarEntry t = temp[i];
                                temp[i] = temp[i + 1];
                                temp[i + 1] = t;
                            }
                            break;
                        
                        case 1:
                            if (temp[i].type > temp[i + 1].type)
                            {
                                run = true;
                                PlotVarEntry t = temp[i];
                                temp[i] = temp[i + 1];
                                temp[i + 1] = t;
                            }
                            break;
                        case 2:
                            if (string.Compare(temp[i].Desc, temp[i + 1].Desc) < 0) 
                            {
                                run = true;
                                PlotVarEntry t = temp[i];
                                temp[i] = temp[i + 1];
                                temp[i + 1] = t;
                            }
                            break;
                    }
                }
            }
            foreach (PlotVarEntry p in temp)
                listBox1.Items.Add("ID: " + p.ID + " TYPE: " + TypeToString(p.type) + " DESCRIPTION: " + p.Desc);
            status.Text = "Elements: " + listBox1.Items.Count;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Search();
        }

        public void Search()
        {
            string s = toolStripTextBox1.Text;
            if (s.Length == 0)
                return;
            s = s.ToLower();
            int n = listBox1.SelectedIndex;
            for (int i = n + 1; i < listBox1.Items.Count; i++)
                if (listBox1.Items[i].ToString().ToLower().Contains(s))
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
        }

        private void toolStripTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                Search();
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            string s = listBox1.Items[n].ToString();
            s = s.Substring(4, s.Length - 4);
            string[] s2 = s.Split(' ');
            int id;
            if(!int.TryParse(s2[0], out id))
                return;
            if(MEVersion == 0)
                for(int i=0;i<database.ME1.Count;i++)
                    if (database.ME1[i].ID == id)
                    {
                        database.ME1.RemoveAt(i);
                        RefreshLists();
                        return;
                    }
            if (MEVersion == 1)
                for (int i = 0; i < database.ME2.Count; i++)
                    if (database.ME2[i].ID == id)
                    {
                        database.ME2.RemoveAt(i);
                        RefreshLists();
                        return;
                    }
            if (MEVersion == 2)
                for (int i = 0; i < database.ME3.Count; i++)
                    if (database.ME3[i].ID == id)
                    {
                        database.ME3.RemoveAt(i);
                        RefreshLists();
                        return;
                    }
        }

        private void newDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            database = new DataBaseType();
            database.ME1 = new List<PlotVarEntry>();
            database.ME2 = new List<PlotVarEntry>();
            database.ME3 = new List<PlotVarEntry>();
            RefreshLists();
        }

        private void saveDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                BitConverter.IsLittleEndian = true;
                fs.Write(BitConverter.GetBytes(database.ME1.Count), 0, 4);
                foreach (PlotVarEntry p in database.ME1)
                {
                    fs.Write(BitConverter.GetBytes(p.ID), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.type), 0, 4);
                    WriteString(fs, p.Desc);
                }
                fs.Write(BitConverter.GetBytes(database.ME2.Count), 0, 4);
                foreach (PlotVarEntry p in database.ME2)
                {
                    fs.Write(BitConverter.GetBytes(p.ID), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.type), 0, 4);
                    WriteString(fs, p.Desc);
                }
                fs.Write(BitConverter.GetBytes(database.ME3.Count), 0, 4);
                foreach (PlotVarEntry p in database.ME3)
                {
                    fs.Write(BitConverter.GetBytes(p.ID), 0, 4);
                    fs.Write(BitConverter.GetBytes(p.type), 0, 4);
                    WriteString(fs, p.Desc);
                }
                fs.Close();
                MessageBox.Show("Done.");
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

        public int ReadInt(FileStream fs)
        {
            int res = 0;
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            res = BitConverter.ToInt32(buff, 0);
            return res;
        }

        private void loadDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                BitConverter.IsLittleEndian = true;
                PlotVarEntry p;
                database = new DataBaseType();
                database.ME1 = new List<PlotVarEntry>();
                database.ME2 = new List<PlotVarEntry>();
                database.ME3 = new List<PlotVarEntry>();
                int count = ReadInt(fs);
                for (int i = 0; i < count; i++)
                {
                    p = new PlotVarEntry();
                    p.ID = ReadInt(fs);
                    p.type = ReadInt(fs);
                    p.Desc = ReadString(fs);
                    database.ME1.Add(p);
                }
                count = ReadInt(fs);
                for (int i = 0; i < count; i++)
                {
                    p = new PlotVarEntry();
                    p.ID = ReadInt(fs);
                    p.type = ReadInt(fs);
                    p.Desc = ReadString(fs);
                    database.ME2.Add(p);
                }
                count = ReadInt(fs);
                for (int i = 0; i < count; i++)
                {
                    p = new PlotVarEntry();
                    p.ID = ReadInt(fs);
                    p.type = ReadInt(fs);
                    p.Desc = ReadString(fs);
                    database.ME3.Add(p);
                }
                fs.Close();
                RefreshLists();
                MessageBox.Show("Done.");
            }
        }

        private void editToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            PlotVarEntry entry = new PlotVarEntry();
            switch (MEVersion)
            {
                case 0:
                    entry = database.ME1[n];
                    break;
                case 1:
                    entry = database.ME2[n];
                    break;
                case 2:
                    entry = database.ME3[n];
                    break;

            }
            PlotVarEditor ed = new PlotVarEditor();
            ed.MdiParent = this.MdiParent;
            ed.Show();
            ed.rtb1.Text = "{" + entry.ID + ", \"" + TypeToString(entry.type) + "\", \"" + entry.Desc + "\"};";
            ed.WindowState = FormWindowState.Maximized;
            ed.parent = this;
            ed.version = MEVersion;
            ed.index = n;
            ed.toolStripButton1.Visible = false;
            ed.toolStripButton2.Visible = true;
        }
    }
}
