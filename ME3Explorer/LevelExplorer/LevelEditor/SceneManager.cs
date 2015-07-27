using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using lib3ds.Net;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.LevelExplorer.LevelEditor
{
    public class SceneManager
    {
        public struct Levelfile
        {
            public string path;
            public PCCObject pcc;
            public Level level;
        }

        public List<Levelfile> Levels;
        public Device device;
        public TreeView GlobalTree;
        public struct Pressedkeys
        {
            public bool w;
            public bool a;
            public bool s;
            public bool d;
        }
        public Pressedkeys Pkeys = new Pressedkeys();
        public bool MoveWASD = true;
        public float MoveSpeed = 10000f;

        public Device CreateView(PictureBox p)
        {
            PresentParameters presentParams = new PresentParameters();
            presentParams.Windowed = true;
            presentParams.SwapEffect = SwapEffect.Discard;
            presentParams.EnableAutoDepthStencil = true;
            presentParams.AutoDepthStencilFormat = DepthFormat.D24S8;
            return new Device(0, DeviceType.Hardware, p.Handle, CreateFlags.SoftwareVertexProcessing, presentParams);
        }

        public void Init(Device d, TreeView tv)
        {
            Levels = new List<Levelfile>();
            device = d;
            Material material = new Material();
            material.Diffuse = Color.White;
            material.Specular = Color.LightGray;
            material.SpecularSharpness = 15.0F;
            device.Material = material;
            device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 1000000.0f);
            device.SetRenderState(RenderStates.ShadeMode, 1);
            device.RenderState.Lighting = true;
            device.RenderState.Ambient = Color.Gray;
            device.SamplerState[0].MinFilter = TextureFilter.Linear;
            device.SamplerState[0].MagFilter = TextureFilter.Linear;
            device.Lights[0].Type = LightType.Directional;
            device.Lights[0].Diffuse = Color.White;
            device.Lights[0].Range = 100000;
            device.Lights[0].Enabled = true;
            device.RenderState.CullMode = Cull.None;
            device.SetRenderState(RenderStates.ZEnable, true);
            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            DirectXGlobal.Tex_Default = TextureLoader.FromFile(device, loc + "\\exec\\Default.bmp");
            DirectXGlobal.Tex_Select = TextureLoader.FromFile(device, loc + "\\exec\\select.bmp");
            GlobalTree = tv;
            GlobalTree.Nodes.Clear();
        }

        public void ResetDX()
        {
            Material material = new Material();
            material.Diffuse = Color.White;
            material.Specular = Color.LightGray;
            material.SpecularSharpness = 15.0F;
            device.Material = material;
            float aspect = (float)device.PresentationParameters.BackBufferWidth / (float)device.PresentationParameters.BackBufferHeight;
            device.Transform.Projection = Matrix.PerspectiveFovLH(1f / aspect, aspect, 1.0f, 1000000.0f);
            device.SetRenderState(RenderStates.ShadeMode, 1);
            device.RenderState.Lighting = true;
            device.RenderState.Ambient = Color.Gray;
            device.SamplerState[0].MinFilter = TextureFilter.Linear;
            device.SamplerState[0].MagFilter = TextureFilter.Linear;
            device.Lights[0].Type = LightType.Directional;
            device.Lights[0].Diffuse = Color.White;
            device.Lights[0].Range = 100000;
            device.Lights[0].Enabled = true;
            device.RenderState.CullMode = Cull.None;
            device.SetRenderState(RenderStates.ZEnable, true);
            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
        }

        public void AddLevel(string path)
        {
            if (!File.Exists(path))
                return;
            Levelfile l = new Levelfile();
            l.path = path;
            l.pcc = new PCCObject(path);
            for (int i = 0; i < l.pcc.Exports.Count; i++)
            {
                PCCObject.ExportEntry e = l.pcc.Exports[i];
                if (e.ClassName == "Level")
                {
                    DebugOutput.Clear();
                    l.level = new Level(l.pcc, i);
                    TreeNode t = new TreeNode(Path.GetFileName(path));
                    t.Nodes.Add(l.level.ToTree(i));
                    GlobalTree.Visible = false;
                    GlobalTree.Nodes.Add(t);
                    GlobalTree.Visible = true;
                    DirectXGlobal.Cam.dir = new Vector3(1.0f, 1.0f, 1.0f);
                    Levels.Add(l);
                }
            }
        }

        public void SaveAllChanges()
        {
            DebugOutput.PrintLn("Saving all changes to files...");
            foreach (Levelfile l in Levels)
                l.level.SaveChanges();
            DebugOutput.PrintLn("Running Tocbinupdater...");
            TOCbinUpdater.UpdateTocBin(ME3Directory.tocFile, ME3Directory.gamePath, null, null);
            DebugOutput.PrintLn("Done.");
        }

        public void CreateModJobs()
        {
            foreach (Levelfile l in Levels)
                l.level.CreateModJobs();
            MessageBox.Show("Done");
        }

        public void Render()
        {
            if (device == null)
                return;
            try
            {
                device.Clear(ClearFlags.Target, System.Drawing.Color.White, 1.0f, 0);
                device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Black, 1.0f, 0);
                device.BeginScene();
                device.Lights[0].Type = LightType.Directional;
                device.Lights[0].Diffuse = Color.White;
                device.Lights[0].Range = 100000;
                device.Lights[0].Direction = -DirectXGlobal.Cam.dir;
                device.Lights[0].Enabled = true;
                device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 1000000.0f);
                device.Transform.View = Matrix.LookAtLH(DirectXGlobal.Cam.pos, DirectXGlobal.Cam.pos + DirectXGlobal.Cam.dir, new Vector3(0.0f, 0.0f, 1.0f));
                foreach (Levelfile l in Levels)
                    l.level.Render(device);
                device.EndScene();
                device.Present();
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("DIRECT X ERROR: " + e.Message);
            }
        }

        public void Update()
        {
            Vector3 n = Vector3.Cross(DirectXGlobal.Cam.dir, new Vector3(0, 0, 1f));
            n.Normalize();
            if (Pkeys.w)
                DirectXGlobal.Cam.pos += DirectXGlobal.Cam.dir * MoveSpeed;
            if (Pkeys.s)
                DirectXGlobal.Cam.pos -= DirectXGlobal.Cam.dir * MoveSpeed;
            if (Pkeys.a)
                DirectXGlobal.Cam.pos += n * MoveSpeed;
            if (Pkeys.d)
                DirectXGlobal.Cam.pos -= n * MoveSpeed;
        }

        public void ProcessKey(int key, bool isdown)
        {
            if (MoveWASD)
            {
                if (key == 87)//w
                    Pkeys.w = isdown;
                if (key == 83)//s
                    Pkeys.s = isdown;
                if (key == 65)//a
                    Pkeys.a = isdown;
                if (key == 68)//d
                    Pkeys.d = isdown;
            }
        }

        public void ProcessTreeClick(string path, bool AutoFocus)
        {
            string[] pathlist = path.Split('/');
            int[] idxpath = new int[pathlist.Length-1];
            for (int i = 0; i < pathlist.Length -1; i++)
                idxpath[i] = Convert.ToInt32(pathlist[i]);
            if (idxpath.Length < 3)         //clicked on pccfile or levelobject
                return;
            DeSelectAll();
            Levels[idxpath[0]].level.ProcessTreeClick(idxpath, AutoFocus);
        }

        public void Process3DClick(Vector3 org, Vector3 dir)
        {
            foreach (Levelfile l in Levels)
                l.level.Process3DClick(org, dir);
        }

        public void ApplyTransform(Matrix m)
        {
            foreach (Levelfile l in Levels)
                l.level.ApplyTransform(m);
        }

        public void ApplyRotation(Vector3 v)
        {
            foreach (Levelfile l in Levels)
                l.level.ApplyRotation(v);
        }

        public void DeSelectAll()
        {
            foreach (Levelfile l in Levels)
                l.level.SetSelection(false);
        }

        public string GetNodePath(TreeNode t)
        {
            string s = "";
            if (t.Parent == null)
                s = "#" + t.Index + " ";
            else
                s = GetNodePath(t.Parent);
            s += t.Text + "//";
            return s;
        }

        public string GetNodeIndexPath(TreeNode t)
        {
            string s = "";
            if (t.Parent != null)
                s = GetNodeIndexPath(t.Parent);
            s += t.Index + "/";
            return s;
        }

        public void ExportScene3DS(string path)
        {
            Lib3dsFile f = Helper3DS.EmptyFile();
            foreach (Levelfile l in Levels)
                l.level.Export3DS(f);
            Helper3DS.ClearFirstMesh(f);
            if (!LIB3DS.lib3ds_file_save(f, path))
                MessageBox.Show("Error while saving!");
        }
    }
}
