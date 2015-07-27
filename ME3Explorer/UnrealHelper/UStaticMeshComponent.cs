using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class UStaticMeshComponent
    {
        public byte[] memory;
        public int memsize;
        public string[] names;
        public string name;
        public int index;
        public int index2;
        public int index3;
        public List<UPropertyReader.Property> props;
        public UPropertyReader UPR;
        public ULevel.LevelProperties LP;
        
        public UStaticMeshComponent(byte[] mem, string[] Names)
        {
            memory = mem;
            memsize = mem.Length;
            names = Names;
            props = new List<UPropertyReader.Property>();
        }

        public void Deserialize()
        {
            int pos = 8;
            if (UPR == null)
                return;
            props = UPR.readProperties(memory, "StaticMeshComponent", names, pos);
        }

        public TreeNode ExportToTree()
        {
            TreeNode t = new TreeNode("StaticMeshComponent");
            t.Nodes.Add(PropsToTree());
            return t;
        }

        public TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            for (int i = 0; i < props.Count; i++)
            {
                TreeNode t = new TreeNode(props[i].name);
                TreeNode t2 = new TreeNode(props[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }
    }
}
