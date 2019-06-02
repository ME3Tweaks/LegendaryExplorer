using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME1Explorer.Unreal;
using ME1Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using ME3Explorer;
using ME3Explorer.SharedUI;
using ME3Explorer.TlkManagerNS;
using ME3Explorer.Unreal;

namespace ME1Explorer
{
    public partial class DialogEditor : WinFormsBase
    {
        public BioTlkFileSet tlkFileSet;
        //public TalkFiles tlkFiles;
        public ITalkFile tlkFile;
        public ME1BioConversation Dialog;
        public List<int> Objs;

        public void InitBioTlkFileSet()
        {
            tlkFileSet = new BioTlkFileSet(Pcc as ME1Package);
            //tlkFiles = new TalkFiles();
            tlkFile = tlkFileSet;
        }


        public DialogEditor()
        {
            InitializeComponent();
            manageTLKSetToolStripMenuItem.Enabled = false;
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.u;*.upk;*sfm|*.u;*.upk;*sfm";
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        public void LoadFile(string fileName)
        {
            try
            {
                LoadME1Package(fileName);
                manageTLKSetToolStripMenuItem.Enabled = true;
                InitBioTlkFileSet();
                Objs = new List<int>();
                for (int i = 0; i < Pcc.Exports.Count; i++)
                    if (Pcc.Exports[i].ClassName == "BioConversation")
                        Objs.Add(i);
                RefreshCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }
        public void RefreshCombo()
        {
            if (Objs == null)
                return;
            toolStripComboBox1.Items.Clear();
            foreach (int i in Objs)
                toolStripComboBox1.Items.Add("#" + i + " : " + Pcc.Exports[i].ObjectName);
            if (toolStripComboBox1.Items.Count != 0)
                toolStripComboBox1.SelectedIndex = 0;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = toolStripComboBox1.SelectedIndex;
            if (n == -1)
                return;
            Dialog = new ME1BioConversation(Pcc as ME1Package, Objs[n]);
            tlkFileSet.loadData(Dialog.TlkFileSet - 1);
            if (!ME1TalkFiles.tlkList.Contains(tlkFileSet.talkFiles[tlkFileSet.selectedTLK]))
            {
                ME1TalkFiles.tlkList.Add(tlkFileSet.talkFiles[tlkFileSet.selectedTLK]);
            }
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
            foreach (ME1BioConversation.EntryListStuct e in Dialog.EntryList)
                treeView1.Nodes.Add(e.ToTree(count++, tlkFile, Pcc as ME1Package));
            count = 0;
            foreach (ME1BioConversation.ReplyListStruct r in Dialog.ReplyList)
                treeView2.Nodes.Add(r.ToTree(count++, tlkFile, Pcc as ME1Package));
            count = 0;
            foreach (ME1BioConversation.SpeakerListStruct sp in Dialog.SpeakerList)
                listBox2.Items.Add((count++) + " : " + sp.SpeakerTag.InstancedString);
            count = 0;
            foreach (ME1BioConversation.ScriptListStruct sd in Dialog.ScriptList)
                listBox3.Items.Add((count++) + " : " + sd.ScriptTag.InstancedString);
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
            Pcc.save();
            MessageBox.Show("Done.");
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox1.SelectedIndex) == -1)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.StartingList[n].ToString(), true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.StartingList[n] = i;
                Dialog.Save();
            }
        }

