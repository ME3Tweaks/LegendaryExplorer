using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.UnrealHelper;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Runtime.Serialization.Formatters.Binary;

namespace ME3Explorer
{
    public partial class Leveleditor : Form
    {
        public ULevel Level;
        public Device device = null;
        public PresentParameters presentParams = new PresentParameters();
        public bool init;
        public Vector3 CamEye, CamDir;
        public float CamRot;
        public int SelectStat;
        public int SelectStatLast;
        public Mesh selbounding;
        public Texture seltex;
        public Texture defaulttex;
        public Material material;

        public bool MoveWASD;
        public bool DrawSphere;
        public bool DrawWireFrame;
        public bool DrawLit;
        public bool DrawTexture;

        public struct RayCastSelection
        {
            public float dist;
            public int entry;
        }

        public Leveleditor()
        {
            InitializeComponent();
        }

        public void LoadLevel(ULevel level)
        {
            Level = level;
            if (!InitializeGraphics(pic1))
                return;
            level.LoadLevelObjects(device,rtb1);
            if(level.UStat.Count == 0)
                return;            
            timer1.Enabled = true;
            CamEye = Vector3.TransformCoordinate(new Vector3(0,0,0),Level.UStat[0].DirectX.m);
            CamDir = new Vector3(0, -1, 1);
            SelectStatLast = -1;
            GenerateTree();
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            seltex = TextureLoader.FromFile(device, loc + "\\exec\\select.bmp");
            defaulttex = TextureLoader.FromFile(device, loc + "\\exec\\Default.bmp");
            DrawSphere = true;
            DrawTexture = true;
            MoveWASD = true;
            
        }

        public void GenerateTree()
        {
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(StatToTree());
            treeView1.Nodes.Add(TexToTree());
        }

        public TreeNode StatToTree()
        {
            TreeNode t = new TreeNode("Static Meshes");
            for (int i = 0; i < Level.UStat.Count; i++)
            {
                string s = "";
                switch(Level.UStat[i].LP.source)
                {
                    case 0:
                        s = "(SMA = " + Level.UStat[i].LP.sourceentry + ")";
                        break;
                    case 1:
                        s = "(SMCA = " + Level.UStat[i].LP.sourceentry + ")";
                        break;
                    case 2:
                        s = "(IA = " + Level.UStat[i].LP.sourceentry + ")";
                        break;
                }
                if (Level.UStat[i].DirectX.visible)
                    t.Nodes.Add(new TreeNode(i.ToString() + " : " + Level.UStat[i].DirectX.name + " (" + Level.UStat[i].DirectX.objindex.ToString() + ")" + s));
                else
                    t.Nodes.Add(new TreeNode(i.ToString() + " : (invis)" + Level.UStat[i].DirectX.name + " (" + Level.UStat[i].DirectX.objindex.ToString() + ")" + s));
            }
            return t;
        }

        public TreeNode TexToTree()
        {
            TreeNode t = new TreeNode("Textures");
            for (int i = 0; i < Level.Tex.Count; i++)
                t.Nodes.Add(Level.Tex[i].name + "(" + Level.Tex[i].index.ToString() + ")");
            return t;
        }

