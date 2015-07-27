using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class StaticMeshComponent : _DXRenderableObject
    {
        public Matrix MyMatrix;
        public Matrix ParentMatrix;
        public float Scale = 1.0f;
        public Vector3 Scale3D = new Vector3(1, 1, 1);
        public Vector3 Rotation = new Vector3(0, 0, 0);
        public Vector3 Translation = new Vector3(0, 0, 0);
        public int idxSTM;
        public StaticMesh STM;

        public StaticMeshComponent(PCCPackage Pcc, int index, Matrix transform)
        {
            pcc = Pcc;
            MyIndex = index;
            byte[] buff = pcc.GetObjectData(index);
            Props = PropertyReader.getPropList(pcc, buff);
            ParentMatrix = transform;
            foreach (PropertyReader.Property p in Props)
            {
                string s = pcc.GetName(p.Name);
                switch (s)
                {
                    case "StaticMesh":
                        idxSTM = p.Value.IntValue;
                        break;
                    case "Scale":
                        Scale = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
                        break;
                    case "Scale3D":
                        Scale3D = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    case "Rotation":
                        Rotation = new Vector3(BitConverter.ToInt32(p.raw, p.raw.Length - 12),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 8),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 4));
                        break;
                    case "Translation":
                        Translation = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    default:
                        break;
                }
            }
            MyMatrix = Matrix.Identity;
            MyMatrix *= Matrix.Scaling(Scale3D);
            MyMatrix *= Matrix.Scaling(new Vector3(Scale, Scale, Scale));
            Vector3 rot = DXHelper.RotatorToDX(Rotation);
            MyMatrix *= Matrix.RotationYawPitchRoll(rot.X, rot.Y, rot.Z);
            MyMatrix *= Matrix.Translation(Translation);
            Matrix t = MyMatrix * ParentMatrix;
            if (idxSTM > 0 && !pcc.GetObject(idxSTM).ToLower().Contains("volumetric") && !pcc.GetObject(idxSTM).ToLower().Contains("spheremesh"))
                STM = new StaticMesh(pcc, idxSTM - 1, t);
        }

        public override void Render(Device device)
        {
            if (STM != null)
                STM.Render(device);
        }

        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            if (STM != null)
                t.Nodes.Add(STM.ToTree());
            return t;
        }

        public override void SetSelection(bool s)
        {
            Selected = s;
            if (STM != null)
                STM.SetSelection(s);
        }

        public override float Process3DClick(Vector3 org, Vector3 dir, float max)
        {
            if (STM != null)
                return STM.Process3DClick(org, dir, max);
            return -1;
        }
    }
}
