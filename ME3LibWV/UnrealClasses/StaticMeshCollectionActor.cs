using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class StaticMeshCollectionActor : _DXRenderableObject
    {        
        public List<int> Entries;
        public List<Matrix> Matrices;
        public List<StaticMeshComponent> STMC;
        private byte[] data;
        public StaticMeshCollectionActor(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
            data = pcc.GetObjectData(index);
            Props = PropertyReader.getPropList(pcc, data);
            ReadObjects();
            ReadMatrices();
        }
        public void ReadObjects()
        {
            int entry = -1;
            for (int i = 0; i < Props.Count; i++)
                if (pcc.GetName(Props[i].Name) == "StaticMeshComponents")
                    entry = i;
            if (entry == -1)
                return;
            int count = BitConverter.ToInt32(Props[entry].raw, 24);
            Entries = new List<int>();
            for (int i = 0; i < count; i++)
                Entries.Add(BitConverter.ToInt32(Props[entry].raw, i * 4 + 28) - 1);
            STMC = new List<StaticMeshComponent>();
            foreach (int idx in Entries)
                STMC.Add(null);
        }
        public void ReadMatrices()
        {
            int pos = Props[Props.Count - 1].offend;
            Matrices = new List<Matrix>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Matrix m = new Matrix();
                float[,] buff = new float[4, 4];
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        buff[x, y] = BitConverter.ToSingle(data, pos);
                        pos += 4;
                    }
                m.M11 = buff[0, 0];
                m.M12 = buff[1, 0];
                m.M13 = buff[2, 0];
                m.M14 = buff[3, 0];

                m.M21 = buff[0, 1];
                m.M22 = buff[1, 1];
                m.M23 = buff[2, 1];
                m.M24 = buff[3, 1];

                m.M31 = buff[0, 2];
                m.M32 = buff[1, 2];
                m.M33 = buff[2, 2];
                m.M34 = buff[3, 2];

                m.M41 = buff[0, 3];
                m.M42 = buff[1, 3];
                m.M43 = buff[2, 3];
                m.M44 = buff[3, 3];
                Matrices.Add(m);
                int idx = Entries[i];
                if (idx >= 0 && idx < pcc.Exports.Count && pcc.GetObject(pcc.Exports[idx].idxClass) == "StaticMeshComponent")
                    STMC[i] = new StaticMeshComponent(pcc, idx, m);
            }
        }

        public override void Render(Device device)
        {
            foreach (StaticMeshComponent stmc in STMC)
                if (stmc != null && stmc.STM != null)
                    stmc.Render(device);
        }
        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            foreach (StaticMeshComponent stmc in STMC)
                if (stmc != null)
                    t.Nodes.Add(stmc.ToTree());
            return t;
        }
        public override void SetSelection(bool s)
        {
            Selected = s;
            foreach (StaticMeshComponent smc in STMC)
                if (smc != null)
                    smc.SetSelection(s);
        }
        public override float Process3DClick(Vector3 org, Vector3 dir, float max)
        {
            float dist = max;
            foreach(StaticMeshComponent stmc in STMC)
                if (stmc != null)
                {
                    float d = stmc.Process3DClick(org, dir, dist);
                    if ((d < dist && d > 0) || (dist == -1f && d > 0))
                        dist = d;
                }
            return dist;
        }
    }
}
