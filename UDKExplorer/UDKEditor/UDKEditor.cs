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
using UDKExplorer.UDK;

namespace UDKExplorer.UDKEditor
{
    public partial class UDKEditor : Form
    {
        public UDKObject udk;
        public int SelectedView = 2; // 0=names,1=imports,2=exports
        public string CurrentFile;

        public UDKEditor()
        {
            InitializeComponent();
        }        

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            SelectedView = 1;
            RefreshLists();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*.udk|*.u;*.upk;*.udk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                udk = new UDKObject(d.FileName);
                RefreshLists();
                RefreshClasses();
                CurrentFile = d.FileName;
                Status.Text = "File: " + CurrentFile;
            }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            SelectedView = 2;
            RefreshLists();
        }

        public void RefreshClasses()
        {
            toolStripComboBox1.Items.Clear();
            List<int> classes = new List<int>();
            List<string> classnames = new List<string>();
            for(int i=0;i<udk.ExportCount;i++)
            {
                bool found = false;
                for (int j = 0; j < classes.Count; j++)
                    if (classes[j] == udk.Exports[i].clas)
                        found = true;
                if (!found)
                    classes.Add(udk.Exports[i].clas);
            }
            for (int i = 0; i < classes.Count; i++)
                classnames.Add(udk.GetClass(classes[i]));
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < classnames.Count - 1; i++)
                    if (classnames[i].CompareTo(classnames[i + 1]) > 0)
                    {
                        string s = classnames[i];
                        classnames[i] = classnames[i + 1];
                        classnames[i + 1] = s;
                        run = true;
                    }
            }
            for (int i = 0; i < classes.Count; i++)
                toolStripComboBox1.Items.Add(classnames[i]);
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            switch (SelectedView)
            {
                case 0://names
                    toolStripButton3.Checked = false;
                    toolStripButton2.Checked = false;
                    toolStripButton1.Checked = true;
                    for (int i = 0; i < udk.NameCount; i++)
                    {
                        string s = i.ToString("d5") + " : ";
                        s += udk.GetName(i);
                        listBox1.Items.Add(s);
                    }
                    break;
                case 1://imports
                    toolStripButton1.Checked = false;
                    toolStripButton3.Checked = false;
                    toolStripButton2.Checked = true;
                    for (int i = 0; i < udk.ImportCount; i++)
                    {
                        string s = i.ToString("d5") + " : ";
                        s += udk.FollowLink(udk.Imports[i].link);
                        s += udk.GetName(udk.Imports[i].name);
                        listBox1.Items.Add(s);
                    }
                    break;
                case 2://exports
                    toolStripButton1.Checked = false;
                    toolStripButton2.Checked = false;
                    toolStripButton3.Checked = true;
                    for (int i = 0; i < udk.ExportCount; i++)
                    {
                        string s = i.ToString("d5") + " : ";
                        s += udk.FollowLink(udk.Exports[i].link);
                        s += udk.GetName(udk.Exports[i].name);
                        listBox1.Items.Add(s);
                    }
                    break;
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            SelectedView = 0;
            RefreshLists();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SelectedView == 2)
                Preview();
        }

        public void Preview()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
            {
                byte[] dummy = new byte[0];
                hb1.ByteProvider = new DynamicByteProvider(dummy);
                return;
            }
            hb1.ByteProvider = new DynamicByteProvider(udk.Exports[n].data);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(GetInformation(n));
        }

        private TreeNode GetInformation(int n)
        {
            TreeNode res = new TreeNode("Object Information");
            res.Nodes.Add(GetFlags(n));
            res.Nodes.Add(GetProperties(n));
            res.ExpandAll();
            return res;
        }        

        private TreeNode GetFlags(int n)
        {
            TreeNode res = new TreeNode("Flags 0x" + udk.Exports[n].flags.ToString("X8"));
            foreach (string row in UnrealFlags.flagdesc)//0x02000000
            {
                string[] t = row.Split(',');
                long l = long.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                l = l >> 32;
                if ((l & udk.Exports[n].flags) != 0)
                    res.Nodes.Add(t[0].Trim());
            }
            return res;
        }

        private TreeNode GetProperties(int n)
        {
            TreeNode res = new TreeNode("Properties");
            BitConverter.IsLittleEndian = true;
            int pos = 0x00;
            try
            {
                
                int test = BitConverter.ToInt32(udk.Exports[n].data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                //if ((udk.Exports[n].flags & 0x04) != 0)
                //    pos += 0x0C;
                if ((udk.Exports[n].flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(udk.Exports[n].data, pos);
                    if (udk.GetName(idxname) == "None" || udk.GetName(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(udk.Exports[n].data, pos + 8);
                    int size = BitConverter.ToInt32(udk.Exports[n].data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (udk.GetName(idxtype) == "StructProperty")
                        size += 8;
                    if (udk.GetName(idxtype) == "ByteProperty")
                        size += 8;
                    string s = pos.ToString("X8") + " " + udk.GetName(idxname) + " (" + udk.GetName(idxtype) + ") : ";
                    switch (udk.GetName(idxtype))
                    {
                        case "ObjectProperty":
                        case "IntProperty":
                            int val = BitConverter.ToInt32(udk.Exports[n].data, pos + 24);
                            s += val.ToString();
                            break;
                        case "NameProperty":
                        case "StructProperty":
                            int name = BitConverter.ToInt32(udk.Exports[n].data, pos + 24);
                            s += udk.GetName(name);
                            break;
                        case "FloatProperty":
                            float f = BitConverter.ToSingle(udk.Exports[n].data, pos + 24);
                            s += f.ToString();
                            break;
                        case "BoolProperty":
                            s += (udk.Exports[n].data[pos + 24] == 1).ToString();
                            break;
                        case "StrProperty":
                            int len = BitConverter.ToInt32(udk.Exports[n].data, pos + 24);
                            for (int i = 0; i < len - 1; i++)
                                s += (char)udk.Exports[n].data[pos + 28 + i];
                            break;
                    }
                    res.Nodes.Add(s);
                    pos += 24 + size;
                    if (pos > udk.Exports[n].data.Length)
                    {
                        pos -= 24 + size;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            res.Nodes.Add(pos.ToString("X8") + " None");
            return res;
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.u;*.upk;*.udk|*.u;*.upk;*.udk";
            d.FileName = CurrentFile;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                udk.SaveToFile(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void saveHexChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || SelectedView != 2 || udk == null)
                return;
            MemoryStream m = new MemoryStream();
            for (int i = 0; i < hb1.ByteProvider.Length; i++)
                m.WriteByte(hb1.ByteProvider.ReadByte(i));
            UDKObject.ExportEntry ent = udk.Exports[n];
            ent.data = m.ToArray();
            if (m.Length != ent.size)
            {
                ent.size = (int)ent.data.Length;
                ent.IsChanged = true;
            }
            udk.Exports[n] = ent;
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            if (n == -1)
                return;
            if (SelectedView != 2)
                return;
            if (udk == null)
                return;
            string s = toolStripComboBox1.Items[n].ToString();
            int m = listBox1.SelectedIndex + 1;
            for(int i=m;i<udk.ExportCount;i++)
                if (udk.GetClass(udk.Exports[i].clas) == s)
                {
                    listBox1.SelectedIndex = i;
                    return;
                }
        }

        private void UDKEditor_Load(object sender, EventArgs e)
        {
            tabPage1.Text = "Raw";
            tabPage2.Text = "Information";
        }

        private void hb1_SelectionLengthChanged(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Selection : 0x" + hb1.SelectionStart.ToString("X") + " - 0x" + (hb1.SelectionStart + hb1.SelectionLength).ToString("X") + " Length : " + hb1.SelectionLength.ToString("X");
            if (hb1.SelectionStart < hb1.ByteProvider.Length - 4)
            {
                byte[] buff = new byte[4];
                for (int i = 0; i < 4; i++)
                    buff[i] = hb1.ByteProvider.ReadByte(hb1.SelectionStart + i);
                BitConverter.IsLittleEndian = true;
                toolStripStatusLabel1.Text += " Values at Selectionstart : " + BitConverter.ToInt32(buff, 0) + "(INT) " + BitConverter.ToSingle(buff, 0) + "f(FLOAT)";
            }
        }

        private int GetPropertyEnd(int n)
        {
            BitConverter.IsLittleEndian = true;
            int pos = 0x00;
            try
            {

                int test = BitConverter.ToInt32(udk.Exports[n].data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((udk.Exports[n].flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(udk.Exports[n].data, pos);
                    if (udk.GetName(idxname) == "None" || udk.GetName(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(udk.Exports[n].data, pos + 8);
                    int size = BitConverter.ToInt32(udk.Exports[n].data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (udk.GetName(idxtype) == "StructProperty")
                        size += 8;
                    if (udk.GetName(idxtype) == "ByteProperty")
                        size += 8;                    
                    pos += 24 + size;
                    if (pos > udk.Exports[n].data.Length)
                    {
                        pos -= 24 + size;
                        break;
                    }
                }               
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            return pos + 8;
        }

        private void dumpBinaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || SelectedView != 2 || udk == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.bin|*.bin";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                int start = GetPropertyEnd(n);
                fs.Write(udk.Exports[n].data, start, udk.Exports[n].data.Length - start);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }
    }
}