        private void listBox2_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox2.SelectedIndex) == -1)
                return;
            string nameResult = PromptDialog.Prompt(null, "Please enter new name", "ME1Explorer", Dialog.SpeakerList[n].SpeakerTag.Name, true);
            if (nameResult == "")
                return;
            string numberResult = PromptDialog.Prompt(null, "Please enter new name index", "ME1Explorer", Dialog.SpeakerList[n].SpeakerTag.Number.ToString(), true);
            if (numberResult == "")
                return;
            if (int.TryParse(numberResult, out int i) && i >= 0)
            {
                ME1BioConversation.SpeakerListStruct sp = new ME1BioConversation.SpeakerListStruct();
                sp.SpeakerTag = new NameReference(nameResult, i);
                Dialog.SpeakerList[n] = sp;
                Dialog.Save();
            }
        }

        private void listBox4_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox4.SelectedIndex) == -1)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.MaleFaceSets[n].ToString(), true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.MaleFaceSets[n] = i;
                Dialog.Save();
            }
        }

        private void listBox5_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox5.SelectedIndex) == -1)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.FemaleFaceSets[n].ToString(), true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.FemaleFaceSets[n] = i;
                Dialog.Save();
            }
        }

        private void toStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Pcc == null || Dialog == null)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "", true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.StartingList.Add(i);
                Dialog.Save();
            }
        }

        private void toSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Pcc == null || Dialog == null)
                return;
            string nameResult = PromptDialog.Prompt(null, "Please enter new name", "ME1Explorer", "", true);
            if (nameResult == "")
                return;
            string numberResult = PromptDialog.Prompt(null, "Please enter new name index", "ME1Explorer", "", true);
            if (numberResult == "")
                return;
            if (int.TryParse(numberResult, out int i) && i >= 0)
            {
                ME1BioConversation.SpeakerListStruct sp = new ME1BioConversation.SpeakerListStruct();
                sp.SpeakerTag = new NameReference(nameResult, i);
                Dialog.SpeakerList.Add(sp);
                Dialog.Save();
            }
        }

        private void toMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Pcc == null || Dialog == null)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "", true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.MaleFaceSets.Add(i);
                Dialog.Save();
            }
        }

        private void toFemaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Pcc == null || Dialog == null)
                return;
            string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "", true);
            if (result == "")
                return;
            int i = 0;
            if (int.TryParse(result, out i))
            {
                Dialog.FemaleFaceSets.Add(i);
                Dialog.Save();
            }
        }

        private void fromStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox1.SelectedIndex) == -1)
                return;
            Dialog.StartingList.RemoveAt(n);
            Dialog.Save();
        }

        private void fromSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox2.SelectedIndex) == -1)
                return;
            Dialog.SpeakerList.RemoveAt(n);
            Dialog.Save();
        }

        private void fromMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox4.SelectedIndex) == -1)
                return;
            Dialog.MaleFaceSets.RemoveAt(n);
            Dialog.Save();
        }

        private void fromFemalFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox5.SelectedIndex) == -1)
                return;
            Dialog.FemaleFaceSets.RemoveAt(n);
            Dialog.Save();
        }

        private void listBox3_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox3.SelectedIndex) == -1)
                return;
            string nameResult = PromptDialog.Prompt(null, "Please enter new name", "ME1Explorer", Dialog.ScriptList[n].ScriptTag.Name, true);
            if (nameResult == "")
                return;
            string numberResult = PromptDialog.Prompt(null, "Please enter new name index", "ME1Explorer", Dialog.ScriptList[n].ScriptTag.Number.ToString(), true);
            if (numberResult == "")
                return;
            if (int.TryParse(numberResult, out int i) && i >= 0)
            {
                ME1BioConversation.ScriptListStruct sp = new ME1BioConversation.ScriptListStruct();
                sp.ScriptTag = new NameReference(nameResult, i);
                Dialog.ScriptList[n] = sp;
                Dialog.Save();
            }
        }

        private void scriptListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (Pcc == null || Dialog == null || (n = listBox3.SelectedIndex) == -1)
                return;
            ME1BioConversation.ScriptListStruct sc = new ME1BioConversation.ScriptListStruct();
            sc.ScriptTag = Dialog.ScriptList[n].ScriptTag;
            Dialog.ScriptList.Add(sc);
            Dialog.Save();
        }

        private void treeView2_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode t = e.Node;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            int n = p.Index, i = 0;
            string result;
            ME1BioConversation.ReplyListStruct rp = Dialog.ReplyList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
                switch (propname)
                {
                    case "Listener Index":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].ListenerIndex.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ListenerIndex = i;
                        break;
                    case "Unskippable":
                        if (Dialog.ReplyList[n].Unskippable) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        rp.Unskippable = (result == "1");
                        break;
                    case "ReplyType":
                        result = InputComboBox.GetValue("Please select new value", ME1UnrealObjectInfo.getEnumValues("EReplyTypes").Select(nRef => nRef.Name), Pcc.getNameEntry(Dialog.ReplyList[n].ReplyTypeValue));
                        if (result == "") return;
                        rp.ReplyTypeValue = Pcc.FindNameOrAdd(result);
                        break;
                    case "Text":
                        result = PromptDialog.Prompt(null, "Please enter new string", "ME1Explorer", Dialog.ReplyList[n].Text, true);
                        if (result == "") return;
                        rp.Text = result;
                        break;
                    case "refText":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].refText.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.refText = i;
                        break;
                    case "ConditionalFunc":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].ConditionalFunc.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ConditionalFunc = i;
                        break;
                    case "ConditionalParam":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].ConditionalParam.ToString(), true);
                        if (result == "") return;                        
                        if (int.TryParse(result, out i)) rp.ConditionalParam = i;                        
                        break;
                    case "StateTransition":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].StateTransition.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.StateTransition = i;                        
                        break;
                    case "StateTransitionParam":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].StateTransitionParam.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.StateTransitionParam = i;                        
                        break;
                    case "ExportID":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].ExportID.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ExportID = i;                        
                        break;
                    case "ScriptIndex":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].ScriptIndex.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.ScriptIndex = i;                        
                        break;
                    case "CameraIntimacy":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].CameraIntimacy.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) rp.CameraIntimacy = i;
                        break;
                    case "FireConditional":
                        if (Dialog.ReplyList[n].FireConditional) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        rp.FireConditional = (result == "1");
                        break;
                    case "Ambient":
                        if (Dialog.ReplyList[n].Ambient) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        rp.Ambient = (result == "1");
                        break;
                    case "NonTextline":
                        if (Dialog.ReplyList[n].NonTextLine) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        rp.NonTextLine = (result == "1");
                        break;
                    case "IgnoreBodyGestures":
                        if (Dialog.ReplyList[n].IgnoreBodyGestures) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        rp.IgnoreBodyGestures = (result == "1");
                        break;
                    case "GUIStyle":
                        result = InputComboBox.GetValue("Please select new value", ME1UnrealObjectInfo.getEnumValues("EConvGUIStyles").Select(nRef => nRef.Name), Pcc.getNameEntry(Dialog.ReplyList[n].GUIStyleValue));
                        if (result == "") return;
                        rp.GUIStyleValue = Pcc.FindNameOrAdd(result);
                        break;
                }
                Dialog.ReplyList[n] = rp;
                Dialog.Save();
            }
            #endregion
            #region EntryList
            else //EntryList
            {
                n = p.Parent.Index;
                rp = Dialog.ReplyList[n];
                int m = t.Index;
                result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", Dialog.ReplyList[n].EntryList[m].ToString(), true);
                if (result == "") return;
                if (int.TryParse(result, out i))
                {
                    rp.EntryList[m] = i;
                    Dialog.Save();
                }
            }
            #endregion

        }

        private void toReplysEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                ME1BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Index];
                int i = 0;
                string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "0", true);
                if (result == "") return;
                if (int.TryParse(result, out i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Index] = rp;
                Dialog.Save();
            }
            else
            {
                ME1BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Parent.Index];
                int i = 0;
                string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer","0", true);
                if (result == "") return;
                if (int.TryParse(result, out i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Parent.Index] = rp;
                Dialog.Save();
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
                Dialog.Save();
            }
        }

        private void replyListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            ME1BioConversation.ReplyListStruct rp = new ME1BioConversation.ReplyListStruct();
            rp.EntryList = new List<int>();
            foreach (int i in Dialog.ReplyList[t.Index].EntryList)
                rp.EntryList.Add(i);
            rp.ListenerIndex = Dialog.ReplyList[t.Index].ListenerIndex;
            rp.Unskippable = Dialog.ReplyList[t.Index].Unskippable;
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
            rp.GUIStyleValue = Dialog.ReplyList[t.Index].GUIStyleValue;
            Dialog.ReplyList.Add(rp);
            Dialog.Save();
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode t = e.Node;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            int n = p.Index, i = 0;
            string result;
            ME1BioConversation.EntryListStuct el = Dialog.EntryList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
                switch (propname)
                {
                    case "SpeakerIndex":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.SpeakerIndex.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.SpeakerIndex = i;
                        break;
                    case "ListenerIndex":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.ListenerIndex.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ListenerIndex = i;
                        break;
                    case "Skippable":
                        if (el.Skippable) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        el.Skippable = (result == "1");
                        break;
                    case "Text":
                        result = PromptDialog.Prompt(null, "Please enter new string", "ME1Explorer", el.Text, true);
                        if (result == "") return;
                        el.Text = result;
                        break;
                    case "refText":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.refText.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.refText = i;
                        break;
                    case "ConditionalFunc":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.ConditionalFunc.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ConditionalFunc = i;
                        break;
                    case "ConditionalParam":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.ConditionalParam.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ConditionalParam = i;
                        break;
                    case "StateTransition":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.StateTransition.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.StateTransition = i;
                        break;
                    case "StateTransitionParam":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.StateTransitionParam.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.StateTransitionParam = i;
                        break;
                    case "ExportID":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.ExportID.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ExportID = i;
                        break;
                    case "ScriptIndex":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.ScriptIndex.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.ScriptIndex = i;
                        break;
                    case "CameraIntimacy":
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.CameraIntimacy.ToString(), true);
                        if (result == "") return;
                        if (int.TryParse(result, out i)) el.CameraIntimacy = i;
                        break;
                    case "FireConditional":
                        if (el.FireConditional) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        el.FireConditional = (result == "1");
                        break;
                    case "Ambient":
                        if (el.Ambient) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        el.Ambient = (result == "1");
                        break;
                    case "NonTextline":
                        if (el.NonTextline) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        el.NonTextline = (result == "1");
                        break;
                    case "IgnoreBodyGestures":
                        if (el.IgnoreBodyGestures) i = 1; else i = 0;
                        result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", i.ToString(), true);
                        if (result == "") return;
                        el.IgnoreBodyGestures = (result == "1");
                        break;
                    case "GUIStyle":
                        result = InputComboBox.GetValue("Please select new value", ME1UnrealObjectInfo.getEnumValues("EConvGUIStyles").Select(nRef => nRef.Name), Pcc.getNameEntry(el.GUIStyleValue));
                        if (result == "") return;
                        el.GUIStyleValue = Pcc.FindNameOrAdd(result);
                        break;
                }
                Dialog.EntryList[n] = el;
                Dialog.Save();
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
                    ME1BioConversation.EntryListReplyListStruct rpe = el.ReplyList[m];
                    result = PromptDialog.Prompt(null, "Please enter new string for \"Paraphrase\"", "ME1Explorer", rpe.Paraphrase.ToString(), true);
                    rpe.Paraphrase = result;
                    result = PromptDialog.Prompt(null, "Please enter new value for \"Index\"", "ME1Explorer", rpe.Index.ToString(), true);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.Index = i;
                    result = PromptDialog.Prompt(null, "Please enter new StringRef value for \"refParaphrase\"", "ME1Explorer", rpe.refParaphrase.ToString(), true);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.refParaphrase = i;
                    result = InputComboBox.GetValue("Please select new value for \"Category\"", ME1UnrealObjectInfo.getEnumValues("EReplyCategory").Select(nRef => nRef.Name), Pcc.getNameEntry(rpe.CategoryValue));
                    if (result == "") return;
                    rpe.CategoryValue = Pcc.FindNameOrAdd(result);
                    el.ReplyList[m] = rpe;
                    Dialog.Save();
                }
                else if (p.Index == 1) //Speaker List
                {
                    result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", el.SpeakerList[m].ToString(), true);
                    if (result == "") return;
                    if (int.TryParse(result, out i))
                    {
                        el.SpeakerList[m] = i;
                        Dialog.Save();
                    }
                }
            }
            #endregion
        }

        private void toEntrysSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                ME1BioConversation.EntryListStuct el = Dialog.EntryList[p.Index];
                int i = 0;
                string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "0", true);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Index] = el;
                Dialog.Save();
            }
            else
            {
                ME1BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                int i = 0;
                string result = PromptDialog.Prompt(null, "Please enter new value", "ME1Explorer", "0", true);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Parent.Index] = el;
                Dialog.Save();
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
                Dialog.Save();
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
                Dialog.Save();
            }
        }

        private void fromReplyListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView2.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.ReplyList.RemoveAt(t.Index);
            Dialog.Save();
        }

        private void fromEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.EntryList.RemoveAt(t.Index);
            Dialog.Save();
        }

        private void entryListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            ME1BioConversation.EntryListStuct el0 = Dialog.EntryList[t.Index];
            ME1BioConversation.EntryListStuct el = new ME1BioConversation.EntryListStuct();
            el.ReplyList = new List<ME1BioConversation.EntryListReplyListStruct>();
            foreach (ME1BioConversation.EntryListReplyListStruct rpe0 in el0.ReplyList)
            {
                ME1BioConversation.EntryListReplyListStruct rpe = new ME1BioConversation.EntryListReplyListStruct();
                rpe.CategoryValue = rpe0.CategoryValue;
                rpe.Index = rpe0.Index;
                rpe.Paraphrase = "" + rpe0.Paraphrase;
                rpe.refParaphrase = rpe0.refParaphrase;
                el.ReplyList.Add(rpe);
            }
            el.SpeakerList = new List<int>();
            foreach (int i in el0.SpeakerList)
                el.SpeakerList.Add(i); 
            el.Ambient = el0.Ambient;
            el.CameraIntimacy = el0.CameraIntimacy;
            el.ConditionalFunc = el0.ConditionalFunc;
            el.ConditionalParam = el0.ConditionalParam;
            el.ExportID = el0.ExportID;
            el.FireConditional = el0.FireConditional;
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
            Dialog.Save();
        }

        private void entrysReplyListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = treeView1.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null && p.Index == 0)
            {
                ME1BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                ME1BioConversation.EntryListReplyListStruct rpe0 = el.ReplyList[t.Index];
                ME1BioConversation.EntryListReplyListStruct rpe = new ME1BioConversation.EntryListReplyListStruct();
                rpe.CategoryValue = rpe0.CategoryValue;
                rpe.Index = rpe0.Index;
                rpe.Paraphrase = "" + rpe0.Paraphrase;
                rpe.refParaphrase = rpe0.refParaphrase;
                el.ReplyList.Add(rpe);
                Dialog.EntryList[p.Parent.Index] = el;
                Dialog.Save();
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
            ME1BioConversation.EntryListStuct el = Dialog.EntryList[Index];
            AddReply ar = new AddReply();
            ar.pcc = Pcc as ME1Package;
            if (SubIndx != -1)
            {
                ME1BioConversation.EntryListReplyListStruct tr = el.ReplyList[SubIndx];
                ar.textBox1.Text = tr.Paraphrase;
                ar.textBox2.Text = tr.refParaphrase.ToString();
                ar.comboBox1.SelectedItem = Pcc.getNameEntry(tr.CategoryValue);
                ar.textBox4.Text = tr.Index.ToString();
            }
            ar.Show();
            while (ar.state == 0) Application.DoEvents();
            ar.Close();
            if (ar.state == -1)
                return;
            if(el.ReplyList == null)
                el.ReplyList = new List<ME1BioConversation.EntryListReplyListStruct>();
            el.ReplyList.Add(ar.res);
            Dialog.EntryList[Index] = el;
            Dialog.Save();
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

        private void manageTLKSetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new TLKManagerWPF().Show();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (Dialog != null && updatedExports.Contains(Dialog.MyIndex))
            {
                //loaded dialog is no longer a dialog
                if (Pcc.getExport(Dialog.MyIndex).ClassName != "BioConversation")
                {
                    listBox1.Items.Clear();
                    listBox2.Items.Clear();
                    listBox3.Items.Clear();
                    listBox4.Items.Clear();
                    listBox5.Items.Clear();
                    treeView1.Nodes.Clear();
                    treeView2.Nodes.Clear();
                }
                else
                {
                    Dialog = new ME1BioConversation(Pcc as ME1Package, Dialog.MyIndex);
                    RefreshTabs();
                }
                updatedExports.Remove(Dialog.MyIndex);
            }
            if (updatedExports.Intersect(Objs).Count() > 0)
            {
                Objs = new List<int>();
                for (int i = 0; i < Pcc.Exports.Count; i++)
                    if (Pcc.Exports[i].ClassName == "BioConversation")
                        Objs.Add(i);
                RefreshCombo();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (Pcc.getExport(i).ClassName == "BioConversation")
                    {
                        Objs = new List<int>();
                        for (int j = 0; j < Pcc.Exports.Count; j++)
                            if (Pcc.Exports[j].ClassName == "BioConversation")
                                Objs.Add(j);
                        RefreshCombo();
                        break;
                    }
                }
            }
        }
    }
}
