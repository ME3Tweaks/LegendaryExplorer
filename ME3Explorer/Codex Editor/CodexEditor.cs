using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Codex_Editor
{
    public partial class CodexEditor : Form
    {
        public struct EntryStruct
        {
            public int offset;
            public int[] Values;
        }

        PCCObject pcc;
        List<EntryStruct> CodexMap;

        public CodexEditor()
        {
            InitializeComponent();
        }

        private void CodexEditor_Activated(object sender, EventArgs e)
        {
            string pathcook = ME3Directory.cookedPath;
            if (!File.Exists(pathcook + "SFXGameInfoSP_SF.pcc"))
            {
                MessageBox.Show("File SFXGameInfoSP_SF.pcc not found!");
                this.Close();
                return;
            }
            pcc = new PCCObject(pathcook + "SFXGameInfoSP_SF.pcc");
            GetEntries();
            RefreshTree();
        }

        public void GetEntries()
        {
            CodexMap = new List<EntryStruct>();
            byte[] buff = pcc.Exports[0].Data;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, buff);
            int pos = props[props.Count - 1].offend;
            BitConverter.IsLittleEndian = true;
            while (pos < buff.Length)
            {
                int count = BitConverter.ToInt32(buff, pos);
                pos += 4;
                for (int i = 0; i < count; i++)
                {
                    EntryStruct entry = new EntryStruct();
                    entry.offset = pos;
                    entry.Values = new int[8];
                    for (int j = 0; j < 8; j++)
                        entry.Values[j] = BitConverter.ToInt32(buff, pos + j * 4);
                    CodexMap.Add(entry);
                    pos += 32;
                }
            }
        }

        public void RefreshTree()
        {
            treeView1.Nodes.Clear();
            TreeNode t = new TreeNode("Codex Map");
            int count = 0;
            foreach (EntryStruct entry in CodexMap)
            {
                string s = (count++) + " @0x" + entry.offset.ToString("X8") + " : ";
                foreach (int i in entry.Values)
                    s += i + " ";
                t.Nodes.Add(s);
            }
            treeView1.Nodes.Add(t);
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null || t.Parent.Text != "Codex Map")
                return;
            EntryStruct entry = CodexMap[t.Index];
            string s = "";
            for (int i = 0; i < 7; i++)
                s += entry.Values[i] + " , ";
            s += entry.Values[7];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values", "ME3Explorer", s, 0, 0);
            string[] sres = result.Split(',');
            if (sres.Length != 8)
                return;
            for (int i = 0; i < 8; i++)
                entry.Values[i] = Convert.ToInt32(sres[i].Trim());
            CodexMap[t.Index] = entry;
            RefreshTree();
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null && CodexMap != null)
            {
                byte[] buff = pcc.Exports[0].Data;
                foreach (EntryStruct entry in CodexMap)
                    for (int i = 0; i < 8; i++)
                    {
                        byte[] tmp = BitConverter.GetBytes(entry.Values[i]);
                        for (int j = 0; j < 4; j++)
                            buff[entry.offset + i * 4 + j] = tmp[j];
                    }
                pcc.Exports[0].Data = buff;
                pcc.altSaveToFile(pcc.pccFileName, true);
                MessageBox.Show("Done");
            }
        }
    }
}
