using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class StaticMeshActor : _DXRenderableObject
    {
        public Matrix MyMatrix;
        public float DrawScale = 1.0f;
        public Vector3 DrawScale3D = new Vector3(1, 1, 1);
        public Vector3 Rotator = new Vector3(0, 0, 0);
        public Vector3 location = new Vector3(0, 0, 0);
        public int idxSTM = 0;
        public StaticMeshComponent STMC;
        public StaticMeshActor(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
            byte[]buff = pcc.GetObjectData(index);
            Props = PropertyReader.getPropList(pcc, buff);
            foreach (PropertyReader.Property p in Props)
            {
                string s = pcc.GetName(p.Name);
                switch (s)
                {
                    case "StaticMeshComponent":
                        idxSTM = p.Value.IntValue;
                        break;
                    case "DrawScale":
                        DrawScale = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
                        break;
                    case "DrawScale3D":
                        DrawScale3D = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    case "Rotation":
                        Rotator = new Vector3(BitConverter.ToInt32(p.raw, p.raw.Length - 12),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 8),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 4));
                        break;
                    case "location":
                        location = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    default:
                        break;
                }
            }
            MyMatrix = Matrix.Identity;
            MyMatrix *= Matrix.Scaling(DrawScale3D);
            MyMatrix *= Matrix.Scaling(new Vector3(DrawScale, DrawScale, DrawScale));
            Vector3 rot = DXHelper.RotatorToDX(Rotator);
            MyMatrix *= Matrix.RotationYawPitchRoll(rot.X, rot.Y, rot.Z);
            MyMatrix *= Matrix.Translation(location);
            if (idxSTM != 0)
                STMC = new StaticMeshComponent(pcc, idxSTM - 1, MyMatrix);
        }

        public override void Render(Device device)
        {
            if (STMC != null)
                STMC.Render(device);
        }

        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            if (STMC != null)
                t.Nodes.Add(STMC.ToTree());
            return t;
        }

        public override void SetSelection(bool s)
        {
            Selected = s;
            if (STMC != null)
                STMC.SetSelection(s);
        }

        public override float Process3DClick(Vector3 org, Vector3 dir, float max)
        {
            if (STMC != null)
                return STMC.Process3DClick(org, dir, max);
            return -1;
        }
    }
}
