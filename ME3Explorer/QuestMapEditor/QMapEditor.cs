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

namespace ME3Explorer.QuestMapEditor
{
    public partial class QMapEditor : Form
    {
        public struct QMapEntryStruct
        {
            public int[] Values;
        }

        public struct QMapStruct
        {
            public QMapEntryStruct[] List1;
            public QMapEntryStruct[] List2;
        }

        PCCObject pcc;
        QMapStruct QuestMap;
        public QMapEditor()
        {
            InitializeComponent();
        }

        private void QMapEditor_Activated(object sender, EventArgs e)
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
            int count1 = 0xAF;
            int count2 = 0x64;
            QuestMap = new QMapStruct();
            byte[] buff = pcc.Exports[3].Data;
            int pos = 0xC;
            BitConverter.IsLittleEndian = true;
            QuestMap.List1 = new QMapEntryStruct[count1];            
            for (int i = 0; i < count1; i++)
            {
                QMapEntryStruct e = new QMapEntryStruct();
                e.Values = new int[0xC];
                for (int j = 0; j < 0xC; j++)
                    e.Values[j] = BitConverter.ToInt32(buff, pos + j * 4);
                QuestMap.List1[i] = e;
                pos += 0x30;
            }
            QuestMap.List2 = new QMapEntryStruct[count2];
            for (int i = 0; i < count2; i++)
            {
                QMapEntryStruct e = new QMapEntryStruct();
                e.Values = new int[0x8];
                for (int j = 0; j < 0x8; j++)
                    e.Values[j] = BitConverter.ToInt32(buff, pos + j * 4);
                QuestMap.List2[i] = e;
                pos += 0x20;
            }
        }

        public void RefreshTree()
        {
            if (QuestMap.List1 == null || QuestMap.List2 == null)
                return;
            treeView1.Nodes.Clear();
            TreeNode t = new TreeNode("Quest Map");
            TreeNode t1 = new TreeNode("List 1");
            int count = 0;
            foreach (QMapEntryStruct e in QuestMap.List1)
            {
                string s = (count++) + " : ";
                foreach (int i in e.Values)
                    s += i + " ";
                t1.Nodes.Add(s);
            }
            t.Nodes.Add(t1);
            TreeNode t2 = new TreeNode("List 2");
            count = 0;
            foreach (QMapEntryStruct e in QuestMap.List2)
            {
                string s = (count++) + " : ";
                foreach (int i in e.Values)
                    s += i + " ";
                t2.Nodes.Add(s);
            }
            t.Nodes.Add(t2);
            t.Expand();
            treeView1.Nodes.Add(t);
        }

        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null || !(t.Parent.Text == "List 1" || t.Parent.Text == "List 2"))
                return;
            if (t.Parent.Text == "List 1")
            {
                string s = "";
                QMapEntryStruct entry = QuestMap.List1[t.Index];
                for (int i = 0; i < 0xB; i++)
                    s += entry.Values[i] + ", ";
                s += entry.Values[0xB];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values", "ME3Explorer", s, 0, 0);
                string[] sres = result.Split(',');
                if (sres.Length != 0xC)
                    return;
                for (int i = 0; i < 0xC; i++)
                    entry.Values[i] = Convert.ToInt32(sres[i].Trim());
                QuestMap.List1[t.Index] = entry;
                RefreshTree();
            }
            else
            {
                string s = "";
                QMapEntryStruct entry = QuestMap.List2[t.Index];
                for (int i = 0; i < 0x7; i++)
                    s += entry.Values[i] + ", ";
                s += entry.Values[0x7];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new values", "ME3Explorer", s, 0, 0);
                string[] sres = result.Split(',');
                if (sres.Length != 0x8)
                    return;
                for (int i = 0; i < 0x8; i++)
                    entry.Values[i] = Convert.ToInt32(sres[i].Trim());
                QuestMap.List2[t.Index] = entry;
                RefreshTree();
            }
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs ee)
        {
            int count1 = 0xAF;
            int count2 = 0x64;
            BitConverter.IsLittleEndian = true;
            MemoryStream m = new MemoryStream();
            m.Write(pcc.Exports[3].Data, 0, 0xC);
            for (int i = 0; i < count1; i++)
            {
                QMapEntryStruct e = QuestMap.List1[i];
                for (int j = 0; j < 0xC; j++)
                    m.Write(BitConverter.GetBytes(e.Values[j]), 0, 4);
            }
            for (int i = 0; i < count2; i++)
            {
                QMapEntryStruct e = QuestMap.List2[i];
                for (int j = 0; j < 0x8; j++)
                    m.Write(BitConverter.GetBytes(e.Values[j]), 0, 4);
            }
            pcc.Exports[3].Data = m.ToArray();
            pcc.altSaveToFile(pcc.pccFileName, true);
            MessageBox.Show("Done");
        }
    }
}
