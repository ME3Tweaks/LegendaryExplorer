using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Be;
using Be.Windows;
using Be.Windows.Forms;


namespace ME3Explorer.Meshplorer
{
    public partial class Meshplorer : Form
    {
        public struct NameEntry
        {
            public int index;
        }

        public PCCObject pcc;
        public List<NameEntry> Objects;
        public List<int> Materials;
        public int MeshplorerMode = 0; //0=PCC,1=PSK
        public StaticMesh stm;
        public SkeletalMesh skm;
        public SkeletalMeshOld skmold;
        public string CurrFile;

        public Meshplorer()
        {
            InitializeComponent();
        }

        private void Meshplorer_Load(object sender, EventArgs e)
        {
            if (Preview3D.InitializeGraphics(pb1))
            {
                Preview3D.DXCube c = Preview3D.NewCubeByOrigSize(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(1, 1, 1), 0);
                Preview3D.Cubes = new List<Preview3D.DXCube>();
                Preview3D.Cubes.Add(c);
                timer1.Enabled = true;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Preview3D.Refresh();
        }

        private void loadPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadPCC(d.FileName);
        }

        public void LoadPCC(string path)
        {
            MeshplorerMode = 0;
            CurrFile = path;
            pcc = new PCCObject(path);
            Materials = new List<int>();
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == "Material" || pcc.Exports[i].ClassName == "MaterialInstanceConstant")
                    Materials.Add(i);
            RefreshList1();
        }

        public void RefreshList1()
        {
            listBox1.Items.Clear();
            toolStripComboBox1.Items.Clear();
            foreach (int index in Materials)
                toolStripComboBox1.Items.Add("#" + index + " : " + pcc.Exports[index].ObjectName);
            Objects = new List<NameEntry>();
            for (int i = 0; i < pcc.Exports.Count(); i++)
            {
                if (pcc.Exports[i].ClassName == "StaticMesh")
                {
                    NameEntry n = new NameEntry();
                    n.index = i;
                    listBox1.Items.Add("StM#" + i + " : " + pcc.Exports[i].ObjectName);
                    Objects.Add(n);
                }
                if (pcc.Exports[i].ClassName == "SkeletalMesh")
                {
                    NameEntry n = new NameEntry();
                    n.index = i;
                    listBox1.Items.Add("SkM#" + i + " : " + pcc.Exports[i].ObjectName);
                    Objects.Add(n);
                }
            }
        }

        public void LoadStaticMesh(int index)
        {
            stm = new StaticMesh(pcc, index);
            Preview3D.StatMesh = stm;
            //Preview3D.SkelMesh = null;
            Preview3D.CamOffset = new Vector3(0, 0, 0);
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(stm.ToTree());
            treeView1.Nodes[0].Expand();
        }

        public void LoadSkeletalMesh(int index)
        {
            try
            {
                DisableLODs();
                skm = new SkeletalMesh(pcc, index);
                skmold = new SkeletalMeshOld(pcc, pcc.Exports[index].Data);
                hb1.ByteProvider = new DynamicByteProvider(pcc.Exports[index].Data);
                Preview3D.StatMesh = null;
                Preview3D.SkelMesh = skm;
                Preview3D.CamDistance = skm.Bounding.r * 2.0f;
                Preview3D.CamOffset = skm.Bounding.origin;
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(skm.ToTree());
                treeView1.Nodes[0].Expand();
                lODToolStripMenuItem.Visible = true;
                lOD1ToolStripMenuItem.Enabled = true;
                lOD1ToolStripMenuItem.Checked = true;
                if (skm.LODModels.Count > 1)
                    lOD2ToolStripMenuItem.Enabled = true;
                if (skm.LODModels.Count > 2)
                    lOD3ToolStripMenuItem.Enabled = true;
                if (skm.LODModels.Count > 3)
                    lOD4ToolStripMenuItem.Enabled = true;
            }
            catch (Exception)
            {
            }
        }

        public float dir;

