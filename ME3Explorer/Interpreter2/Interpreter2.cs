//Interpreter2.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;
using System.Diagnostics;

namespace ME3Explorer.Interpreter2
{
    public partial class Interpreter2 : Form
    {
        public PCCObject pcc;
        public int Index;
        public string className;
        public byte[] memory;
        public int memsize;
        public int readerpos;
        private int previousArrayView = -1; //-1 means it has not been previously set
        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }

        public string[] Types =
        {
            "StructProperty", //0
            "IntProperty",
            "FloatProperty",
            "ObjectProperty",
            "NameProperty",
            "BoolProperty",  //5
            "ByteProperty",
            "ArrayProperty",
            "StrProperty",
            "StringRefProperty",
            "DelegateProperty"//10
        };

        public enum nodeType
        {
            Unknown = -1,
            StructProperty = 0,
            IntProperty = 1,
            FloatProperty = 2,
            ObjectProperty = 3,
            NameProperty = 4,
            BoolProperty = 5,
            ByteProperty = 6,
            ArrayProperty = 7,
            StrProperty = 8,
            StringRefProperty = 9,
            DelegateProperty = 10,
            
            ArrayLeafObject,
            ArrayLeafName,
            ArrayLeafEnum,
            ArrayLeafStruct,
            ArrayLeafBool,
            ArrayLeafString,
            ArrayLeafFloat,
            ArrayLeafInt,

            StructLeafByte,
            StructLeafFloat,
            StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
            StructLeafInt,

            Root,
        }
        
        
        private int lastSetOffset = -1; //offset set by program, used for checking if user changed since set 
        private nodeType LAST_SELECTED_PROP_TYPE = nodeType.Unknown; //last property type user selected. Will use to check the current offset for type
        private TreeNode LAST_SELECTED_NODE = null; //last selected tree node

        public Interpreter2()
        {
            InitializeComponent();
        }

        public void InitInterpreter()
        {
            DynamicByteProvider db = new DynamicByteProvider(pcc.Exports[Index].Data);
            hb1.ByteProvider = db;
            memory = pcc.Exports[Index].Data;
            memsize = memory.Length;
            className = pcc.Exports[Index].ClassName;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            StartScan();
        }

        public new void Show()
        {
            base.Show();
            toolStripStatusLabel1.Text = "Class: " + className + ", Export Index: " + Index;
            toolStripStatusLabel2.Text = "@" + Path.GetFileName(pcc.pccFileName);
            StartScan();
        }

