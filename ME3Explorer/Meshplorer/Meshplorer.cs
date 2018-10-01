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
using ME3Explorer.Packages;
using Be.Windows.Forms;
using ME3Explorer.Scene3D;
using System.Globalization;

namespace ME3Explorer.Meshplorer
{
    public partial class Meshplorer : WinFormsBase
    {
        public List<int> Objects = new List<int>();
        public List<int> Materials = new List<int>();
        public List<int> ChosenMaterials = new List<int>(); // materials included in the skeletal mesh.
        public int MeshplorerMode = 0; //0=PCC,1=PSK
        public StaticMesh stm;
        public SkeletalMesh skm;
        public float PreviewRotation = 0;
        public List<string> RFiles;
        public static readonly string MeshplorerDataFolder = Path.Combine(App.AppDataFolder, @"Meshplorer\");
        private readonly string RECENTFILES_FILE = "RECENTFILES";
        private string pendingFileToLoad = null;

        public Meshplorer()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent(false);
        }

        public Meshplorer(string filepath)
        {
            pendingFileToLoad = filepath;
            InitializeComponent();
            LoadRecentList();
            RefreshRecent(false);
        }

        private void Meshplorer_Load(object sender, EventArgs e)
        {
            view.LoadDirect3D();
            rotatingToolStripMenuItem.Checked = Properties.Settings.Default.MeshplorerViewRotating;
            wireframeToolStripMenuItem.Checked = Properties.Settings.Default.MeshplorerViewWireframeEnabled;
            solidToolStripMenuItem.Checked = Properties.Settings.Default.MeshplorerViewSolidEnabled;
            firstPersonToolStripMenuItem.Checked = Properties.Settings.Default.MeshplorerViewFirstPerson;
            firstPersonToolStripMenuItem_Click(null, null); // Force first/third person setting to take effect

            if (pendingFileToLoad != null)
            {
                LoadFile(pendingFileToLoad);
                pendingFileToLoad = null;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            view.UpdateScene();
            view.Invalidate();
        }

        private void loadPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
                LoadFile(d.FileName);
        }

