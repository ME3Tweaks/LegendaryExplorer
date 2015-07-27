using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class Level
    {
        public List<int> Objects;
        public List<_DXRenderableObject> RenderObjects;
        public int MyIndex;
        public PCCPackage pcc;
        public List<PropertyReader.Property> Props;
        public Level(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
            byte[] buff = pcc.GetObjectData(index);
            Props = PropertyReader.getPropList(pcc, buff);
            int off = Props[Props.Count() - 1].offend + 4;
            int count = BitConverter.ToInt32(buff, off);
            Objects = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int idx = BitConverter.ToInt32(buff, off + 4 + i * 4);
                Objects.Add(idx);
            }
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].idxLink - 1 == index)
                {
                    bool found = false;
                    foreach (int j in Objects)
                        if (j == i + 1)
                            found = true;
                    if (!found)
                        Objects.Add(i + 1);
                }
            RenderObjects = new List<_DXRenderableObject>();
            foreach (int i in Objects)
                if (i > 0)
                {
                    string c = pcc.GetObject(pcc.Exports[i - 1].idxClass);
                    switch (c)
                    {
                        case "ModelComponent":
                            RenderObjects.Add(new ModelComponent(pcc, i - 1));
                            break;
                        case "StaticMeshActor":
                        case "InterpActor":
                            RenderObjects.Add(new StaticMeshActor(pcc, i - 1));
                            break;
                        case "StaticMeshCollectionActor":
                            RenderObjects.Add(new StaticMeshCollectionActor(pcc, i - 1));
                            break;
                        default: break;
                    }
                }
        }

        public void Render(Device device)
        {
            foreach (_DXRenderableObject o in RenderObjects)
                o.Render(device);
        }

        public TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : Level");
            t.Name = (MyIndex + 1).ToString();
            foreach (int i in Objects)
                if (i > 0)
                {
                    string c = pcc.GetObject(pcc.Exports[i - 1].idxClass);
                    bool found = false;
                    foreach (_DXRenderableObject m in RenderObjects)
                        if (m.MyIndex == i - 1)
                        {
                            t.Nodes.Add(m.ToTree());
                            found = true;
                            break;
                        }
                    if (!found)
                    {
                        TreeNode t2 = new TreeNode("E#" + (i - 1).ToString("d6") + " : " + pcc.GetObject(i));
                        t2.Name = i.ToString();
                        t.Nodes.Add(t2);
                    }
                }
                else if (i < 0)
                {
                    TreeNode t2 = new TreeNode("I#" + (-i - 1).ToString("d6") + " : " + pcc.GetObject(i));
                    t2.Name = i.ToString();
                    t.Nodes.Add(t2);
                }
                else
                {
                    TreeNode t2 = new TreeNode("#000000 : \"this\"");
                    t2.Name = i.ToString();
                    t.Nodes.Add(t2);
                }
            t.Expand();
            return t;
        }

        public  void Process3DClick(Vector3 org, Vector3 dir)
        {
            float dist = -1;
            DeSelectAll();
            foreach (_DXRenderableObject o in RenderObjects)
            {
                float d = o.Process3DClick(org, dir, dist);
                if ((d < dist && d > 0) || (dist == -1 && d > 0))
                    dist = d;
            }
        }
        public void DeSelectAll()
        {
            foreach (_DXRenderableObject o in RenderObjects)
                o.SetSelection(false);
        }
    }
}
