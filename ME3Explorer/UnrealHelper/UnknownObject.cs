using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class UnknownObject
    {
        public string classname;
        public List<UPropertyReader.Property> Properties;
        public UPropertyReader UPR;
        public byte[] memory;
        public int memsize;
        public string[] Names;

        public UnknownObject(byte[] mem, string cls, string[] names)
        {
            memory = mem;
            memsize = mem.Length;
            Names = names;
            classname = cls;
            UPR = new UPropertyReader();
        }

        public void Deserialize()
        {
            Properties = UPR.readProperties(memory, classname, Names);
        }

        public TreeNode getProperties()
        {
            TreeNode r = new TreeNode(classname);
            TreeNode t = new TreeNode("Properties");
            Properties = UPR.readProperties(memory, classname, Names);
            if (Properties.Count == 0)
                return null;
            for (int i = 0; i < Properties.Count; i++)
            {
                if (Properties[i].name != "ArrayProperty\0")
                {
                    TreeNode t2 = new TreeNode(Properties[i].name);
                    TreeNode t3 = new TreeNode(Properties[i].value);
                    t2.Nodes.Add(t3);
                    t.Nodes.Add(t2);
                }
                else
                {
                    TreeNode t2 = new TreeNode(Properties[i].name);
                    TreeNode t3 = new TreeNode(Properties[i].value);
                    t2.Nodes.Add(t3);
                    if (t.Nodes.Count == 0)
                        return null;
                    t.Nodes[t.Nodes.Count - 1].Nodes.Clear();
                    t.Nodes[t.Nodes.Count - 1].Nodes.Add(t2);
                }
            }
            r.Nodes.Add(t);
            return r;
        }
    }
}
