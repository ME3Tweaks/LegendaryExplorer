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
using ME3Explorer.Packages;

namespace ME3Explorer.DialogEditor
{
    public partial class DialogEditor : WinFormsBase
    {
        public ME3BioConversation Dialog;
        public List<IExportEntry> Objs;

        public DialogEditor()
        {
            InitializeComponent();
        }

        private void openPCCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "*.pcc|*.pcc" };
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        public void LoadFile(string fileName)
        {
            try
            {
                LoadME3Package(fileName);
                
                RefreshCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        public void RefreshCombo()
        {
            Objs = new List<IExportEntry>();
            IReadOnlyList<IExportEntry> Exports = pcc.Exports;
            foreach (var exp in Exports)
            {
                if (exp.ClassName == "BioConversation")
                {
                    Objs.Add(exp);
                }
            }
            bioConversationComboBox.Items.Clear();
            foreach (var exp in Objs)
                bioConversationComboBox.Items.Add("#" + exp.Index + " : " + exp.ObjectName);
            if (bioConversationComboBox.Items.Count != 0)
                bioConversationComboBox.SelectedIndex = 0;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            int n = bioConversationComboBox.SelectedIndex;
            if (n == -1)
                return;
            Dialog = new ME3BioConversation(Objs[n] as ME3ExportEntry);
            RefreshTabs();
            RefreshVisualizer();
        }

        private void RefreshVisualizer()
        {
            for (int i = 0; i < Dialog.StartingList.Count; i++)
			{
                //dialogVis.addNode(new DlgStart(i, i * 100, 0, Dialog, dialogVis));
			}
        }

        public void RefreshTabs()
        {
            if (Dialog == null)
                return;
            startingListBox.Items.Clear();
            speakerListBox.Items.Clear();
            stageDirectionsListBox.Items.Clear();
            maleFaceSetsListBox.Items.Clear();
            femaleFaceSetsListBox.Items.Clear();
            entryListTreeView.Nodes.Clear();
            replyListTreeView.Nodes.Clear();
            int count = 0;
            foreach (int i in Dialog.StartingList)
                startingListBox.Items.Add((count++) + " : " + i);
            count = 0;
            foreach (ME3BioConversation.EntryListStuct e in Dialog.EntryList)
                entryListTreeView.Nodes.Add(e.ToTree(count++, pcc as ME3Package));
            count = 0;
            foreach (ME3BioConversation.ReplyListStruct r in Dialog.ReplyList)
                replyListTreeView.Nodes.Add(r.ToTree(count++, pcc as ME3Package));
            count = 0;
            foreach (int i in Dialog.SpeakerList)
                speakerListBox.Items.Add((count++) + " : " + i + " , " + pcc.getNameEntry(i));
            count = 0;
            foreach (ME3BioConversation.StageDirectionStruct sd in Dialog.StageDirections)
                stageDirectionsListBox.Items.Add((count++) + " : " + sd.Text.Substring(0, sd.Text.Length - 1) + " , " + sd.StringRef + " , " + ME3TalkFiles.findDataById(sd.StringRef));
            count = 0;
            foreach (int i in Dialog.MaleFaceSets)
                maleFaceSetsListBox.Items.Add((count++) + " : " + i);
            count = 0;
            foreach (int i in Dialog.FemaleFaceSets)
                femaleFaceSetsListBox.Items.Add((count++) + " : " + i);
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Dialog == null)
                return;
            Dialog.Save();
            pcc.save();
            MessageBox.Show("Done.");
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = startingListBox.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.StartingList[n].ToString(), 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.StartingList[n] = i;
                Dialog.Save();
            }
        }

