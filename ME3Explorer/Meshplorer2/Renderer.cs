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

namespace ME3Explorer.Meshplorer2
{
    public static class Renderer
    {
        public static Device device = null;
        public static PresentParameters presentParams = new PresentParameters();
        public static float CamDistance;
        public static Vector3 CamOffset = new Vector3(0, 0, 0);
        public static Vector3 boxorigin;
        public static Vector3 box;
        public static bool init;
        public static bool rotate = true;
        public static float fAngle = 0;
        public static CustomVertex.PositionColored[] lines;
        public static CustomVertex.PositionTextured[] RawTriangles;
        public static Material Mat;
        public static Texture DefaultTex;
        public static StaticMesh STM;
        public static SkeletalMesh SKM;
        public static int LOD;

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
                CreateCoordLines();
                init = true;
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
        }

        public static void CreateCoordLines()
        {
            lines = new CustomVertex.PositionColored[6];
            lines[1] = lines[3] = lines[5] = new CustomVertex.PositionColored(new Vector3(0, 0, 0), 0);
            lines[0] = new CustomVertex.PositionColored(new Vector3(1, 0, 0), 0);
            lines[2] = new CustomVertex.PositionColored(new Vector3(0, 1, 0), 0);
            lines[4] = new CustomVertex.PositionColored(new Vector3(0, 0, 1), 0);
        }

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
                if (rotate)
                    fAngle = iTime * (2.0f * (float)Math.PI) / 10000.0f;
                device.Transform.World = Matrix.RotationZ(fAngle);
                device.Transform.View = Matrix.LookAtLH(new Vector3(0.0f, CamDistance, CamDistance / 2) + CamOffset, new Vector3(0, 0, 0) + CamOffset, new Vector3(0.0f, 0.0f, 1.0f));
                device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 100000.0f);
                device.SetTexture(0, null);
                RenderCoordsystem(device);
                device.VertexFormat = CustomVertex.PositionColored.Format;
                device.RenderState.Lighting = false;
                device.SetTexture(0, DefaultTex);
                if (SKM != null)
                    SKM.DrawMesh(device, LOD);
                device.EndScene();
                device.Present();
            }
            catch (DirectXException)
            {
                return;
            }
        }

        public static void RenderCoordsystem(Device device)
        {
            device.RenderState.FillMode = FillMode.WireFrame;
            device.VertexFormat = CustomVertex.PositionColored.Format;
            device.RenderState.Ambient = Color.Black;
            device.DrawUserPrimitives(PrimitiveType.LineList, 3, lines);
            device.RenderState.Ambient = Color.LightGray;
        }
    }
}
