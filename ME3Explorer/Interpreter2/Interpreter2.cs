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
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Interpreter2
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

        private TalkFile talkFile;

        public Interpreter2()
        {
            InitializeComponent();
        }

        public void InitInterpreter(Object editorTalkFile = null)
        {
            DynamicByteProvider db = new DynamicByteProvider(pcc.Exports[Index].Data);
            hb1.ByteProvider = db;
            memory = pcc.Exports[Index].Data;
            memsize = memory.Length;
            
            // Load the default TLK file into memory.
            if (editorTalkFile == null)
            {
                if (ME3Directory.cookedPath != null)
                {
                    var tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                    talkFile = new TalkFile();
                    talkFile.LoadTlkData(tlkPath);
                }
            }
            else
            {
                talkFile = (TalkFile)editorTalkFile;
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            StartScan();
        }

        public void Show()
        {
            base.Show();
            StartScan();
        }

        private void StartScan()
        {
            treeView1.Nodes.Clear();
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].ObjectFlags);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> l = ReadHeadersTillNone();
            TreeNode t = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            t = GenerateTree(t, l);
            treeView1.Nodes.Add(t);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
        }

        public TreeNode Scan()
        {
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].ObjectFlags);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> l = ReadHeadersTillNone();
            TreeNode t = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            return GenerateTree(t, l);
        }

        public TreeNode GenerateTree(TreeNode input, List<PropHeader> l)
        {
            TreeNode ret = input;
            foreach (PropHeader p in l)
            {
                int type = isType(pcc.getNameEntry(p.type));
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
            s += "Name: \"" + pcc.getNameEntry(p.name) + "\" ";
            s += "Type: \"" + pcc.getNameEntry(p.type) + "\" ";
            s += "Size: " + p.size.ToString() + " Value: ";
            switch (isType(pcc.getNameEntry(p.type)))
            {
                case 1:
                case 3:
                    int idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case 8:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count * -1 - 1; i++)
                        s += (char)memory[p.offset + 28 + i * 2];
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
                    s += "\"" + pcc.getNameEntry(idx) + "\"";
                    break;
                case 6:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    int idx2 = BitConverter.ToInt32(memory, p.offset + 32);
                    s += "\"" + pcc.getNameEntry(idx) + "\",\"" + pcc.getNameEntry(idx2) + "\"";
                    break;
                case 7:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case 9:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx.ToString() + ": ";
                    s += talkFile == null ? "(.tlk not loaded)" : talkFile.findDataById(idx);
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
                    if (pcc.getNameEntry(p.name) != "None")
                    {
                        p.type = BitConverter.ToInt32(memory, readerpos + 8);
                        if (!pcc.isName(p.type) || isType(pcc.getNameEntry(p.type)) == -1)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;
                            if (isType(pcc.getNameEntry(p.type)) == 5)//Boolbyte
                                readerpos++;
                            if (isType(pcc.getNameEntry(p.type)) == 0 ||//StructName
                                isType(pcc.getNameEntry(p.type)) == 6)//byteprop
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
            TryParseProperty();         
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
                    case "NameProperty":
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
                }
                proptext.Visible = toolStripButton3.Visible = visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
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
                    case "NameProperty":
                        if (int.TryParse(proptext.Text, out i))
                        {
                            WriteMem(pos + 24, BitConverter.GetBytes(i));
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void WriteMem(int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        private void RefreshMem()
        {
            pcc.Exports[Index].Data = memory;
            hb1.ByteProvider = new DynamicByteProvider(memory);
            StartScan();
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
    }
}
