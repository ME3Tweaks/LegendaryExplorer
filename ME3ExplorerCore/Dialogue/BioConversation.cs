using System.Collections.Generic;
using System.ComponentModel;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Dialogue
{
    //Contains nested conversation structure.
    // - InterpData
    //Extended Nested Collections:
    // - Speakers have FaceFX Objects
    // - DialogueNodeExtended has InterpData, WwiseStream_M, Wwisestream_F, FaceFX_ID_M, FaceFX_ID_F.


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

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

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