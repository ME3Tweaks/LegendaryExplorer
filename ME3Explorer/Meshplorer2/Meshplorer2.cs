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
using AmaroK86.MassEffect3;
using ME3Explorer.Debugging;
using ME3Explorer.Scene3D;

namespace ME3Explorer.Meshplorer2
{
    public partial class MeshDatabase : Form
    {
        public struct EntryStruct
        {
            public string Filename;
            public string DLCName;
            public string ObjectPath;
            public int UIndex;
            public bool isDLC;
            public bool isSkeletal;
        }

        public List<EntryStruct> Entries;
        public int DisplayStyle = 0; //0 = per file, 1 = per path
        public List<int> Objects;
        public ModelPreview preview;
        public float PreviewRotation = 0;

        public MeshDatabase()
        {
            InitializeComponent();
        }

        private void scanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }
            DebugOutput.StartDebugger("Meshplorer2");
            int count = 0;
            timer1.Enabled = false;
            Entries = new List<EntryStruct>();
            #region Basegame Stuff
            string dir = ME3Directory.cookedPath;
            string[] files = Directory.GetFiles(dir, "*.pcc");
            pbar1.Maximum = files.Length - 1;
            foreach (string file in files)
            {
                DebugOutput.PrintLn("Scan file #" + count + " : " + file, count % 10 == 0);
                try
                {
                    using (IMEPackage pcc = MEPackageHandler.OpenME3Package(file))
                    {
                        foreach (ExportEntry entry in pcc.Exports)
                        {
                            if (entry.ClassName == "SkeletalMesh" ||
                                entry.ClassName == "StaticMesh")
                            {
                                EntryStruct ent = new EntryStruct();
                                ent.DLCName = "";
                                ent.Filename = Path.GetFileName(file);
                                ent.UIndex = entry.UIndex;
                                ent.isDLC = false;
                                ent.ObjectPath = entry.FullPath;
                                ent.isSkeletal = entry.ClassName == "SkeletalMesh";
                                Entries.Add(ent);
                            }
                        }

                        if (count % 10 == 0)
                        {
                            Application.DoEvents();
                            pbar1.Value = count;
                        }

                        count++;
                    }
                }
                catch (Exception ex)
                {
                    DebugOutput.PrintLn("=====ERROR=====\n" + ex.ToString() + "\n=====ERROR=====");
                }
            }
            #endregion
            #region Sorting
            bool run = true;
            DebugOutput.PrintLn("=====Info=====\n\nSorting names...\n\n=====Info=====");
            count = 0;
            while (run)
            {
                run = false;
                for (int i = 0; i < Entries.Count - 1; i++)
                    if (Entries[i].Filename.CompareTo(Entries[i + 1].Filename) > 0)
                    {
                        EntryStruct tmp = Entries[i];
                        Entries[i] = Entries[i + 1];
                        Entries[i + 1] = tmp;
                        run = true;
                        if (count++ % 100 == 0)
                            Application.DoEvents();
                    }
            }
            #endregion
            TreeRefresh();
            timer1.Enabled = true;
        }

        public void TreeRefresh()
        {
            treeView1.Nodes.Clear();
            treeView2.Nodes.Clear();
            treeView1.Visible = false;
            Application.DoEvents();
            int count = 0;
            pbar1.Maximum = Entries.Count - 1;
            if (DisplayStyle == 0)
            {
                foreach (EntryStruct e in Entries)
                {
                    if (!e.isDLC)
                    {
                        int f = -1;
                        for (int i = 0; i < treeView1.Nodes.Count; i++)
                            if (treeView1.Nodes[i].Text == e.Filename)
                                f = i;
                        string pre = "SKM";
                        if (!e.isSkeletal)
                            pre = "STM";
                        if (f == -1)
                        {
                            TreeNode t = new TreeNode(e.Filename);
                            t.Nodes.Add(count.ToString(), pre + "#" + e.UIndex + " : " + e.ObjectPath);
                            treeView1.Nodes.Add(t);
                        }
                        else
                        {
                            treeView1.Nodes[f].Nodes.Add(count.ToString(), pre + "#" + e.UIndex + " : " + e.ObjectPath);
                        }
                        if (count % 100 == 0)
                        {
                            pbar1.Value = count;
                            Application.DoEvents();
                        }
                        count++;
                    }
                    else
                    {
                        int f = -1;
                        for (int i = 0; i < treeView1.Nodes.Count; i++)
                            if (treeView1.Nodes[i].Text == e.DLCName + "::" + e.Filename)
                                f = i;
                        string pre = "SKM";
                        if (!e.isSkeletal)
                            pre = "STM";
                        if (f == -1)
                        {
                            TreeNode t = new TreeNode(e.DLCName + "::" + e.Filename);
                            t.Nodes.Add(count.ToString(), pre + "#" + e.UIndex + " : " + e.ObjectPath);
                            treeView1.Nodes.Add(t);
                        }
                        else
                        {
                            treeView1.Nodes[f].Nodes.Add(count.ToString(), pre + "#" + e.UIndex + " : " + e.ObjectPath);
                        }
                        if (count % 100 == 0)
                        {
                            pbar1.Value = count;
                            Application.DoEvents();
                        }
                        count++;
                    }
                }
            }
            treeView1.Visible = true;
            pbar1.Value = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            view.UpdateScene(0.1f); // TODO: Measure actual elapsed time
            view.Invalidate();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);

                fs.Write(BitConverter.GetBytes(Entries.Count), 0, 4);
                foreach (EntryStruct es in Entries)
                {
                    WriteString(fs, es.Filename);
                    WriteString(fs, es.DLCName);
                    WriteString(fs, es.ObjectPath);
                    fs.Write(BitConverter.GetBytes(es.UIndex), 0, 4);
                    if (es.isDLC)
                        fs.WriteByte(1);
                    else
                        fs.WriteByte(0);
                    if (es.isSkeletal)
                        fs.WriteByte(1);
                    else
                        fs.WriteByte(0);
                }
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes(s.Length), 0, 4);
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

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (DisplayStyle == 0)
            {
                if (t.Parent == null || t.Name == "")
                    return;
                preview?.Dispose();
                preview = null;
                try
                {
                    if (int.TryParse(t.Name, out int i))
                    {
                        EntryStruct en = Entries[i];
                        if (!en.isDLC)
                        {
                            using (IMEPackage pcc = MEPackageHandler.OpenME3Package(ME3Directory.cookedPath + en.Filename))
                            {
                                if (en.isSkeletal)
                                {
                                    SkeletalMesh skmesh = new SkeletalMesh(pcc.GetUExport(en.UIndex)); // TODO: pass device
                                    preview = new ModelPreview(view.Context.Device, skmesh, view.Context.TextureCache);
                                    CenterView();
                                    treeView2.Nodes.Clear();
                                    if (previewWithTreeToolStripMenuItem.Checked)
                                    {
                                        treeView2.Visible = false;
                                        Application.DoEvents();
                                        treeView2.Nodes.Add(skmesh.ToTree());
                                        treeView2.Visible = true;
                                    }
                                }
                                else
                                {

                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.db|*.db";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                timer1.Enabled = false;
                Entries = new List<EntryStruct>();
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                int count = ReadInt(fs);
                for (int i = 0; i < count; i++)
                {
                    EntryStruct en = new EntryStruct();
                    en.Filename = ReadString(fs);
                    en.DLCName = ReadString(fs);
                    en.ObjectPath = ReadString(fs);
                    en.UIndex = ReadInt(fs);
                    byte b = (byte)fs.ReadByte();
                    en.isDLC = b == 1;
                    b = (byte)fs.ReadByte();
                    en.isSkeletal = b == 1;
                    Entries.Add(en);
                }
                fs.Close();
                TreeRefresh();
                timer1.Enabled = true;
            }
        }

        public int ReadInt(FileStream fs)
        {
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        private void Meshplorer2_Load(object sender, EventArgs e)
        {
            view.LoadDirect3D();
        }

        public void FindNext()
        {
            string s = toolStripTextBox1.Text.ToLower();
            if (s == "")
                return;
            int startp = 0, startn = 0;
            if (treeView1.SelectedNode != null)
            {
                TreeNode t = treeView1.SelectedNode;
                if (t.Parent != null)
                {
                    startp = t.Parent.Index;
                    startn = t.Index + 1;
                }
            }
            for (int i = startp; i < treeView1.Nodes.Count; i++)
            {
                TreeNode p = treeView1.Nodes[i];
                for (int j = startn; j < p.Nodes.Count; j++)
                {
                    if (p.Nodes[j].Text.ToLower().Contains(s))
                    {
                        treeView1.SelectedNode = p.Nodes[j];
                        return;
                    }
                }
                startn = 0;
            }
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            FindNext();
        }

        private void toolStripTextBox1_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                FindNext();
        }

        private void Meshplorer2_FormClosing(object sender, FormClosingEventArgs e)
        {
            preview?.Dispose();
            view.Context.UnloadDirect3D();
        }

        private void view_Update(object sender, float e)
        {
            if (rotateToolStripMenuItem.Checked) PreviewRotation += e * 0.05f;

        }

        private void view_Render(object sender, EventArgs e)
        {
            if (preview != null)
            {
                view.Context.Wireframe = false;
                preview.Render(view.Context, 0, SharpDX.Matrix.RotationY(PreviewRotation));
                view.Context.Wireframe = true;
                SceneRenderContext.WorldConstants ViewConstants = new SceneRenderContext.WorldConstants(SharpDX.Matrix.Transpose(view.Context.Camera.ProjectionMatrix), SharpDX.Matrix.Transpose(view.Context.Camera.ViewMatrix), SharpDX.Matrix.Transpose(SharpDX.Matrix.RotationY(PreviewRotation)));
                view.Context.DefaultEffect.PrepDraw(view.Context.ImmediateContext);
                view.Context.DefaultEffect.RenderObject(view.Context.ImmediateContext, ViewConstants, preview.LODs[0].Mesh, new SharpDX.Direct3D11.ShaderResourceView[] { null });
            }
        }

        private void CenterView()
        {
            if (preview != null && preview.LODs.Count > 0)
            {
                WorldMesh m = preview.LODs[0].Mesh;
                view.Context.Camera.Position = m.AABBCenter;
                view.Context.Camera.FocusDepth = Math.Max(m.AABBHalfSize.X * 2.0f, Math.Max(m.AABBHalfSize.Y * 2.0f, m.AABBHalfSize.Z * 2.0f));
                if (view.Context.Camera.FirstPerson)
                {
                    view.Context.Camera.Position -= view.Context.Camera.CameraForward * view.Context.Camera.FocusDepth;
                }
            }
            else
            {
                view.Context.Camera.Position = SharpDX.Vector3.Zero;
                view.Context.Camera.Pitch = -(float)Math.PI / 5.0f;
                view.Context.Camera.Yaw = (float)Math.PI / 4.0f;
            }
        }
    }
}
