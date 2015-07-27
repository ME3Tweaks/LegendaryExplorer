using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Be;
using Be.Windows.Forms;

namespace ME3Explorer.UECodeEditor
{
    public partial class UECodeEditor : Form
    {
        public List<int> Objects;
        public PCCObject pcc;
        public byte[] memory;

        public UECodeEditor()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LoadFile(d.FileName);
        }

        public void LoadFile(string path)
        {
            pcc = new PCCObject(path);
            Objects = new List<int>();
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == "Function")
                    Objects.Add(i);
            RefreshLists();
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (int n in Objects)
                listBox1.Items.Add(n.ToString() + " : " + pcc.Exports[n].GetFullPath);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            LoadScript(n);
        }

        public void LoadScript(int n)
        {
            memory = pcc.Exports[Objects[n]].Data;
            hb1.ByteProvider = new DynamicByteProvider(memory);
            treeView1.Nodes.Clear();
            GlobalPointer = 0x20;
            VirtualPointer = 0x00;
            foundEnd = false;
            while (OPCodes.isValid(memory, GlobalPointer) && !foundEnd)
                treeView1.Nodes.Add(ReadToken());
            if (!OPCodes.isValid(memory, GlobalPointer) && !foundEnd)
                treeView1.Nodes.Add("Unknown OPCode: 0x" + memory[GlobalPointer].ToString("X"));
            treeView1.ExpandAll();
        }

        public TreeNode ReadToken()
        {
            if (memory[GlobalPointer] == 0x53)
                foundEnd = true;
            bool isNative = ((memory[GlobalPointer] & 0xF0) == 0x70);
            string temp = "GlPtr: 0x" + GlobalPointer.ToString("X4") + " VirtPtr: 0x" + VirtualPointer.ToString("X4") + " OPC: 0x";
            int index;
            if (isNative)
                index = ((memory[GlobalPointer] - 0x70) << 8) + memory[GlobalPointer + 1];
            else
                index = memory[GlobalPointer];
            temp += index.ToString("X4");
            TreeNode t = new TreeNode(temp);
            string pat = OPCodes.GetPattern(memory, GlobalPointer);
            if (!isNative)
                GlobalPointer += 1;
            else
                GlobalPointer += 2;
            VirtualPointer += 4;
            int pos = 0;
            string s = "";
            while(pos<pat.Length)
                if (pat[pos] != '%')
                {
                    s += pat[pos];
                    pos++;
                }
                else
                {
                    if (s != "")
                        t.Nodes.Add(s);
                    s = "";
                    char c = pat[pos + 1];
                    pos += 2;
                    int n;
                    float f;
                    switch (c)
                    {
                        case 'o':
                        case 'O':
                            n = BitConverter.ToInt32(memory, GlobalPointer);
                            if (n > 0)
                            {
                                n--;
                                if(pcc.isExport(n))
                                    t.Nodes.Add(pcc.Exports[n].ObjectName);
                            }
                            else if (n < 0)
                            {
                                n++;
                                if(pcc.isImport(n))
                                    t.Nodes.Add(pcc.Imports[n].ObjectName);
                            }
                            GlobalPointer += 4;
                            VirtualPointer += 4;
                            break;
                        case '1':
                            n = BitConverter.ToInt32(memory, GlobalPointer);
                            if (n > 0)
                            {
                                n--;
                                if (pcc.isExport(n))
                                {
                                    t.Nodes.Add(pcc.Exports[n].ClassName);
                                    t.Nodes.Add(pcc.Exports[n].ObjectName);
                                }
                            }
                            else if (n < 0)
                            {
                                n++;
                                if (pcc.isImport(n))
                                {
                                    t.Nodes.Add(pcc.Imports[n].ClassName);
                                    t.Nodes.Add(pcc.Imports[n].ObjectName);
                                }
                            }
                            GlobalPointer += 4;
                            VirtualPointer += 4;
                            break;
                        case 's':
                        case 'S':
                            n = BitConverter.ToInt16(memory, GlobalPointer);
                                    t.Nodes.Add(n.ToString("X4"));
                            GlobalPointer += 2;
                            VirtualPointer += 2;
                            break;
                        case 'f':
                        case 'F':
                            f = BitConverter.ToSingle(memory, GlobalPointer);
                            t.Nodes.Add(f.ToString() + "f");
                            GlobalPointer += 4;
                            VirtualPointer += 4;
                            break;
                        case 'b':
                        case 'B':
                            t.Nodes.Add(memory[GlobalPointer].ToString("X"));
                            GlobalPointer += 1;
                            VirtualPointer += 1;
                            break;
                        case 'i':
                        case 'I':
                            n = BitConverter.ToInt32(memory, GlobalPointer);
                            t.Nodes.Add(n.ToString("X8"));
                            GlobalPointer += 4;
                            VirtualPointer += 4;
                            break;
                        case 't':
                        case 'T':
                            t.Nodes.Add(ReadToken());
                            break;
                        case 'n':
                        case 'N':
                            int count = 0;
                            while (memory[GlobalPointer] != 0x16) 
                                if (count++ == 0)
                                    t.Nodes.Add(ReadToken());
                                else
                                {
                                    t.Nodes.Add(",");
                                    t.Nodes.Add(ReadToken());
                                }
                            GlobalPointer  += 1;
                            VirtualPointer += 4;
                            break;
                    }

                }
            if (s != "")
                t.Nodes.Add(s);
            return t;
        }

        public int GlobalPointer;
        public int VirtualPointer;
        public bool foundEnd;

        private void openOPCodeTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OPCodeTable o = new OPCodeTable();
            o.MdiParent = this.MdiParent;
            o.Show();
            o.WindowState = FormWindowState.Maximized;
        }
    }
}
