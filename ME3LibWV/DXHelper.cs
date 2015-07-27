using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3LibWV.UnrealClasses;

namespace ME3LibWV
{
    public static class DXHelper
    {
        public static Device device = null;
        public static Material Mat;
        public static Texture DefaultTex;
        public static Texture SelectTex;
        public static PresentParameters presentParams = new PresentParameters();
        public static CustomVertex.PositionColored[] lines;
        public static float CamDistance;
        public static float speed = 1.0f;
        public static bool init = false;
        public static bool error = false;
        public static Level level;
        public static Vector3 cam, dir;
        public static float camdist;

        public static void Init(PictureBox handle)
        {
            try
            {
                presentParams = new PresentParameters();
                presentParams.Windowed = true;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D24S8;
                presentParams.SwapEffect = SwapEffect.Discard;
                device = new Device(0, DeviceType.Hardware, handle, CreateFlags.SoftwareVertexProcessing, presentParams);
                if (device == null)
                    return;
                SetupEnvironment();                
                CreateCoordLines();
                CamDistance = 4;
                init = true;
                cam = new Vector3(0, 0, 0);
                dir = new Vector3(1, 1, 1);
                camdist = 3f;
                DebugLog.PrintLn(presentParams.ToString());
                DebugLog.PrintLn(device.DeviceCaps.ToString());
                DebugLog.PrintLn("DirectX Init succeeded...");
            }
            catch (DirectXException ex)
            {
                string s = "DirectX Init error:"
                                + "\n\n" + ex.ToString()
                                + "\n\n" + presentParams.ToString();
                if (device != null)
                    s += "\n\n" + device.DeviceCaps.ToString();
                DebugLog.PrintLn(s);
                error = true;
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
        public static void SetupEnvironment()
        {
            Mat = new Material();
            Mat.Diffuse = Color.White;
            Mat.Ambient = Color.LightGray;
            device.SetTextureStageState(0, TextureStageStates.TextureCoordinateIndex, 0);
            device.SetTextureStageState(0, TextureStageStates.ColorOperation, (int)TextureOperation.Modulate);
            device.SetTextureStageState(0, TextureStageStates.ColorArgument1, (int)TextureArgument.TextureColor);
            device.SetTextureStageState(0, TextureStageStates.ColorArgument2, (int)TextureArgument.Diffuse);
            device.SetTextureStageState(0, TextureStageStates.AlphaOperation, (int)TextureOperation.Disable);
            device.SetTextureStageState(1, TextureStageStates.TextureCoordinateIndex, 0);
            device.SetTextureStageState(1, TextureStageStates.ColorOperation, (int)TextureOperation.Disable);
            device.SetTextureStageState(1, TextureStageStates.AlphaOperation, (int)TextureOperation.Disable);
            DefaultTex = TextureLoader.FromStream(device, new MemoryStream(ME3LibWV.Properties.Resources.Default));
            SelectTex = TextureLoader.FromStream(device, new MemoryStream(ME3LibWV.Properties.Resources.Select));
        }
        public static void Setup()
        {
            device.RenderState.ShadeMode = ShadeMode.Phong;
            device.Lights[0].Type = LightType.Directional;
            device.Lights[0].Diffuse = Color.White;
            device.Lights[0].Range = 1000;
            device.Lights[0].Position = cam;
            Vector3 t = DXHelper.dir;
            t.Z = 1;
            t.Normalize();
            device.Lights[0].Direction =  t;
            device.Lights[0].Attenuation0 = 0.01f;
            device.Lights[0].Attenuation1 = 0.0125f;
            device.Lights[0].Enabled = true;
            device.Material = Mat;
            device.RenderState.CullMode = Cull.None;
            device.RenderState.ZBufferEnable = true;
            device.RenderState.Ambient = Color.LightGray;
            device.SamplerState[0].MinFilter = TextureFilter.Anisotropic;
            device.SamplerState[0].MagFilter = TextureFilter.Anisotropic;
            device.RenderState.ZBufferFunction = Compare.LessEqual;
            device.Clear(ClearFlags.Target, System.Drawing.Color.White, 1.0f, 0);
            device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Black, 1.0f, 0);
            device.Transform.World = Matrix.Identity;
            device.Transform.View = Matrix.LookAtLH(cam - dir * camdist, cam, new Vector3(0.0f, 0.0f, 1.0f));
            device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 2, 1.0f, 1.0f, 100000.0f);
            device.Material = Mat;
        }
        public static void Render()
        {
            if (error || device == null)
                return;
            try
            {
                Setup();
                device.BeginScene();
                RenderCoordsystem();
                if (level != null)
                    level.Render(device);
                device.EndScene();
                device.Present();
            }
            catch (Exception)
            {
                error = true;
            }
        }
        public static void RenderCoordsystem()
        {
            device.RenderState.FillMode = FillMode.WireFrame;
            device.VertexFormat = CustomVertex.PositionColored.Format;
            device.RenderState.Ambient = Color.Black;
            device.DrawUserPrimitives(PrimitiveType.LineList, 3, lines);
            device.RenderState.Ambient = Color.LightGray;
        }
        public static void Process3DClick(Point p)
        {
            Vector3 source = cam - dir * camdist;
            Vector3 near = Vector3.Unproject(new Vector3(p.X, p.Y, 0), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 far = Vector3.Unproject(new Vector3(p.X, p.Y, 1), device.Viewport, device.Transform.Projection, device.Transform.View, Matrix.Identity);
            Vector3 dirout = far - near;
            dirout.Normalize();            
            if (level != null)
                level.Process3DClick(source, dirout);
            //lines[0].Position = source;
            //lines[1].Position = source + new Vector3(0, 0, 10);
            //lines[2].Position = source;
            //lines[3].Position = source + dirout * 10000;
            //lines[4].Position = source;
            //lines[5].Position = source;
        }

        public static Vector3 RotatorToDX(Vector3 v)
        {
            Vector3 r = v;
            r.X = (int)r.X % 65536;
            r.Y = (int)r.Y % 65536;
            r.Z = (int)r.Z % 65536;
            float f = (3.1415f * 2f) / 65536f;
            r.X = v.Z * f;//z
            r.Y = v.X * f;//x
            r.Z = v.Y * f;//y
            return r;
        }
        public static Vector3 DxToRotator(Vector3 v)
        {
            Vector3 r = new Vector3();
            float f = 65536f / (3.1415f * 2f);
            r.X = -v.X * f;
            r.Y = v.Z * f;
            r.Z = -v.Y * f;
            r.X = (int)r.X % 65536;
            r.Y = (int)r.Y % 65536;
            r.Z = (int)r.Z % 65536;
            return r;
        }
        public static byte[] Vector3ToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            m.Write(BitConverter.GetBytes(v.X), 0, 4);
            m.Write(BitConverter.GetBytes(v.Y), 0, 4);
            m.Write(BitConverter.GetBytes(v.Z), 0, 4);
            return m.ToArray();
        }
        public static byte[] RotatorToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            m.Write(BitConverter.GetBytes((int)v.X), 0, 4);
            m.Write(BitConverter.GetBytes((int)v.Y), 0, 4);
            m.Write(BitConverter.GetBytes((int)v.Z), 0, 4);
            return m.ToArray();
        }
        public static float HalfToFloat(UInt16 val)
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
        public static UInt16 FloatToHalf(float f)
        {
            byte[] bytes = BitConverter.GetBytes((double)f);
            ulong bits = BitConverter.ToUInt64(bytes, 0);
            ulong exponent = bits & 0x7ff0000000000000L;
            ulong mantissa = bits & 0x000fffffffffffffL;
            ulong sign = bits & 0x8000000000000000L;
            int placement = (int)((exponent >> 52) - 1023);
            if (placement > 15 || placement < -14)
                return 0;
            UInt16 exponentBits = (UInt16)((15 + placement) << 10);
            UInt16 mantissaBits = (UInt16)(mantissa >> 42);
            UInt16 signBits = (UInt16)(sign >> 48);
            return (UInt16)(exponentBits | mantissaBits | signBits);
        }
        public static bool RayIntersectTriangle(Vector3 rayPosition, Vector3 rayDirection, Vector3 tri0, Vector3 tri1, Vector3 tri2, out float pickDistance)
        {
            pickDistance = -1f;
            Vector3 edge1 = tri1 - tri0;
            Vector3 edge2 = tri2 - tri0;
            Vector3 pvec = Vector3.Cross(rayDirection, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (det < 0.0001f)
                return false;
            Vector3 tvec = rayPosition - tri0;
            float barycentricU = Vector3.Dot(tvec, pvec);
            if (barycentricU < 0.0f || barycentricU > det)
                return false;
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float barycentricV = Vector3.Dot(rayDirection, qvec);
            if (barycentricV < 0.0f || barycentricU + barycentricV > det)
                return false;
            pickDistance = Vector3.Dot(edge2, qvec);
            float fInvDet = 1.0f / det;
            pickDistance *= fInvDet;
            return true;
        }

    }
}