        private void Meshplorer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Add)
                dir = 1;
            if (e.KeyCode == Keys.Subtract)
                dir = -1;
        }

        private void Meshplorer_KeyUp(object sender, KeyEventArgs e)
        {
            dir = 0;
        }

        private void Meshplorer_FormClosing(object sender, FormClosingEventArgs e)
        {
            Preview3D.device = null;
        }

        private void exportToPSKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.psk|*.psk";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    stm.ExportToPsk(d.FileName);
                    MessageBox.Show("Done.");
                }
            }
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.psk|*.psk";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    skmold.ExportToPsk(d.FileName, getLOD());
                    MessageBox.Show("Done.");
                }
            }
        }

        public int getLOD()
        {
            int res = 0;
            if (lOD2ToolStripMenuItem.Checked) res = 1;
            if (lOD3ToolStripMenuItem.Checked) res = 2;
            if (lOD4ToolStripMenuItem.Checked) res = 3;
            return res;
        }

        private void dumpBinaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[Objects[n].index].Data;
                    int start = stm.props[stm.props.Count - 1].offend;
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[Objects[n].index].Data;
                    int start = skm.GetPropertyEnd();
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
        }

        private void serializeToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh") 
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    stm.SerializeToFile(d.FileName);
                    MessageBox.Show("Done.");
                }
            }
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SerializingContainer c = new SerializingContainer();
                    c.Memory = new MemoryStream();
                    c.isLoading = false;
                    skm.Serialize(c);
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    fs.Write(c.Memory.ToArray(), 0, (int)c.Memory.Length);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
        }

        private void importFromPSKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh") 
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.psk|*.psk;*.pskx";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    timer1.Enabled = false;
                    stm.ImportFromPsk(d.FileName);
                    byte[] buff = stm.SerializeToBuffer();
                    int idx =Objects[n].index;
                    PCCObject.ExportEntry en = pcc.Exports[idx];
                    en.Data = buff;
                    pcc.altSaveToFile(CurrFile, true);
                    MessageBox.Show("Done.");
                    timer1.Enabled = true;
                }
            }
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.psk|*.psk;*.pskx";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    timer1.Enabled = false;
                    rtb1.Visible = true;
                    skmold.ImportFromPsk(d.FileName, getLOD());
                    byte[] buff = skmold.Serialize();
                    int idx = Objects[n].index;
                    PCCObject.ExportEntry en = pcc.Exports[idx];
                    en.Data = buff;
                    pcc.altSaveToFile(CurrFile, true);
                    MessageBox.Show("Done.");
                    rtb1.Visible = false;
                    timer1.Enabled = true;
                }
            }
        }

        private void lOD1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview3D.LOD = 0;
            UnCheckLODs();
            lOD1ToolStripMenuItem.Checked = true;
        }

        private void lOD2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview3D.LOD = 1;
            UnCheckLODs();
            lOD2ToolStripMenuItem.Checked = true;
        }

        private void lOD3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview3D.LOD = 2;
            UnCheckLODs();
            lOD3ToolStripMenuItem.Checked = true;
        }

        private void lOD4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview3D.LOD = 3;
            UnCheckLODs();
            lOD4ToolStripMenuItem.Checked = true;
        }

        public void UnCheckLODs()
        {
            lOD1ToolStripMenuItem.Checked = false;
            lOD2ToolStripMenuItem.Checked = false;
            lOD3ToolStripMenuItem.Checked = false;
            lOD4ToolStripMenuItem.Checked = false;
        }

        public void DisableLODs()
        {
            lOD1ToolStripMenuItem.Enabled= false;
            lOD2ToolStripMenuItem.Enabled = false;
            lOD3ToolStripMenuItem.Enabled = false;
            lOD4ToolStripMenuItem.Enabled = false;
        }

        private void rotatingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Preview3D.rotate = rotatingToolStripMenuItem.Checked;
        }

        private void exportTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Textfiles(*.txt)|*.txt";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                PrintNodes(treeView1.Nodes, fs, 0);
                fs.Close();
                MessageBox.Show("Done.");
            }
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

        private void importOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportOptions im = new ImportOptions();
            im.MdiParent = this.MdiParent;
            im.Show();
            im.WindowState = FormWindowState.Maximized;
        }

        private void loadFromDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MeshDatabase db = new MeshDatabase();
            db.MdiParent = this.MdiParent;
            db.Show();
            db.WindowState = FormWindowState.Maximized;
            db.MyParent = this;
        }

        private void exportTo3DSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.3ds|*.3ds";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if(File.Exists(d.FileName))
                        File.Delete(d.FileName);
                    PSKFile p = stm.ExportToPsk();
                    Helper3DS.ConvertPSKto3DS(p, d.FileName);
                    MessageBox.Show("Done.");
                }
            }
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            lODToolStripMenuItem.Visible = false;
            UnCheckLODs();
            stm = null;
            skm = null;
            skmold = null;
            Preview3D.SkelMesh = null;
            Preview3D.StatMesh = null;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh")
                LoadStaticMesh(Objects[n].index);
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
                LoadSkeletalMesh(Objects[n].index);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Name != "")
            {
                try
                {
                    int off = Convert.ToInt32(e.Node.Name);
                    IByteProvider db = hb1.ByteProvider;
                    if (off >= 0 && off < db.Length)
                    {
                        hb1.SelectionStart = off;
                        hb1.SelectionLength = 1;
                    }
                }
                catch (Exception ex)
                {
                }
            }
            TreeNode t = e.Node;
            if (t.Parent != null && t.Parent.Text == "Materials")
            {
                try
                {
                    string s = t.Text;
                    for (int i = 0; i < s.Length; i++)
                        if (s[i] == '#')
                            s = s.Substring(i + 1);
                    int idx = Convert.ToInt32(s) - 1;
                    for (int i = 0; i < Materials.Count; i++)
                        if (Materials[i] == idx)
                            toolStripComboBox1.SelectedIndex = i;
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void dumpBinaryToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 | pcc == null)
                return;
            if (pcc.Exports[Objects[n].index].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[Objects[n].index].Data;
                    int start = stm.props[stm.props.Count - 1].offend;
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
            if (pcc.Exports[Objects[n].index].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[Objects[n].index].ObjectName + ".bin";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[Objects[n].index].Data;
                    int start = skm.GetPropertyEnd();
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.");
                }
            }
        }

        private void importFromUDKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int idx = Objects[n].index;
            MPOpt.SelectedObject = idx;
            MPOpt.pcc = pcc;
            MPOpt.SelectedLOD = getLOD();
            UDKCopy u = new UDKCopy();
            u.MdiParent = this.MdiParent;
            u.Show();
            u.WindowState = FormWindowState.Maximized;
        }

        private void selectMatForSectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            int exp = Objects[n].index;
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null || t.Index != 0)
                return;
            TreeNode t2 = t.Parent; //SectionN
            if (t2.Parent == null) return;
            TreeNode t3 = t2.Parent; //Sections
            if (t3.Text != "Sections")
                return;
            TreeNode t4 = t3.Parent;
            int lod = t4.Index;
            int sec = t2.Index;
            int currmat = skm.LODModels[lod].Sections[sec].MaterialIndex;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new ID", "ME3 Explorer", currmat.ToString(), 0, 0);
            int newmat = currmat;
            if (Int32.TryParse(result, out newmat))
            {
                SkeletalMesh.SectionStruct s = skm.LODModels[lod].Sections[sec];
                s.MaterialIndex = (short)newmat;
                skm.LODModels[lod].Sections[sec] = s;
                SerializingContainer con = new SerializingContainer();
                con.Memory = new MemoryStream();
                con.isLoading = false;
                skm.Serialize(con);
                int end = skm.GetPropertyEnd();
                MemoryStream mem = new MemoryStream();
                mem.Write(pcc.Exports[exp].Data, 0, end);
                mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
                pcc.Exports[exp].Data = mem.ToArray();
                pcc.altSaveToFile(pcc.pccFileName, true);
                MessageBox.Show("Done");
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            int m = listBox1.SelectedIndex;
            TreeNode t = treeView1.SelectedNode;
            if (n == -1 || m == -1 || pcc == null || t == null || t.Parent == null  || t.Parent.Text !="Materials")
                return;
            int idx = Objects[m].index;
            if (pcc.Exports[idx].ClassName == "StaticMesh")
                return;
            skm.Materials[t.Index] = Materials[n] + 1;
            SerializingContainer con = new SerializingContainer();
            con.Memory = new MemoryStream();
            con.isLoading = false;
            skm.Serialize(con);
            int end = skm.GetPropertyEnd();
            MemoryStream mem = new MemoryStream();
            mem.Write(pcc.Exports[idx].Data, 0, end);
            mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
            pcc.Exports[idx].Data = mem.ToArray();
            pcc.altSaveToFile(pcc.pccFileName, true);
            MessageBox.Show("Done");
        }
    }
}
