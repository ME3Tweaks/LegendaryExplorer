using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class UWwiseStream
    {
        public byte[] memory;
        public int memsize;
        public string[] names;
        public string name;
        public int offset;
        public int size;
        public List<UPropertyReader.Property> props;
        public UPropertyReader UPR;
        public ULevel.LevelProperties LP;

        public UWwiseStream(byte[] mem, string[] Names)
        {
            memory = mem;
            memsize = mem.Length;
            names = Names;
            props = new List<UPropertyReader.Property>();
        }

        public void Deserialize()
        {
            props = UPR.readProperties(memory, "WwiseStream", names, 4);
            ReadOffsets();
        }

        public void ReadOffsets()
        {
            int off = props[props.Count - 1].offset + props[props.Count - 1].raw.Length;
            off += 8;
            size = BitConverter.ToInt32(memory, off);
            offset = BitConverter.ToInt32(memory, off+4);
        }

        public void ExportToFile()
        {
            int entry = UPR.FindProperty("Filename", props);
            if(entry == -1)
                return;
            int index = BitConverter.ToInt32(props[entry].raw, 24);
            if(index<0 || index >= names.Length)
                return;
            string nm = names[index];
            nm = nm.Substring(0, nm.Length - 1);
            nm += ".afc";
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = nm + "|" + nm;
            if (d.ShowDialog() == DialogResult.OK)
            {
                AFCFile afc = new AFCFile();
                afc.ExtractWav(d.FileName, offset, size);
            }

        }

        public TreeNode ExportToTree()
        {
            TreeNode t = new TreeNode("WwiseStream");
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
