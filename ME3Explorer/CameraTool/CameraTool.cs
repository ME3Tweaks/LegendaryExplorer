using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ME3LibWV;
namespace ME3Explorer.CameraTool
{
    public partial class CamTool : Form
    {
        public string currfilepath;
        public PCCPackage pcc;
        public List<int> Indexes;
        public CamTool()
        {
            BitConverter.IsLittleEndian = true;
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadFile(d.FileName);
        }

        public void LoadFile(string path)
        {
            currfilepath = path;
            this.Text = "Camera Tool - " + Path.GetFileName(path);
            pcc = new PCCPackage(path, true, false, true);
            Indexes = new List<int>();
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                PCCPackage.ExportEntry e = pcc.Exports[i];
                string c = pcc.getObjectName(e.idxClass);
                if (c == "InterpData")
                {
                    List<PropertyReader.Property> props = PropertyReader.ReadProp(pcc, e.Data, PropertyReader.detectStart(pcc, e.Data, (uint)e.ObjectFlags));
                    bool has = false;
                    foreach (PropertyReader.Property p in props)
                        if (pcc.GetName(p.Name) == "InterpGroups")
                        {
                            has = true;
                            break;
                        }
                    if (has)
                        Indexes.Add(i);
                }
            }
            FreshList();
        }

        public void FreshList()
        {
            splitContainer1.BringToFront();
            listBox1.Items.Clear();
            foreach (int idx in Indexes)
                listBox1.Items.Add(idx + " : " + pcc.GetObjectPath(idx + 1) + pcc.getObjectName(idx + 1) + "(" + pcc.Exports[idx].Index + ")");
        }

        public void FreshTree(int n)
        {
            splitContainer1.BringToFront();
            treeView1.Nodes.Clear();
            PCCPackage.ExportEntry e = pcc.Exports[n];
            List<PropertyReader.Property> props = PropertyReader.ReadProp(pcc, e.Data, PropertyReader.detectStart(pcc, e.Data, (uint)e.ObjectFlags));
            TreeNode t = new TreeNode(n + " : " + pcc.getObjectName(n + 1));
            int idx;
            foreach (PropertyReader.Property p in props)
                switch (pcc.GetName(p.Name))
                {
                    case "InterpGroups":
                        int count = BitConverter.ToInt32(p.raw, 24);
                        TreeNode groups = new TreeNode("Interp Groups (" + count + ")");
                        for (int i = 0; i < count; i++)
                        {
                            idx = BitConverter.ToInt32(p.raw, 28 + i * 4);
                            if (idx > 0)
                                groups.Nodes.Add(MakeInterpGroupNode(idx - 1));
                            else
                                groups.Nodes.Add(idx + "");
                        }
                        if (groups.Nodes.Count != 0)
                            t.Nodes.Add(groups);
                        break;
                    case "InterpLength":
                        byte[] buff = BitConverter.GetBytes(p.Value.IntValue);
                        float f = BitConverter.ToSingle(buff, 0);
                        t.Nodes.Add("Interp Length : " + f);
                        break;
                    case "ParentSequence":
                        idx = p.Value.IntValue;
                        if (idx > 0)
                            t.Nodes.Add("Parent Sequence : " + idx + " (" + pcc.getObjectName(idx) + ")");
                        break;
                }
            treeView1.Nodes.Add(t);
            treeView1.Nodes[0].Expand();
        }