        private void StartScan(IEnumerable<string> expandedNodes = null, string topNodeName = null)
        {
            treeView1.BeginUpdate();
            treeView1.Nodes.Clear();
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].ObjectFlags);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
            TreeNode topLevelTree = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            topLevelTree.Tag = nodeType.Root;
            GenerateTree(topLevelTree, topLevelHeaders);
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
            if (expandedNodes != null)
            {
                TreeNode[] nodes;
                foreach (var item in expandedNodes)
                {
                    nodes = treeView1.Nodes.Find(item, true);
                    if(nodes.Length > 0)
                    {
                        foreach (var node in nodes)
                        {
                            node.Expand();
                        }
                    }
                }
            }
            TreeNode[] topNodes = treeView1.Nodes.Find(topNodeName, true);
            if (topNodes.Length > 0)
            {
                treeView1.TopNode = topNodes[0];
            }
            treeView1.EndUpdate();
        }

        public void GenerateTree(TreeNode localRoot, List<PropHeader> headersList)
        {
            foreach (PropHeader header in headersList)
            {
                nodeType type = getType(pcc.getNameEntry(header.type));
                if (type != nodeType.ArrayProperty && type != nodeType.StructProperty)
                    localRoot.Nodes.Add(GenerateNode(header));
                else
                {
                    if (type == nodeType.ArrayProperty)
                    {
                        TreeNode t = GenerateNode(header);
                        int arrayLength = BitConverter.ToInt32(memory, header.offset + 24);
                        readerpos = header.offset + 28;
                        int tmp = readerpos;
                        UnrealObjectInfo.ArrayType arrayType;
                        try
                        {
                            arrayType = UnrealObjectInfo.getArrayType(className, pcc.getNameEntry(header.name));
                        }
                        catch (Exception)
                        {
                            arrayType = UnrealObjectInfo.ArrayType.Int;
                        }
                        if (arrayType == UnrealObjectInfo.ArrayType.Struct)
                        {
                            UnrealObjectInfo.PropertyInfo info = UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(header.name));
                            string arrayLeafType = "";
                            if (info != null)
                            {
                                arrayLeafType = $": ({info.reference})";
                            }
                            for (int i = 0; i < arrayLength; i++)
                            {
                                readerpos = tmp;
                                int pos = tmp;
                                List<PropHeader> arrayListPropHeaders = ReadHeadersTillNone();
                                tmp = readerpos;
                                TreeNode n = new TreeNode(i.ToString() + (arrayListPropHeaders.Count == 0 ? arrayLeafType : ""));
                                n.Tag = nodeType.ArrayLeafStruct;
                                n.Name = pos.ToString();
                                t.Nodes.Add(n);
                                n = t.LastNode;
                                if (arrayListPropHeaders.Count > 0)
                                {
                                    GenerateTree(n, arrayListPropHeaders); 
                                }
                                else
                                {
                                    if (info != null)
                                    {
                                        GenerateSpecialStruct(n, info.reference, header.size / arrayLength);
                                        tmp = readerpos;
                                    }
                                }
                                t.LastNode.Remove();
                                t.Nodes.Add(n);
                            }
                            localRoot.Nodes.Add(t);
                        }
                        else
                        {
                            int count = 0;
                            for (int i = 0; i < (header.size - 4); count++)
                            {
                                int val = BitConverter.ToInt32(memory, header.offset + 28 + i);
                                string s = (header.offset + 28 + i).ToString("X4") + "|" + count + ": ";
                                TreeNode node = new TreeNode();
                                node.Name = (header.offset + 28 + i).ToString();
                                if (arrayType == UnrealObjectInfo.ArrayType.Object)
                                {
                                    node.Tag = nodeType.ArrayLeafObject;
                                    int value = val;
                                    if (value == 0)
                                    {
                                        //invalid
                                        s += "Null [" + value + "] ";
                                    }
                                    else
                                    {

                                        bool isImport = value < 0;
                                        if (isImport)
                                        {
                                            value = -value;
                                        }
                                        value--; //0-indexed
                                        if (isImport)
                                        {
                                            if (pcc.Imports.Count > value)
                                            {
                                                s += pcc.Imports[value].ObjectName + " [IMPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in import list [" + value + "]";
                                            }
                                        }
                                        else
                                        {
                                            if (pcc.Exports.Count > value)
                                            {
                                                s += pcc.Exports[value].ObjectName + " [EXPORT " + value + "]";
                                            }
                                            else
                                            {
                                                s += "Index not in export list [" + value + "]";
                                            }
                                        }
                                    }
                                    i += 4;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Name || arrayType == UnrealObjectInfo.ArrayType.Enum)
                                {

                                    node.Tag = arrayType == UnrealObjectInfo.ArrayType.Name ? nodeType.ArrayLeafName : nodeType.ArrayLeafEnum;
                                    int value = val;
                                    if (value < 0)
                                    {
                                        s += "Invalid Name Index [" + value + "]";
                                    }
                                    else
                                    {
                                        if (pcc.Names.Count > value)
                                        {
                                            s += $"\"{pcc.Names[value]}\"_{BitConverter.ToInt32(memory, header.offset + 28 + i + 4)}[NAMEINDEX {value}]";
                                        }
                                        else
                                        {
                                            s += "Index not in name list [" + value + "]";
                                        }
                                    }
                                    i += 8;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Float)
                                {
                                    node.Tag = nodeType.ArrayLeafFloat;
                                    s += BitConverter.ToSingle(memory, header.offset + 28 + i).ToString("0.0######");
                                    i += 4;
                                }
                                else if (arrayType == UnrealObjectInfo.ArrayType.Bool)
                                {
                                    node.Tag = nodeType.ArrayLeafBool;
                                    s += BitConverter.ToBoolean(memory, header.offset + 28 + i);
                                    i += 1;
                                } 
                                else if (arrayType == UnrealObjectInfo.ArrayType.String)
                                {
                                    node.Tag = nodeType.ArrayLeafString;
                                    int pos = header.offset + 28 + i + 4;
                                    s += "\"";
                                    int len = val > 0 ? val : -val;
                                    for (int j = 1; j < len; j++)
                                    {
                                        s += BitConverter.ToChar(memory, pos);
                                        pos += 2;
                                    }
                                    s += "\"";
                                    i += (len * 2) + 4;
                                }
                                else
                                {
                                    node.Tag = nodeType.ArrayLeafInt;
                                    s += val.ToString();
                                    i += 4;
                                }
                                node.Text = s;
                                t.Nodes.Add(node);
                            }
                            localRoot.Nodes.Add(t);
                        }
                    }
                    if (type == nodeType.StructProperty)
                    {
                        TreeNode t = GenerateNode(header);
                        readerpos = header.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            GenerateTree(t, ll);
                        }
                        else
                        {
                            string structType = pcc.getNameEntry(BitConverter.ToInt32(memory, header.offset + 24));
                            GenerateSpecialStruct(t, structType, header.size);
                        }
                        localRoot.Nodes.Add(t);
                    }

                }
            }
        }

        //structs that are serialized down to just their values.
        //TODO: write a general deserializer for these instead of a bunch of bespoke ones that don't even cover all the cases.
        private void GenerateSpecialStruct(TreeNode t, string structType, int size)
        {
            TreeNode node;
            int pos = readerpos;
            if (structType == "Vector2d" || structType == "RwVector2")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "Vector" || structType == "RwVector3")
            {
                string[] labels = { "X", "Y", "Z" };
                for (int i = 0; i < 3; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                int val;
                for (int i = 0; i < 3; i++)
                {
                    val = BitConverter.ToInt32(memory, pos);
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + val + " (" + ((float)val * 360f / 65536f).ToString("0.0######") + " degrees)");
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafDeg;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "Color")
            {
                string[] labels = { "B", "G", "R", "A" };
                for (int i = 0; i < 4; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + memory[pos]);
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafByte;
                    t.Nodes.Add(node);
                    pos += 1;
                }
            }
            else if (structType == "LinearColor")
            {
                string[] labels = { "R", "G", "B", "A" };
                for (int i = 0; i < 4; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            //uses EndsWith to support RwQuat, RwVector4, and RwPlane
            else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
            {
                string[] labels = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 4; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "TwoVectors")
            {
                string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                for (int i = 0; i < 6; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafFloat;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "Matrix" || structType == "RwMatrix44")
            {
                string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
                string[] labels2 = { "X", "Y", "Z", "W" };
                TreeNode node2;
                for (int i = 0; i < 3; i++)
                {
                    node2 = new TreeNode(labels[i]);
                    node2.Name = pos.ToString();
                    for (int j = 0; j < 4; j++)
                    {
                        node = new TreeNode(pos.ToString("X4") + " : " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        node2.Nodes.Add(node);
                        pos += 4;
                    }
                    t.Nodes.Add(node2);
                }
            }
            else if (structType == "Guid")
            {
                string[] labels = { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "IntPoint")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    node = new TreeNode(pos.ToString("X4") + " : " + labels[i] + " : " + BitConverter.ToInt32(memory, pos));
                    node.Name = pos.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    t.Nodes.Add(node);
                    pos += 4;
                }
            }
            else if (structType == "Box" || structType == "BioRwBox")
            {
                string[] labels = { "Min", "Max" };
                string[] labels2 = { "X", "Y", "Z" };
                TreeNode node2;
                for (int i = 0; i < 2; i++)
                {
                    node2 = new TreeNode(labels[i]);
                    node2.Name = pos.ToString();
                    for (int j = 0; j < 3; j++)
                    {
                        node = new TreeNode(pos.ToString("X4") + " : " + labels2[j] + " : " + BitConverter.ToSingle(memory, pos).ToString("0.0######"));
                        node.Name = pos.ToString();
                        node.Tag = nodeType.StructLeafFloat;
                        node2.Nodes.Add(node);
                        pos += 4;
                    }
                    t.Nodes.Add(node2);
                }
                node = new TreeNode(pos.ToString("X4") + " : IsValid : " + memory[pos]);
                node.Name = pos.ToString();
                node.Tag = nodeType.StructLeafByte;
                t.Nodes.Add(node);
                pos += 1;
            }
            else
            {
                for (int i = 0; i < size / 4; i++)
                {
                    int val = BitConverter.ToInt32(memory, pos);
                    string s = pos.ToString("X4") + " : " + val.ToString();
                    t.Nodes.Add(s);
                    pos += 4;
                }
            }
            readerpos = pos;
        }

        public TreeNode GenerateNode(PropHeader p)
        {
            string s = p.offset.ToString("X4") + " : ";
            s += "Name: \"" + pcc.getNameEntry(p.name) + "\" ";
            s += "Type: \"" + pcc.getNameEntry(p.type) + "\" ";
            s += "Size: " + p.size.ToString() + " Value: ";
            nodeType propertyType = getType(pcc.getNameEntry(p.type));
            int idx;
            byte val;
            switch (propertyType)
            {
                case nodeType.IntProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case nodeType.ObjectProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() +  " (" + pcc.getObjectName(idx) + ")";
                    break;
                case nodeType.StrProperty:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count * -1 - 1; i++)
                        s += (char)memory[p.offset + 28 + i * 2];
                    s += "\"";
                    break;
                case nodeType.BoolProperty:
                    val = memory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case nodeType.FloatProperty:
                    float f = BitConverter.ToSingle(memory, p.offset + 24);
                    s += f.ToString() + "f";
                    break;
                case nodeType.NameProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"_" + BitConverter.ToInt32(memory, p.offset + 28);
                    break;
                case nodeType.StructProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"";
                    break;
                case nodeType.ByteProperty:
                    if(p.size == 1)
                    {
                        val = memory[p.offset + 32];
                        s += val.ToString();
                    }
                    else
	                {
                        idx = BitConverter.ToInt32(memory, p.offset + 24);
                        int idx2 = BitConverter.ToInt32(memory, p.offset + 32);
                        s += "\"" + pcc.getNameEntry(idx) + "\",\"" + pcc.getNameEntry(idx2) + "\""; 
                    }
                    break;
                case nodeType.ArrayProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case nodeType.StringRefProperty:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx.ToString() + ": ";
                    s += TalkFiles.tlkList.Count == 0 ? "(.tlk not loaded)" : TalkFiles.findDataById(idx);
                    break;
            }
            TreeNode ret = new TreeNode(s);
            ret.Tag = propertyType;
            ret.Name = p.offset.ToString();
            return ret;
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

        public List<PropHeader> ReadHeadersTillNone()
        {
            List<PropHeader> ret = new List<PropHeader>();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                if (readerpos > memory.Length)
                {
                    //nothing else to interpret.
                    run = false;
                    continue; 
                }
                p.name = BitConverter.ToInt32(memory, readerpos);
                if (!pcc.isName(p.name))
                    run = false;
                else
                {
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        if (!pcc.isName(p.type) || getType(pcc.getNameEntry(p.type)) == nodeType.Unknown)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;
                            if (getType(pcc.getNameEntry(p.type)) == nodeType.BoolProperty)//Boolbyte
                                readerpos++;
                            if (getType(pcc.getNameEntry(p.type)) == nodeType.StructProperty ||//StructName
                                getType(pcc.getNameEntry(p.type)) == nodeType.ByteProperty)//byteprop
                                readerpos += 8;
                        }
                    }
                    else
                    {
                        p.type = p.name;
                        p.size = 0;
                        p.index = 0;
                        p.offset = readerpos;
                        ret.Add(p);
                        readerpos += 8;
                        run = false;
                    }
                }
            }
            return ret;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.txt|*.txt";
            d.FileName = pcc.Exports[Index].ObjectName + ".txt";
            if (d.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                PrintNodes(treeView1.Nodes, fs, 0);
                fs.Close();
                MessageBox.Show("Done.");
            }
        }

        public void PrintNodes(TreeNodeCollection t, FileStream fs, int depth)
        {
            string tab = "";
            for (int i = 0; i < depth; i++)
                tab += ' ';
            foreach (TreeNode t1 in t)
            {
                string s = tab + t1.Text;
                WriteString(fs, s);
                fs.WriteByte(0xD);
                fs.WriteByte(0xA);
                if (t1.Nodes.Count != 0)
                    PrintNodes(t1.Nodes, fs, depth + 4);
            }
        }

        public void WriteString(FileStream fs, string s)
        {
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        private string getEnclosingType(TreeNode node)
        {
            Stack<TreeNode> nodeStack = new Stack<TreeNode>();
            string typeName = className;
            string propname;
            uint throwaway;
            UnrealObjectInfo.PropertyInfo p;
            while (node != null && !node.Tag.Equals(nodeType.Root))
            {
                nodeStack.Push(node);
                node = node.Parent;
            }
            bool isStruct = false;
            while(nodeStack.Count > 0)
            {
                node = nodeStack.Pop();
                if (uint.TryParse(node.Text, out throwaway))
                {
                    continue;
                }
                propname = pcc.getNameEntry(BitConverter.ToInt32(memory, Convert.ToInt32(node.Name)));
                p = UnrealObjectInfo.getPropertyInfo(typeName, propname, isStruct);
                typeName = p.reference;
                isStruct = true;
            }
            return typeName;
        }

        private bool isArrayLeaf(nodeType type)
        {
            return (type == nodeType.ArrayLeafBool || type == nodeType.ArrayLeafEnum || type == nodeType.ArrayLeafFloat ||
                type == nodeType.ArrayLeafInt || type == nodeType.ArrayLeafName || type == nodeType.ArrayLeafObject ||
                type == nodeType.ArrayLeafString || type == nodeType.ArrayLeafStruct);
        }

        private bool isStructLeaf(nodeType type)
        {
            return (type == nodeType.StructLeafByte || type == nodeType.StructLeafDeg || type == nodeType.StructLeafFloat);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            LAST_SELECTED_NODE = e.Node;
            nameEntry.Visible = proptext.Visible = setPropertyButton.Visible = enumDropdown.Visible = false;
            addArrayElementButton.Visible = deleteArrayElement.Visible = false;
            if (e.Node.Name == "")
            {
                Debug.WriteLine("This node is not parsable.");
                //can't attempt to parse this.
                LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                return;
            }
            try
            {
                int off = Convert.ToInt32(e.Node.Name);
                hb1.SelectionStart = off;
                lastSetOffset = off;
                hb1.SelectionLength = 1;
                //Debug.WriteLine("Node offset: " + off);
                if (e.Node.Tag != null && isArrayLeaf((nodeType)e.Node.Tag))
                {
                    TryParseArrayLeaf(e.Node);
                    LAST_SELECTED_PROP_TYPE = (nodeType)e.Node.Tag;
                }
                else if (e.Node.Tag != null && isStructLeaf((nodeType)e.Node.Tag))
                {
                    LAST_SELECTED_PROP_TYPE = (nodeType)e.Node.Tag;
                    TryParseStructProperty(LAST_SELECTED_PROP_TYPE);
                }
                else if (e.Node.Tag != null && e.Node.Tag.Equals(nodeType.ArrayProperty))
                {
                    LAST_SELECTED_PROP_TYPE = nodeType.ArrayProperty;
                    addArrayElementButton.Visible = true;
                    proptext.Clear();
                    UnrealObjectInfo.ArrayType arrayType = UnrealObjectInfo.getArrayType(getEnclosingType(e.Node.Parent), pcc.getNameEntry(BitConverter.ToInt32(memory, off)));
                    switch (arrayType)
                    {
                        case UnrealObjectInfo.ArrayType.Int:
                        case UnrealObjectInfo.ArrayType.Float:
                        case UnrealObjectInfo.ArrayType.Bool:
                        case UnrealObjectInfo.ArrayType.String:
                        case UnrealObjectInfo.ArrayType.Object:
                            proptext.Visible = true;
                            break;
                        case UnrealObjectInfo.ArrayType.Name:
                            proptext.Text = "0";
                            nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                            proptext.Visible = nameEntry.Visible = true;
                            break;
                        case UnrealObjectInfo.ArrayType.Enum:
                            string enumName = getEnclosingType(e.Node);
                            List<string> values = UnrealObjectInfo.getEnumValues(enumName);
                            if (values == null)
                            {
                                addArrayElementButton.Visible = false;
                                return;
                            }
                            enumDropdown.Items.Clear();
                            enumDropdown.Items.AddRange(values.ToArray());
                            enumDropdown.Visible = true;
                            break;
                        case UnrealObjectInfo.ArrayType.Struct:
                        default:
                            break;
                    }
                }
                else
                {
                    TryParseProperty();
                    LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Node name is not in correct format.");
                //name is wrong, don't attempt to continue parsing.
                LAST_SELECTED_PROP_TYPE = nodeType.Unknown;
                return;
            }
        }

        private void TryParseProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int type = BitConverter.ToInt32(memory, pos + 8);
                int test = BitConverter.ToInt32(memory, pos + 12);
                if (test != 0 || !pcc.isName(type))
                    return;
                bool visible = false;
                switch (pcc.getNameEntry(type))
                {
                    case "IntProperty":
                    case "ObjectProperty":
                    case "StringRefProperty":
                        proptext.Text = BitConverter.ToInt32(memory, pos + 24).ToString();
                        visible = true;
                        break;
                    case "FloatProperty":
                        proptext.Text = BitConverter.ToSingle(memory, pos + 24).ToString();
                        visible = true;
                        break;
                    case "BoolProperty":
                        proptext.Text = memory[pos + 24].ToString();
                        visible = true;
                        break;
                    case "NameProperty":
                        proptext.Text  = BitConverter.ToInt32(memory, pos + 28).ToString();
                        nameEntry.Text = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + 24));
                        nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                        nameEntry.Visible = true;
                        visible = true;
                        break;
                    case "StrProperty":
                        string s = "";
                        int count = -(int)BitConverter.ToInt64(memory, pos + 24);
                        pos += 28;
                        for (int i = 0; i < count; i++)
                        {
                            s += (char)memory[pos + i*2];
                        }
                        proptext.Text = s;
                        visible = true;
                        break;
                    case "ByteProperty":
                        string enumName = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + 24));
                        if (enumName != "None")
                        {
                            try
                            {
                                List<string> values = UnrealObjectInfo.getEnumValues(enumName);
                                if (values != null)
                                {
                                    enumDropdown.Items.Clear();
                                    enumDropdown.Items.AddRange(values.ToArray());
                                    setPropertyButton.Visible = enumDropdown.Visible = true;
                                    string curVal = pcc.getNameEntry(BitConverter.ToInt32(memory, pos + 32));
                                    int idx = values.IndexOf(curVal);
                                    enumDropdown.SelectedIndex = idx;
                                    return;
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            proptext.Text = memory[pos + 32].ToString();
                            visible = true;
                        }
                        break;
                }
                proptext.Visible = setPropertyButton.Visible = visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void TryParseStructProperty(nodeType type)
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 8)
                    return;
                switch (type)
                {
                    case nodeType.StructLeafFloat:
                        proptext.Text = BitConverter.ToSingle(memory, pos).ToString();
                        break;
                    case nodeType.StructLeafByte:
                        proptext.Text = memory[pos].ToString();
                        break;
                    case nodeType.StructLeafDeg:
                        proptext.Text = ((float)BitConverter.ToInt32(memory, pos) * 360f / 65536f).ToString();
                        break;
                    case nodeType.StructLeafInt:
                        proptext.Text = BitConverter.ToInt32(memory, pos).ToString();
                        break;
                }
                proptext.Visible = setPropertyButton.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void TryParseArrayLeaf(TreeNode node)
        {
            try
            {
                nodeType type = (nodeType)node.Tag;
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 8)
                    return;
                switch (type)
                {
                    case nodeType.ArrayLeafInt:
                    case nodeType.ArrayLeafObject:
                        proptext.Text = BitConverter.ToInt32(memory, pos).ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafFloat:
                        proptext.Text = BitConverter.ToSingle(memory, pos).ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafBool:
                        proptext.Text = memory[pos].ToString();
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafName:
                        proptext.Text = BitConverter.ToInt32(memory, pos + 4).ToString();
                        nameEntry.Text = pcc.getNameEntry(BitConverter.ToInt32(memory, pos));
                        nameEntry.AutoCompleteCustomSource.AddRange(pcc.Names.ToArray());
                        proptext.Visible = nameEntry.Visible = true;
                        break;
                    case nodeType.ArrayLeafString:
                        string s = "";
                        int count = -BitConverter.ToInt32(memory, pos);
                        for (int i = 0; i < count - 1; i++)
                        {
                            s += (char)memory[pos + 4 + i * 2];
                        }
                        proptext.Text = s;
                        proptext.Visible = true;
                        break;
                    case nodeType.ArrayLeafEnum:
                        string enumName = getEnclosingType(node.Parent);
                        List<string> values = UnrealObjectInfo.getEnumValues(enumName);
                        if (values == null)
                        {
                            return;
                        }
                        enumDropdown.Items.Clear();
                        enumDropdown.Items.AddRange(values.ToArray());
                        setPropertyButton.Visible = enumDropdown.Visible = true;
                        string curVal = pcc.getNameEntry(BitConverter.ToInt32(memory, pos));
                        int idx = values.IndexOf(curVal);
                        enumDropdown.SelectedIndex = idx;
                        break;
                    case nodeType.ArrayLeafStruct:
                        break;
                    default:
                        return;
                }
                deleteArrayElement.Visible = setPropertyButton.Visible = true;
                if (type == nodeType.ArrayLeafStruct)
                {
                    setPropertyButton.Visible = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setProperty_Click(object sender, EventArgs e)
        {
            if (hb1.SelectionStart != lastSetOffset)
            {
                return; //user manually moved cursor
            }
            if (isArrayLeaf(LAST_SELECTED_PROP_TYPE))
            {
                setArrayProperty();
            }
            else if(isStructLeaf(LAST_SELECTED_PROP_TYPE))
            {
                setStructProperty();
            }
            else
            {
                setNonArrayProperty();
            }
        }

        private void setStructProperty()
        {
            try
            {
                int pos = lastSetOffset;
                if (memory.Length - pos < 8)
                    return;
                byte b = 0;
                float f = 0;
                switch (LAST_SELECTED_PROP_TYPE)
                {
                    case nodeType.StructLeafByte:
                        if (byte.TryParse(proptext.Text, out b))
                        {
                            memory[pos] = b;
                            RefreshMem();
                        }
                        break;
                    case nodeType.StructLeafFloat:
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos, BitConverter.GetBytes(f));
                            RefreshMem();
                        }
                        break;
                    case nodeType.StructLeafDeg:
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos, BitConverter.GetBytes(Convert.ToInt32(f * 65536f / 360f)));
                            RefreshMem();
                        }
                        break;
                    case nodeType.StructLeafInt:
                        int i = 0;
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setNonArrayProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int type = BitConverter.ToInt32(memory, pos + 8);
                int test = BitConverter.ToInt32(memory, pos + 12);
                if (test != 0 || !pcc.isName(type))
                    return;
                int i = 0;
                float f = 0;
                switch (pcc.getNameEntry(type))
                {
                    case "IntProperty":
                    case "ObjectProperty":
                    case "StringRefProperty":
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos + 24, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        break;
                    case "NameProperty":
                        if (int.TryParse(proptext.Text, out i))
                        {
                            if (!pcc.Names.Contains(nameEntry.Text) &&
                                DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.pccFileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                            {
                                break;
                            }
                            WriteMem(pos + 24, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                            WriteMem(pos + 28, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        break;
                    case "FloatProperty":
                        proptext.Text = CheckSeperator(proptext.Text);
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos + 24, BitConverter.GetBytes(f));
                            RefreshMem();
                        }
                        break;
                    case "BoolProperty":
                        if (int.TryParse(proptext.Text, out i) && (i == 0 || i == 1))
                        {
                            memory[pos + 24] = (byte)i;
                            RefreshMem();
                        }
                        break;
                    case "ByteProperty":
                        if (enumDropdown.Visible)
                        {
                            i = pcc.FindNameOrAdd(enumDropdown.SelectedItem as string);
                            WriteMem(pos + 32, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        else if(int.TryParse(proptext.Text, out i) && i >= 0 && i <= 255)
                        {
                            memory[pos + 32] = (byte)i;
                            RefreshMem();
                        }
                        break;
                    case "StrProperty":
                        string s = proptext.Text;
                        int offset = pos + 24;
                        int oldSize = BitConverter.ToInt32(memory, pos + 16);
                        int oldLength = -(int)BitConverter.ToInt64(memory, offset);
                        List<byte> stringBuff = new List<byte>(s.Length * 2);
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                        }
                        stringBuff.Add(0);
                        stringBuff.Add(0);
                        byte[] buff = BitConverter.GetBytes((s.LongCount() + 1) * 2 + 4);
                        for (int j = 0; j < 4; j++)
                            memory[offset - 8 + j] = buff[j];
                        buff = BitConverter.GetBytes(-(s.Count() + 1));
                        for (int j = 0; j < 4; j++)
                            memory[offset + j] = buff[j];
                        buff = new byte[memory.Length - (oldLength * 2) + stringBuff.Count];
                        int startLength = offset + 4;
                        int startLength2 = startLength + (oldLength * 2);
                        for (int j = 0; j < startLength; j++)
                        {
                            buff[j] = memory[j];
                        }
                        for (int j = 0; j < stringBuff.Count; j++)
                        {
                            buff[j + startLength] = stringBuff[j];
                        }
                        startLength += stringBuff.Count;
                        for (int j = 0; j < memory.Length - startLength2; j++)
                        {
                            buff[j + startLength] = memory[j + startLength2];
                        }
                        memory = buff;

                        //bubble up size
                        uint throwaway;
                        TreeNode parent = LAST_SELECTED_NODE.Parent;
                        while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                        {
                            if (uint.TryParse(parent.Text, out throwaway))
                            {
                                parent = parent.Parent;
                                continue;
                            }
                            updateArrayLength(Convert.ToInt32(parent.Name), 0, (stringBuff.Count + 4) - oldSize);
                            parent = parent.Parent;
                        }
                        RefreshMem();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void setArrayProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 8)
                    return;
                int i = 0;
                switch (LAST_SELECTED_PROP_TYPE)
                {
                    case nodeType.ArrayLeafInt:
                    case nodeType.ArrayLeafObject:
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        break;
                    case nodeType.ArrayLeafFloat:
                        proptext.Text = CheckSeperator(proptext.Text);
                        float f = 0f;
                        if (float.TryParse(proptext.Text, out f))
                        {
                            WriteMem(pos, BitConverter.GetBytes(f));
                            RefreshMem();
                        }
                        break;
                    case nodeType.ArrayLeafBool:
                        if (int.TryParse(proptext.Text, out i) && (i == 0 || i == 1))
                        {
                            memory[pos] = (byte)i;
                            RefreshMem();
                        }
                        break;
                    case nodeType.ArrayLeafName:
                        if (int.TryParse(proptext.Text, out i))
                        {
                            if (!pcc.Names.Contains(nameEntry.Text) &&
                                DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.pccFileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                            {
                                break;
                            }
                            WriteMem(pos, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                            WriteMem(pos + 4, BitConverter.GetBytes(i));
                            RefreshMem();
                        }
                        break;
                    case nodeType.ArrayLeafEnum:
                        i = pcc.FindNameOrAdd(enumDropdown.SelectedItem as string);
                        WriteMem(pos, BitConverter.GetBytes(i));
                        RefreshMem();
                        break;
                    case nodeType.ArrayLeafString:
                        string s = proptext.Text;
                        int offset = pos;
                        int oldLength = -(int)BitConverter.ToInt64(memory, offset);
                        int oldSize = 4 + (oldLength * 2);
                        List<byte> stringBuff = new List<byte>(s.Length * 2);
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                        }
                        stringBuff.Add(0);
                        stringBuff.Add(0);
                        byte[] buff = BitConverter.GetBytes(-(s.LongCount() + 1));
                        for (int j = 0; j < 8; j++)
                            memory[offset + j] = buff[j];
                        buff = new byte[memory.Length - (oldLength * 2) + stringBuff.Count];
                        int startLength = offset + 4;
                        int startLength2 = startLength + (oldLength * 2);
                        for (int j = 0; j < startLength; j++)
                        {
                            buff[j] = memory[j];
                        }
                        for (int j = 0; j < stringBuff.Count; j++)
                        {
                            buff[j + startLength] = stringBuff[j];
                        }
                        startLength += stringBuff.Count;
                        for (int j = 0; j < memory.Length - startLength2; j++)
                        {
                            buff[j + startLength] = memory[j + startLength2];
                        }
                        memory = buff;

                        //bubble up size
                        uint throwaway;
                        TreeNode parent = LAST_SELECTED_NODE.Parent;
                        while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                        {
                            if (uint.TryParse(parent.Text, out throwaway))
                            {
                                parent = parent.Parent;
                                continue;
                            }
                            updateArrayLength(Convert.ToInt32(parent.Name), 0, (stringBuff.Count + 4) - oldSize);
                            parent = parent.Parent;
                        }
                        RefreshMem();
                        break;
                    default:
                        return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void deleteArrayLeaf()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (hb1.SelectionStart != lastSetOffset)
                {
                    return; //user manually moved cursor
                }

                if (memory.Length - pos < 8) //not long enough to deal with
                    return;

                TreeNode parent = LAST_SELECTED_NODE.Parent;
                int leafOffset = Convert.ToInt32(LAST_SELECTED_NODE.Name);
                int parentOffset = Convert.ToInt32(parent.Name);
                
                int size;
                switch (LAST_SELECTED_PROP_TYPE)
                {
                    case nodeType.ArrayLeafInt:
                    case nodeType.ArrayLeafFloat:
                    case nodeType.ArrayLeafObject:
                        size = 4;
                        break;
                    case nodeType.ArrayLeafName:
                    case nodeType.ArrayLeafEnum:
                        size = 8;
                        break;
                    case nodeType.ArrayLeafBool:
                        size = 1;
                        break;
                    case nodeType.ArrayLeafString:
                        size = BitConverter.ToInt32(memory, leafOffset) * -2 + 4;
                        break;
                    case nodeType.ArrayLeafStruct:
                        int tmp = readerpos = leafOffset;
                        ReadHeadersTillNone();
                        size = readerpos - tmp;
                        break;
                    default:
                        return;
                }
                //bubble up size
                bool firstbubble = true;
                uint throwaway;
                while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                {
                    if (uint.TryParse(parent.Text, out throwaway))
                    {
                        parent = parent.Parent;
                        continue;
                    }
                    parentOffset = Convert.ToInt32(parent.Name);
                    if (firstbubble)
                    {
                        memory = RemoveIndices(memory, leafOffset, size);
                        firstbubble = false;
                        updateArrayLength(parentOffset, -1, -size);
                    }
                    else
                    {
                        updateArrayLength(parentOffset, 0, -size);
                    }
                    parent = parent.Parent;
                }
                RefreshMem();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void addArrayLeaf()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (hb1.SelectionStart != lastSetOffset)
                {
                    return; //user manually moved cursor
                }
                
                int size = BitConverter.ToInt32(memory, pos + 16);
                int count = BitConverter.ToInt32(memory, pos + 24);
                int leafSize  = 0;
                string propName = pcc.getNameEntry(BitConverter.ToInt32(memory, pos));
                UnrealObjectInfo.ArrayType arrayType = UnrealObjectInfo.getArrayType(getEnclosingType(LAST_SELECTED_NODE.Parent), propName);
                List<byte> memList = memory.ToList();
                int i;
                float f;
                switch (arrayType)
                {
                    case UnrealObjectInfo.ArrayType.Int:
                    case UnrealObjectInfo.ArrayType.Object:
                        leafSize = 4;
                        if (!int.TryParse(proptext.Text, out i))
                        {
                            return; //not valid element
                        }
                        memList.InsertRange(pos + 24 + size, BitConverter.GetBytes(i));
                        break;
                    case UnrealObjectInfo.ArrayType.Float:
                        leafSize = 4;
                        if (!float.TryParse(proptext.Text, out f))
                        {
                            return; //not valid element
                        }
                        memList.InsertRange(pos + 24 + size, BitConverter.GetBytes(f));
                        break;
                    case UnrealObjectInfo.ArrayType.Bool:
                        leafSize = 1;
                        if (!(int.TryParse(proptext.Text, out i) && (i == 0 || i == 1)))
                        {
                            return; //not valid
                        }
                        memList.Insert(pos + 24 + size, (byte)i);
                        break;
                    case UnrealObjectInfo.ArrayType.Name:
                        leafSize = 8;
                        if (!int.TryParse(proptext.Text, out i))
                        {
                            return; //not valid
                        }
                        if (!pcc.Names.Contains(nameEntry.Text) &&
                            DialogResult.No == MessageBox.Show($"{Path.GetFileName(pcc.pccFileName)} does not contain the Name: {nameEntry.Text}\nWould you like to add it to the Name list?", "", MessageBoxButtons.YesNo))
                        {
                            return;
                        }
                        memList.InsertRange(pos + 24 + size, BitConverter.GetBytes(pcc.FindNameOrAdd(nameEntry.Text)));
                        memList.InsertRange(pos + 24 + size + 4, BitConverter.GetBytes(i));
                        break;
                    case UnrealObjectInfo.ArrayType.Enum:
                        leafSize = 8;
                        string selectedItem = enumDropdown.SelectedItem as string;
                        if (selectedItem == null)
                        {
                            return;
                        }
                        i = pcc.FindNameOrAdd(selectedItem);
                        memList.InsertRange(pos + 24 + size, BitConverter.GetBytes(i));
                        memList.InsertRange(pos + 24 + size + 4, new byte[4]);
                        break;
                    case UnrealObjectInfo.ArrayType.String:
                        memList.InsertRange(pos + 24 + size, BitConverter.GetBytes(-(proptext.Text.Length + 1)));
                        List<byte> stringBuff = new List<byte>();
                        for (int j = 0; j < proptext.Text.Length; j++)
                        {
                            stringBuff.AddRange(BitConverter.GetBytes(proptext.Text[j]));
                        }
                        stringBuff.Add(0);
                        stringBuff.Add(0);
                        memList.InsertRange(pos + 24 + size + 4, stringBuff);
                        leafSize = 4 + stringBuff.Count;
                        break;
                    case UnrealObjectInfo.ArrayType.Struct:
                        byte[] buff = UnrealObjectInfo.getDefaultClassValue(pcc, getEnclosingType(LAST_SELECTED_NODE));
                        if (buff == null)
                        {
                            return;
                        }
                        memList.InsertRange(pos + 24 + size, buff);
                        leafSize = buff.Length;
                        break;
                    default:
                        return;
                }
                memory = memList.ToArray();
                updateArrayLength(pos, 1, leafSize);

                //bubble up size
                uint throwaway;
                TreeNode parent = LAST_SELECTED_NODE.Parent;
                while (parent != null && (parent.Tag.Equals(nodeType.StructProperty) || parent.Tag.Equals(nodeType.ArrayProperty) || parent.Tag.Equals(nodeType.ArrayLeafStruct)))
                {
                    if (uint.TryParse(parent.Text, out throwaway))
                    {
                        parent = parent.Parent;
                        continue;
                    }
                    updateArrayLength(Convert.ToInt32(parent.Name), 0, leafSize);
                    parent = parent.Parent;
                }
                RefreshMem();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private T[] RemoveIndices<T>(T[] IndicesArray, int RemoveAt, int NumElementsToRemove)
        {
            if (RemoveAt < 0 || RemoveAt > IndicesArray.Length - 1 || NumElementsToRemove < 0 || NumElementsToRemove + RemoveAt > IndicesArray.Length - 1)
            {
                return IndicesArray;
            }
            T[] newIndicesArray = new T[IndicesArray.Length - NumElementsToRemove];

            int i = 0;
            int j = 0;
            while (i < IndicesArray.Length)
            {
                if (i < RemoveAt || i >= RemoveAt + NumElementsToRemove)
                {                    
                    newIndicesArray[j] = IndicesArray[i];
                    j++;
                } else
                {
                    //Debug.WriteLine("Skipping byte: " + i.ToString("X4"));
                }

                i++;
            }

            return newIndicesArray;
        }

        private void WriteMem(int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        /// <summary>
        /// Updates an array properties length and size in bytes. Does not refresh the memory view
        /// </summary>
        /// <param name="startpos">Starting index of the array property</param>
        /// <param name="countDelta">Delta in terms of how many items the array has</param>
        /// <param name="byteDelta">Delta in terms of how many bytes the array data is</param>
        private void updateArrayLength(int startpos, int countDelta, int byteDelta)
        {
            int sizeOffset = 16;
            int countOffset = 24;
            int oldSize = BitConverter.ToInt32(memory, sizeOffset + startpos);
            int oldCount = BitConverter.ToInt32(memory, countOffset + startpos);

            int newSize = oldSize + byteDelta;
            int newCount = oldCount + countDelta;

            WriteMem(startpos + sizeOffset, BitConverter.GetBytes(newSize));
            WriteMem(startpos + countOffset, BitConverter.GetBytes(newCount));

        }


        private void RefreshMem()
        {
            nameEntry.Visible = proptext.Visible = setPropertyButton.Visible = enumDropdown.Visible = addArrayElementButton.Visible = deleteArrayElement.Visible = false;
            pcc.Exports[Index].Data = memory;
            hb1.ByteProvider = new DynamicByteProvider(memory);
            //adds rootnode to list
            List<TreeNode> allNodes = treeView1.Nodes.Cast<TreeNode>().ToList();
            //flatten tree of nodes into list.
            for (int i = 0; i < allNodes.Count(); i++)
            {
                allNodes.AddRange(allNodes[i].Nodes.Cast<TreeNode>());
            }

            var expandedNodes = allNodes.Where(x => x.IsExpanded).Select(x => x.Name);
            StartScan(expandedNodes, treeView1.TopNode.Name);
        }

        private string CheckSeperator(string s)
        {
            string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string wrongsep;
            if (seperator == ".")
                wrongsep = ",";
            else
                wrongsep = ".";
            return s.Replace(wrongsep, seperator);
        }

        private void expandAllButton_Click(object sender, EventArgs e)
        {
            if (treeView1 != null)
            {
                treeView1.ExpandAll();
            }
        }

        private void collapseAllButton_Click(object sender, EventArgs e)
        {
            if (treeView1 != null)

            {
                treeView1.CollapseAll();
                treeView1.Nodes[0].Expand();
            }
        }

        private void deleteArrayElement_Click(object sender, EventArgs e)
        {
            deleteArrayLeaf();
        }

        private void addArrayElementButton_Click(object sender, EventArgs e)
        {
             addArrayLeaf();
        }
    }
}
