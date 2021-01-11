using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3ExplorerCore.Dialogue
{
    //Contains nested conversation structure.
    // - InterpData
    //Extended Nested Collections:
    // - Speakers have FaceFX Objects
    // - DialogueNodeExtended has InterpData, WwiseStream_M, Wwisestream_F, FaceFX_ID_M, FaceFX_ID_F.


    [DebuggerDisplay("ConversationExtended {ExportUID} {ConvName}")]
    public class ConversationExtended : INotifyPropertyChanged
    {
        public int ExportUID { get; set; }
        public PropertyCollection BioConvo { get; set; }
        public ExportEntry Export { get; set; }
        public string ConvName { get; set; }
        public bool IsParsed { get; set; }
        public bool IsFirstParsed { get; set; }
        public SortedDictionary<int, int> StartingList { get; set; } = new SortedDictionary<int, int>();
        public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
        public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; set; }
        public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; set; }

        public ObservableCollectionExtended<StageDirection> StageDirections { get; } = new ObservableCollectionExtended<StageDirection>();
        /// <summary>
        /// WwiseBank Reference Export
        /// </summary>
        public ExportEntry WwiseBank { get; set; }
        /// <summary>
        /// Sequence Reference UIndex
        /// </summary>
        public IEntry Sequence { get; set; }
        /// <summary>
        /// NonSpkrFaceFX IEntry
        /// </summary>
        public IEntry NonSpkrFFX { get; set; }

        public ObservableCollectionExtended<NameReference> ScriptList { get; } = new ObservableCollectionExtended<NameReference>();
        public ConversationExtended(ExportEntry export)
        {
            Export = export;
            ExportUID = export.UIndex;
            ConvName = export.ObjectName;
            BioConvo = export.GetProperties();
            Speakers = new ObservableCollectionExtended<SpeakerExtended>();
            EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
        }

        public ConversationExtended(ConversationExtended other)
        {
            ExportUID = other.ExportUID;
            ConvName = other.ConvName;
            BioConvo = other.BioConvo;
            Export = other.Export;
            IsParsed = other.IsParsed;
            IsFirstParsed = other.IsFirstParsed;
            StartingList = other.StartingList;
            Speakers = other.Speakers;
            EntryList = other.EntryList;
            ReplyList = other.ReplyList;
            WwiseBank = other.WwiseBank;
            Sequence = other.Sequence;
            NonSpkrFFX = other.NonSpkrFFX;
            ScriptList.AddRange(other.ScriptList);
            StageDirections.AddRange(other.StageDirections);
        }

        public void LoadConversation(Func<int, IMEPackage, string> tlkLookup = null)
        {
            ParseStartingList();
            ParseSpeakers();
            //GenerateSpeakerList();
            ParseEntryList(tlkLookup);
            ParseReplyList(tlkLookup);
            ParseScripts();
            ParseNSFFX();
            ParseSequence();
            ParseWwiseBank();
            ParseStageDirections(tlkLookup);
        }

        public void ParseEntryList(Func<int, IMEPackage, string> tlkLookup = null)
        {
            EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var entryprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            int cnt = 0;

            foreach (StructProperty Node in entryprop)
            {
                EntryList.Add(ParseSingleLine(Node, cnt, false, tlkLookup));
                cnt++;
            }
        }
        public void ParseReplyList(Func<int, IMEPackage, string> tlkLookup = null)
        {
            ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var replyprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList"); //ME3
            if (replyprop != null)
            {
                int cnt = 0;
                foreach (StructProperty Node in replyprop)
                {
                    ReplyList.Add(ParseSingleLine(Node, cnt, true, tlkLookup));
                    cnt++;
                }
            }
        }

        public DialogueNodeExtended ParseSingleLine(StructProperty Node, int count, bool isReply, Func<int, IMEPackage, string> tlkLookupFunc = null)
        {
            int linestrref = 0;
            int spkridx = -2;
            int cond = -1;
            string line = "Unknown Reference";
            int stevent = -1;
            bool bcond = false;
            EBCReplyTypes eReply = EBCReplyTypes.REPLY_STANDARD;
            try
            {
                linestrref = Node.GetProp<StringRefProperty>("srText")?.Value ?? 0;
                line = tlkLookupFunc?.Invoke(linestrref, Export.FileRef);
                cond = Node.GetProp<IntProperty>("nConditionalFunc")?.Value ?? -1;
                stevent = Node.GetProp<IntProperty>("nStateTransition")?.Value ?? -1;
                bcond = Node.GetProp<BoolProperty>("bFireConditional");
                if (isReply)
                {
                    Enum.TryParse(Node.GetProp<EnumProperty>("ReplyType").Value.Name, out eReply);
                }
                else
                {
                    spkridx = Node.GetProp<IntProperty>("nSpeakerIndex");
                }

                return new DialogueNodeExtended(Node, isReply, count, spkridx, linestrref, line, bcond, cond, stevent, eReply);
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception($"List Parse failed: N{count} Reply?:{isReply}, {linestrref}, {line}, {cond}, {stevent}, {bcond.ToString()}, {eReply.ToString()}", e);  //Note some convos don't have replies.
#endif
                return new DialogueNodeExtended(Node, isReply, count, spkridx, linestrref, line, bcond, cond, stevent, eReply);
            }
        }

        /// <summary>
        /// Gets dictionary of starting list and position
        /// </summary>
        /// <returns>Key = position on list, Value = Outlink</returns>
        public void ParseStartingList()
        {
            StartingList = new SortedDictionary<int, int>();
            var prop = Export.GetProperty<ArrayProperty<IntProperty>>("m_StartingList"); //ME1/ME2/ME3
            if (prop != null)
            {
                int pos = 0;
                foreach (var sl in prop)
                {
                    StartingList.Add(pos, sl.Value);
                    pos++;
                }
            }
        }

        public void ParseSpeakers()
        {
            Speakers = new ObservableCollectionExtended<SpeakerExtended>
            {
                new SpeakerExtended(-2, "player", null, null, 125303, "\"Shepard\""),
                new SpeakerExtended(-1, "owner", null, null, 0, "No data")
            };
            try
            {
                if (Export.FileRef.Game != MEGame.ME3)
                {
                    var s_speakers = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                    if (s_speakers != null)
                    {
                        for (int id = 0; id < s_speakers.Count; id++)
                        {
                            var spkr = new SpeakerExtended(id, s_speakers[id].GetProp<NameProperty>("sSpeakerTag").Value.Instanced);
                            Speakers.Add(spkr);
                        }
                    }
                }
                else
                {
                    var a_speakers = BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                    if (a_speakers != null)
                    {
                        int id = 0;
                        foreach (NameProperty n in a_speakers)
                        {
                            var spkr = new SpeakerExtended(id, n.Value.Instanced);
                            Speakers.Add(spkr);
                            id++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("Starting List Parse failed", e);
#endif
            }
        }

        public void ParseScripts()
        {
            ScriptList.Add("None");
            if (Export.FileRef.Game == MEGame.ME3)
            {
                var a_scripts = BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aScriptList");
                if (a_scripts != null)
                {
                    foreach (var scriptprop in a_scripts)
                    {
                        var scriptname = scriptprop.Value;
                        ScriptList.Add(scriptname);
                    }
                }
            }
            else
            {
                var a_sscripts = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ScriptList");
                if (a_sscripts != null)
                {
                    foreach (var scriptprop in a_sscripts)
                    {
                        var s = scriptprop.GetProp<NameProperty>("sScriptTag");
                        ScriptList.Add(s.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the IEntry of NonSpeaker FaceFX
        /// </summary>
        public void ParseNSFFX()
        {
            string propname = "m_pNonSpeakerFaceFXSet";
            if (Export.FileRef.Game == MEGame.ME1)
            {
                propname = "m_pConvFaceFXSet";
            }

            var seq = BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                NonSpkrFFX = Export.FileRef.GetEntry(seq.Value);
            }
            else
            {
                NonSpkrFFX = null;
            }
        }
        /// <summary>
        /// Sets the Uindex of WwiseBank
        /// </summary>
        public void ParseWwiseBank()
        {
            WwiseBank = null;
            if (Export.FileRef.Game != MEGame.ME1)
            {
                try
                {
                    ArrayProperty<ObjectProperty> wwevents;
                    IEntry ffxo = GetFaceFX(-1, true); //find owner animset

                    if (ffxo == null) //if no facefx then maybe soundobject conversation
                    {
                        wwevents = Export.GetProperty<ArrayProperty<ObjectProperty>>("m_aMaleSoundObjects");

                    }
                    else
                    {
                        ExportEntry ffxoExport = (ExportEntry)ffxo;

                        wwevents = ffxoExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues"); //pull an owner wwiseevent array
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxp = GetFaceFX(-2, true); //find player as alternative
                            if (!Export.FileRef.IsUExport(ffxp.UIndex))
                                return;
                            ExportEntry ffxpExport = (ExportEntry)ffxp;
                            wwevents = ffxpExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxS = GetFaceFX(0, true); //find speaker 1 as alternative
                            if (ffxS == null || !Export.FileRef.IsUExport(ffxS.UIndex))
                                return;
                            ExportEntry ffxSExport = (ExportEntry)ffxS;
                            wwevents = ffxSExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                    }

                    if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                    {
                        WwiseBank = null;
                        return;
                    }

                    if (Export.FileRef.Game == MEGame.ME3)
                    {
                        StructProperty r = Export.FileRef.GetUExport(wwevents[0].Value).GetProperty<StructProperty>("Relationships"); //lookup bank
                        var bank = r.GetProp<ObjectProperty>("Bank");
                        WwiseBank = Export.FileRef.GetUExport(bank.Value);
                    }
                    else if (Export.FileRef.Game == MEGame.ME2) //Game is ME2.  Wwisebank ref in Binary.
                    {
                        var wwiseEvent = Export.FileRef.GetUExport(wwevents[0].Value).GetBinaryData<WwiseEvent>();
                        foreach (var link in wwiseEvent.Links)
                        {
                            if (link.WwiseBanks.FirstOrDefault() is UIndex bankIdx)
                            {
                                WwiseBank = Export.FileRef.GetUExport(bankIdx);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception($"WwiseBank Parse Failed. {ConvName}", e);
#endif
                }
            }
        }

        /// <summary>
        /// Sets the IEntry of appropriate sequence
        /// </summary>
        public void ParseSequence()
        {
            string propname = "MatineeSequence";
            if (Export.FileRef.Game == MEGame.ME1)
            {
                propname = "m_pEvtSystemSeq";
            }

            var seq = BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                Sequence = Export.FileRef.GetEntry(seq.Value);
            }
            else
            {
                Sequence = null;
            }
        }

        public void ParseStageDirections(Func<int, IMEPackage, string> tlkLookup = null)
        {
            if (Export.FileRef.Game == MEGame.ME3)
            {
                var dprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_aStageDirections"); //ME3 Only not in ME1/2
                if (dprop != null)
                {
                    foreach (var direction in dprop)
                    {
                        int strref = 0;
                        string line = "No data";
                        string action = "None";
                        try
                        {
                            var strrefprop = direction.GetProp<StringRefProperty>("srStrRef");
                            if (strrefprop != null)
                            {
                                strref = strrefprop.Value;
                                line = tlkLookup?.Invoke(strref, Export.FileRef);
                            }
                            var actionprop = direction.GetProp<StrProperty>("sText");
                            if (actionprop != null)
                            {
                                action = actionprop.Value;
                            }
                            StageDirections.Add(new StageDirection(strref, line, action));
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            throw new Exception($"stage directions parse failed {ConvName}", e);
#endif
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Returns the IEntry of FaceFXAnimSet
        /// </summary>
        /// <param name="conv"></param>
        /// <param name="speakerID">SpeakerID: -1 = Owner, -2 = Player</param>
        /// <param name="isMale">will pull female by default</param>
        public IEntry GetFaceFX(int speakerID, bool isMale = false)
        {
            string ffxPropName = "m_aFemaleFaceSets"; //ME2/M£3
            if (isMale)
            {
                ffxPropName = "m_aMaleFaceSets";
            }
            var ffxList = BioConvo.GetProp<ArrayProperty<ObjectProperty>>(ffxPropName);
            if (ffxList != null && ffxList.Count > speakerID + 2)
            {
                return Export.FileRef.GetEntry(ffxList[speakerID + 2].Value);
            }

            return null;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }


    [DebuggerDisplay("SpeakerExtended {SpeakerID} {SpeakerName}")]

    public class SpeakerExtended : INotifyPropertyChanged
    {
        public int SpeakerID { get; set; }
        public string SpeakerName { get; set; }
        /// <summary>
        /// Male UIndex object reference
        /// </summary>
        public IEntry FaceFX_Male { get; set; }
        /// <summary>
        /// Female UIndex object reference
        /// </summary>
        public IEntry FaceFX_Female { get; set; }
        public int StrRefID { get; set; }
        public string FriendlyName { get; set; }

        public SpeakerExtended(int SpeakerID, string SpeakerName)
        {
            this.SpeakerID = SpeakerID;
            this.SpeakerName = SpeakerName;
        }

        public SpeakerExtended(int SpeakerID, string SpeakerName, IEntry FaceFX_Male, IEntry FaceFX_Female, int StrRefID, string FriendlyName)
        {
            this.SpeakerID = SpeakerID;
            this.SpeakerName = SpeakerName;
            this.FaceFX_Male = FaceFX_Male;
            this.FaceFX_Female = FaceFX_Female;
            this.StrRefID = StrRefID;
            this.FriendlyName = FriendlyName;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }


    [DebuggerDisplay("DNExtended {ReplyType} IsReply: {IsReply}, Line: {Line}")]

    public class DialogueNodeExtended : INotifyPropertyChanged
    {
        public bool IsReply { get; set; }
        public int NodeCount { get; set; } //This is the count for reply and node.
        public StructProperty NodeProp { get; set; }
        public int SpeakerIndex { get; set; }
        public int LineStrRef { get; set; }
        public string Line { get; set; }
        public bool FiresConditional { get; set; }
        public int ConditionalOrBool { get; set; }
        public int Transition { get; set; }
        /// <summary>
        /// Tag of speaker - generated.
        /// </summary>
        public SpeakerExtended SpeakerTag { get; set; }
        /// <summary>
        /// InterpData object reference UIndex
        /// </summary>
        public ExportEntry Interpdata { get; set; }
        /// <summary>
        /// Length of interpdata
        /// </summary>
        public float InterpLength { get; set; }
        /// <summary>
        /// WwiseStream object reference Male UIndex
        /// </summary>
        public ExportEntry WwiseStream_Male { get; set; }
        /// <summary>
        /// WwiseStream object reference Female UIndex
        /// </summary>
        public ExportEntry WwiseStream_Female { get; set; }
        /// <summary>
        /// FaceFX reference Male TBD
        /// </summary>
        public string FaceFX_Male { get; set; }
        /// <summary>
        /// FaceFX reference female TBD
        /// </summary>
        public string FaceFX_Female { get; set; }
        public int Listener { get; set; }
        public int ConditionalParam { get; set; }
        public int TransitionParam { get; set; }
        public int ExportID { get; set; }
        public bool IsSkippable { get; set; }
        public bool IsUnskippable { get; set; }
        public bool IsDefaultAction { get; set; }
        public bool IsMajorDecision { get; set; }
        public bool IsNonTextLine { get; set; }
        public bool IgnoreBodyGesture { get; set; }
        public bool IsAmbient { get; set; }
        public int CameraIntimacy { get; set; }
        public bool HideSubtitle { get; set; }
        public NameReference Script { get; set; }
        public EBCConvGUIStyles GUIStyle { get; set; }
        public EBCReplyTypes ReplyType { get; set; }

        public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition, EBCReplyTypes ReplyType)
        {
            this.NodeProp = NodeProp;
            this.IsReply = IsReply;
            this.NodeCount = NodeCount;
            this.SpeakerIndex = SpeakerIndex;
            this.LineStrRef = LineStrRef;
            this.Line = Line;
            this.FiresConditional = FiresConditional;
            this.ConditionalOrBool = ConditionalOrBool;
            this.Transition = Transition;
            this.ReplyType = ReplyType;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="nodeExtended"></param>
        public DialogueNodeExtended(DialogueNodeExtended nodeExtended)
        {
            NodeProp = nodeExtended.NodeProp;
            IsReply = nodeExtended.IsReply;
            NodeCount = nodeExtended.NodeCount;
            SpeakerIndex = nodeExtended.SpeakerIndex;
            LineStrRef = nodeExtended.LineStrRef;
            Line = nodeExtended.Line;
            FiresConditional = nodeExtended.FiresConditional;
            ConditionalOrBool = nodeExtended.ConditionalOrBool;
            Transition = nodeExtended.Transition;
            SpeakerTag = nodeExtended.SpeakerTag;
            Interpdata = nodeExtended.Interpdata;
            WwiseStream_Male = nodeExtended.WwiseStream_Male;
            WwiseStream_Female = nodeExtended.WwiseStream_Female;
            FaceFX_Male = nodeExtended.FaceFX_Male;
            FaceFX_Female = nodeExtended.FaceFX_Female;
            Listener = nodeExtended.Listener;
            ConditionalParam = nodeExtended.ConditionalParam;
            TransitionParam = nodeExtended.TransitionParam;
            ExportID = nodeExtended.ExportID;
            IsSkippable = nodeExtended.IsSkippable;
            IsUnskippable = nodeExtended.IsUnskippable;
            IsDefaultAction = nodeExtended.IsDefaultAction;
            IsMajorDecision = nodeExtended.IsMajorDecision;
            IsNonTextLine = nodeExtended.IsNonTextLine;
            IgnoreBodyGesture = nodeExtended.IgnoreBodyGesture;
            IsAmbient = nodeExtended.IsAmbient;
            CameraIntimacy = nodeExtended.CameraIntimacy;
            HideSubtitle = nodeExtended.HideSubtitle;
            Script = nodeExtended.Script;
            GUIStyle = nodeExtended.GUIStyle;
            ReplyType = nodeExtended.ReplyType;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public class ReplyChoiceNode : INotifyPropertyChanged
    {
        public int Order { get; set; }
        public int Index { get; set; }
        public string Paraphrase { get; set; }
        /// <summary>
        /// Reply choice strref
        /// </summary>
        public int ReplyStrRef { get; set; }
        /// <summary>
        /// reply choice category
        /// </summary>
        public EBCReplyCategory RCategory { get; set; }
        public string ReplyLine { get; set; }
        public string NodeIDLink { get; set; }
        public string Ordinal { get; set; }
        public int TgtCondition { get; set; }
        public string TgtFireCnd { get; set; }
        public string TgtLine { get; set; }
        public string TgtSpeaker { get; set; }

        public ReplyChoiceNode(int Index, string Paraphrase, int ReplyStrRef, EBCReplyCategory RCategory, string ReplyLine)
        {
            this.Index = Index;
            this.Paraphrase = Paraphrase;
            this.ReplyStrRef = ReplyStrRef;
            this.RCategory = RCategory;
            this.ReplyLine = ReplyLine;
        }

        public ReplyChoiceNode(ReplyChoiceNode other)
        {
            Order = other.Order;
            Index = other.Index;
            Paraphrase = other.Paraphrase;
            ReplyStrRef = other.ReplyStrRef;
            RCategory = other.RCategory;
            ReplyLine = other.ReplyLine;
            NodeIDLink = other.NodeIDLink;
            Ordinal = other.Ordinal;
            TgtCondition = other.TgtCondition;
            TgtFireCnd = other.TgtFireCnd;
            TgtLine = other.TgtLine;
            TgtSpeaker = other.TgtSpeaker;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public class StageDirection : INotifyPropertyChanged
    {
        public int StageStrRef { get; set; }
        public string StageLine { get; set; }
        public string Direction { get; set; }

        public StageDirection(int StageStrRef, string StageLine, string Direction)
        {

            this.StageStrRef = StageStrRef;
            this.StageLine = StageLine;
            this.Direction = Direction;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    /// <summary>
    /// EGUIStyles enum with the MAX item removed
    /// </summary>
    public enum EBCConvGUIStyles
    {
        GUI_STYLE_NONE = 0,
        GUI_STYLE_CHARM,
        GUI_STYLE_INTIMIDATE,
        GUI_STYLE_PLAYER_ALERT,
        GUI_STYLE_ILLEGAL,
        //GUI_STYLE_MAX,
    }
    /// <summary>
    /// EReplyTypes enum with the MAX item removed
    /// </summary>
    public enum EBCReplyTypes
    {
        REPLY_STANDARD = 0,
        REPLY_AUTOCONTINUE,
        REPLY_DIALOGEND,
        //REPLY_MAX
    }
    /// <summary>
    /// EReplyCategory enum with the MAX item removed
    /// </summary>
    public enum EBCReplyCategory
    {
        REPLY_CATEGORY_DEFAULT = 0,
        REPLY_CATEGORY_AGREE,
        REPLY_CATEGORY_DISAGREE,
        REPLY_CATEGORY_FRIENDLY,
        REPLY_CATEGORY_HOSTILE,
        REPLY_CATEGORY_INVESTIGATE,
        REPLY_CATEGORY_RENEGADE_INTERRUPT,
        REPLY_CATEGORY_PARAGON_INTERRUPT,
        //REPLY_CATEGORY_MAX,
    }
}