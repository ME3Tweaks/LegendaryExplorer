using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class UPropertyReader
    {
        public struct Property
        {
            public string name;
            public string value;
            public byte[] raw;
            public int offset;
            public List<PropertyMeta> Meta;

        }
        public struct PropertyMeta
        {
            public int size;
            public int type;
        }

        public struct ClassDefinition
        {
            public string name;
            public List<Property> props;
        }

        private List<Property> tempProps;
        private byte[] memory;
        private int memsize;
        private string tempClass;
        private string[] Names;

        public List<ClassDefinition> Definitions;

        public UPropertyReader()
        {
            Definitions = new List<ClassDefinition>();
        }

        public void AddClass(string name)
        {            
            for (int i = 0; i < Definitions.Count; i++)
                if (Definitions[i].name == name)
                    return;
            ClassDefinition t = new ClassDefinition();
            t.name = name;
            t.props = new List<Property>();
            Definitions.Add(t);
        }

        public void AddProp(string nameclass, Property p)
        {
            int n = -1;
            for (int i = 0; i < Definitions.Count; i++)
                if (Definitions[i].name == nameclass)
                    n=i;
            if (n == -1)
                return;
            Definitions[n].props.Add(p);
        }

        public TreeNode ExportDefinitions()
        {
            TreeNode exp = new TreeNode("Root");
            for (int i = 0; i < Definitions.Count; i++)
            {
                TreeNode t = new TreeNode(Definitions[i].name);
                for (int j = 0; j < Definitions[i].props.Count; j++)
                {
                    TreeNode t2 = new TreeNode(Definitions[i].props[j].name);
                    for (int k = 0; k < Definitions[i].props[j].Meta.Count; k++)
                    {
                        t2.Nodes.Add(new TreeNode(Definitions[i].props[j].Meta[k].size.ToString()));
                        t2.Nodes.Add(new TreeNode(Definitions[i].props[j].Meta[k].type.ToString()));
                    }
                    t.Nodes.Add(t2);
                }
                exp.Nodes.Add(t);
            }
            return exp;
        }

        public void ExportDefinitionsXML(string path)
        {
            TreeNode exp = ExportDefinitions();
            TreeViewSerializer ts = new TreeViewSerializer();
            TreeView v = new TreeView();
            v.Nodes.Add(exp);
            ts.SerializeTreeView(v, path);
        }
        
        public void ImportDefinitions(TreeNode Root)
        {
            int count = Root.Nodes.Count;
            Definitions = new List<ClassDefinition>();
            for (int i = 0; i < count; i++)
            {
                TreeNode n1 = Root.Nodes[i];
                ClassDefinition t = new ClassDefinition();
                t.name = n1.Text;
                t.props = new List<Property>();
                for (int j = 0; j < n1.Nodes.Count; j++)
                {
                    Property p = new Property();
                    TreeNode n2 = n1.Nodes[j];
                    p.name = n2.Text;
                    p.Meta = new List<PropertyMeta>();
                    for (int k = 0; k < n2.Nodes.Count / 2 ; k++)
                    {
                        TreeNode s1 = n2.Nodes[k * 2];
                        TreeNode t1 = n2.Nodes[k * 2 + 1];
                        PropertyMeta m = new PropertyMeta();
                        m.size = Convert.ToInt32(s1.Text);
                        m.type = Convert.ToInt32(t1.Text);
                        p.Meta.Add(m);
                    }
                    t.props.Add(p);
                }
                Definitions.Add(t);
            }
            bool run=true;
            while (run)
            {
                run = false;
                for (int i = 0; i < Definitions.Count - 1; i++)
                    if (String.Compare(Definitions[i].name, Definitions[i + 1].name) > 0)
                    {
                        run = true;
                        ClassDefinition t = Definitions[i];
                        Definitions[i] = Definitions[i + 1];
                        Definitions[i + 1] = t;
                    }
                for (int i = 0; i < Definitions.Count - 1; i++)
                    for(int j=0;j<Definitions[i].props.Count-1;j++)
                        if (String.Compare(Definitions[i].props[j].name, Definitions[i].props[j + 1].name) > 0)
                        {
                            run=true;
                            Property p = Definitions[i].props[j];
                            Definitions[i].props[j] = Definitions[i].props[j + 1];
                            Definitions[i].props[j + 1] = p;
                        }

            }
        }

        public void ImportDefinitionsXML(string path)
        {
            TreeViewSerializer ts = new TreeViewSerializer();
            TreeView v = new TreeView();
            ts.LoadXmlFileInTreeView(v, path);
            TreeNode t = v.Nodes[0];
            TreeNode t2 = t.Nodes[0];
            TreeNode Root = t2.Nodes[0].Clone() as TreeNode;
            ImportDefinitions(Root);
        }

        public List<Property> readProperties(byte[] buff, string cls, string[] names)
        {
            tempProps = new List<Property>();
            memory = buff;
            memsize = buff.Length;
            Names = names;
            tempClass = cls;
            Guess(4, true);
            if(tempProps.Count == 0)
                Guess(8, true);
            return tempProps;
        }

        public List<Property> readProperties(byte[] buff, string cls, string[] names,int start)
        {
            tempProps = new List<Property>();
            memory = buff;
            memsize = buff.Length;
            Names = names;
            tempClass = cls;
            Guess(start, true);
            return tempProps;
        }

        private string clr0(string input)
        {
            if (input[input.Length - 1] == '\0')
                return input.Substring(0, input.Length - 1);
            else
                return input;
        }

        public float getFloat16(int pos)
        {
            UInt16 u = BitConverter.ToUInt16(memory, pos);
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }

        private void Guess(int off, bool auto = false)
        {
            if (off + 4 >= memsize)
                return;
            uint n = BitConverter.ToUInt32(memory, off);
            if (n >= Names.Length)
                return;
            string s = Names[n];
            for (int i = 0; i < Definitions.Count; i++)
                if (tempClass == Definitions[i].name)
                    for (int j = 0; j < Definitions[i].props.Count; j++)
                    {
                        if (Definitions[i].props[j].name + "\0" == s)
                        {
                            int pos = off;
                            Property p = Definitions[i].props[j];
                            for (int k = 0; k < Definitions[i].props[j].Meta.Count; k++)
                            {
                                UPropertyReader.PropertyMeta m = Definitions[i].props[j].Meta[k];

                                if (m.type == 0)
                                    switch (m.size)
                                    {
                                        case 1:
                                            p.value += memory[pos].ToString() + " ";
                                            break;
                                        case 2:
                                            p.value += BitConverter.ToUInt16(memory, pos).ToString() + " ";
                                            break;
                                        case 4:
                                            p.value += BitConverter.ToUInt32(memory, pos).ToString() + " ";
                                            break;
                                    }
                                if (m.type == 1)
                                    switch (m.size)
                                    {                                        
                                        case 2:
                                            p.value += getFloat16(pos).ToString() + " ";
                                            break;
                                        case 4:
                                            p.value += BitConverter.ToSingle(memory, pos).ToString() + " ";
                                            break;
                                        default:
                                            p.value += "";
                                            break;
                                    }
                                   
                                if (m.type == 2)
                                {
                                    uint z = BitConverter.ToUInt32(memory, pos);
                                    if (z >= 0 && z < Names.Count() && k != 0)
                                        p.value += clr0(Names[z]) + " ";
                                }
                                pos += m.size;
                            }
                            p.raw = new byte[pos - off];
                            p.offset = off;
                            for (int k = 0; k < pos - off; k++)
                                p.raw[k] = memory[off + k];
                            tempProps.Add(p);
                            if (auto)
                            {
                                Guess(pos, auto);
                                return;
                            }
                        }
                        if (s == "ArrayProperty\0")
                        {
                            int pos = off;
                            int len = BitConverter.ToInt32(memory, pos + 8);
                            int count = BitConverter.ToInt32(memory, pos + 16);
                            int size;
                            if (count == 0)
                                size = len;
                            else
                                size = (len - 4) / count;
                            Property p = new Property();
                            p.offset = pos;
                            pos += 20;
                            p.name = s;
                            for(int k=0;k<count;k++)
                            {
                                switch(size)
                                {
                                    case 1:
                                        p.value += memory[pos].ToString() + " ";
                                        pos += 4;
                                        break;
                                    case 2:
                                        p.value += BitConverter.ToUInt16(memory, pos).ToString() + " ";
                                        pos += 4;
                                        break;
                                    case 4:
                                        p.value += BitConverter.ToUInt32(memory, pos).ToString() + " ";
                                        pos += 4;
                                        break;
                                    default :
                                        p.value = "";
                                        pos += size;
                                        break;
                                }
                            }
                            p.raw = new byte[pos - off];
                            for (int k = 0; k < pos - off; k++)
                                p.raw[k] = memory[off + k];
                            tempProps.Add(p);
                            if (auto)
                            {
                                Guess(pos, auto);
                                return;
                            }
                        }
                        if (s == "StrProperty\0")
                        {
                            int pos = off;
                            int len = BitConverter.ToInt32(memory, pos + 16)*-1;
                            Property p = new Property();
                            p.offset = pos;
                            pos += 20;
                            p.name = s;
                            p.value = "";
                            for (int k = 0; k < len; k++)
                            {
                                p.value += (char)memory[pos];
                                pos += 2;
                            }
                            p.raw = new byte[pos - off];
                            for (int k = 0; k < pos - off; k++)
                                p.raw[k] = memory[off + k];
                            tempProps.Add(p);
                            if (auto)
                            {
                                Guess(pos, auto);
                                return;
                            }
                        }
                        if (s == "m_aObjComment\0")
                        {
                            int pos = off;
                            int len = BitConverter.ToInt32(memory, pos + 28) * -1;
                            Property p = new Property();
                            p.offset = pos;
                            pos += 32;
                            p.name = s;
                            p.value = "";
                            for (int k = 0; k < len; k++)
                            {
                                p.value += (char)memory[pos];
                                pos += 2;
                            }
                            p.raw = new byte[pos - off];
                            for (int k = 0; k < pos - off; k++)
                                p.raw[k] = memory[off + k];
                            tempProps.Add(p);
                            if (auto)
                            {
                                Guess(pos, auto);
                                return;
                            }
                        }
                    }

        }

        public int FindProperty(string s,List<Property> p)
        {
            int ret = -1;
            for (int i = 0; i < p.Count; i++)
                if (p[i].name == s)
                    ret = i;
            return ret;
        }

        public int FindObjectProperty(string s, List<Property> p)
        {
            int ret = -1;
            for (int i = 0; i < p.Count; i++)
                if (p[i].name == s)
                    ret = i;
            if (ret != -1)
                ret = PropToInt(p[ret].raw) - 1;
            return ret;
        }

        public int PropToInt(byte[] buff)
        {
            if (buff == null || buff.Length < 4)
                return -1;
            return BitConverter.ToInt32(buff, buff.Length - 4);
        }

        public Vector3 PropToVector3(byte[] buff)
        {
            Vector3 v = new Vector3(0, 0, 0);
            if (buff == null || buff.Length < 12)
                return v;
            v.X = BitConverter.ToSingle(buff, buff.Length - 12);
            v.Y = BitConverter.ToSingle(buff, buff.Length - 8);
            v.Z = BitConverter.ToSingle(buff, buff.Length - 4);
            return v;
        }

        public Vector3 PropToIntVector3(byte[] buff)
        {
            Vector3 v = new Vector3(0, 0, 0);
            if (buff == null || buff.Length < 12)
                return v;
            v.X = BitConverter.ToInt32(buff, buff.Length - 12);
            v.Y = BitConverter.ToInt32(buff, buff.Length - 8);
            v.Z = BitConverter.ToInt32(buff, buff.Length - 4);
            return v;
        }
    }
}
