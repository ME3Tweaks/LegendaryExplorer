//Interpreter2.cs
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME1Explorer.Unreal;
using ME1Explorer.Unreal.Classes;

namespace ME1Explorer.Interpreter2
{
    public partial class Interpreter2 : Form
    {
        public PCCObject pcc;
        public int Index;
        public byte[] memory;
        public int memsize;
        public int readerpos;
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
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            treeView1.Nodes.Clear();
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].flagint);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> l = ReadHeadersTillNone();
            TreeNode t = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            t = GenerateTree(t, l);
            treeView1.Nodes.Add(t);
            treeView1.CollapseAll();
        }

        public TreeNode GenerateTree(TreeNode input, List<PropHeader> l)
        {
            TreeNode ret = input;
            foreach (PropHeader p in l)
            {
                int type = isType(pcc.GetName(p.type));
                if (type != 7 && type != 0)
                    ret.Nodes.Add(GenerateNode(p));
                else
                {
                    if (type == 7)
                    {
                        TreeNode t = GenerateNode(p);
                        int count = BitConverter.ToInt32(memory, p.offset + 24);
                        readerpos = p.offset + 28;
                        int tmp = readerpos;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0 && count > 0)
                        {
                            if (count == 1)
                            {
                                readerpos = tmp;
                                t = GenerateTree(t, ll);
                            }
                            else
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    readerpos = tmp;
                                    List<PropHeader> ll2 = ReadHeadersTillNone();
                                    tmp = readerpos;
                                    TreeNode n = new TreeNode(i.ToString());
                                    n = GenerateTree(n, ll2);
                                    t.Nodes.Add(n);
                                }
                            }
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < (p.size - 4) / 4; i++)
                            {
                                int val = BitConverter.ToInt32(memory, p.offset + 28 + i * 4);
                                string s = (p.offset + 28 + i * 4).ToString("X4") + " : " + val.ToString();
                                t.Nodes.Add(s);
                            }
                            ret.Nodes.Add(t);
                        }
                    }
                    if (type == 0)
                    {
                        TreeNode t = GenerateNode(p);
                        int name = BitConverter.ToInt32(memory, p.offset + 24);
                        readerpos = p.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            t = GenerateTree(t, ll);
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < p.size / 4; i++)
                            {
                                int val = BitConverter.ToInt32(memory, p.offset + 32 + i * 4);
                                string s = (p.offset + 32 + i * 4).ToString("X4") + " : " + val.ToString();
                                t.Nodes.Add(s);
                            }
                            ret.Nodes.Add(t);
                        }
                    }

                }
            }
            return ret;
        }

        public TreeNode GenerateNode(PropHeader p)
        {
            string s = p.offset.ToString("X4") + " : ";
            s += "Name: \"" + pcc.GetName(p.name) + "\" ";
            s += "Type: \"" + pcc.GetName(p.type) + "\" ";
            s += "Size: " + p.size.ToString() + " Value: ";
            switch (isType(pcc.GetName(p.type)))
            {
                case 1:
                    int idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case 3:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + " ";
                    if (idx == 0)
                        s += "None";
                    else
                        s += pcc.GetClass(idx);
                    break;
                case 8:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count - 1; i++)
                        s += (char)memory[p.offset + 28 + i];
                    s += "\"";
                    break;
                case 5:
                    byte val = memory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case 2:
                    float f = BitConverter.ToSingle(memory, p.offset + 24);
                    s += f.ToString() + "f";
                    break;
                case 0:
                case 4:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.GetName(idx) + "\"";
                    break;
                case 6:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.GetName(idx) + "\"";
                    break;
                case 7:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case 9:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx.ToString();
                    break;
            }
            TreeNode ret = new TreeNode(s);
            ret.Name = p.offset.ToString();
            return ret;
        }

        public int isType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return ret;
        }

        public List<PropHeader> ReadHeadersTillNone()
        {
            List<PropHeader> ret = new List<PropHeader>();
            bool run = true;
            while (run)
            {
                PropHeader p = new PropHeader();
                p.name = BitConverter.ToInt32(memory, readerpos);
                if (!pcc.isName(p.name))
                    run = false;
                else
                {
                    if (pcc.GetName(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        if (!pcc.isName(p.type) || isType(pcc.GetName(p.type)) == -1)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;
                            if (isType(pcc.GetName(p.type)) == 5)//Boolbyte
                                readerpos+=4;
                            if (isType(pcc.GetName(p.type)) == 0)//StructName
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
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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

        public int CharToInt(char c)
        {
            int r = -1;
            string signs = "0123456789ABCDEF";
            for (int i = 0; i < signs.Length; i++)
                if (signs[i] == c)
                    r = i;
            return r;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Name == "")
                return;
            int off = Convert.ToInt32(e.Node.Name);
            hb1.SelectionStart = off;
            hb1.SelectionLength = 1;
         
        }
    }
}
