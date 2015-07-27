using System;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.Material_Viewer
{
    public static class MaterialPreview
    {
        public static Device device = null;
        public static PresentParameters presentParams = new PresentParameters();
        public static string script;
        public static float CamDistance;
        public static bool init;
        public struct Triangle
        {
            public Vector3 v0;
            public Vector3 v1;
            public Vector3 v2;
            public Vector2 UV0;
            public Vector2 UV1;
            public Vector2 UV2;
            public Vector3 norm;
        }
        public class CustomVertex
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MyStruct
            {
                public Vector3 Position;
                public Vector3 Normal;
                public float u0, v0;
                public float u1, v1;
                public float u2, v2;
                public float u3, v3;
                public static readonly VertexFormats Format = VertexFormats.Texture1 | VertexFormats.Texture0 | VertexFormats.Position | VertexFormats.Normal;
            }

        }

        public static List<Triangle> Triangles;
        public static CustomVertex.MyStruct[] DefaultMesh;
        public static Effect DrawEff;
        public static List<Texture> Textures;

        public static void CreateEffect()
        {

            script = System.Text.Encoding.UTF8.GetString(Properties.Resources.shader);
            try
            {
                DrawEff = Effect.FromString(device, script, null, null, ShaderFlags.None, null);
            }
            catch (Direct3DXException e)
            {
            }
        }

        public static CustomVertex.MyStruct NewVertex(Vector3 pos, Vector3 norm, float u0, float v0)
        {
            CustomVertex.MyStruct res = new CustomVertex.MyStruct();
            res.Position = pos;
            res.Normal = norm;
            res.u0 = u0;
            res.v0 = v0;
            res.u1 = u0;
            res.v1 = v0;
            return res;
        }

        public static Triangle MakeTri(Vector3 v0, Vector3 v1, Vector3 v2, Vector2 UV0, Vector2 UV1, Vector2 UV2, Vector3 norm)
        {
            Triangle res = new Triangle();
            res.v0 = v0;
            res.v1 = v1;
            res.v2 = v2;
            res.UV0 = UV0;
            res.UV1 = UV1;
            res.UV2 = UV2;
            res.norm = norm;
            return res;
        }

        public static void InitializeGraphics(Control handle)
        {
            try
            {
                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Discard;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D24S8;
                device = new Device(0, DeviceType.Hardware, handle, CreateFlags.HardwareVertexProcessing, presentParams);
                SetupEnvironment();

                CamDistance = 4;
                init = true;
            }
            catch (DirectXException)
            {
            }
        }

        public static void SetupLight(float f)
        {
            device.RenderState.ShadeMode = ShadeMode.Phong;
            device.RenderState.Lighting = true;
            device.Lights[0].Type = LightType.Point;
            device.Lights[0].Diffuse = Color.White;
            device.Lights[0].Range = 5;
            device.Lights[0].Position = new Vector3((float)Math.Sin(f * 3) * 3, 2, 3);
            device.Lights[0].Attenuation0 = 0.0f;
            device.Lights[0].Attenuation1 = 0.125f;
            device.Lights[0].Attenuation2 = 0.0f;
            device.Lights[0].Enabled = true;
            device.RenderState.CullMode = Cull.None;
            device.SetRenderState(RenderStates.ZEnable, true);
        }

        public static void SetupEnvironment()
        {
            SetStagesDefault();
        }

        public static void Render()
        {
            if (device == null)
                return;
            try
            {
                int iTime = Environment.TickCount;
                float fAngle = iTime * (2.0f * (float)Math.PI) / 10000.0f;
                SetupLight(fAngle);
                device.Clear(ClearFlags.Target, System.Drawing.Color.White, 1.0f, 0);
                device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Black, 1.0f, 0);
                device.BeginScene();
                device.Transform.World = Matrix.RotationY(fAngle * 0.5f);
                device.Transform.View = Matrix.LookAtLH(new Vector3(0.0f, CamDistance / 2, CamDistance), new Vector3(0, 0, 0), new Vector3(0.0f, 1.0f, 0.0f));
                device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 100000.0f);
                DrawNormal();
                device.EndScene();
                device.Present();
            }
            catch (DirectXException)
            {
                return;
            }
        }

        public static void DrawNormal()
        {
            device.SetTexture(0, null);
            device.VertexFormat = CustomVertex.MyStruct.Format;
            DrawEff.Technique = "Simplest";
            DrawEff.SetValue("xViewProjection", device.Transform.World * device.Transform.View * device.Transform.Projection);
            int numpasses = DrawEff.Begin(0);
            for (int i = 0; i < numpasses; i++)
            {
                DrawEff.BeginPass(i);
                device.DrawUserPrimitives(PrimitiveType.TriangleList, DefaultMesh.Length / 3, DefaultMesh);
                DrawEff.EndPass();
            }
            DrawEff.End();
        }

        public static void SetStagesDefault()
        {
            device.SetTextureStageState(0, TextureStageStates.TextureCoordinateIndex, 0);
            device.SetTextureStageState(0, TextureStageStates.ColorOperation, (int)TextureOperation.Modulate);
            device.SetTextureStageState(0, TextureStageStates.ColorArgument1, (int)TextureArgument.TextureColor);
            device.SetTextureStageState(0, TextureStageStates.ColorArgument2, (int)TextureArgument.Diffuse);
            device.SetTextureStageState(0, TextureStageStates.AlphaOperation, (int)TextureOperation.Disable);

            device.SetTextureStageState(1, TextureStageStates.TextureCoordinateIndex, 0);
            device.SetTextureStageState(1, TextureStageStates.ColorOperation, (int)TextureOperation.Disable);
            device.SetTextureStageState(1, TextureStageStates.AlphaOperation, (int)TextureOperation.Disable);
        }

        public static void GenerateTriangles()
        {
            Triangles = new List<Triangle>();
            //Front
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(0, 0, -1)));
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(1, 1, -1), new Vector3(1, -1, -1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(0, 0, -1)));
            //Back
            Triangles.Add(MakeTri(new Vector3(-1, -1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(0, 0, 1)));
            Triangles.Add(MakeTri(new Vector3(-1, -1, 1), new Vector3(1, 1, 1), new Vector3(1, -1, 1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(0, 0, 1)));
            //Top
            Triangles.Add(MakeTri(new Vector3(-1, 1, -1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(0, 1, 0)));
            Triangles.Add(MakeTri(new Vector3(-1, 1, -1), new Vector3(1, 1, 1), new Vector3(1, 1, -1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(0, 1, 0)));
            //Bottom
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(0, -1, 0)));
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(1, -1, 1), new Vector3(1, -1, -1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(0, -1, 0)));
            //Right
            Triangles.Add(MakeTri(new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(1, 0, 0)));
            Triangles.Add(MakeTri(new Vector3(1, -1, -1), new Vector3(1, 1, 1), new Vector3(1, -1, 1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(1, 0, 0)));
            //Left
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, 1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector3(-1, 0, 0)));
            Triangles.Add(MakeTri(new Vector3(-1, -1, -1), new Vector3(-1, 1, 1), new Vector3(-1, -1, 1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1), new Vector3(-1, 0, 0)));
            DefaultMesh = new CustomVertex.MyStruct[Triangles.Count * 3];
            for (int i = 0; i < Triangles.Count(); i++)
            {
                DefaultMesh[i * 3] = NewVertex(Triangles[i].v0, Triangles[i].norm, Triangles[i].UV0.X, Triangles[i].UV0.Y);
                DefaultMesh[i * 3 + 1] = NewVertex(Triangles[i].v1, Triangles[i].norm, Triangles[i].UV1.X, Triangles[i].UV1.Y);
                DefaultMesh[i * 3 + 2] = NewVertex(Triangles[i].v2, Triangles[i].norm, Triangles[i].UV2.X, Triangles[i].UV2.Y);
            }
        }


    }
}