        public void LoadFile(string path)
        {
            try
            {
                //LoadME3Package(path);
                LoadMEPackage(path);
                if (pcc.Game != MEGame.ME3)
                {
                    MessageBox.Show(this, "Only files from Mass Effect 3 are supported.\nIf you want to help us debug loading ME1/ME2 files, please come to the ME3Tweaks Discord server.", "Unsupported game");
                    pcc.Release();
                    return;
                }
                MeshplorerMode = 0;
                RefreshMaterialList();
                RefreshMeshList();
                lblStatus.Text = Path.GetFileName(path);

                AddRecent(path, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        public void RefreshMeshList()
        {
            view.TextureCache.Dispose(); // Clear out the loaded textures from the previous pcc
            listBox1.Items.Clear();
            Objects.Clear();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            IExportEntry exportEntry;
            for (int i = 0; i < Exports.Count(); i++)
            {
                exportEntry = Exports[i];
                if (exportEntry.ClassName == "StaticMesh")
                {
                    listBox1.Items.Add("StM#" + i + " : " + exportEntry.ObjectName);
                    Objects.Add(i);
                }
                else if (exportEntry.ClassName == "SkeletalMesh")
                {
                    listBox1.Items.Add("SkM#" + i + " : " + exportEntry.ObjectName);
                    Objects.Add(i);
                }
            }
        }

        public void RefreshMaterialList()
        {
            Materials.Clear();
            MaterialBox.Items.Clear();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            IExportEntry exportEntry;
            for (int i = 0; i < Exports.Count(); i++)
            {
                exportEntry = Exports[i];
                if (exportEntry.ClassName == "Material" || exportEntry.ClassName == "MaterialInstanceConstant")
                {
                    Materials.Add(i);
                    MaterialBox.Items.Add("#" + i + " : " + exportEntry.ObjectName);
                }
            }
        }

        public void RefreshChosenMaterialsList()
        {
            ChosenMaterials.Clear();
            MaterialIndexBox.Items.Clear();
            if (skm != null)
            {
                for (int i = 0; i < skm.Materials.Count; i++)
                {
                    ChosenMaterials.Add(skm.Materials[i]);
                    string desc = "";
                    if (skm.Materials[i] > 0)
                    { // Material is export
                        IExportEntry export = pcc.getExport(skm.Materials[i] - 1);
                        desc = " Export #" + skm.Materials[i] + " : " + export.ObjectName;
                    }
                    else if (skm.Materials[i] < 0)
                    { // Material is import???
                        desc = "Import #" + -skm.Materials[i];
                    }
                    MaterialIndexBox.Items.Add(i + " - " + desc);
                }
            }
        }

        public void LoadStaticMesh(int index)
        {
            stm = new StaticMesh(pcc, index);

            // Load meshes for the LODs
            preview?.Dispose();
            preview = new ModelPreview(view.Device, stm, view.TextureCache);
            RefreshChosenMaterialsList();
            CenterView();

            // Update treeview
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(stm.ToTree());
            treeView1.Nodes[0].Expand();
            treeView1.EndUpdate();
            MaterialBox.Visible = false;
            MaterialApplyButton.Visible = false;
            MaterialIndexBox.Visible = false;
            MaterialIndexApplyButton.Visible = false;
        }

        public void LoadSkeletalMesh(int index)
        {
            DisableLODs();
            UnCheckLODs();
            skm = new SkeletalMesh(pcc as ME3Package, index);

            // Load preview model
            preview?.Dispose();
            preview = new ModelPreview(view.Device, skm, view.TextureCache);
            RefreshChosenMaterialsList();
            CenterView();

            // Update treeview
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(skm.ToTree());
            treeView1.Nodes[0].Expand();
            treeView1.EndUpdate();
            lODToolStripMenuItem.Visible = true;
            lOD0ToolStripMenuItem.Enabled = true;
            lOD0ToolStripMenuItem.Checked = true;
            if (skm.LODModels.Count > 1)
                lOD1ToolStripMenuItem.Enabled = true;
            if (skm.LODModels.Count > 2)
                lOD2ToolStripMenuItem.Enabled = true;
            if (skm.LODModels.Count > 3)
                lOD3ToolStripMenuItem.Enabled = true;
            MaterialBox.Visible = false;
            MaterialApplyButton.Visible = false;
            MaterialIndexBox.Visible = false;
            MaterialIndexApplyButton.Visible = false;
        }

        public float dir;

        private void Meshplorer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Add)
                dir = 1;
            if (e.KeyCode == Keys.Subtract)
                dir = -1;
            if (e.KeyCode == Keys.F)
            {
                CenterView();
            }
        }

        private void Meshplorer_KeyUp(object sender, KeyEventArgs e)
        {
            dir = 0;
        }

        private void Meshplorer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!e.Cancel)
            {
                preview?.Dispose();
                preview = null;
                view.UnloadDirect3D();
                Properties.Settings.Default.MeshplorerViewRotating = rotatingToolStripMenuItem.Checked;
                Properties.Settings.Default.MeshplorerViewWireframeEnabled = wireframeToolStripMenuItem.Checked;
                Properties.Settings.Default.MeshplorerViewSolidEnabled = solidToolStripMenuItem.Checked;
                Properties.Settings.Default.MeshplorerViewFirstPerson = firstPersonToolStripMenuItem.Checked;
                Properties.Settings.Default.Save();
            }
        }