        public bool InitializeGraphics(Control handle)
        {
            try
            {

                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Discard;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D24S8;
                device = new Device(0, DeviceType.Hardware, handle, CreateFlags.SoftwareVertexProcessing, presentParams);
                DrawWireFrame = true;
                DrawLit = false;
                material = new Material();
                material.Diffuse = Color.White;
                material.Specular = Color.LightGray;
                material.SpecularSharpness = 15.0F;
                device.Material = material;
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
        }               

        public void Render()
        {
            if (device == null)
                return;
            try
            {
                device.Transform.View = Matrix.LookAtLH(CamEye, CamEye + CamDir, new Vector3(0.0f, 0.0f, 1.0f));
                device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 1000000.0f);
                device.SetRenderState(RenderStates.ShadeMode, 1);
                device.RenderState.Lighting = DrawLit;
                device.RenderState.Ambient = Color.Gray;
                device.Material = material;
                device.SamplerState[0].MinFilter = TextureFilter.Linear;
                device.SamplerState[0].MagFilter = TextureFilter.Linear;
                device.Lights[0].Type = LightType.Directional;
                device.Lights[0].Diffuse = Color.White;
                device.Lights[0].Range = 100000;
                device.Lights[0].Direction = -CamDir; //new Vector3(0.8f, 0.4f, 1);
                device.Lights[0].Enabled = true;
                device.RenderState.CullMode = Cull.None;
                device.SetRenderState(RenderStates.ZEnable, true);
                if(DrawWireFrame)
                    device.RenderState.FillMode = FillMode.WireFrame;
                else
                    device.RenderState.FillMode = FillMode.Solid;
                device.VertexShader = null;
                device.Clear(ClearFlags.Target, System.Drawing.Color.Blue, 1.0f, 0);
                device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Black, 1.0f, 0);
                device.BeginScene();                
                device.RenderState.AlphaBlendEnable = false;
                    for (int i = 0; i < Level.UStat.Count(); i++)
                        if (Level.UStat[i].DirectX.visible & CheckVisible(Level.UStat[i].DirectX.tfpos, Level.UStat[i].DirectX.tfr))
                        {
                            if (DrawTexture)
                            {
                                if (Level.UStat[i].LP.textureindex == -1)
                                    device.SetTexture(0, defaulttex);
                                else
                                    device.SetTexture(0, Level.Tex[Level.UStat[i].LP.textureindex].tex);
                            }
                            else
                                device.SetTexture(0, null);
                            ULevel.DirectXObject t = Level.UStat[i].DirectX;
                            device.Transform.World = t.m;
                            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
                            device.DrawUserPrimitives(PrimitiveType.TriangleList, t.verts.Length / 3, t.verts);
                        }
                if (DrawSphere && selbounding != null)
                {
                    device.RenderState.SourceBlend = Blend.One;
                    device.RenderState.DestinationBlend = Blend.One;
                    device.RenderState.AlphaBlendEnable = true;
                    device.RenderState.Lighting = false;
                    ULevel.DirectXObject t = Level.UStat[SelectStat].DirectX;
                    device.Transform.World = Matrix.Translation(t.tfpos);
                    device.SetTexture(0, seltex);
                    selbounding.DrawSubset(0);
                    device.SetTexture(0, null);
                }
                device.EndScene();
                device.Present();
            }
            catch (DirectXException)
            {
                return;
            }
        }

        public bool CheckVisible(Vector3 pos, float r)
        {
            Plane p = Plane.FromPointNormal(CamEye, CamDir);
            float d = Vector3.Dot(CamDir, pos) + p.D;
            if (d + r < 0)
                return false;
            return true;
        }

