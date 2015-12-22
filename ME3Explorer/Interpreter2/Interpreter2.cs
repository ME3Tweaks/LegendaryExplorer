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
using System.Diagnostics;

namespace ME3Explorer.Interpreter2
{
    public partial class Interpreter2 : Form
    {
        public PCCObject pcc;
        public int Index;
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

        public const int STRUCT_PROPERTY = 0;
        public const int INT_PROPERTY = 1;
        public const int FLOAT_PROPERTY = 2;
        public const int OBJECT_PROPERTY = 3;
        public const int NAME_PROPERTY = 4;
        public const int BOOL_PROPERTY = 5;
        public const int BYTE_PROPERTY = 6;
        public const int ARRAY_PROPERTY = 7;
        public const int STRING_PROPERTY = 8;
        public const int STRINGREF_PROPERTY = 9;
        public const int DELEGATE_PROPERTY = 10;

        public const int TOPLEVEL_TAG = -1; //indicates this is a top level object in the tree
        public const int ARRAYLEAF_TAG = -2; //indicates this is a generic leaf in an arraylist, with no defined type
        public const int NONARRAYLEAF_TAG = -100; //indicates this is not an array leaf but does not specify what it is (e.g. could be unknown.)

        private const int ARRAYSVIEW_RAW = 0;
        private const int ARRAYSVIEW_IMPORTEXPORT = 1;
        private const int ARRAYSVIEW_NAMES = 2;

        private TalkFile talkFile;
        private int lastSetOffset = -1; //offset set by program, used for checking if user changed since set 
        private int LAST_SELECTED_PROP_TYPE = -100; //last property type user selected. Will use to check the current offset for type
        private TreeNode LAST_SELECTED_NODE = null; //last selected tree node

        public Interpreter2()
        {
            InitializeComponent();
            arrayViewerDropdown.SelectedIndex = 0;
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
            List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
            TreeNode topLevelTree = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            topLevelTree = GenerateTree(topLevelTree, topLevelHeaders);
            topLevelTree.Tag = TOPLEVEL_TAG;
            treeView1.Nodes.Add(topLevelTree);
            treeView1.CollapseAll();
            treeView1.Nodes[0].Expand();
        }

        public TreeNode Scan()
        {
            readerpos = PropertyReader.detectStart(pcc, memory, pcc.Exports[Index].ObjectFlags);
            BitConverter.IsLittleEndian = true;
            List<PropHeader> topLevelHeaders = ReadHeadersTillNone();
            TreeNode t = new TreeNode("0000 : " + pcc.Exports[Index].ObjectName);
            return GenerateTree(t, topLevelHeaders);
        }

