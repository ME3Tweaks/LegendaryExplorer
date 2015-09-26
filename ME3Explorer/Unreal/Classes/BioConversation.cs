using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer.Unreal.Classes
{
    public class BioConversation
    {
        public PCCObject pcc;
        public int MyIndex;
        public int Unk1;
        public byte[] Memory;
        public List<PropertyReader.Property> Props;
        public List<int> StartingList;
        public List<int> SpeakerList;
        public List<int> MaleFaceSets;
        public List<int> FemaleFaceSets;
        public List<StageDirectionStruct> StageDirections;
        public List<EntryListStuct> EntryList;
        public List<ReplyListStruct> ReplyList;

        public struct StageDirectionStruct
        {
            public string Text;
            public int StringRef;
        }

        public struct EntryListReplyListStruct
        {
            public string Paraphrase;
            public int Index;
            public int refParaphrase;
            public int CategoryType;
            public int CategoryValue;
        }

        public struct EntryListStuct
        {
            public List<EntryListReplyListStruct> ReplyList;
            public List<int> SpeakerList;
            public int SpeakerIndex;
            public int ListenerIndex;
            public bool Skippable;
            public string Text;
            public int refText;
            public int ConditionalFunc;
            public int ConditionalParam;
            public int StateTransition;
            public int StateTransitionParam;
            public int ExportID;
            public int ScriptIndex;
            public int CameraIntimacy;
            public bool FireConditional;
            public bool Ambient;
            public bool NonTextline;
            public bool IgnoreBodyGestures;
            public bool AlwaysHideSubtitle;
            public int GUIStyleType;
            public int GUIStyleValue;
            public TreeNode ToTree(int MyIndex, TalkFile talk, PCCObject pcc)
            {
                string s = "";
                if (Text.Length != 0)
                    s = Text.Substring(0, Text.Length - 1);
                TreeNode res = new TreeNode(MyIndex + " : " + s + "  " + talk.findDataById(refText));
                TreeNode t = new TreeNode("Reply List");
                for (int i = 0; i < ReplyList.Count; i++)
                {
                    EntryListReplyListStruct e = ReplyList[i];
                    string par = e.Paraphrase;
                    if (par.Length != 0 && par[par.Length - 1] == '\0')
                        par = par.Substring(0, par.Length - 1);
                    t.Nodes.Add(i + " : " 
                                  + par
                                  + " " 
                                  + e.refParaphrase 
                                  + " " 
                                  + talk.findDataById(e.refParaphrase) 
                                  + " " 
                                  + e.Index 
                                  + " " 
                                  + pcc.getNameEntry(e.CategoryType)
                                  + " " 
                                  + pcc.getNameEntry(e.CategoryValue));
                }
                res.Nodes.Add(t);
                TreeNode t2 = new TreeNode("Speaker List");
                for (int i = 0; i < SpeakerList.Count; i++)
                    t2.Nodes.Add(i + " : " + SpeakerList[i]);
                res.Nodes.Add(t2);
                res.Nodes.Add("SpeakerIndex : " + SpeakerIndex);
                res.Nodes.Add("ListenerIndex : " + ListenerIndex);
                res.Nodes.Add("ConditionalFunc : " + ConditionalFunc);
                res.Nodes.Add("ConditionalParam : " + ConditionalParam);
                res.Nodes.Add("StateTransition : " + StateTransition);
                res.Nodes.Add("StateTransitionParam : " + StateTransitionParam);
                res.Nodes.Add("ExportID : " + ExportID);
                res.Nodes.Add("ScriptIndex : " + ScriptIndex);
                res.Nodes.Add("CameraIntimacy : " + CameraIntimacy);
                res.Nodes.Add("Skippable : " + Skippable);
                res.Nodes.Add("FireConditional : " + FireConditional);
                res.Nodes.Add("Ambient : " + Ambient);
                res.Nodes.Add("NonTextline : " + NonTextline);
                res.Nodes.Add("IgnoreBodyGestures : " + IgnoreBodyGestures);
                res.Nodes.Add("AlwaysHideSubtitle : " + AlwaysHideSubtitle);
                res.Nodes.Add("Text : " + Text);
                res.Nodes.Add("refText : " + refText + " " + talk.findDataById(refText));
                res.Nodes.Add("GUIStyle : (" + pcc.getNameEntry(GUIStyleType) + ") " + pcc.getNameEntry(GUIStyleValue));
                return res;
            }
        }

        public struct ReplyListStruct
        {
            public List<int> EntryList;
            public int ListenerIndex;
            public bool Unskippable;
            public bool IsDefaultAction;
            public bool IsMajorDecision;
            public int ReplyTypeType;
            public int ReplyTypeValue;
            public string Text;
            public int refText;
            public int ConditionalFunc;
            public int ConditionalParam;
            public int StateTransition;
            public int StateTransitionParam;
            public int ExportID;
            public int ScriptIndex;
            public int CameraIntimacy;
            public bool FireConditional;
            public bool Ambient;
            public bool NonTextLine;
            public bool IgnoreBodyGestures;
            public bool AlwaysHideSubtitle;
            public int GUIStyleType;
            public int GUIStyleValue;

            public TreeNode ToTree(int MyIndex, TalkFile talk, PCCObject pcc)
            {
                string s = "";
                if (Text.Length != 0)
                    s = Text.Substring(0, Text.Length - 1);
                TreeNode res = new TreeNode(MyIndex + " : " + s + "  " + talk.findDataById(refText));
                TreeNode t = new TreeNode("Entry List");
                for (int i = 0; i < EntryList.Count; i++)
                    t.Nodes.Add(i + " : " + EntryList[i]);
                res.Nodes.Add(t);
                res.Nodes.Add("Listener Index : " + ListenerIndex);
                res.Nodes.Add("Unskippable : " + Unskippable);
                res.Nodes.Add("IsDefaultAction : " + IsDefaultAction);
                res.Nodes.Add("IsMajorDecision : " + IsMajorDecision);
                res.Nodes.Add("ReplyType : (" + pcc.getNameEntry(ReplyTypeType) + ") " + pcc.getNameEntry(ReplyTypeValue));
                res.Nodes.Add("Text : " + Text);
                res.Nodes.Add("refText : " + refText + " " + talk.findDataById(refText));
                res.Nodes.Add("ConditionalFunc : " + ConditionalFunc);
                res.Nodes.Add("ConditionalParam : " + ConditionalParam);
                res.Nodes.Add("StateTransition : " + StateTransition);
                res.Nodes.Add("StateTransitionParam : " + StateTransitionParam);
                res.Nodes.Add("ExportID : " + ExportID);
                res.Nodes.Add("ScriptIndex : " + ScriptIndex);
                res.Nodes.Add("CameraIntimacy : " + CameraIntimacy);
                res.Nodes.Add("FireConditional : " + FireConditional);
                res.Nodes.Add("Ambient : " + Ambient);
                res.Nodes.Add("NonTextline : " + NonTextLine);
                res.Nodes.Add("IgnoreBodyGestures : " + IgnoreBodyGestures);
                res.Nodes.Add("AlwaysHideSubtitle : " + AlwaysHideSubtitle);
                res.Nodes.Add("GUIStyle : (" + pcc.getNameEntry(GUIStyleType) + ") " + pcc.getNameEntry(GUIStyleValue));
                return res;
            }
        }

        public BioConversation(PCCObject Pcc, int Index)
        {
            pcc = Pcc;
            MyIndex = Index;
            Memory = pcc.Exports[Index].Data;
            ReadData();
        }

        private void ReadData()
        {
            BitConverter.IsLittleEndian = true;
            Unk1 = BitConverter.ToInt32(Memory, 0);
            Props = PropertyReader.getPropList(pcc, Memory);
            ReadStartingList();
            ReadEntryList();
            ReadReplyList();
            ReadSpeakerList();
            ReadStageDirections();
            ReadMaleFaceSets();
            ReadFemaleFaceSets();
        }

        public int FindPropByName(string name)
        {
            int res = -1;
            for (int i = 0; i < Props.Count; i++)
                if (pcc.getNameEntry(Props[i].Name) == name)
                    res = i;
            return res;
        }

        private void ReadStartingList()
        {
            StartingList = new List<int>();
            int f = FindPropByName("m_StartingList");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            for (int i = 0; i < count; i++)
                StartingList.Add(BitConverter.ToInt32(buff, 0x1C + i * 4));
        }

        private void ReadEntryList()
        {
            EntryList = new List<EntryListStuct>();
            int f = FindPropByName("m_EntryList");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            int pos = 0x1C;
            for (int i = 0; i < count; i++)
            {
                List<PropertyReader.Property> p = PropertyReader.ReadProp(pcc, buff, pos);
                EntryListStuct e = new EntryListStuct();
                foreach (PropertyReader.Property pp in p)
                {
                    string name = pcc.getNameEntry(pp.Name);
                    switch (name)
                    {
                        case"ReplyListNew":
                            byte[] buff2 = pp.raw;
                            int count2 = BitConverter.ToInt32(buff2, 0x18);
                            int pos2 = 0x1C;
                            e.ReplyList = new List<EntryListReplyListStruct>();
                            for (int j = 0; j < count2; j++)
                            {
                                List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, buff2, pos2);
                                EntryListReplyListStruct r = new EntryListReplyListStruct();
                                foreach(PropertyReader.Property ppp in p2)
                                    switch (pcc.getNameEntry(ppp.Name))
                                    {
                                        case "sParaphrase":
                                            r.Paraphrase = ppp.Value.StringValue; 
                                            break;
                                        case "nIndex":
                                            r.Index = ppp.Value.IntValue;
                                            break;
                                        case "srParaphrase":
                                            r.refParaphrase = ppp.Value.IntValue;
                                            break;
                                        case "Category":
                                            r.CategoryType = BitConverter.ToInt32(ppp.raw, 24);
                                            r.CategoryValue = BitConverter.ToInt32(ppp.raw, 32);
                                            break;
                                    }
                                e.ReplyList.Add(r);
                                pos2 = p2[p2.Count - 1].offend;
                            }
                            break;
                        case "aSpeakerList":
                            buff2 = pp.raw;
                            count2 = BitConverter.ToInt32(buff2, 0x18);
                            e.SpeakerList = new List<int>();
                            for (int j = 0; j < count2; j++)
                                e.SpeakerList.Add(BitConverter.ToInt32(buff2, 0x1C + 4 * j));
                            break;
                        case "nSpeakerIndex":
                            e.SpeakerIndex = pp.Value.IntValue;
                            break;
                        case "nListenerIndex":
                            e.ListenerIndex = pp.Value.IntValue;
                            break;
                        case "bSkippable":
                            e.Skippable = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "sText":
                            e.Text = pp.Value.StringValue;
                            break;
                        case "srText":
                            e.refText = pp.Value.IntValue;
                            break;
                        case "nConditionalFunc":
                            e.ConditionalFunc = pp.Value.IntValue;
                            break;
                        case "nConditionalParam":
                            e.ConditionalParam = pp.Value.IntValue;
                            break;
                        case "nStateTransition":
                            e.StateTransition = pp.Value.IntValue;
                            break;
                        case "nStateTransitionParam":
                            e.StateTransitionParam = pp.Value.IntValue;
                            break;
                        case "nExportID":
                            e.ExportID = pp.Value.IntValue;
                            break;
                        case "nScriptIndex":
                            e.ScriptIndex = pp.Value.IntValue;
                            break;
                        case "nCameraIntimacy":
                            e.CameraIntimacy = pp.Value.IntValue;
                            break;
                        case "bFireConditional":
                            e.FireConditional = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bAmbient":
                            e.Ambient = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bNonTextLine":
                            e.NonTextline = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bIgnoreBodyGestures":
                            e.IgnoreBodyGestures = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bAlwaysHideSubtitle":
                            e.AlwaysHideSubtitle = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "eGUIStyle":
                            e.GUIStyleType = BitConverter.ToInt32(pp.raw, 24);
                            e.GUIStyleValue = BitConverter.ToInt32(pp.raw, 32);
                            break;

                    }
                }
                EntryList.Add(e);
                pos = p[p.Count - 1].offend;
            }
        }

        private void ReadReplyList()
        {
            ReplyList = new List<ReplyListStruct>();
            int f = FindPropByName("m_ReplyList");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            int pos = 0x1C;
            for (int i = 0; i < count; i++)
            {
                List<PropertyReader.Property> p = PropertyReader.ReadProp(pcc, buff, pos);
                ReplyListStruct e = new ReplyListStruct();
                foreach (PropertyReader.Property pp in p)
                {
                    string name = pcc.getNameEntry(pp.Name);
                    switch (name)
                    {
                        case "EntryList":
                            byte[] buff2 = pp.raw;
                            int count2 = BitConverter.ToInt32(buff2, 0x18);
                            int pos2 = 0x1C;
                            e.EntryList = new List<int>();
                            for (int j = 0; j < count2; j++)
                            {
                                e.EntryList.Add(BitConverter.ToInt32(buff2, pos2));
                                pos2 += 4;
                            }
                            break;
                        case "nListenerIndex":
                            e.ListenerIndex = pp.Value.IntValue;
                            break;                        
                        case "bUnskippable":
                            e.Unskippable = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bIsDefaultAction":
                            e.IsDefaultAction = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bIsMajorDecision":
                            e.IsMajorDecision = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "ReplyType":
                            e.ReplyTypeType = BitConverter.ToInt32(pp.raw, 24);
                            e.ReplyTypeValue = BitConverter.ToInt32(pp.raw, 32);
                            break;
                        case "sText":
                            e.Text = pp.Value.StringValue;
                            break;
                        case "srText":
                            e.refText = pp.Value.IntValue;
                            break;
                        case "nConditionalFunc":
                            e.ConditionalFunc = pp.Value.IntValue;
                            break;
                        case "nConditionalParam":
                            e.ConditionalParam = pp.Value.IntValue;
                            break;
                        case "nStateTransition":
                            e.StateTransition = pp.Value.IntValue;
                            break;
                        case "nStateTransitionParam":
                            e.StateTransitionParam = pp.Value.IntValue;
                            break;
                        case "nExportID":
                            e.ExportID = pp.Value.IntValue;
                            break;
                        case "nScriptIndex":
                            e.ScriptIndex = pp.Value.IntValue;
                            break;
                        case "nCameraIntimacy":
                            e.CameraIntimacy = pp.Value.IntValue;
                            break;
                        case "bFireConditional":
                            e.FireConditional = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bAmbient":
                            e.Ambient = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bNonTextLine":
                            e.NonTextLine = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bIgnoreBodyGestures":
                            e.IgnoreBodyGestures = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "bAlwaysHideSubtitle":
                            e.AlwaysHideSubtitle = (pp.raw[pp.raw.Length - 1] == 1);
                            break;
                        case "eGUIStyle":
                            e.GUIStyleType = BitConverter.ToInt32(pp.raw, 24);
                            e.GUIStyleValue = BitConverter.ToInt32(pp.raw, 32);
                            break;

                    }
                }
                ReplyList.Add(e);
                pos = p[p.Count - 1].offend;
            }
        }

        private void ReadSpeakerList()
        {
            SpeakerList = new List<int>();
            int f = FindPropByName("m_aSpeakerList");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            for (int i = 0; i < count; i++)
                SpeakerList.Add(BitConverter.ToInt32(buff, 0x1C + i * 8));
        }

        private void ReadStageDirections()
        {
            StageDirections = new List<StageDirectionStruct>();
            int f = FindPropByName("m_aStageDirections");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            int pos = 0x1C;
            for (int i = 0; i < count; i++)
            {
                List<PropertyReader.Property> p = PropertyReader.ReadProp(pcc, buff, pos);
                StageDirectionStruct sd = new StageDirectionStruct();
                sd.Text = p[0].Value.StringValue;
                sd.StringRef = p[1].Value.IntValue;
                StageDirections.Add(sd);
                pos = p[p.Count - 1].offend;
            }
        }

        private void ReadMaleFaceSets()
        {
            MaleFaceSets = new List<int>();
            int f = FindPropByName("m_aMaleFaceSets");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            for (int i = 0; i < count; i++)
                MaleFaceSets.Add(BitConverter.ToInt32(buff, 0x1C + i * 4));
        }

        private void ReadFemaleFaceSets()
        {
            FemaleFaceSets = new List<int>();
            int f = FindPropByName("m_aFemaleFaceSets");
            if (f == -1)
                return;
            byte[] buff = Props[f].raw;
            int count = BitConverter.ToInt32(buff, 0x18);
            for (int i = 0; i < count; i++)
                FemaleFaceSets.Add(BitConverter.ToInt32(buff, 0x1C + i * 4));
        }

        public int FindName(string s)
        {
            int res = -1;
            for (int i = 0; i < pcc.Names.Count; i++)
                if (pcc.Names[i] == s)
                    res = i;
            return res;
        }

        public int WriteStrProperty(MemoryStream m, string name, string text)
        {
            if (text != "" && text[text.Length - 1] != 0)
                text += '\0';
            int sizetext = text.Length * 2 + 4;
            int NAME_name = FindName(name);
            int NAME_StrProperty = FindName("StrProperty");
            m.Write(BitConverter.GetBytes(NAME_name), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_StrProperty), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(sizetext), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(-text.Length), 0, 4);
            foreach (char c in text)
                m.Write(BitConverter.GetBytes((Int16)c), 0, 2);            
            return 24 + sizetext;
        }

        public int WriteIntProperty(MemoryStream m, string name, int value, string type = "IntProperty")
        {
            int NAME_name = FindName(name);
            int NAME_type = FindName(type);
            m.Write(BitConverter.GetBytes(NAME_name), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_type), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)4), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)value), 0, 4);
            return 28;
        }

        public int WriteByteProperty(MemoryStream m, string name, int valuetype, int valuename)
        {
            int NAME_name = FindName(name);
            int NAME_type = FindName("ByteProperty");
            m.Write(BitConverter.GetBytes(NAME_name), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_type), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)8), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)valuetype), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)valuename), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            return 40;
        }

        public int WriteIntArrayProperty(MemoryStream m, string name, List<int> value)
        {
            int NAME_name = FindName(name);
            int NAME_type = FindName("ArrayProperty");
            m.Write(BitConverter.GetBytes(NAME_name), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_type), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(value.Count * 4 + 4), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(value.Count), 0, 4);
            foreach(int i in value)
                m.Write(BitConverter.GetBytes(i), 0, 4);
            return 28 + value.Count * 4;
        }

        public int WriteBoolProperty(MemoryStream m, string name, bool value, string type = "BoolProperty")
        {
            int NAME_name = FindName(name);
            int NAME_type = FindName(type);
            m.Write(BitConverter.GetBytes(NAME_name), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_type), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            if (value)
                m.WriteByte(1);
            else
                m.WriteByte(0);
            return 25;
        }

        public int WriteNone(MemoryStream m)
        {
            int NAME_None = FindName("None");
            m.Write(BitConverter.GetBytes(NAME_None), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            return 8;
        }

        public int WriteStageDirection(MemoryStream m, StageDirectionStruct sd)
        {
            int size = 0;            
            size += WriteStrProperty(m, "sText", sd.Text);
            size += WriteIntProperty(m, "srStrRef", sd.StringRef, "StringRefProperty");
            size += WriteNone(m);
            return size;
        }

        public int WriteEntryList(MemoryStream m, EntryListStuct el)
        {
            int size = 0;            
            size += WriteEntryReplyList(m, el.ReplyList);
            size += WriteIntArrayProperty(m, "aSpeakerList", el.SpeakerList);
            size += WriteIntProperty(m, "nSpeakerIndex", el.SpeakerIndex);
            size += WriteIntProperty(m, "nListenerIndex", el.ListenerIndex);
            size += WriteBoolProperty(m, "bSkippable", el.Skippable);
            size += WriteStrProperty(m, "sText", el.Text);
            size += WriteIntProperty(m, "srText", el.refText, "StringRefProperty");
            size += WriteIntProperty(m, "nConditionalFunc", el.ConditionalFunc);
            size += WriteIntProperty(m, "nConditionalParam", el.ConditionalParam);
            size += WriteIntProperty(m, "nStateTransition", el.StateTransition);
            size += WriteIntProperty(m, "nStateTransitionParam", el.StateTransitionParam);
            size += WriteIntProperty(m, "nExportID", el.ExportID);
            size += WriteIntProperty(m, "nScriptIndex", el.ScriptIndex);
            size += WriteIntProperty(m, "nCameraIntimacy", el.CameraIntimacy);
            size += WriteBoolProperty(m, "bFireConditional", el.FireConditional);
            size += WriteBoolProperty(m, "bAmbient", el.Ambient);
            size += WriteBoolProperty(m, "bNonTextLine", el.NonTextline);
            size += WriteBoolProperty(m, "bIgnoreBodyGestures", el.IgnoreBodyGestures);
            size += WriteBoolProperty(m, "bAlwaysHideSubtitle", el.AlwaysHideSubtitle);
            size += WriteByteProperty(m, "eGUIStyle", el.GUIStyleType, el.GUIStyleValue);
            size += WriteNone(m);
            return size;
        }

        public int WriteEntryReplyList(MemoryStream m, List<EntryListReplyListStruct> list)
        {
            int size = 28;
            int tmp1 = (int)m.Position;
            int NAME_ArrayProperty = FindName("ArrayProperty");
            int NAME_ReplyListNew = FindName("ReplyListNew");
            m.Write(BitConverter.GetBytes(NAME_ReplyListNew), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(BitConverter.GetBytes(NAME_ArrayProperty), 0, 4);
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            m.Write(new byte[8], 0, 8);//size dummy
            int arraysize = 4;
            int tmpsize = 0;
            m.Write(BitConverter.GetBytes(list.Count), 0, 4);
            foreach (EntryListReplyListStruct el in list)
            {
                tmpsize += WriteStrProperty(m, "sParaphrase", el.Paraphrase);
                tmpsize += WriteIntProperty(m, "nIndex", el.Index);
                tmpsize += WriteIntProperty(m, "srParaphrase", el.refParaphrase, "StringRefProperty");
                tmpsize += WriteByteProperty(m, "Category", el.CategoryType, el.CategoryValue);
                tmpsize += WriteNone(m);
                arraysize += tmpsize;
                size += tmpsize;
                tmpsize = 0;
            }
            int tmp2 = (int)m.Position;
            m.Seek(tmp1 + 0x10, 0);
            m.Write(BitConverter.GetBytes(arraysize), 0, 4);
            m.Seek(tmp2, 0);
            return size;
        }

        public int WriteReplyList(MemoryStream m, ReplyListStruct rp)
        {
            int size = 0;
            size += WriteIntArrayProperty(m, "EntryList", rp.EntryList);
            size += WriteIntProperty(m, "nListenerIndex", rp.ListenerIndex);
            size += WriteBoolProperty(m, "bUnskippable", rp.Unskippable);
            size += WriteBoolProperty(m, "bIsDefaultAction", rp.IsDefaultAction);
            size += WriteBoolProperty(m, "bIsMajorDecision", rp.IsMajorDecision);
            size += WriteByteProperty(m, "ReplyType", rp.ReplyTypeType, rp.ReplyTypeValue);
            size += WriteStrProperty(m, "sText", rp.Text);
            size += WriteIntProperty(m, "srText", rp.refText, "StringRefProperty");
            size += WriteIntProperty(m, "nConditionalFunc", rp.ConditionalFunc);
            size += WriteIntProperty(m, "nConditionalParam", rp.ConditionalParam);
            size += WriteIntProperty(m, "nStateTransition", rp.StateTransition);
            size += WriteIntProperty(m, "nStateTransitionParam", rp.StateTransitionParam);
            size += WriteIntProperty(m, "nExportID", rp.ExportID);
            size += WriteIntProperty(m, "nScriptIndex", rp.ScriptIndex);
            size += WriteIntProperty(m, "nCameraIntimacy", rp.CameraIntimacy);
            size += WriteBoolProperty(m, "bFireConditional", rp.FireConditional);
            size += WriteBoolProperty(m, "bAmbient", rp.Ambient);
            size += WriteBoolProperty(m, "bNonTextLine", rp.NonTextLine);
            size += WriteBoolProperty(m, "bIgnoreBodyGestures", rp.IgnoreBodyGestures);
            size += WriteBoolProperty(m, "bAlwaysHideSubtitle", rp.AlwaysHideSubtitle);
            size += WriteByteProperty(m, "eGUIStyle", rp.GUIStyleType, rp.GUIStyleValue);
            size += WriteNone(m);
            return size;
        }

        public void Save()
        {
            int tmp1, tmp2, size;
            if (pcc == null)
                return;
            MemoryStream m = new MemoryStream();
            m.Write(BitConverter.GetBytes(Unk1), 0, 4);
            foreach(PropertyReader.Property p in Props)
                switch (pcc.getNameEntry(p.Name))
                {
                    case "m_StartingList":
                        m.Write(p.raw, 0, 0x10);
                        m.Write(BitConverter.GetBytes(StartingList.Count * 4 + 4), 0, 4);
                        m.Write(BitConverter.GetBytes((int)0), 0, 4);
                        m.Write(BitConverter.GetBytes(StartingList.Count), 0, 4);
                        foreach (int i in StartingList)
                            m.Write(BitConverter.GetBytes(i), 0, 4);
                        break;
                    case "m_EntryList":
                        tmp1 = (int)m.Position;
                        m.Write(p.raw, 0, 0x10);
                        m.Write(new byte[8], 0, 8);
                        m.Write(BitConverter.GetBytes(EntryList.Count), 0, 4);
                        size = 4;
                        foreach (EntryListStuct el in EntryList)
                            size += WriteEntryList(m, el);
                        tmp2 = (int)m.Position;
                        m.Seek(tmp1 + 0x10, 0);
                        m.Write(BitConverter.GetBytes(size), 0, 4);
                        m.Seek(tmp2, 0);
                        break;
                    case "m_ReplyList":
                        tmp1 = (int)m.Position;
                        m.Write(p.raw, 0, 0x10);
                        m.Write(new byte[8], 0, 8);
                        m.Write(BitConverter.GetBytes(ReplyList.Count), 0, 4);
                        size = 4;
                        foreach (ReplyListStruct rp in ReplyList)
                            size += WriteReplyList(m, rp);
                        tmp2 = (int)m.Position;
                        m.Seek(tmp1 + 0x10, 0);
                        m.Write(BitConverter.GetBytes(size), 0, 4);
                        m.Seek(tmp2, 0);
                        break;
                    case "m_aSpeakerList":
                        m.Write(p.raw, 0, 0x10);
                        m.Write(BitConverter.GetBytes(SpeakerList.Count * 8 + 4), 0, 4);
                        m.Write(BitConverter.GetBytes((int)0), 0, 4);
                        m.Write(BitConverter.GetBytes(SpeakerList.Count), 0, 4);
                        foreach (int i in SpeakerList)
                            m.Write(BitConverter.GetBytes((long)i), 0, 8);
                        break;
                    case "m_aFemaleFaceSets":
                        m.Write(p.raw, 0, 0x10);
                        m.Write(BitConverter.GetBytes(FemaleFaceSets.Count * 4 + 4), 0, 4);
                        m.Write(BitConverter.GetBytes((int)0), 0, 4);
                        m.Write(BitConverter.GetBytes(FemaleFaceSets.Count), 0, 4);
                        foreach (int i in FemaleFaceSets)
                            m.Write(BitConverter.GetBytes(i), 0, 4);
                        break;
                    case "m_aMaleFaceSets":
                        m.Write(p.raw, 0, 0x10);
                        m.Write(BitConverter.GetBytes(MaleFaceSets.Count * 4 + 4), 0, 4);
                        m.Write(BitConverter.GetBytes((int)0), 0, 4);
                        m.Write(BitConverter.GetBytes(MaleFaceSets.Count), 0, 4);
                        foreach (int i in MaleFaceSets)
                            m.Write(BitConverter.GetBytes(i), 0, 4);
                        break;
                    case "m_aStageDirections":
                        tmp1 = (int)m.Position;
                        m.Write(p.raw, 0, 0x10);
                        m.Write(new byte[8], 0, 8);
                        m.Write(BitConverter.GetBytes(StageDirections.Count), 0, 4);
                        size = 4;
                        foreach (StageDirectionStruct sd in StageDirections)
                            size += WriteStageDirection(m, sd);
                        tmp2 = (int)m.Position;
                        m.Seek(tmp1 + 0x10, 0);
                        m.Write(BitConverter.GetBytes(size), 0, 4);
                        m.Seek(tmp2, 0);
                        break;
                    default:
                        m.Write(p.raw, 0, p.raw.Length);
                        break;
                }
            m.Write(BitConverter.GetBytes((int)0), 0, 4);
            pcc.Exports[MyIndex].Data = m.ToArray();
            pcc.altSaveToFile(pcc.pccFileName, true);
        }
    }
}
