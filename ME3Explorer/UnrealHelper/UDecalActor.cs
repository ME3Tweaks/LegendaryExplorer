using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class UDecalActor
    {
        public byte[] memory;
        public int memsize;
        public string[] names;
        public string name;
        public List<UPropertyReader.Property> props;
        public UPropertyReader UPR;
        public ULevel.LevelProperties LP;

        public UDecalActor(byte[] mem, string[] Names)
        {
            memory = mem;
            memsize = mem.Length;
            names = Names;
            props = new List<UPropertyReader.Property>();
        }

        public void Deserialize()
        {
            int pos = 16;
            int test = BitConverter.ToInt32(memory, 0);
            name = names[BitConverter.ToUInt32(memory, pos)];
            if (UPR == null)
                return;
            if (test < 0)
                pos += 14;
            else 
                pos = 4;
            props = UPR.readProperties(memory, "DecalActor", names, pos);
        }

        public TreeNode ExportToTree()
        {
            TreeNode t = new TreeNode("DecalActor");
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
