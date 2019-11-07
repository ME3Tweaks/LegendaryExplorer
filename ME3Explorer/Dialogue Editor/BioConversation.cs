using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;

namespace ME3Explorer.Dialogue_Editor.BioConversationExtended
{
    //Contains nested conversation structure.
    // - InterpData
    //Extended Nested Collections:
    // - Speakers have FaceFX Objects
    // - DialogueNodeExtended has InterpData, WwiseStream_M, Wwisestream_F, FaceFX_ID_M, FaceFX_ID_F.


    public class ConversationExtended : NotifyPropertyChangedBase
    {
        private int _ExportUID;
        public int ExportUID { get => _ExportUID; set => SetProperty(ref _ExportUID, value); }
        public PropertyCollection BioConvo { get; set; }
        public ExportEntry Export { get; set; }
        private string _ConvName;
        public string ConvName { get => _ConvName; set => SetProperty(ref _ConvName, value); }
        private bool _IsParsed;
        public bool IsParsed { get => _IsParsed; set => SetProperty(ref _IsParsed, value); }
        public bool IsFirstParsed { get; set; }
        private SortedDictionary<int, int> _StartingList = new SortedDictionary<int, int>();
        public SortedDictionary<int, int> StartingList { get => _StartingList; set => SetProperty(ref _StartingList, value); }
        public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
        public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; set; }
        public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; set; }

        public ObservableCollectionExtended<StageDirection> StageDirections { get; } = new ObservableCollectionExtended<StageDirection>();
        private ExportEntry _WwiseBank;
        /// <summary>
        /// WwiseBank Reference Export
        /// </summary>
        public ExportEntry WwiseBank { get => _WwiseBank; set => SetProperty(ref _WwiseBank, value); }
        private IEntry _Sequence;
        /// <summary>
        /// Sequence Reference UIndex
        /// </summary>
        public IEntry Sequence { get => _Sequence; set => SetProperty(ref _Sequence, value); }
        private IEntry _NonSpkrFFX;
        /// <summary>
        /// NonSpkrFaceFX IEntry
        /// </summary>
        public IEntry NonSpkrFFX { get => _NonSpkrFFX; set => SetProperty(ref _NonSpkrFFX, value); }

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
    }

    public class SpeakerExtended : NotifyPropertyChangedBase
    {
        private int _speakerid;
        public int SpeakerID { get => _speakerid; set => SetProperty(ref _speakerid, value); }

        private string _speakername;
        public string SpeakerName { get => _speakername; set => SetProperty(ref _speakername, value); }

        private IEntry _facefx_male;
        /// <summary>
        /// Male UIndex object reference
        /// </summary>
        public IEntry FaceFX_Male { get => _facefx_male; set => SetProperty(ref _facefx_male, value); }
        private IEntry _facefx_female;
        /// <summary>
        /// Female UIndex object reference
        /// </summary>
        public IEntry FaceFX_Female { get => _facefx_female; set => SetProperty(ref _facefx_female, value); }
        private int _strrefid;
        public int StrRefID { get => _strrefid; set => SetProperty(ref _strrefid, value); }
        private string _friendlyname;
        public string FriendlyName { get => _friendlyname; set => SetProperty(ref _friendlyname, value); }

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
    }

    public class DialogueNodeExtended : NotifyPropertyChangedBase
    {
        private bool _IsReply;
        public bool IsReply { get => _IsReply; set => SetProperty(ref _IsReply, value); }
        private int _NodeCount;
        public int NodeCount { get => _NodeCount; set => SetProperty(ref _NodeCount, value); }//This is the count for reply and node.
        private StructProperty _NodeProp;
        public StructProperty NodeProp { get => _NodeProp; set => SetProperty(ref _NodeProp, value); }
        private int _SpeakerIndex;
        public int SpeakerIndex { get => _SpeakerIndex; set => SetProperty(ref _SpeakerIndex, value); }
        private int _LineStrRef;
        public int LineStrRef { get => _LineStrRef; set => SetProperty(ref _LineStrRef, value); }
        private string _Line;
        public string Line { get => _Line; set => SetProperty(ref _Line, value); }
        private bool _FiresConditional;
        public bool FiresConditional { get => _FiresConditional; set => SetProperty(ref _FiresConditional, value); }
        private int _ConditionalOrBool;
        public int ConditionalOrBool { get => _ConditionalOrBool; set => SetProperty(ref _ConditionalOrBool, value); }
        private int _Transition;
        public int Transition { get => _Transition; set => SetProperty(ref _Transition, value); }
        private SpeakerExtended _SpeakerTag;
        /// <summary>
        /// Tag of speaker - generated.
        /// </summary>
        public SpeakerExtended SpeakerTag { get => _SpeakerTag; set => SetProperty(ref _SpeakerTag, value); }
        private ExportEntry _Interpdata;
        /// <summary>
        /// InterpData object reference UIndex
        /// </summary>
        public ExportEntry Interpdata { get => _Interpdata; set => SetProperty(ref _Interpdata, value); }
        private float _InterpLength = -1;
        /// <summary>
        /// Length of interpdata
        /// </summary>
        public float InterpLength { get => _InterpLength; set => SetProperty(ref _InterpLength, value); }
        private ExportEntry _WwiseStream_Male;
        /// <summary>
        /// WwiseStream object reference Male UIndex
        /// </summary>
        public ExportEntry WwiseStream_Male { get => _WwiseStream_Male; set => SetProperty(ref _WwiseStream_Male, value); }
        private ExportEntry _WwiseStream_Female;
        /// <summary>
        /// WwiseStream object reference Female UIndex
        /// </summary>
        public ExportEntry WwiseStream_Female { get => _WwiseStream_Female; set => SetProperty(ref _WwiseStream_Female, value); }
        private string _FaceFX_Male;
        /// <summary>
        /// FaceFX reference Male TBD
        /// </summary>
        public string FaceFX_Male { get => _FaceFX_Male; set => SetProperty(ref _FaceFX_Male, value); }
        private string _FaceFX_Female;
        /// <summary>
        /// FaceFX reference female TBD
        /// </summary>
        public string FaceFX_Female { get => _FaceFX_Female; set => SetProperty(ref _FaceFX_Female, value); }
        private int _Listener;
        public int Listener { get => _Listener; set => SetProperty(ref _Listener, value); }
        private int _ConditionalParam;
        public int ConditionalParam { get => _ConditionalParam; set => SetProperty(ref _ConditionalParam, value); }
        private int _TransitionParam;
        public int TransitionParam { get => _TransitionParam; set => SetProperty(ref _TransitionParam, value); }
        private int _ExportID;
        public int ExportID { get => _ExportID; set => SetProperty(ref _ExportID, value); }
        private bool _IsSkippable;
        public bool IsSkippable { get => _IsSkippable; set => SetProperty(ref _IsSkippable, value); }
        private bool _IsUnskippable;
        public bool IsUnskippable { get => _IsUnskippable; set => SetProperty(ref _IsUnskippable, value); }
        private bool _IsDefaultAction;
        public bool IsDefaultAction { get => _IsDefaultAction; set => SetProperty(ref _IsDefaultAction, value); }
        private bool _IsMajorDecision;
        public bool IsMajorDecision { get => _IsMajorDecision; set => SetProperty(ref _IsMajorDecision, value); }
        private bool _IsNonTextLine;
        public bool IsNonTextLine { get => _IsNonTextLine; set => SetProperty(ref _IsNonTextLine, value); }
        private bool _IgnoreBodyGesture;
        public bool IgnoreBodyGesture { get => _IgnoreBodyGesture; set => SetProperty(ref _IgnoreBodyGesture, value); }
        private bool _IsAmbient;
        public bool IsAmbient { get => _IsAmbient; set => SetProperty(ref _IsAmbient, value); }
        private int _CameraIntimacy;
        public int CameraIntimacy { get => _CameraIntimacy; set => SetProperty(ref _CameraIntimacy, value); }
        private bool _HideSubtitle;
        public bool HideSubtitle { get => _HideSubtitle; set => SetProperty(ref _HideSubtitle, value); }
        private NameReference _Script;
        public NameReference Script { get => _Script; set => SetProperty(ref _Script, value); }
        private EConvGUIStyles _GUIStyle;
        public EConvGUIStyles GUIStyle { get => _GUIStyle; set => SetProperty(ref _GUIStyle, value); }
        private EReplyTypes _ReplyType;
        public EReplyTypes ReplyType { get => _ReplyType; set => SetProperty(ref _ReplyType, value); }

        public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition, EReplyTypes ReplyType)
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
    }

    public class ReplyChoiceNode : NotifyPropertyChangedBase
    {
        private int _order;
        public int Order { get => _order; set => SetProperty(ref _order, value); }

        private int _Index;
        public int Index { get => _Index; set => SetProperty(ref _Index, value); }

        private string _Paraphrase;
        public string Paraphrase { get => _Paraphrase; set => SetProperty(ref _Paraphrase, value); }

        private int _ReplyStrRef;
        /// <summary>
        /// Reply choice strref
        /// </summary>
        public int ReplyStrRef { get => _ReplyStrRef; set => SetProperty(ref _ReplyStrRef, value); }
        private EReplyCategory _RCategory;
        /// <summary>
        /// reply choice category
        /// </summary>
        public EReplyCategory RCategory { get => _RCategory; set => SetProperty(ref _RCategory, value); }
        private string _ReplyLine;
        public string ReplyLine { get => _ReplyLine; set => SetProperty(ref _ReplyLine, value); }
        public string NodeIDLink { get; set; }
        public string Ordinal { get; set; }
        public int TgtCondition { get; set; }
        public string TgtFireCnd { get; set; }
        public string TgtLine { get; set; }
        public string TgtSpeaker { get; set; }

        public ReplyChoiceNode(int Index, string Paraphrase, int ReplyStrRef, EReplyCategory RCategory, string ReplyLine)
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
    }

    public class StageDirection : NotifyPropertyChangedBase
    {
        private int _StageStrRef;
        public int StageStrRef { get => _StageStrRef; set => SetProperty(ref _StageStrRef, value); }
        private string _StageLine;
        public string StageLine { get => _StageLine; set => SetProperty(ref _StageLine, value); }
        private string _Direction;
        public string Direction { get => _Direction; set => SetProperty(ref _Direction, value); }

        public StageDirection(int StageStrRef, string StageLine, string Direction)
        {

            this.StageStrRef = StageStrRef;
            this.StageLine = StageLine;
            this.Direction = Direction;
        }
    }

    public enum EConvGUIStyles
    {
        GUI_STYLE_NONE = 0,
        GUI_STYLE_CHARM,
        GUI_STYLE_INTIMIDATE,
        GUI_STYLE_PLAYER_ALERT,
        GUI_STYLE_ILLEGAL,
        //GUI_STYLE_MAX,
    }
    public enum EReplyTypes
    {
        REPLY_STANDARD = 0,
        REPLY_AUTOCONTINUE,
        REPLY_DIALOGEND,
        //REPLY_MAX
    }
    public enum EReplyCategory
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