        private void exportToPSKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.psk|*.psk";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    stm.ExportPSK(d.FileName);
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            if (pcc.Exports[n].ClassName == "SkeletalMesh")
            {
                /*SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.psk|*.psk";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    skmold.ExportToPsk(d.FileName, getLOD());
                    MessageBox.Show("Done.","Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }*/
                MessageBox.Show("benji: Sorry, skeletal PSK export isn't available for the time being.");
            }
        }

        public int getLOD()
        {
            int res = 0;
            if (lOD1ToolStripMenuItem.Checked) res = 1;
            if (lOD2ToolStripMenuItem.Checked) res = 2;
            if (lOD3ToolStripMenuItem.Checked) res = 3;
            return res;
        }

        private void dumpBinaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[n].Data;
                    int start = stm.props[stm.props.Count - 1].offend;
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            if (pcc.Exports[n].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[n].Data;
                    int start = skm.GetPropertyEnd();
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        private void serializeToFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    stm.SerializeToFile(d.FileName);
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            if (pcc.Exports[n].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    SerializingContainer c = new SerializingContainer();
                    c.Memory = new MemoryStream();
                    c.isLoading = false;
                    skm.Serialize(c);
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    fs.Write(c.Memory.ToArray(), 0, (int)c.Memory.Length);
                    fs.Close();
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        private void importFromPSKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.psk|*.psk;*.pskx";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    timer1.Enabled = false;
                    stm.ImportPSK(d.FileName);
                    byte[] buff = stm.SerializeToBuffer();
                    int idx = n;
                    IExportEntry en = pcc.Exports[idx];
                    en.Data = buff;
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    timer1.Enabled = true;
                }
            }
            if (pcc.Exports[n].ClassName == "SkeletalMesh")
            {
                /*OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.psk|*.psk;*.pskx";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    timer1.Enabled = false;
                    rtb1.Visible = true;
                    skmold.ImportFromPsk(d.FileName, getLOD());
                    byte[] buff = skmold.Serialize();
                    int idx = n;
                    IExportEntry en = pcc.Exports[idx];
                    en.Data = buff;
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                    rtb1.Visible = false;
                    timer1.Enabled = true;
                }*/
                MessageBox.Show("benji: Sorry, skeletal PSK export is unavailable for the time being.");
            }
        }

        private void lOD0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentLOD = 0;
            UnCheckLODs();
            lOD0ToolStripMenuItem.Checked = true;
        }

        private void lOD1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentLOD = 1;
            UnCheckLODs();
            lOD1ToolStripMenuItem.Checked = true;
        }

        private void lOD2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentLOD = 2;
            UnCheckLODs();
            lOD2ToolStripMenuItem.Checked = true;
        }

        private void lOD3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentLOD = 3;
            UnCheckLODs();
            lOD3ToolStripMenuItem.Checked = true;
        }

        public void UnCheckLODs()
        {
            lOD0ToolStripMenuItem.Checked = false;
            lOD1ToolStripMenuItem.Checked = false;
            lOD2ToolStripMenuItem.Checked = false;
            lOD3ToolStripMenuItem.Checked = false;
        }

        public void DisableLODs()
        {
            lOD0ToolStripMenuItem.Enabled = false;
            lOD1ToolStripMenuItem.Enabled = false;
            lOD2ToolStripMenuItem.Enabled = false;
            lOD3ToolStripMenuItem.Enabled = false;
        }

        private void exportTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Textfiles(*.txt)|*.txt";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                PrintNodes(treeView1.Nodes, fs, 0);
                fs.Close();
                MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
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
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.3ds|*.3ds";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(d.FileName))
                        File.Delete(d.FileName);
                    stm.ExportPSK(d.FileName);
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
            {
                return;
            }
            n = Objects[n];
            lODToolStripMenuItem.Visible = false;
            UnCheckLODs();
            stm = null;
            skm = null;
            preview?.Dispose();
            preview = null;
            MaterialBox.Visible = false;
            MaterialApplyButton.Visible = false;
            MaterialIndexBox.Visible = false;
            MaterialIndexApplyButton.Visible = false;
            if (pcc.getExport(n).ClassName == "StaticMesh")
                LoadStaticMesh(n);
            if (pcc.getExport(n).ClassName == "SkeletalMesh")
                LoadSkeletalMesh(n);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = e.Node;
            MaterialBox.Visible = false;
            MaterialApplyButton.Visible = false;
            MaterialIndexBox.Visible = false;
            MaterialIndexApplyButton.Visible = false;
            if (skm != null)
            {
                if (t.Parent != null && t.Parent.Text == "Materials")
                {
                    MaterialBox.Visible = true;
                    MaterialApplyButton.Visible = true;
                    try
                    {
                        string s = t.Text.Split(' ')[0].Trim('#');
                        int idx = Convert.ToInt32(s);
                        for (int i = 0; i < Materials.Count; i++)
                            if (Materials[i] == idx)
                                MaterialBox.SelectedIndex = i;
                    }
                    catch
                    {

                    }
                }
                if (t.Parent != null && t.Parent.Text == "Sections")
                {
                    MaterialIndexBox.Visible = true;
                    MaterialIndexApplyButton.Visible = true;
                    try
                    {
                        int m = skm.LODModels[t.Parent.Parent.Index].Sections[t.Index].MaterialIndex;
                        MaterialIndexBox.SelectedIndex = m;
                    }
                    catch
                    {

                    }
                }
            }
            else if (stm != null)
            {
                if (t.Parent != null && t.Parent.Text == "Sections")
                {
                    MaterialBox.Visible = true;
                    MaterialApplyButton.Visible = true;
                    // HACK: assume that all static meshes have only 1 LOD. This has been true in my experience.
                    int section = t.Index;
                    int mat = stm.Mesh.Mat.Lods[0].Sections[section].Name - 1;
                    for (int i = 0; i < Materials.Count; i++)
                        if (Materials[i] == mat)
                            MaterialBox.SelectedIndex = i;
                }
            }
        }

        private void dumpBinaryToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            if (pcc.Exports[n].ClassName == "StaticMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[n].Data;
                    int start = stm.props[stm.props.Count - 1].offend;
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            if (pcc.Exports[n].ClassName == "SkeletalMesh")
            {
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.bin|*.bin";
                d.FileName = pcc.Exports[n].ObjectName + ".bin";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                    byte[] buff = pcc.Exports[n].Data;
                    int start = skm.GetPropertyEnd();
                    for (int i = start; i < buff.Length; i++)
                        fs.WriteByte(buff[i]);
                    fs.Close();
                    MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        private void importFromUDKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            UDKCopy u = new UDKCopy(pcc as ME3Package, n, getLOD());
            u.Show();
        }

        private void selectMatForSectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (stm != null)
            {
                n = stm.index;
            }
            else if (skm != null)
            {
                n = skm.MyIndex;
            }
            else
            {
                return;
            }
            int exp = n;
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
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = MaterialBox.SelectedIndex;
            TreeNode t = treeView1.SelectedNode;
            if (n == -1 || pcc == null || t == null || t.Parent == null)
                return;

            if (stm != null && t.Parent.Text == "Sections")
            {
                stm.SetSectionMaterial(CurrentLOD, t.Index, Materials[n] + 1);
                //SerializingCont
                MemoryStream ms = new MemoryStream();
                pcc.Exports[stm.index].Data = stm.SerializeToBuffer();
                // Update treeview

                // Update preview
                preview.Dispose();
                preview = new ModelPreview(view.Device, stm, view.TextureCache);
            }
            else if (skm != null && t.Parent.Text == "Materials")
            {
                skm.Materials[t.Index] = Materials[n] + 1;
                SerializingContainer con = new SerializingContainer();
                con.Memory = new MemoryStream();
                con.isLoading = false;
                skm.Serialize(con);
                int end = skm.GetPropertyEnd();
                MemoryStream mem = new MemoryStream();
                mem.Write(pcc.Exports[skm.MyIndex].Data, 0, end);
                mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
                pcc.Exports[skm.MyIndex].Data = mem.ToArray();
            }
        }

        private void MaterialIndexApplyButton_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (skm != null && t != null && t.Parent != null && t.Parent.Parent != null && t.Parent.Text == "Sections")
            {
                SkeletalMesh.SectionStruct section = skm.LODModels[t.Parent.Parent.Index].Sections[t.Index];
                section.MaterialIndex = (short)MaterialIndexBox.SelectedIndex;
                skm.LODModels[t.Parent.Parent.Index].Sections[t.Index] = section;

                SerializingContainer con = new SerializingContainer();
                con.Memory = new MemoryStream();
                con.isLoading = false;
                skm.Serialize(con);
                int end = skm.GetPropertyEnd();
                MemoryStream mem = new MemoryStream();
                mem.Write(pcc.Exports[skm.MyIndex].Data, 0, end);
                mem.Write(con.Memory.ToArray(), 0, (int)con.Memory.Length);
                pcc.Exports[skm.MyIndex].Data = mem.ToArray();
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (skm != null && updatedExports.Contains(skm.MyIndex))
            {
                int index = skm.MyIndex;
                //loaded SkeletalMesh is no longer a SkeletalMesh
                if (pcc.getExport(index).ClassName != "SkeletalMesh")
                {
                    skm = null;
                    preview?.Dispose();
                    preview = null;
                    treeView1.Nodes.Clear();
                    RefreshMeshList();
                }
                else
                {
                    LoadSkeletalMesh(index);
                }
                updatedExports.Remove(index);
            }
            else if (stm != null && updatedExports.Contains(stm.index))
            {
                int index = stm.index;
                //loaded SkeletalMesh is no longer a SkeletalMesh
                if (pcc.getExport(index).ClassName != "StaticMesh")
                {
                    stm = null;
                    preview?.Dispose();
                    preview = null;
                    treeView1.Nodes.Clear();
                    RefreshMeshList();
                }
                else
                {
                    LoadStaticMesh(index);
                }
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(Materials).Count() > 0)
            {
                RefreshMaterialList();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    string className = pcc.getExport(i).ClassName;
                    if (className == "MaterialInstanceConstant" || className == "Material")
                    {
                        RefreshMaterialList();
                        break;
                    }
                }
            }
            if (updatedExports.Intersect(Objects).Count() > 0)
            {
                RefreshMeshList();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    string className = pcc.getExport(i).ClassName;
                    if (className == "SkeletalMesh" || className == "StaticMesh")
                    {
                        RefreshMeshList();
                        break;
                    }
                }
            }
        }

        private void savePCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            pcc.save();
            MessageBox.Show("Done.", "Meshplorer", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
        }

        #region 3D Viewport
        //private List<WorldMesh> CurrentMeshLODs = new List<WorldMesh>();
        private int CurrentLOD = 0;
        //private List<SharpDX.Direct3D11.Texture2D> CurrentTextures = new List<SharpDX.Direct3D11.Texture2D>();
        //private List<SharpDX.Direct3D11.ShaderResourceView> CurrentTextureViews = new List<SharpDX.Direct3D11.ShaderResourceView>();
        private ModelPreview preview = null;
        private float globalscale = 1.0f;

        private void CenterView()
        {
            if (preview != null && preview.LODs.Count > 0)
            {
                WorldMesh m = preview.LODs[CurrentLOD].Mesh;
                globalscale = 0.5f / m.AABBHalfSize.Length();
                view.Camera.Position = m.AABBCenter * globalscale;
                view.Camera.FocusDepth = 1.0f;
                if (view.Camera.FirstPerson)
                {
                    view.Camera.Position -= view.Camera.CameraForward * view.Camera.FocusDepth;
                }
            }
            else
            {
                view.Camera.Position = SharpDX.Vector3.Zero;
                view.Camera.Pitch = -(float)Math.PI / 5.0f;
                view.Camera.Yaw = (float)Math.PI / 4.0f;
                globalscale = 1.0f;
            }
        }

        private void view_Render(object sender, EventArgs e)
        {
            if (preview != null && preview.LODs.Count > 0) // For some reason, reading props calls DoEvents which means that this might be called *in the middle of* loading a preview
            {
                if (solidToolStripMenuItem.Checked && CurrentLOD < preview.LODs.Count)
                {
                    view.Wireframe = false;
                    preview.Render(view, CurrentLOD, SharpDX.Matrix.Scaling(globalscale) * SharpDX.Matrix.RotationY(PreviewRotation));
                }
                if (wireframeToolStripMenuItem.Checked)
                {
                    view.Wireframe = true;
                    SceneRenderControl.WorldConstants ViewConstants = new SceneRenderControl.WorldConstants(SharpDX.Matrix.Transpose(view.Camera.ProjectionMatrix), SharpDX.Matrix.Transpose(view.Camera.ViewMatrix), SharpDX.Matrix.Transpose(SharpDX.Matrix.Scaling(globalscale) * SharpDX.Matrix.RotationY(PreviewRotation)));
                    view.DefaultEffect.PrepDraw(view.ImmediateContext);
                    view.DefaultEffect.RenderObject(view.ImmediateContext, ViewConstants, preview.LODs[CurrentLOD].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
                }
            }
        }

        private void view_Update(object sender, float e)
        {
            if (rotatingToolStripMenuItem.Checked) PreviewRotation += e * 0.05f;
            //view.Camera.Pitch = (float)Math.Sin(view.Time);
        }

        private void firstPersonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool old = view.Camera.FirstPerson;
            view.Camera.FirstPerson = firstPersonToolStripMenuItem.Checked;
            // Adjust view position so the camera doesn't teleport
            if (!old && view.Camera.FirstPerson)
            {
                view.Camera.Position += -view.Camera.CameraForward * view.Camera.FocusDepth;
            }
            else if (old && !view.Camera.FirstPerson)
            {
                view.Camera.Position += view.Camera.CameraForward * view.Camera.FocusDepth;
            }
        }
        #endregion

        private void meshplorer_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList();
            if (DroppedFiles.Count > 0)
            {
                LoadFile(DroppedFiles[0]);
            }
        }

        private void meshplorer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            recentToolStripMenuItem.Enabled = true;
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(MeshplorerDataFolder))
            {
                Directory.CreateDirectory(MeshplorerDataFolder);
            }
            string path = MeshplorerDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        private void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed
                var forms = Application.OpenForms;
                foreach (Form form in forms)
                {
                    if (form is Meshplorer && this != form)
                    {
                        ((Meshplorer)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            recentToolStripMenuItem.DropDownItems.Clear();
            if (RFiles.Count <= 0)
            {
                recentToolStripMenuItem.Enabled = false;
                return;
            }
            recentToolStripMenuItem.Enabled = true;

            foreach (string filepath in RFiles)
            {
                ToolStripMenuItem fr = new ToolStripMenuItem(filepath, null, RecentFile_click);
                recentToolStripMenuItem.DropDownItems.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = sender.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        private void LoadRecentList()
        {
            RFiles = new List<string>();
            RFiles.Clear();
            string path = MeshplorerDataFolder + RECENTFILES_FILE;
            recentToolStripMenuItem.Enabled = false;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        recentToolStripMenuItem.Enabled = true;
                        AddRecent(recent, true);
                    }
                }
            }
        }

        private void exportToOBJToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Wavefront OBJ File (*.obj)|*.obj";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (stm != null)
                {
                    stm.ExportOBJ(dialog.FileName);
                }
                else if (skm != null)
                {
                    skm.ExportOBJ(dialog.FileName);
                }
            }
        }

        private void importFromOBJToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (stm == null)
            {
                MessageBox.Show("Only static meshes can be imported from OBJ files.");
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Wavefront OBJ File (*.obj)|*.obj";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                timer1.Enabled = false;
                stm.ImportOBJ(dialog.FileName);
                byte[] buff = stm.SerializeToBuffer();
                IExportEntry en = pcc.Exports[stm.index];
                en.Data = buff;
                MessageBox.Show("OBJ import complete.");
                timer1.Enabled = true;
            }
        }

        /// <summary>
        /// Internal method for decoding UV values.
        /// </summary>
        /// <param name="val">The <see cref="Single"/> encoded as a <see cref="UInt16"/>.</param>
        /// <returns>The decoded <see cref="Single"/>.</returns>
        private float HalfToFloat(ushort val)
        {

            UInt16 u = val;
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }
    }
}