        private void listBox2_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = speakerListBox.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new name entry", "ME3Explorer", Dialog.SpeakerList[n].ToString(), 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i) && pcc.isName(i))
            {
                Dialog.SpeakerList[n] = i;
                Dialog.Save();
            }
        }

        private void listBox4_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = maleFaceSetsListBox.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.MaleFaceSets[n].ToString(), 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.MaleFaceSets[n] = i;
                Dialog.Save();
            }
        }

        private void listBox5_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = femaleFaceSetsListBox.SelectedIndex) == -1)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.FemaleFaceSets[n].ToString(), 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.FemaleFaceSets[n] = i;
                Dialog.Save();
            }
        }

        private void toStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.StartingList.Add(i);
                Dialog.Save();
            }
        }

        private void toSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i) && pcc.isName(i))
            {
                Dialog.SpeakerList.Add(i);
                Dialog.Save();
            }
        }

        private void toMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.MaleFaceSets.Add(i);
                Dialog.Save();
            }
        }

        private void toFemaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null || Dialog == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "", 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
            {
                Dialog.FemaleFaceSets.Add(i);
                Dialog.Save();
            }
        }

        private void fromStartingListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = startingListBox.SelectedIndex) == -1)
                return;
            Dialog.StartingList.RemoveAt(n);
            Dialog.Save();
        }

        private void fromSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = speakerListBox.SelectedIndex) == -1)
                return;
            Dialog.SpeakerList.RemoveAt(n);
            Dialog.Save();
        }

        private void fromMaleFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = maleFaceSetsListBox.SelectedIndex) == -1)
                return;
            Dialog.MaleFaceSets.RemoveAt(n);
            Dialog.Save();
        }

        private void fromFemalFaceSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = femaleFaceSetsListBox.SelectedIndex) == -1)
                return;
            Dialog.FemaleFaceSets.RemoveAt(n);
            Dialog.Save();
        }

        private void listBox3_DoubleClick(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = stageDirectionsListBox.SelectedIndex) == -1)
                return;
            ME3BioConversation.StageDirectionStruct sd = Dialog.StageDirections[n];
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string", "ME3Explorer", Dialog.StageDirections[n].Text, 0, 0);
            if (result == "")
                return;
            sd.Text = result;
            result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.StageDirections[n].StringRef.ToString(), 0, 0);
            if (result == "")
                return;
            if (int.TryParse(result, out int i))
                sd.StringRef = i;
            Dialog.StageDirections[n] = sd;
            Dialog.Save();
        }

        private void stageDirectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (pcc == null || Dialog == null || (n = stageDirectionsListBox.SelectedIndex) == -1)
                return;
            Dialog.StageDirections.Add(new ME3BioConversation.StageDirectionStruct
            {
                Text = Dialog.StageDirections[n].Text,
                StringRef = Dialog.StageDirections[n].StringRef
            });
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
            ME3BioConversation.ReplyListStruct rp = Dialog.ReplyList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
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
                        result = InputComboBox.GetValue("Please select new value", ME3UnrealObjectInfo.getEnumValues("EReplyTypes"), pcc.getNameEntry(Dialog.ReplyList[n].ReplyTypeValue));
                        if (result == "") return;
                        rp.ReplyTypeValue= pcc.FindNameOrAdd(result);
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
                        result = InputComboBox.GetValue("Please select new value", ME3UnrealObjectInfo.getEnumValues("EConvGUIStyles"), pcc.getNameEntry(Dialog.ReplyList[n].GUIStyleValue));
                        if (result == "") return;
                        rp.GUIStyleValue = pcc.FindNameOrAdd(result);
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
                result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", Dialog.ReplyList[n].EntryList[m].ToString(), 0, 0);
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
            TreeNode t = replyListTreeView.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                ME3BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Index];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (int.TryParse(result, out int i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Index] = rp;
                Dialog.Save();
            }
            else
            {
                ME3BioConversation.ReplyListStruct rp = Dialog.ReplyList[p.Parent.Index];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (int.TryParse(result, out int i)) rp.EntryList.Add(i);
                Dialog.ReplyList[p.Parent.Index] = rp;
                Dialog.Save();
            }
        }

        private void fromReplysEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = replyListTreeView.SelectedNode;
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
            TreeNode t = replyListTreeView.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            ME3BioConversation.ReplyListStruct src = Dialog.ReplyList[t.Index];
            ME3BioConversation.ReplyListStruct rp = new ME3BioConversation.ReplyListStruct
            {
                EntryList = src.EntryList.Clone(),
                ListenerIndex = src.ListenerIndex,
                Unskippable = src.Unskippable,
                IsDefaultAction = src.IsDefaultAction,
                IsMajorDecision = src.IsMajorDecision,
                ReplyTypeType = src.ReplyTypeType,
                ReplyTypeValue = src.ReplyTypeValue,
                Text = src.Text,
                refText = src.refText,
                ConditionalFunc = src.ConditionalFunc,
                ConditionalParam = src.ConditionalParam,
                StateTransition = src.StateTransition,
                StateTransitionParam = src.StateTransitionParam,
                ExportID = src.ExportID,
                ScriptIndex = src.ScriptIndex,
                CameraIntimacy = src.CameraIntimacy,
                FireConditional = src.FireConditional,
                Ambient = src.Ambient,
                NonTextLine = src.NonTextLine,
                IgnoreBodyGestures = src.IgnoreBodyGestures,
                AlwaysHideSubtitle = src.AlwaysHideSubtitle,
                GUIStyleType = src.GUIStyleType,
                GUIStyleValue = src.GUIStyleValue
            };
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
            ME3BioConversation.EntryListStuct el = Dialog.EntryList[n];
            #region MainProps
            if (p.Parent == null)//MainProps
            {
                string propname = t.Text.Split(':')[0].Trim();
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
                        result = InputComboBox.GetValue("Please select new value", ME3UnrealObjectInfo.getEnumValues("EConvGUIStyles"), pcc.getNameEntry(el.GUIStyleValue));
                        if (result == "") return;
                        el.GUIStyleValue = pcc.FindNameOrAdd(result);
                        break;
                    default:
                        return;
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
                    ME3BioConversation.EntryListReplyListStruct rpe = el.ReplyList[m];
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new string for \"Paraphrase\"", "ME3Explorer", rpe.Paraphrase.ToString(), 0, 0);
                    rpe.Paraphrase = result;
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value for \"Index\"", "ME3Explorer", rpe.Index.ToString(), 0, 0);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.Index = i;
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new StringRef value for \"refParaphrase\"", "ME3Explorer", rpe.refParaphrase.ToString(), 0, 0);
                    if (result == "") return;
                    if (int.TryParse(result, out i)) rpe.refParaphrase = i;
                    result = InputComboBox.GetValue("Please select new value for \"Category\"", ME3UnrealObjectInfo.getEnumValues("EReplyCategory"), pcc.getNameEntry(rpe.CategoryValue));
                    if (result == "") return;
                    rpe.CategoryValue = pcc.FindNameOrAdd(result);
                    el.ReplyList[m] = rpe;
                    Dialog.Save();
                }
                if (p.Index == 1) //Speaker List
                {
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", el.SpeakerList[m].ToString(), 0, 0);
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
            TreeNode t = entryListTreeView.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent == null)
            {
                ME3BioConversation.EntryListStuct el = Dialog.EntryList[p.Index];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out int i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Index] = el;
                Dialog.Save();
            }
            else
            {
                ME3BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", "0", 0, 0);
                if (result == "") return;
                if (el.SpeakerList == null)
                    el.SpeakerList = new List<int>();
                if (int.TryParse(result, out int i)) el.SpeakerList.Add(i);
                Dialog.EntryList[p.Parent.Index] = el;
                Dialog.Save();
            }
        }

        private void fromEntrysSpeakerListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = entryListTreeView.SelectedNode;
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
            TreeNode t = entryListTreeView.SelectedNode;
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
            TreeNode t = replyListTreeView.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.ReplyList.RemoveAt(t.Index);
            Dialog.Save();
        }

        private void fromEntryListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = entryListTreeView.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            Dialog.EntryList.RemoveAt(t.Index);
            Dialog.Save();
        }

        private void entryListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = entryListTreeView.SelectedNode;
            if (t == null || t.Parent != null)
                return;
            ME3BioConversation.EntryListStuct el0 = Dialog.EntryList[t.Index];
            ME3BioConversation.EntryListStuct el = new ME3BioConversation.EntryListStuct
            {
                ReplyList = new List<ME3BioConversation.EntryListReplyListStruct>(),
                SpeakerList = el0.SpeakerList.Clone(),
                AlwaysHideSubtitle = el0.AlwaysHideSubtitle,
                Ambient = el0.Ambient,
                CameraIntimacy = el0.CameraIntimacy,
                ConditionalFunc = el0.ConditionalFunc,
                ConditionalParam = el0.ConditionalParam,
                ExportID = el0.ExportID,
                FireConditional = el0.FireConditional,
                GUIStyleType = el0.GUIStyleType,
                GUIStyleValue = el0.GUIStyleValue,
                IgnoreBodyGestures = el0.IgnoreBodyGestures,
                ListenerIndex = el0.ListenerIndex,
                NonTextline = el0.NonTextline,
                refText = el0.refText,
                ScriptIndex = el0.ScriptIndex,
                Skippable = el0.Skippable,
                SpeakerIndex = el0.SpeakerIndex,
                StateTransition = el0.StateTransition,
                StateTransitionParam = el0.StateTransitionParam,
                Text = el0.Text
            };
            foreach (ME3BioConversation.EntryListReplyListStruct rpe0 in el0.ReplyList)
            {
                ME3BioConversation.EntryListReplyListStruct rpe = new ME3BioConversation.EntryListReplyListStruct
                {
                    CategoryType = rpe0.CategoryType,
                    CategoryValue = rpe0.CategoryValue,
                    Index = rpe0.Index,
                    Paraphrase = rpe0.Paraphrase,
                    refParaphrase = rpe0.refParaphrase
                };
                el.ReplyList.Add(rpe);
            }
            Dialog.EntryList.Add(el);
            Dialog.Save();
        }

        private void entrysReplyListEntryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = entryListTreeView.SelectedNode;
            if (t == null || t.Parent == null)
                return;
            TreeNode p = t.Parent;
            if (p.Parent != null && p.Index == 0)
            {
                ME3BioConversation.EntryListStuct el = Dialog.EntryList[p.Parent.Index];
                ME3BioConversation.EntryListReplyListStruct rpe0 = el.ReplyList[t.Index];
                ME3BioConversation.EntryListReplyListStruct rpe = new ME3BioConversation.EntryListReplyListStruct
                {
                    CategoryType = rpe0.CategoryType,
                    CategoryValue = rpe0.CategoryValue,
                    Index = rpe0.Index,
                    Paraphrase = rpe0.Paraphrase,
                    refParaphrase = rpe0.refParaphrase
                };
                el.ReplyList.Add(rpe);
                Dialog.EntryList[p.Parent.Index] = el;
                Dialog.Save();
            }
        }

        private void loadDifferentTLKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TlkManager tm = new TlkManager();
            tm.InitTlkManager();
            tm.Show();
        }

        private void toEntrysReplyListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode t = entryListTreeView.SelectedNode;
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
            ME3BioConversation.EntryListStuct el = Dialog.EntryList[Index];
            AddReply ar = new AddReply
            {
                pcc = pcc as ME3Package
            };
            if (SubIndx != -1)
            {
                ME3BioConversation.EntryListReplyListStruct tr = el.ReplyList[SubIndx];
                ar.textBox1.Text = tr.Paraphrase;
                ar.textBox2.Text = tr.refParaphrase.ToString();
                ar.comboBox1.SelectedItem = pcc.getNameEntry(tr.CategoryValue);
                ar.textBox4.Text = tr.Index.ToString();
            }
            ar.Show();
            while (ar.state == 0) Application.DoEvents();
            ar.Close();
            if (ar.state == -1)
                return;
            if(el.ReplyList == null)
                el.ReplyList = new List<ME3BioConversation.EntryListReplyListStruct>();
            el.ReplyList.Add(ar.res);
            Dialog.EntryList[Index] = el;
            Dialog.Save();
        }

        private void entriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            entryListTreeView.ExpandAll();
        }

        private void repliesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            replyListTreeView.ExpandAll();
        }

        private void entriesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            entryListTreeView.CollapseAll();
        }

        private void repliesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            replyListTreeView.CollapseAll();
        }

        private void DialogEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void DialogEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList().Where(f => f.EndsWith(".pcc")).ToList();
            if (DroppedFiles.Count > 0)
            {
                LoadFile(DroppedFiles[0]);
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (Dialog != null && updatedExports.Contains(Dialog.export.Index))
            {
                //loaded dialog is no longer a dialog
                if (Dialog.export.ClassName != "BioConversation")
                {
                    startingListBox.Items.Clear();
                    speakerListBox.Items.Clear();
                    stageDirectionsListBox.Items.Clear();
                    maleFaceSetsListBox.Items.Clear();
                    femaleFaceSetsListBox.Items.Clear();
                    entryListTreeView.Nodes.Clear();
                    replyListTreeView.Nodes.Clear();
                }
                else
                {
                    Dialog = new ME3BioConversation(Dialog.export);
                    RefreshTabs();
                }
                updatedExports.Remove(Dialog.export.Index);
            }
            if (updatedExports.Intersect(Objs.Select(x => x.Index)).Any())
            {
                RefreshCombo();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (pcc.getExport(i).ClassName == "BioConversation")
                    {
                        RefreshCombo();
                        break;
                    }
                }
            }
        }
    }
}