        public TreeNode GenerateTree(TreeNode input, List<PropHeader> headersList)
        {
            TreeNode ret = input;
            foreach (PropHeader header in headersList)
            {
                int type = getType(pcc.getNameEntry(header.type));
                if (type != ARRAY_PROPERTY && type != STRUCT_PROPERTY)
                    ret.Nodes.Add(GenerateNode(header));
                else
                {
                    if (type == ARRAY_PROPERTY)
                    {
                        TreeNode t = GenerateNode(header);
                        int arrayLength = BitConverter.ToInt32(memory, header.offset + 24);
                        readerpos = header.offset + 28;
                        int tmp = readerpos;
                        List<PropHeader> propHeaders = ReadHeadersTillNone();
                        if (propHeaders.Count != 0 && arrayLength > 0)
                        {
                            if (arrayLength == 1)
                            {
                                readerpos = tmp;
                                t = GenerateTree(t, propHeaders);
                            }
                            else
                            {
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    readerpos = tmp;
                                    List<PropHeader> arrayListPropHeaders = ReadHeadersTillNone();
                                    tmp = readerpos;
                                    TreeNode n = new TreeNode(i.ToString());
                                    n = GenerateTree(n, arrayListPropHeaders);
                                    t.Nodes.Add(n);
                                }
                            }
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < (header.size - 4) / 4; i++)
                            {
                                int val = BitConverter.ToInt32(memory, header.offset + 28 + i * 4);
                                string s = (header.offset + 28 + i * 4).ToString("X4") + "|";
                                if (arrayViewerDropdown.SelectedIndex == ARRAYSVIEW_IMPORTEXPORT)
                                {
                                    s += i + ": ";
                                    Debug.WriteLine("IMPEXP BLOCK REACHED.");
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
                                }
                                else if (arrayViewerDropdown.SelectedIndex == ARRAYSVIEW_NAMES)
                                {
                                    s += i / 2 + ": ";
                                    Debug.WriteLine("NAMES BLOCK REACHED.");
                                    int value = val;
                                    if (value < 0)
                                    {
                                        //invalid
                                        s += "Invalid Name Index [" + value + "]";
                                    }
                                    else
                                    {
                                        if (pcc.Names.Count > value)
                                        {
                                            s += pcc.Names[value] + " [NAMEINDEX " + value + "]";
                                        }
                                        else
                                        {
                                            s += "Index not in name list [" + value + "]";
                                        }
                                    }
                                    i++; //names are 8 bytes so skip an entry
                                }
                                else
                                {
                                    s += i + ": ";
                                    s += val.ToString();
                                }
                                TreeNode node = new TreeNode(s);
                                node.Tag = ARRAYLEAF_TAG;
                                node.Name = (header.offset + 28 + i * 4).ToString();
                                t.Nodes.Add(node);
                            }
                            ret.Nodes.Add(t);
                        }
                    }
                    if (type == STRUCT_PROPERTY)
                    {
                        TreeNode t = GenerateNode(header);
                        int name = BitConverter.ToInt32(memory, header.offset + 24);
                        readerpos = header.offset + 32;
                        List<PropHeader> ll = ReadHeadersTillNone();
                        if (ll.Count != 0)
                        {
                            t = GenerateTree(t, ll);
                            ret.Nodes.Add(t);
                        }
                        else
                        {
                            for (int i = 0; i < header.size / 4; i++)
                            {
                                int val = BitConverter.ToInt32(memory, header.offset + 32 + i * 4);
                                string s = (header.offset + 32 + i * 4).ToString("X4") + " : " + val.ToString();
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
            int propertyType = getType(pcc.getNameEntry(p.type));
            switch (propertyType)
            {
                case INT_PROPERTY:
                case OBJECT_PROPERTY:
                    int idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString();
                    break;
                case STRING_PROPERTY:
                    int count = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"";
                    for (int i = 0; i < count * -1 - 1; i++)
                        s += (char)memory[p.offset + 28 + i * 2];
                    s += "\"";
                    break;
                case BOOL_PROPERTY:
                    byte val = memory[p.offset + 24];
                    s += (val == 1).ToString();
                    break;
                case FLOAT_PROPERTY:
                    float f = BitConverter.ToSingle(memory, p.offset + 24);
                    s += f.ToString() + "f";
                    break;
                case STRUCT_PROPERTY:
                case NAME_PROPERTY:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "\"" + pcc.getNameEntry(idx) + "\"";
                    break;
                case BYTE_PROPERTY:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    int idx2 = BitConverter.ToInt32(memory, p.offset + 32);
                    s += "\"" + pcc.getNameEntry(idx) + "\",\"" + pcc.getNameEntry(idx2) + "\"";
                    break;
                case ARRAY_PROPERTY:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += idx.ToString() + "(count)";
                    break;
                case STRINGREF_PROPERTY:
                    idx = BitConverter.ToInt32(memory, p.offset + 24);
                    s += "#" + idx.ToString() + ": ";
                    s += talkFile == null ? "(.tlk not loaded)" : talkFile.findDataById(idx);
                    break;
            }
            TreeNode ret = new TreeNode(s);
            ret.Tag = propertyType;
            ret.Name = p.offset.ToString();
            return ret;
        }

        public int getType(string s)
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
                        if (!pcc.isName(p.type) || getType(pcc.getNameEntry(p.type)) == -1)
                            run = false;
                        else
                        {
                            p.size = BitConverter.ToInt32(memory, readerpos + 16);
                            p.index = BitConverter.ToInt32(memory, readerpos + 20);
                            p.offset = readerpos;
                            ret.Add(p);
                            readerpos += p.size + 24;
                            if (getType(pcc.getNameEntry(p.type)) == BOOL_PROPERTY)//Boolbyte
                                readerpos++;
                            if (getType(pcc.getNameEntry(p.type)) == STRUCT_PROPERTY ||//StructName
                                getType(pcc.getNameEntry(p.type)) == BYTE_PROPERTY)//byteprop
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
            LAST_SELECTED_NODE = e.Node;
            if (e.Node.Name == "")
            {
                Debug.WriteLine("This node is not parsable.");
                //can't attempt to parse this.
                arrayPropertyDropdown.Enabled = true;
                return;
            }
            try
            {
                int off = Convert.ToInt32(e.Node.Name);
                hb1.SelectionStart = off;
                lastSetOffset = off;
                hb1.SelectionLength = 1;
                Debug.WriteLine("Node offset: " + off);
                if (e.Node.Tag.Equals(ARRAYLEAF_TAG))
                {
                    TryParseArrayProperty();
                    LAST_SELECTED_PROP_TYPE = ARRAYLEAF_TAG;
                }
                else
                {
                    arrayPropertyDropdown.Enabled = false;
                    TryParseProperty();
                    LAST_SELECTED_PROP_TYPE = NONARRAYLEAF_TAG;
                }
            }
            catch (System.FormatException ex)
            {
                Debug.WriteLine("Node name is not in correct format.");
                //name is wrong, don't attempt to continue parsing.
                LAST_SELECTED_PROP_TYPE = NONARRAYLEAF_TAG;
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
                proptext.Visible = setPropertyButton.Visible = setValueSeparator.Visible = visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void TryParseArrayProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int value = BitConverter.ToInt32(memory, pos);
                proptext.Text = value.ToString();
                proptext.Visible = setPropertyButton.Visible = setValueSeparator.Visible = true;
                arrayPropertyDropdown.Enabled = true;
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
            if (LAST_SELECTED_PROP_TYPE == ARRAYLEAF_TAG)
            {
                setArrayProperty();
            }
            else
            {
                setNonArrayProperty();
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

        private void setArrayProperty()
        {
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos < 16)
                    return;
                int i = 0;
                if (int.TryParse(proptext.Text, out i))
                {
                    WriteMem(pos, BitConverter.GetBytes(i));
                    RefreshMem();
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

        /// <summary>
        /// This removes a specific amount of bytes from the memory array at the starting position indicated and will return a new memory array with those bytes removed (not just 0'd).
        /// This will make the array smaller.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pos">Position to start removing bytes at.</param>
        /// <param name="numbytestoremove">Number of bytes to remove.</param>
        /// <returns>New memory with the removed bytes</returns>
        private T[] RemoveMem<T>(int pos, int numbytestoremove)
        {
            T[] dest = new T[memory.Length - numbytestoremove];
            if (pos > 0)
                Array.Copy(memory, 0, dest, 0, pos); //get pre-removed bytes

            if (pos < memory.Length - numbytestoremove)
                Array.Copy(memory, pos + numbytestoremove, dest, pos, memory.Length - pos - numbytestoremove - 1); //append post-removed bytes

            return dest;
        }

        private T[] AddMem<T>(int pos, T[] datatoadd)
        {
            T[] dest = new T[memory.Length + datatoadd.Length];
            if (pos > 0)
                Array.Copy(memory, 0, dest, 0, pos); //get pre-insert bytes

            Array.Copy(datatoadd, 0, dest, pos, datatoadd.Length);

            if (pos < memory.Length/* + datatoadd.Length*/)
                Array.Copy(memory, pos + datatoadd.Length, dest, pos + datatoadd.Length, memory.Length + datatoadd.Length - 1); //append post-insert bytes

            return dest;
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

        private void arrayViewerDropdown_selectionChanged(object sender, EventArgs e)
        {
            if (previousArrayView > -1)
            {
                StartScan();
            }
            previousArrayView = arrayViewerDropdown.SelectedIndex;
        }

        private void arrayRemove4Bytes_Click(object sender, EventArgs e)
        {
            if (hb1.SelectionStart != lastSetOffset || LAST_SELECTED_NODE == null || !LAST_SELECTED_NODE.Tag.Equals(ARRAYLEAF_TAG))
            {
                return; //user manually moved cursor or we have an invalid state
            }
            try
            {
                int pos = (int)hb1.SelectionStart;
                if (memory.Length - pos - 4 < 16) //4 bytes
                    return;
                memory = RemoveMem<byte>(pos, 4);
                int off = Convert.ToInt32(LAST_SELECTED_NODE.Parent.Name);
                updateArrayLength(off, -1, -4);
                RefreshMem();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