        public TreeNode MakeInterpGroupNode(int n)
        {
            TreeNode res = new TreeNode(n + " : " + pcc.getObjectName(n + 1));
            PCCPackage.ExportEntry e = pcc.Exports[n];
            List<PropertyReader.Property> props = PropertyReader.ReadProp(pcc, e.Data, PropertyReader.detectStart(pcc, e.Data, (uint)e.ObjectFlags));
            int idx;
            foreach (PropertyReader.Property p in props)
                switch (pcc.GetName(p.Name))
                {
                    case "InterpTracks":
                        int count = BitConverter.ToInt32(p.raw, 24);
                        TreeNode t = new TreeNode("Interp Tracks (" + count + ")");
                        for (int i = 0; i < count; i++)
                        {
                            idx = BitConverter.ToInt32(p.raw, 28 + i * 4);
                            if (idx > 0)
                                t.Nodes.Add(MakeInterpTrackNode(idx - 1));
                            else
                                t.Nodes.Add(idx + "");
                        }
                        if (t.Nodes.Count != 0)
                            res.Nodes.Add(t);
                        break;
                    case "GroupName":
                        idx = p.Value.IntValue;
                        res.Text += " (" + pcc.GetName(idx) + ")";
                        break;
                    case "GroupColor":
                        idx = BitConverter.ToInt32(p.raw, 0x20);
                        res.BackColor = Color.FromArgb(idx);
                        break;
                    case "m_nmSFXFindActor":
                        idx = p.Value.IntValue;
                        res.Nodes.Add("m_nmSFXFindActor : " + pcc.GetName(idx));
                        break;
                }
            return res;
        }

