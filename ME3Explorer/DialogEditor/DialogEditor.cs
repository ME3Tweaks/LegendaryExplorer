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
using KFreonLib.MEDirectories;

namespace ME3Explorer.DialogEditor
{
    public partial class DialogEditor : Form
    {
        public TalkFile talkFile;
        public BioConversation Dialog;
        public PCCObject pcc;
        public List<int> Objs;

        public void InitTalkFile(Object editorTalkFile = null)
        {
            if (editorTalkFile == null)
            {
                var tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
                talkFile = new TalkFile();
                talkFile.LoadTlkData(tlkPath);
            }
            else
            {
                talkFile = (TalkFile)editorTalkFile;
            }
        }


        public DialogEditor()
        {
            InitializeComponent();
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(ME3Directory.cookedPath) && talkFile == null)
            {
                MessageBox.Show("ME3 install directory not found. Set its path at:\n Options > Set Custom Path > Mass Effect 3\n\n Or, specify a .tlk file location with:\n File > Load Different TLK");
                return;
            }
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InitTalkFile(talkFile);
                pcc = new PCCObject(d.FileName);
                Objs = new List<int>();
                for (int i = 0; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ClassName == "BioConversation")
                        Objs.Add(i);
                RefreshCombo();
            }
        }

        public void RefreshCombo()
        {
            if (Objs == null)
                return;
            toolStripComboBox1.Items.Clear();
            foreach (int i in Objs)
                toolStripComboBox1.Items.Add("#" + i + " : " + pcc.Exports[i].ObjectName);
            if (toolStripComboBox1.Items.Count != 0)
                toolStripComboBox1.SelectedIndex = 0;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            if (n == -1)
                return;
            Dialog = new BioConversation(pcc, Objs[n]);
            RefreshTabs();
        }

        public void RefreshTabs()
        {
            if (Dialog == null)
                return;
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            listBox3.Items.Clear();
            listBox4.Items.Clear();
            listBox5.Items.Clear();
            treeView1.Nodes.Clear();
            treeView2.Nodes.Clear();
            int count = 0;
            foreach (int i in Dialog.StartingList)
                listBox1.Items.Add((count++) + " : " + i);
            count = 0;
            foreach (BioConversation.EntryListStuct e in Dialog.EntryList)
                treeView1.Nodes.Add(e.ToTree(count++, talkFile, pcc));
            count = 0;
            foreach (BioConversation.ReplyListStruct r in Dialog.ReplyList)
                treeView2.Nodes.Add(r.ToTree(count++, talkFile, pcc));
            count = 0;
            foreach (int i in Dialog.SpeakerList)
                listBox2.Items.Add((count++) + " : " + i + " , " + pcc.getNameEntry(i));
            count = 0;
            foreach (BioConversation.StageDirectionStruct sd in Dialog.StageDirections)
                listBox3.Items.Add((count++) + " : " + sd.Text.Substring(0, sd.Text.Length - 1) + " , " + sd.StringRef + " , " + talkFile.findDataById(sd.StringRef));
            count = 0;
            foreach (int i in Dialog.MaleFaceSets)
                listBox4.Items.Add((count++) + " : " + i);
            count = 0;
            foreach (int i in Dialog.FemaleFaceSets)
                listBox5.Items.Add((count++) + " : " + i);
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Dialog == null)
                return;
            Dialog.Save();
            MessageBox.Show("Done.");
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox1.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.StartingList[n].ToString(), 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.StartingList[n] = i;
            RefreshTabs();
        }

        private void listBox2_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox2.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name entry", "ME3Explorer", Dialog.SpeakerList[n].ToString(), 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i) && pcc.isName(i))
                Dialog.SpeakerList[n] = i;
            RefreshTabs();
        }

        private void listBox4_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox4.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.MaleFaceSets[n].ToString(), 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.MaleFaceSets[n] = i;
            RefreshTabs();
        }

        private void listBox5_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox5.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.FemaleFaceSets[n].ToString(), 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.FemaleFaceSets[n] = i;
            RefreshTabs();
        }

        private void toStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.StartingList.Add(i);
            RefreshTabs();
        }

        private void toSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i) && pcc.isName(i))
                Dialog.SpeakerList.Add(i);
            RefreshTabs();
        }

        private void toMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.MaleFaceSets.Add(i);
            RefreshTabs();
        }

        private void toFemaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                Dialog.FemaleFaceSets.Add(i);
            RefreshTabs();
        }

        private void fromStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox1.SelectedIndex) == -1)
                return;
            Dialog.StartingList.RemoveAt(n);
            RefreshTabs();
        }

        private void fromSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox2.SelectedIndex) == -1)
                return;
            Dialog.SpeakerList.RemoveAt(n);
            RefreshTabs();
        }

        private void fromMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox4.SelectedIndex) == -1)
                return;
            Dialog.MaleFaceSets.RemoveAt(n);
            RefreshTabs();
        }

        private void fromFemalFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox5.SelectedIndex) == -1)
                return;
            Dialog.FemaleFaceSets.RemoveAt(n);
            RefreshTabs();
        }

        private void listBox3_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox3.SelectedIndex) == -1)
                return;
            BioConversation.StageDirectionStruct sd = Dialog.StageDirections[n];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string", "ME3Explorer", Dialog.StageDirections[n].Text, 0, 0);
            if (result == "")
                return;
            sd.Text = result;
            result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.StageDirections[n].StringRef.ToString(), 0, 0);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
                sd.StringRef = i;
            Dialog.StageDirections[n] = sd;
            RefreshTabs();
        }

        private void stageDirectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = listBox3.SelectedIndex) == -1)
                return;
            BioConversation.StageDirectionStruct sd = new BioConversation.StageDirectionStruct();
            sd.Text = "";
            foreach (char c in Dialog.StageDirections[n].Text)
                sd.Text += c;
            sd.StringRef = Dialog.StageDirections[n].StringRef;
            Dialog.StageDirections.Add(sd);
            RefreshTabs();
        }

        private void treeView2_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode t = e.Node;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            int n = p.Index, i = 0;
            string result;
            BioConversation.ReplyListStruct rp = Dialog.ReplyList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
                string[] reslist;
                switch (propname)
                {
                    case "Listener Index":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].ListenerIndex.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ListenerIndex = i;
                        break;
                    case "Unskippable":
                        if (Dialog.ReplyList[n].Unskippable) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.Unskippable = (result == "1");
                        break;
                    case "IsDefaultAction":
                        if (Dialog.ReplyList[n].IsDefaultAction) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.IsDefaultAction = (result == "1");
                        break;
                    case "IsMajorDecision":
                        if (Dialog.ReplyList[n].IsMajorDecision) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.IsMajorDecision = (result == "1");
                        break;
                    case "ReplyType":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name indicies", "ME3Explorer", Dialog.ReplyList[n].ReplyTypeType + " , " + Dialog.ReplyList[n].ReplyTypeValue, 0, 0);
                        if (result == "") return;
                        reslist = result.Split(',');
                        if (reslist.Length != 2) return;                                               
                        if (int.TryParse(reslist[0], out i)) rp.ReplyTypeType = i;
                        if (int.TryParse(reslist[1], out i)) rp.ReplyTypeValue= i;
                        break;
                    case "Text":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string", "ME3Explorer", Dialog.ReplyList[n].Text, 0, 0);
                        if (result == "") return;
                        rp.Text = result;
                        break;
                    case "refText":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].refText.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.refText = i;
                        break;
                    case "ConditionalFunc":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].ConditionalFunc.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ConditionalFunc = i;
                        break;
                    case "ConditionalParam":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].ConditionalParam.ToString(), 0, 0);
                        if (result == "") return;                        
                        if (int.TryParse(result, out i)) rp.ConditionalParam = i;                        
                        break;
                    case "StateTransition":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].StateTransition.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.StateTransition = i;                        
                        break;
                    case "StateTransitionParam":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].StateTransitionParam.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.StateTransitionParam = i;                        
                        break;
                    case "ExportID":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].ExportID.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ExportID = i;                        
                        break;
                    case "ScriptIndex":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].ScriptIndex.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ScriptIndex = i;                        
                        break;
                    case "CameraIntimacy":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].CameraIntimacy.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.CameraIntimacy = i;
                        break;
                    case "FireConditional":
                        if (Dialog.ReplyList[n].FireConditional) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.FireConditional = (result == "1");
                        break;
                    case "Ambient":
                        if (Dialog.ReplyList[n].Ambient) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.Ambient = (result == "1");
                        break;
                    case "NonTextline":
                        if (Dialog.ReplyList[n].NonTextLine) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.NonTextLine = (result == "1");
                        break;
                    case "IgnoreBodyGestures":
                        if (Dialog.ReplyList[n].IgnoreBodyGestures) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.IgnoreBodyGestures = (result == "1");
                        break;
                    case "AlwaysHideSubtitle":
                        if (Dialog.ReplyList[n].AlwaysHideSubtitle) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        rp.AlwaysHideSubtitle = (result == "1");
                        break;
                    case "GUIStyle":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name indicies", "ME3Explorer", Dialog.ReplyList[n].GUIStyleType + " , " + Dialog.ReplyList[n].GUIStyleValue, 0, 0);
                        if (result == "") return;
                        reslist = result.Split(',');
                        if (reslist.Length != 2) return;
                        if (int.TryParse(reslist[0], out i)) rp.GUIStyleType = i;
                        if (int.TryParse(reslist[1], out i)) rp.GUIStyleValue = i;
                        break;
                }
                Dialog.ReplyList[n] = rp;
            }
            #endregion
            #region EntryList
            else //EntryList
            {
                n = p.Parent.Index;
                rp = Dialog.ReplyList[n];
                int m = t.Index;
                result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].EntryList[m].ToString(), 0, 0);
                if (result == "") return;
                if (int.TryParse(result, out i)) rp.EntryList[m] = i;
            }
            #endregion
            RefreshTabs();

        }

        private void toReplysEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Index];
                int i = 0;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (int.TryParse(result, out i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Index] = rp;
                RefreshTabs();
            }
            else
            {
                BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Parent.Index];
                int i = 0;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer","0", 0, 0);
                if (result == "") return;
                if (int.TryParse(result, out i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Parent.Index] = rp;
                RefreshTabs();
            }
        }

        private void fromReplysEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null)
            {
                Dialog.ReplyList[p.Parent.Index].EntryList.RemoveAt(t.Index);
                RefreshTabs();
            }
        }

        private void replyListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            BioConversation.ReplyListStruct rp = new BioConversation.ReplyListStruct();
            rp.EntryList = new List<int>();
            foreach (int i in Dialog.ReplyList[t.Index].EntryList)
                rp.EntryList.Add(i);
            rp.ListenerIndex = Dialog.ReplyList[t.Index].ListenerIndex;
            rp.Unskippable = Dialog.ReplyList[t.Index].Unskippable;
            rp.IsDefaultAction = Dialog.ReplyList[t.Index].IsDefaultAction;
            rp.IsMajorDecision = Dialog.ReplyList[t.Index].IsMajorDecision;
            rp.ReplyTypeType = Dialog.ReplyList[t.Index].ReplyTypeType;
            rp.ReplyTypeValue = Dialog.ReplyList[t.Index].ReplyTypeValue;
            rp.Text = Dialog.ReplyList[t.Index].Text;
            rp.refText = Dialog.ReplyList[t.Index].refText;
            rp.ConditionalFunc = Dialog.ReplyList[t.Index].ConditionalFunc;
            rp.ConditionalParam = Dialog.ReplyList[t.Index].ConditionalParam;
            rp.StateTransition = Dialog.ReplyList[t.Index].StateTransition;
            rp.StateTransitionParam = Dialog.ReplyList[t.Index].StateTransitionParam;
            rp.ExportID = Dialog.ReplyList[t.Index].ExportID;
            rp.ScriptIndex = Dialog.ReplyList[t.Index].ScriptIndex;
            rp.CameraIntimacy = Dialog.ReplyList[t.Index].CameraIntimacy;
            rp.FireConditional = Dialog.ReplyList[t.Index].FireConditional;
            rp.Ambient = Dialog.ReplyList[t.Index].Ambient;
            rp.NonTextLine = Dialog.ReplyList[t.Index].NonTextLine;
            rp.IgnoreBodyGestures = Dialog.ReplyList[t.Index].IgnoreBodyGestures;
            rp.AlwaysHideSubtitle = Dialog.ReplyList[t.Index].AlwaysHideSubtitle;
            rp.GUIStyleType = Dialog.ReplyList[t.Index].GUIStyleType;
            rp.GUIStyleValue = Dialog.ReplyList[t.Index].GUIStyleValue;
            Dialog.ReplyList.Add(rp);
            RefreshTabs();
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode t = e.Node;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            int n = p.Index, i = 0;
            string result;
            BioConversation.EntryListStuct el = Dialog.EntryList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
                string[] reslist;
                switch (propname)
                {
                    case "SpeakerIndex":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.SpeakerIndex.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.SpeakerIndex = i;
                        break;
                    case "ListenerIndex":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.ListenerIndex.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ListenerIndex = i;
                        break;
                    case "Skippable":
                        if (el.Skippable) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.Skippable = (result == "1");
                        break;
                    case "Text":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string", "ME3Explorer", el.Text, 0, 0);
                        if (result == "") return;
                        el.Text = result;
                        break;
                    case "refText":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.refText.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.refText = i;
                        break;
                    case "ConditionalFunc":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.ConditionalFunc.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ConditionalFunc = i;
                        break;
                    case "ConditionalParam":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.ConditionalParam.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ConditionalParam = i;
                        break;
                    case "StateTransition":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.StateTransition.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.StateTransition = i;
                        break;
                    case "StateTransitionParam":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.StateTransitionParam.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.StateTransitionParam = i;
                        break;
                    case "ExportID":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.ExportID.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ExportID = i;
                        break;
                    case "ScriptIndex":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.ScriptIndex.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ScriptIndex = i;
                        break;
                    case "CameraIntimacy":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.CameraIntimacy.ToString(), 0, 0);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.CameraIntimacy = i;
                        break;
                    case "FireConditional":
                        if (el.FireConditional) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.FireConditional = (result == "1");
                        break;
                    case "Ambient":
                        if (el.Ambient) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.Ambient = (result == "1");
                        break;
                    case "NonTextline":
                        if (el.NonTextline) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.NonTextline = (result == "1");
                        break;
                    case "IgnoreBodyGestures":
                        if (el.IgnoreBodyGestures) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.IgnoreBodyGestures = (result == "1");
                        break;
                    case "AlwaysHideSubtitle":
                        if (el.AlwaysHideSubtitle) i = 1; else i = 0;
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", i.ToString(), 0, 0);
                        if (result == "") return;
                        el.AlwaysHideSubtitle = (result == "1");
                        break;
                    case "GUIStyle":
                        result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name indicies", "ME3Explorer", el.GUIStyleType + " , " + el.GUIStyleValue, 0, 0);
                        if (result == "") return;
                        reslist = result.Split(',');
                        if (reslist.Length != 2) return;
                        if (int.TryParse(reslist[0], out i)) el.GUIStyleType = i;
                        if (int.TryParse(reslist[1], out i)) el.GUIStyleValue = i;
                        break;
                }
                Dialog.EntryList[n] = el;
            }
            #endregion
            #region EntryList
            else //ReplyList/SpeakerList
            {
                n = p.Parent.Index;
                el = Dialog.EntryList[n];
                int m = t.Index;
                if (p.Index == 0) //ReplyList
                {
                    BioConversation.EntryListReplyListStruct rpe = el.ReplyList[m];
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string for \"Paraphrase\"", "ME3Explorer", rpe.Paraphrase.ToString(), 0, 0);
                    rpe.Paraphrase = result;
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value for \"Index\"", "ME3Explorer", rpe.Index.ToString(), 0, 0);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.Index = i;
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new StringRef value for \"refParaphrase\"", "ME3Explorer", rpe.refParaphrase.ToString(), 0, 0);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.refParaphrase = i;
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name indicies for \"Category\"", "ME3Explorer", rpe.CategoryType + " , " + rpe.CategoryValue, 0, 0);
                    if (result == "") return;
                    string[] reslist = result.Split(',');
                    if (reslist.Length != 2) return;
                    if (int.TryParse(reslist[0], out i)) rpe.CategoryType = i;
                    if (int.TryParse(reslist[1], out i)) rpe.CategoryValue = i;
                    el.ReplyList[m] = rpe;
                }
                if (p.Index == 1) //Speaker List
                {
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.SpeakerList[m].ToString(), 0, 0);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) el.SpeakerList[m] = i;
                }
            }
            #endregion
            RefreshTabs();
        }

        private void toEntrysSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                BioConversation.EntryListStuct el = Dialog.EntryList[p.Index];
                int i = 0;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Index] = el;
                RefreshTabs();
            }
            else
            {
                BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                int i = 0;
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Parent.Index] = el;
                RefreshTabs();
            }
        }

        private void fromEntrysSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null && p.Index == 1)
            {
                Dialog.EntryList[p.Parent.Index].SpeakerList.RemoveAt(t.Index);
                RefreshTabs();
            }
        }

        private void fromEntrysReplyListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null && p.Index == 0)
            {
                Dialog.EntryList[p.Parent.Index].ReplyList.RemoveAt(t.Index);
                RefreshTabs();
            }
        }

        private void fromReplyListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.ReplyList.RemoveAt(t.Index);
            RefreshTabs();
        }

        private void fromEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.EntryList.RemoveAt(t.Index);
            RefreshTabs();
        }

        private void entryListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            BioConversation.EntryListStuct el0 = Dialog.EntryList[t.Index];
            BioConversation.EntryListStuct el = new BioConversation.EntryListStuct();
            el.ReplyList = new List<BioConversation.EntryListReplyListStruct>();
            foreach (BioConversation.EntryListReplyListStruct rpe0 in el0.ReplyList)
            {
                BioConversation.EntryListReplyListStruct rpe = new BioConversation.EntryListReplyListStruct();
                rpe.CategoryType = rpe0.CategoryType;
                rpe.CategoryValue = rpe0.CategoryValue;
                rpe.Index = rpe0.Index;
                rpe.Paraphrase = "" + rpe0.Paraphrase;
                rpe.refParaphrase = rpe0.refParaphrase;
                el.ReplyList.Add(rpe);
            }
            el.SpeakerList = new List<int>();
            foreach (int i in el0.SpeakerList)
                el.SpeakerList.Add(i);
            el.AlwaysHideSubtitle = el0.AlwaysHideSubtitle;
            el.Ambient = el0.Ambient;
            el.CameraIntimacy = el0.CameraIntimacy;
            el.ConditionalFunc = el0.ConditionalFunc;
            el.ConditionalParam = el0.ConditionalParam;
            el.ExportID = el0.ExportID;
            el.FireConditional = el0.FireConditional;
            el.GUIStyleType = el0.GUIStyleType;
            el.GUIStyleValue = el0.GUIStyleValue;
            el.IgnoreBodyGestures = el0.IgnoreBodyGestures;
            el.ListenerIndex = el0.ListenerIndex;
            el.NonTextline = el0.NonTextline;
            el.refText = el0.refText;
            el.ScriptIndex = el0.ScriptIndex;
            el.Skippable = el0.Skippable;
            el.SpeakerIndex = el0.SpeakerIndex;
            el.StateTransition = el0.StateTransition;
            el.StateTransitionParam = el0.StateTransitionParam;
            el.Text = "" + el0.Text;
            Dialog.EntryList.Add(el);
            RefreshTabs();
        }

        private void entrysReplyListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null && p.Index == 0)
            {
                BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                BioConversation.EntryListReplyListStruct rpe0 = el.ReplyList[t.Index];
                BioConversation.EntryListReplyListStruct rpe = new BioConversation.EntryListReplyListStruct();
                rpe.CategoryType = rpe0.CategoryType;
                rpe.CategoryValue = rpe0.CategoryValue;
                rpe.Index = rpe0.Index;
                rpe.Paraphrase = "" + rpe0.Paraphrase;
                rpe.refParaphrase = rpe0.refParaphrase;
                el.ReplyList.Add(rpe);
                Dialog.EntryList[p.Parent.Index] = el;
                RefreshTabs();
            }
        }

        private void loadDifferentTLKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.tlk|*.tlk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (talkFile == null)
                {
                    talkFile = new TalkFile();
                }
                talkFile.LoadTlkData(d.FileName);
                MessageBox.Show("Done.");
            }
        }

        private void toEntrysReplyListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            int Index;
            int SubIndx = -1;
            if (p.Parent == null)
                Index = p.Index;
            else
            {
                Index = p.Parent.Index;
                SubIndx = t.Index;
            }
            BioConversation.EntryListStuct el = Dialog.EntryList[Index];
            AddReply ar = new AddReply();
            ar.pcc = pcc;
            if (SubIndx != -1)
            {
                BioConversation.EntryListReplyListStruct tr = el.ReplyList[SubIndx];
                ar.textBox1.Text = tr.Paraphrase;
                ar.textBox2.Text = tr.refParaphrase.ToString();
                ar.textBox3.Text = tr.CategoryValue.ToString();
                ar.textBox4.Text = tr.Index.ToString();
            }
            ar.Show();
            while (ar.state == 0) Application.DoEvents();
            ar.Close();
            if (ar.state == -1)
                return;
            if(el.ReplyList == null)
                el.ReplyList = new List<BioConversation.EntryListReplyListStruct>();
            el.ReplyList.Add(ar.res);
            Dialog.EntryList[Index] = el;
            RefreshTabs();
        }

        private void entriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.ExpandAll();
        }

        private void repliesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView2.ExpandAll();
        }

        private void entriesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            treeView1.CollapseAll();
        }

        private void repliesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            treeView2.CollapseAll();
        }
    }
}
