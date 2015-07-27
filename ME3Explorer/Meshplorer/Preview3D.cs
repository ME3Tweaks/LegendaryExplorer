using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3Explorer.Unreal.Classes;

namespace ME3Explorer.Meshplorer
{
    public static class Preview3D
    {
        public static Device device = null;
        public static PresentParameters presentParams = new PresentParameters();        
        public static float CamDistance;
        public static Vector3 CamOffset = new Vector3(0,0,0);
        public static Vector3 boxorigin;
        public static Vector3 box;
        public static bool init;
        public static bool rotate=true;
        public static int BodySetup;
        public static CustomVertex.PositionTextured[] RawTriangles;
        public static Material Mat;
        public static Texture DefaultTex;
        public static Texture CurrentTex;
        public static StaticMesh StatMesh;
        public static SkeletalMesh SkelMesh;
        public static int LOD;

        public struct DXCube
        {
            public Vector3 center;
            public Vector3 origin;
            public Vector3 size;
            public Vector3 min;
            public Vector3 max;
            public List<DXCube> childs;
            public CustomVertex.PositionColored[] verts;
        }

        public static List<DXCube> Cubes;

        public static Vector3 switchYZ(Vector3 v)
        {
            float f = v.Y;
            v.Y = v.Z;
            v.Z = f;
            return v;
        }

        public static DXCube NewCubeByCubeMinMax(DXCube org, Vector3 min, Vector3 max, int c)
        {
            Vector3 orig = org.center;
            Vector3 antiorig = org.center;
            orig -= new Vector3(org.size.X * min.X, org.size.Y * min.Y, org.size.Z * min.Z);
            antiorig += new Vector3(org.size.X * max.X, org.size.Y * max.Y, org.size.Z * max.Z);
            Vector3 box = antiorig - orig;
            return NewCubeByOrigSize(orig, box, c);
        }

        public static DXCube NewCubeByOrigSize(Vector3 origin, Vector3 size,int c)
        {
            DXCube res = new DXCube();            
            res.origin = origin;
            //res.origin = switchYZ(res.origin);
            res.size = size;
            //res.size = switchYZ(res.size);
            res.center = origin + size * 0.5f;
            res.min = res.max = new Vector3(0.5f, 0.5f, 0.5f);
            res.childs = new List<DXCube>();
            res.verts = new CustomVertex.PositionColored[24];
            res.verts[0] = new CustomVertex.PositionColored(origin, c);
            res.verts[1] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, 0), c);
            res.verts[2] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, 0), c);
            res.verts[3] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, 0), c);
            res.verts[4] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, 0), c);
            res.verts[5] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, 0), c);
            res.verts[6] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, 0), c);
            res.verts[7] = new CustomVertex.PositionColored(origin, c);

            res.verts[8] = new CustomVertex.PositionColored(origin + new Vector3(0, 0, size.Z), c);
            res.verts[9] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, size.Z), c);
            res.verts[10] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, size.Z), c);
            res.verts[11] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, size.Z), c);
            res.verts[12] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, size.Z), c);
            res.verts[13] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, size.Z), c);
            res.verts[14] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, size.Z), c);
            res.verts[15] = new CustomVertex.PositionColored(origin + new Vector3(0, 0, size.Z), c);

            res.verts[16] = new CustomVertex.PositionColored(origin, c);
            res.verts[17] = new CustomVertex.PositionColored(origin + new Vector3(0, 0, size.Z), c);
            res.verts[18] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, 0), c);
            res.verts[19] = new CustomVertex.PositionColored(origin + new Vector3(size.X, 0, size.Z), c); 
            res.verts[20] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, 0), c);
            res.verts[21] = new CustomVertex.PositionColored(origin + new Vector3(size.X, size.Y, size.Z), c);
            res.verts[22] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, 0), c);
            res.verts[23] = new CustomVertex.PositionColored(origin + new Vector3(0, size.Y, size.Z), c);
            return res;
        }

        public static void Refresh()
        {
            if (init) Render();
        }

        public static bool InitializeGraphics(Control handle)
        {
            try
            {
                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Discard;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D16;
                device = new Device(0, DeviceType.Hardware, handle, CreateFlags.SoftwareVertexProcessing, presentParams);
                CamDistance = 10;
                Mat = new Material();
                Mat.Diffuse = Color.White;
                Mat.Specular = Color.LightGray;
                Mat.SpecularSharpness = 15.0F;
                device.Material = Mat;
                string loc = Path.GetDirectoryName(Application.ExecutablePath);
                DefaultTex = TextureLoader.FromFile(device, loc + "\\exec\\Default.bmp");
                init = true;
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
        }

        private static float fAngle=0;

        private static void Render()
        {
            if (device == null)
                return;
            try
            {
                device.SetRenderState(RenderStates.ShadeMode, 1);
                device.RenderState.Lighting = false;
                device.Lights[0].Type = LightType.Directional;
                device.Lights[0].Diffuse = Color.White;
                device.Lights[0].Range = 100000;
                device.Lights[0].Direction = new Vector3(1, 1, -1);
                device.Lights[0].Enabled = true;
                device.RenderState.CullMode = Cull.None;
                device.SetRenderState(RenderStates.ZEnable, true);
                device.Clear(ClearFlags.Target, System.Drawing.Color.White, 1.0f, 0);
                device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Black, 1.0f, 0);
                device.BeginScene();
                    int iTime = Environment.TickCount;
                    if(rotate)
                        fAngle = iTime * (2.0f * (float)Math.PI) / 10000.0f;
                    device.Transform.World = Matrix.RotationZ(fAngle);
                    device.Transform.View = Matrix.LookAtLH(new Vector3(0.0f, CamDistance, CamDistance / 2) + CamOffset, new Vector3(0, 0, 0) + CamOffset, new Vector3(0.0f, 0.0f, 1.0f));
                    device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 100000.0f);
                    
                    device.SetTexture(0, null);
                    device.VertexFormat = CustomVertex.PositionColored.Format;
                    device.RenderState.Lighting = false;
                    if(Cubes != null)
                        for (int i = 0; i < Cubes.Count(); i++)
                            device.DrawUserPrimitives(PrimitiveType.LineList,12,Cubes[i].verts);

                    if (StatMesh != null)
                        StatMesh.DrawMesh(device);
                    device.SetTexture(0, DefaultTex);
                    if (SkelMesh != null)
                        SkelMesh.DrawMesh(device, LOD);
                device.EndScene();
                device.Present();
            }
            catch (DirectXException)
            {
                return;
            }
        }
    }
}