        public TreeNode MakeInterpTrackNode(int n)
        {
            TreeNode res = new TreeNode(n + " : " + pcc.getObjectName(n + 1));
            PCCPackage.ExportEntry e = pcc.Exports[n];
            List<PropertyReader.Property> props = PropertyReader.ReadProp(pcc, e.Data, PropertyReader.detectStart(pcc, e.Data, (uint)e.ObjectFlags));
            int pos, count;
            TreeNode t;
            foreach (PropertyReader.Property p in props)
                switch (pcc.GetName(p.Name))
                {
                    case "m_aTrackKeys":
                        count = BitConverter.ToInt32(p.raw, 24);
                        t = new TreeNode("Track Keys (" + count + ")");
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            List<PropertyReader.Property> key = PropertyReader.ReadProp(pcc, p.raw, pos);
                            if (key.Count != 0)
                            {
                                TreeNode t2 = new TreeNode("Key " + i);
                                int idx2;
                                foreach (PropertyReader.Property p2 in key)
                                    switch (pcc.GetName(p2.Name))
                                    {
                                        case "KeyName":
                                            idx2 = p2.Value.IntValue;
                                            t2.Nodes.Add("Key Name : " + pcc.GetName(idx2));
                                            break;
                                        case "fTime":
                                            float f = BitConverter.ToSingle(p2.raw, 24);
                                            t2.Nodes.Add("Time : " + f);
                                            break;
                                    }
                                if (t2.Nodes.Count != 0)
                                    t.Nodes.Add(t2);
                                pos = key[key.Count - 1].offend;
                            }
                        }
                        if (t.Nodes.Count != 0)
                            res.Nodes.Add(t);
                        break;
                    case "m_aGestures":
                        count = BitConverter.ToInt32(p.raw, 24);
                        t = new TreeNode("Gestures (" + count + ")");
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            List<PropertyReader.Property> key = PropertyReader.ReadProp(pcc, p.raw, pos);
                            if (key.Count != 0)
                            {
                                TreeNode t2 = new TreeNode("Gesture " + i);
                                foreach (PropertyReader.Property p2 in key)
                                    switch (pcc.GetName(p2.Name))
                                    {
                                        default:
                                            TreeNode td = MakeDefaultPropNode(p2);
                                            if (td != null)
                                                t2.Nodes.Add(td);
                                            break;
                                    }
                                if (t2.Nodes.Count != 0)
                                    t.Nodes.Add(t2);
                                pos = key[key.Count - 1].offend;
                            }
                        }
                        if (t.Nodes.Count != 0)
                            res.Nodes.Add(t);
                        break;
                    case "m_aDOFData":
                    case "CutTrack":
                        count = BitConverter.ToInt32(p.raw, 24);
                        t = new TreeNode(pcc.GetName(p.Name) + " (" + count + ")");
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            List<PropertyReader.Property> key = PropertyReader.ReadProp(pcc, p.raw, pos);
                            if (key.Count != 0)
                            {
                                TreeNode t2 = new TreeNode("Entry " + i);
                                foreach (PropertyReader.Property p2 in key)
                                    switch (pcc.GetName(p2.Name))
                                    {
                                        default:
                                            TreeNode td = MakeDefaultPropNode(p2);
                                            if (td != null)
                                                t2.Nodes.Add(td);
                                            break;
                                    }
                                if (t2.Nodes.Count != 0)
                                    t.Nodes.Add(t2);
                                pos = key[key.Count - 1].offend;
                            }
                        }
                        if (t.Nodes.Count != 0)
                            res.Nodes.Add(t);
                        break;
                    case "FloatTrack":
                    case "PosTrack":
                    case "LookupTrack":
                    case "EulerTrack":
                        List<PropertyReader.Property> content = PropertyReader.ReadProp(pcc, p.raw, 32);
                        if (content.Count == 2)
                        {
                            count = BitConverter.ToInt32(content[0].raw, 24);
                            t = new TreeNode(pcc.GetName(p.Name) + " Points (" + count + ")");
                            pos = 28;
                            for (int i = 0; i < count; i++)
                            {
                                List<PropertyReader.Property> point = PropertyReader.ReadProp(pcc, content[0].raw, pos);
                                if (point.Count != 0)
                                {
                                    TreeNode t2 = new TreeNode("Point " + i);
                                    foreach (PropertyReader.Property p2 in point)
                                        switch (pcc.GetName(p2.Name))
                                        {
                                            default:
                                                TreeNode td = MakeDefaultPropNode(p2);
                                                if (td != null)
                                                    t2.Nodes.Add(td);
                                                break;
                                        }
                                    if (t2.Nodes.Count != 0)
                                        t.Nodes.Add(t2);
                                    pos = point[point.Count - 1].offend;
                                }
                            }
                            if (t.Nodes.Count != 0)
                                res.Nodes.Add(t);
                        }
                        break;
                    default:
                        TreeNode t3 = MakeDefaultPropNode(p);
                        if (t3 != null)
                            res.Nodes.Add(t3);
                        break;
                }
            return res;
        }
        public TreeNode MakeDefaultPropNode(PropertyReader.Property p)
        {
            string tp = PropertyReader.TypeToString((int)p.TypeVal);
            switch (tp)
            {
                case "Byte Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : (" + pcc.GetName(BitConverter.ToInt32(p.raw, 24)) + ") " + pcc.GetName(BitConverter.ToInt32(p.raw, 32)));
                case "Bool Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + (p.Value.IntValue == 1));
                case "Object Property":
                    string s = " ";
                    if (p.Value.IntValue != 0)
                        s = pcc.getObjectName(p.Value.IntValue);
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + p.Value.IntValue + s);
                case "Integer Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + p.Value.IntValue);
                case "Name Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + pcc.GetName(p.Value.IntValue));
                case "Float Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + BitConverter.ToSingle(p.raw, 24));
                case "String Property":
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ") : " + p.Value.StringValue);
                default:
                    return new TreeNode(pcc.GetName(p.Name) + " (" + tp + ")");
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            FreshTree(Indexes[n]);
        }

        public void PrintNodes(TreeNodeCollection t, FileStream fs, int depth)
        {
            string tab = "";
            for (int i = 0; i < depth; i++)
                tab += ' ';
            foreach (TreeNode t1 in t)
            {
                string s = tab + t1.Text;
                WriteString(fs, s);
                fs.WriteByte(0xD);
                fs.WriteByte(0xA);
                if (t1.Nodes.Count != 0)
                    PrintNodes(t1.Nodes, fs, depth + 4);
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.txt|*.txt";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                PrintNodes(treeView1.Nodes, fs, 0);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void showCamerActorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.getObjectName(pcc.Exports[i].idxClass) == "CameraActor")
                    res.Append(i + " : " + pcc.getObjectName(i + 1) + "\n");
            if (res.Length != 0)
                MessageBox.Show(res.ToString());
        }

        public struct OverViewStruct
        {
            public string filepath;
            public List<int> Indexes;
        }

        public List<OverViewStruct> OverView = new List<OverViewStruct>();

        private void generateFromBasefolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string basepath = KFreonLib.MEDirectories.ME3Directory.cookedPath;
            if (String.IsNullOrEmpty(basepath))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }
            KFreonLib.Debugging.DebugOutput.StartDebugger("Main ME3Explorer Form");
            string[] files = Directory.GetFiles(basepath, "*.pcc");
            OverView = new List<OverViewStruct>();
            for (int f = 0; f < files.Length; f++)
            {
                string file = files[f];
                KFreonLib.Debugging.DebugOutput.PrintLn((f + 1) + " / " + files.Length + " : " + file + " :", true);
                PCCPackage p = new PCCPackage(file, false, false, true);
                OverViewStruct o = new OverViewStruct();
                o.filepath = file;
                o.Indexes = new List<int>();
                int count = 0;
                for (int i = 0; i < p.Exports.Count; i++)
                {
                    if (p.getObjectName(p.Exports[i].idxClass) == "InterpData")
                    {
                        o.Indexes.Add(i);
                        string s = "";
                        if (count++ == 0)
                            s = "\n";
                        KFreonLib.Debugging.DebugOutput.PrintLn(s + "found " + i + " : " + p.GetObjectPath(i + 1) + p.getObjectName(i + 1), false);
                    }
                }
                if (o.Indexes.Count != 0)
                    OverView.Add(o);
                Application.DoEvents();
            }
            FreshOverView();
        }

        public void FreshOverView()
        {
            splitContainer2.BringToFront();
            listBox2.Items.Clear();
            listBox3.Items.Clear();
            foreach (OverViewStruct o in OverView)
                listBox2.Items.Add(Path.GetFileName(o.filepath) + " (" + o.Indexes.Count + ")");
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            splitContainer1.BringToFront();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            splitContainer2.BringToFront();
        }

        private void saveToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff = BitConverter.GetBytes((int)OverView.Count);
                fs.Write(buff, 0, 4);
                foreach (OverViewStruct o in OverView)
                {
                    buff = BitConverter.GetBytes((int)o.filepath.Length);
                    fs.Write(buff, 0, 4);
                    foreach (char c in o.filepath)
                        fs.WriteByte((byte)c);
                    buff = BitConverter.GetBytes((int)o.Indexes.Count);
                    fs.Write(buff, 0, 4);
                    foreach (int i in o.Indexes)
                    {
                        buff = BitConverter.GetBytes(i);
                        fs.Write(buff, 0, 4);
                    }
                }
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        private void loadFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                OverView = new List<OverViewStruct>();
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                int count = BitConverter.ToInt32(buff, 0);
                for (int i = 0; i < count; i++)
                {
                    OverViewStruct o = new OverViewStruct();
                    fs.Read(buff, 0, 4);
                    int len = BitConverter.ToInt32(buff, 0);
                    o.filepath = "";
                    for (int j = 0; j < len; j++)
                        o.filepath += (char)fs.ReadByte();
                    fs.Read(buff, 0, 4);
                    int count2 = BitConverter.ToInt32(buff, 0);
                    o.Indexes = new List<int>();
                    for (int j = 0; j < count2; j++)
                    {
                        fs.Read(buff, 0, 4);
                        o.Indexes.Add(BitConverter.ToInt32(buff, 0));
                    }
                    OverView.Add(o);
                }
                fs.Close();
                FreshOverView();
                MessageBox.Show("Done.");
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            if (n == -1)
                return;
            PCCPackage p = new PCCPackage(OverView[n].filepath, false, false, true);
            listBox3.Items.Clear();
            foreach (int i in OverView[n].Indexes)
                listBox3.Items.Add(i + " : " + p.GetObjectPath(i + 1) + p.getObjectName(i + 1) + "(" + p.Exports[i].Index + ")");
        }

        private void openSelectedInterpDataInDetailViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox2.SelectedIndex;
            int m = listBox3.SelectedIndex;
            if (n == -1 || m == -1)
                return;
            LoadFile(OverView[n].filepath);
            FreshList();
            listBox1.SelectedIndex = m;
        }
    }
}
