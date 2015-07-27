using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;


namespace ME3Explorer.UnrealHelper
{
    public class Preview3D
    {
        Device device = null;
        PresentParameters presentParams = new PresentParameters();
        public CustomVertex.PositionNormalTextured[] verts;
        public float CamDistance;
        public Vector3 boxorigin;
        public Vector3 box;
        public bool init;
        public int BodySetup;
        public PCCFile Pcc;

        public Preview3D(CustomVertex.PositionNormalTextured[] Verts,Control handle,int Setup,PCCFile pcc)
        {
            verts = Verts;
            Pcc = pcc;
            if (verts.Length == 0)
                return;
            boxorigin = new Vector3(verts[0].X, verts[0].Y, verts[0].Z); ;
            box = new Vector3(verts[0].X, verts[0].Y, verts[0].Z); ;
            for (int i = 0; i < verts.Length; i++)
            {
                boxorigin.X = Math.Min(boxorigin.X, verts[i].X);
                boxorigin.Y = Math.Min(boxorigin.Y, verts[i].Y);
                boxorigin.Z = Math.Min(boxorigin.Z, verts[i].Z);
                box.X = Math.Max(box.X, verts[i].X);
                box.Y = Math.Max(box.Y, verts[i].Y);
                box.Z = Math.Max(box.Z, verts[i].Z);
            }
            box -= boxorigin;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].X -= boxorigin.X + box.X / 2;
                verts[i].Y -= boxorigin.Y + box.Y / 2;
                verts[i].Z -= boxorigin.Z + box.Z / 2;
            }
            CamDistance = (float)Math.Sqrt(box.X * box.X + box.Y * box.Y + box.Z * box.Z);
            LoadBodySetup(BodySetup);
            init = InitializeGraphics(handle);
        }

        public void LoadBodySetup(int setup)
        {
            if (setup == -1) return;
            BodySetup = setup - 1;
            if (BodySetup < 0 || BodySetup >= Pcc.Header.ExportCount) return;
            byte[] buff = Pcc.EntryToBuff(BodySetup);
            string c = Pcc.getClassName(Pcc.Export[BodySetup].Class);
            UnrealObject UOb = new UnrealObject(buff, c, Pcc.names);
            string l = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(l + "\\exec\\DefaultProp.xml"))
                UOb.UUkn.UPR.ImportDefinitionsXML(l + "\\exec\\DefaultProp.xml");
            List<UPropertyReader.Property> p = UOb.UUkn.Properties;
            for (int i = 0; i < p.Count; i++)
            { }
        }

        public void Refresh()
        {
            if (init) Render();
        }

        public bool InitializeGraphics(Control handle)
        {
            try
            {

                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Discard;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D16;
                device = new Device(0, DeviceType.Hardware, handle, CreateFlags.SoftwareVertexProcessing, presentParams);
                return true;
            }
            catch (DirectXException)
            {
                return false;
            }
        }

        private void Render()
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
                device.Lights[0].Direction = new Vector3(0.8f, 0, 1);
                device.Lights[0].Enabled = true;
                device.RenderState.CullMode = Cull.Clockwise;
                device.RenderState.FillMode = FillMode.WireFrame;
                device.SetRenderState(RenderStates.ZEnable, true);
                device.Clear(ClearFlags.Target, System.Drawing.Color.Blue, 1.0f, 0);
                device.Clear(ClearFlags.ZBuffer, System.Drawing.Color.Blue, 1.0f, 0);
                device.BeginScene();
                int iTime = Environment.TickCount;
                float fAngle = iTime * (2.0f * (float)Math.PI) / 10000.0f;
                device.Transform.World = Matrix.RotationY(fAngle);
                device.Transform.View = Matrix.LookAtLH(new Vector3(0.0f, CamDistance/2, CamDistance), new Vector3(0,0,0), new Vector3(0.0f, 1.0f, 0.0f));
                device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, 1.0f, 1.0f, 100000.0f);
                device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts.Length / 3, verts);
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