        private void Leveleditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (MoveWASD)
            {
                float speed = 200f;
                if (e.KeyCode != Keys.S)
                    CamEye += CamDir * speed;
                if (e.KeyCode != Keys.W)
                    CamEye -= CamDir * speed;
                Vector3 c = Vector3.Cross(CamDir, new Vector3(0, 0, -1));
                c.Normalize();
                if (e.KeyCode != Keys.A)
                    CamEye += c * speed;
                if (e.KeyCode != Keys.D)
                    CamEye -= c * speed;
            }
        }

        private void solidToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DrawWireFrame = false;
            DrawTexture = false;
            DrawLit = true;
        }

        private void wireframeLitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DrawWireFrame = true;
            DrawTexture = true;
            DrawLit = true;
        }

        private void wireframeUnlitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DrawWireFrame = true;
            DrawTexture = true;
            DrawLit = false;
        }

        private void timer1_Tick_1(object sender, EventArgs e)
        {
            Render();
        }
        
        public void CheckSelect()
        {
            if (SelectStat != -1)
            {
                ULevel.DirectXObject d;
                if (SelectStatLast != -1)
                {
                    d = Level.UStat[SelectStatLast].DirectX;
                    d.verts = Level.UStat[SelectStatLast].ExportDirectX(Color.White.ToArgb());
                    Level.UStat[SelectStatLast].DirectX = d;
                }
                SelectStatLast = SelectStat;
                d = Level.UStat[SelectStat].DirectX;
                d.verts = Level.UStat[SelectStat].ExportDirectX(Color.Red.ToArgb());
                Level.UStat[SelectStat].DirectX = d;
                selbounding = CreateSphere(Level.UStat[SelectStat].DirectX.tfr);
            }
            else
            {
                selbounding = null;
                ULevel.DirectXObject d;
                if (SelectStatLast != -1)
                {
                    d = Level.UStat[SelectStatLast].DirectX;
                    d.verts = Level.UStat[SelectStatLast].ExportDirectX(Color.White.ToArgb());
                    Level.UStat[SelectStatLast].DirectX = d;
                }
            }
        }

        public Mesh CreateSphere(float radius)
        {
            Mesh t = Mesh.Sphere(device, radius, 20, 20);
            return t;
        }
        
        private void saveCamPosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "Campos(*.scn)|*.scn";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(FileDialog1.FileName, FileMode.Create, FileAccess.Write);
                byte[] buff = BitConverter.GetBytes(CamEye.X);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(CamEye.Y);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(CamEye.Z);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(CamDir.X);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(CamDir.Y);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(CamDir.Z);
                fs.Write(buff, 0, 4);
                fs.Close();
            }
        }

        private void loadCamPosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog FileDialog1 = new OpenFileDialog();
            FileDialog1.Filter = "Campos(*.scn)|*.scn";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(FileDialog1.FileName, FileMode.Open, FileAccess.Read);                
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                CamEye.X = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                CamEye.Y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                CamEye.Z = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                CamDir.X = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                CamDir.Y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                CamDir.Z = BitConverter.ToSingle(buff, 0);
                fs.Close();
            }
        }

        private void transformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectStat = -1;
            TreeNode t = treeView1.SelectedNode;
            if (t != null && t.Parent != null)
            {
                TreeNode t2 = t.Parent;
                if (t2.Text == "Static Meshes")
                    SelectStat = t.Index;
            }
            if (SelectStat == -1)
                return;
            Lvl_Transform l = new Lvl_Transform();
            l.refO = this;
            l.refStat = SelectStat;
            treeView1.Enabled = false;
            l.ShowDialog();
        }

        private void moveWASDIn3DScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveWASD = moveWASDIn3DScreenToolStripMenuItem.Checked;
        }

        private void drawBoundingSphereToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DrawSphere = drawBoundingSphereToolStripMenuItem.Checked;
        }

        private void GetSelection3D(MouseEventArgs e)
        {
            device.Transform.View = Matrix.LookAtLH(CamEye, CamEye + CamDir, new Vector3(0.0f, 0.0f, 1.0f));
            device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 1000000.0f);
            Vector3 near = Vector3.Unproject(new Vector3(e.X, e.Y, 0), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 far = Vector3.Unproject(new Vector3(e.X, e.Y, 1), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 dir = far - near;
            dir.Normalize();
            List<RayCastSelection> selection = new List<RayCastSelection>();
            for (int i = 0; i < Level.UStat.Count; i++)
                if (Level.UStat[i].DirectX.visible)
                {
                    ULevel.DirectXObject d = Level.UStat[i].DirectX;
                    Vector2 result = RaySphereIntersect(CamEye - d.tfpos, dir, d.tfr);
                    if (result != new Vector2(-1, -1))
                    {
                        RayCastSelection r = new RayCastSelection();
                        r.dist = result.Y;
                        r.entry = i;
                        if (!float.IsNaN(r.dist))
                            selection.Add(r);
                    }
                }
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < selection.Count - 1; i++)
                    if (selection[i].dist > selection[i + 1].dist)
                    {
                        run = true;
                        RayCastSelection r = selection[i];
                        selection[i] = selection[i + 1];
                        selection[i + 1] = r;
                    }
            }
            int found = -1;
            for (int i = 0; i < selection.Count - 1; i++)
                if (selection[i].entry == SelectStat)
                    found = i;
            if (found != -1)
            {
                if (found < Level.UStat.Count - 1) found++;
                else found = 0;
                SelectStat = found;
                CheckSelect();
                TreeNode t = treeView1.Nodes[0];
                treeView1.SelectedNode = t.Nodes[selection[found].entry];
            }
            else
            {
                SelectStat = selection[0].entry;
                CheckSelect();
                TreeNode t = treeView1.Nodes[0];
                treeView1.SelectedNode = t.Nodes[selection[0].entry];
            }
        }

        Vector2 RaySphereIntersect(Vector3 p, Vector3 d, float r)
        {
            float det, b;
            b = -Vector3.Dot(p, d);
            det = b * b - Vector3.Dot(p, p) + r * r;
            if (det < 0) return new Vector2(-1,-1);
            det = (float)Math.Sqrt(det);
            Vector2 v = new Vector2(b - det, b + det);
            if (v.Y < 0) return new Vector2(-1, -1);
            if (v.X < 0) v.X = 0;
            return v;
            
        }

        private void pic1_MouseClick_1(object sender, MouseEventArgs e)
        {
            GetSelection3D(e);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            SelectStat = -1;
            TreeNode t = treeView1.SelectedNode;
            if (t != null & t.Parent != null)
            {
                TreeNode t2 = t.Parent;
                if (t2.Text == "Static Meshes")
                    SelectStat = t.Index;
            }
            CheckSelect();
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            SelectStat = -1;
            TreeNode t = treeView1.SelectedNode;
            if (t != null & t.Parent != null)
            {
                TreeNode t2 = t.Parent;
                if (t2.Text == "Static Meshes")
                    SelectStat = t.Index;
            }
            if (SelectStat != -1)
            {
                ULevel.DirectXObject d;
                d = Level.UStat[SelectStat].DirectX;
                d.visible = !d.visible;
                Level.UStat[SelectStat].DirectX = d;
                GenerateTree();
                TreeNode t2 = treeView1.Nodes[0];
                t2.ExpandAll();
                treeView1.SelectedNode = t2.Nodes[SelectStat];
            }
        }

        private void pic1_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveWASD)
            {
                float h = (float)e.Y / (float)pic1.Height - 0.5f;
                float w = (float)e.X / (float)pic1.Width;
                CamDir.Z = -h * 2;
                CamDir.Y = 1;
                CamDir.X = 0;
                CamRot = w * 360;
                CamDir = Vector3.TransformCoordinate(CamDir, Matrix.RotationZ(CamRot * (3.1415f / 180f)));
            }
        }

        private void texturedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DrawWireFrame = false;
            DrawTexture = true;
            DrawLit = true;
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int Sel = -1;
            TreeNode t = treeView1.SelectedNode;
            if (t != null && t.Parent != null)
            {
                TreeNode t2 = t.Parent;
                if (t2.Text == "Static Meshes")
                    Sel = t.Index;
            }
            if (Sel == -1)
                return;
            if (Level.UStat[Sel].LP.source == 1)//Collection
            {
                int index = Level.UStat[Sel].LP.sourceentry;
                int index2 = Level.UStat[Sel].index2;
                for(int i=0;i<Level.UStatColl.Count;i++)
                    if (Level.UStatColl[i].LP.sourceentry == index)
                        for (int j = 0; j < Level.UStatColl[i].Entries.Count; j++)
                            if (Level.UStatColl[i].Entries[j] == index2)
                            {
                                Level.UStatColl[i].CloneObject(j);                                                              
                                UStaticMesh u = Level.UStat[Sel];
                                UStaticMesh u2 = new UStaticMesh();
                                u2.LP = u.LP;
                                u2.DirectX = u.DirectX;
                                u2.bound = u.bound;
                                u2.index = u.index;
                                u2.index2 = u.index2;
                                Level.UStat.Add(u2);
                                GenerateTree();
                                TreeNode t2 = treeView1.Nodes[0];
                                t2.Expand();
                                treeView1.SelectedNode = t2.Nodes[Sel];
                                MessageBox.Show("Done.");
                                return;
                            }

            }
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int size = Level.pcc.memsize;
            for (int i = 0; i < Level.UStatColl.Count; i++)
                if (Level.UStatColl[i].CHANGED)
                {
                    Level.pcc.appendStream(Level.UStatColl[i].memory);
                    Level.pcc.RedirectEntry(Level.UStatColl[i].LP.sourceentry, size, Level.UStatColl[i].memsize);
                }
            if (Level.pcc.memsize != size)
            {
                TOCeditor tc = new TOCeditor();
                if (!tc.UpdateFile(Level.pcc.loadedFilename, (uint)size))
                    MessageBox.Show("Didn't found Entry");
            }
        }

        private void pic1_Click(object sender, EventArgs e)
        {

        }

    }
}
