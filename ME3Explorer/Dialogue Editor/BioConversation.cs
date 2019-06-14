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
using ME3Explorer.Unreal.Classes;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace ME3Explorer.Dialogue_Editor
{
    public partial class BioConversationExtended : NotifyPropertyChangedWindowBase
    {

        #region Convo
            //Contains nested conversation structure.
            // - IntepData
            //Extended Nested Collections:
            // - Speakers have FaceFX Objects
            // - DialogueNodeExtended has InterpData, WwiseStream_M, Wwisestream_F, FaceFX_ID_M, FaceFX_ID_F.


        public class ConversationExtended : BioConversationExtended
        {
            private int _ExportUID;
            public int ExportUID { get => _ExportUID; set => SetProperty(ref _ExportUID, value); }
            public PropertyCollection BioConvo { get; set; }
            public IExportEntry Export { get; set; }
            private string _ConvName;
            public string ConvName { get => _ConvName; set => SetProperty(ref _ConvName, value); }
            public bool IsParsed { get; set; }
            public bool IsFirstParsed { get; set; }
            private SortedDictionary<int, int> _StartingList = new SortedDictionary<int, int>();
            public SortedDictionary<int, int> StartingList { get => _StartingList; set => SetProperty(ref _StartingList, value); }
            public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; set; }
            private ObservableCollectionExtended<StageDirection> _StageDirections;
            public ObservableCollectionExtended<StageDirection> StageDirections { get => _StageDirections; set => SetProperty(ref _StageDirections, value); }
            private IExportEntry _WwiseBank;
            /// <summary>
            /// WwiseBank Reference Export
            /// </summary>
            public IExportEntry WwiseBank { get => _WwiseBank; set => SetProperty(ref _WwiseBank, value); }
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
            private List<String> _ScriptList;
            public List<String> ScriptList { get => _ScriptList; set => SetProperty(ref _ScriptList, value); }

            public ConversationExtended(int ExportUID, string ConvName, PropertyCollection BioConvo, IExportEntry Export, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList, ObservableCollectionExtended<StageDirection> StageDirections)
            {
                this.ExportUID = ExportUID;
                this.ConvName = ConvName;
                this.BioConvo = BioConvo;
                this.Export = Export;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
                this.StageDirections = StageDirections;
            }

            public ConversationExtended(int ExportUID, string ConvName, PropertyCollection BioConvo, IExportEntry Export, bool IsParsed, bool IsFirstParsed, SortedDictionary<int, int> StartingList, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList, ObservableCollectionExtended<StageDirection> StageDirections, IExportEntry WwiseBank, IEntry Sequence, IEntry NonSpkrFFX, List<string> ScriptList)
            {
                this.ExportUID = ExportUID;
                this.ConvName = ConvName;
                this.BioConvo = BioConvo;
                this.Export = Export;
                this.IsParsed = IsParsed;
                this.IsFirstParsed = IsFirstParsed;
                this.StartingList = StartingList;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
                this.WwiseBank = WwiseBank;
                this.Sequence = Sequence;
                this.NonSpkrFFX = NonSpkrFFX;
                this.ScriptList = ScriptList;
                this.StageDirections = StageDirections;
            }
        }

        public class SpeakerExtended : BioConversationExtended
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

            //public event PropertyChangedEventHandler Speaker_PropertyChanged;
            //protected void Speaker_OnPropertyChanged(string name)
            //{
            //    Speaker_PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            //}
        }

        public class DialogueNodeExtended : BioConversationExtended
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
            private bool _FiresConditional = false;
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
            private IExportEntry _Interpdata;
            /// <summary>
            /// InterpData object reference UIndex
            /// </summary>
            public IExportEntry Interpdata { get => _Interpdata; set => SetProperty(ref _Interpdata, value); }
            private float _InterpLength = -1;
            /// <summary>
            /// Length of interpdata
            /// </summary>
            public float InterpLength { get => _InterpLength; set => SetProperty(ref _InterpLength, value); }
            private IExportEntry _WwiseStream_Male;
            /// <summary>
            /// WwiseStream object reference Male UIndex
            /// </summary>
            public IExportEntry WwiseStream_Male { get => _WwiseStream_Male; set => SetProperty(ref _WwiseStream_Male, value); }
            private IExportEntry _WwiseStream_Female;
            /// <summary>
            /// WwiseStream object reference Female UIndex
            /// </summary>
            public IExportEntry WwiseStream_Female { get => _WwiseStream_Female; set => SetProperty(ref _WwiseStream_Female, value); }
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
            private bool _IsSkippable = false;
            public bool IsSkippable { get => _IsSkippable; set => SetProperty(ref _IsSkippable, value); }
            private bool _IsUnskippable = false;
            public bool IsUnskippable { get => _IsUnskippable; set => SetProperty(ref _IsUnskippable, value); }
            private bool _IsDefaultAction;
            public bool IsDefaultAction { get => _IsDefaultAction; set => SetProperty(ref _IsDefaultAction, value); }
            private bool _IsMajorDecision = false;
            public bool IsMajorDecision { get => _IsMajorDecision; set => SetProperty(ref _IsMajorDecision, value); }
            private bool _IsNonTextLine = false;
            public bool IsNonTextLine { get => _IsNonTextLine; set => SetProperty(ref _IsNonTextLine, value); }
            private bool _IgnoreBodyGesture = false;
            public bool IgnoreBodyGesture { get => _IgnoreBodyGesture; set => SetProperty(ref _IgnoreBodyGesture, value); }
            private bool _IsAmbient = false;
            public bool IsAmbient { get => _IsAmbient; set => SetProperty(ref _IsAmbient, value); }
            private int _CameraIntimacy;
            public int CameraIntimacy { get => _CameraIntimacy; set => SetProperty(ref _CameraIntimacy, value); }
            private bool _HideSubtitle = false;
            public bool HideSubtitle { get => _HideSubtitle; set => SetProperty(ref _HideSubtitle, value); }
            private string _Script;
            public string Script { get => _Script; set => SetProperty(ref _Script, value); }
            private  EConvGUIStyles _GUIStyle;
            public EConvGUIStyles GUIStyle
            {
                get => _GUIStyle; set => SetProperty(ref _GUIStyle, value);

            }
            private EReplyTypes _ReplyType;
            public EReplyTypes ReplyType
            {
                get => _ReplyType; set => SetProperty(ref _ReplyType, value);

            }

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

            public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition, SpeakerExtended SpeakerTag,
                IExportEntry Interpdata, IExportEntry WwiseStream_Male, IExportEntry WwiseStream_Female, string FaceFX_Male, string FaceFX_Female, int Listener, int ConditionalParam, int TransitionParam, int ExportID,
                bool IsSkippable, bool IsUnskippable, bool IsDefaultAction, bool IsMajorDecision, bool IsNonTextLine, bool IgnoreBodyGesture, bool IsAmbient, int CameraIntimacy, bool HideSubtitle, string Script, EConvGUIStyles GUIStyle, 
                EReplyTypes ReplyType, float InterpLength)
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
                this.SpeakerTag = SpeakerTag;
                this.Interpdata = Interpdata;
                this.WwiseStream_Male = WwiseStream_Male;
                this.WwiseStream_Female = WwiseStream_Female;
                this.FaceFX_Male = FaceFX_Male;
                this.FaceFX_Female = FaceFX_Female;
                this.Listener = Listener;
                this.ConditionalParam = ConditionalParam;
                this.TransitionParam = TransitionParam;
                this.ExportID = ExportID;
                this.IsSkippable = IsSkippable;
                this.IsUnskippable = IsUnskippable;
                this.IsDefaultAction = IsDefaultAction;
                this.IsMajorDecision = IsMajorDecision;
                this.IsNonTextLine = IsNonTextLine;
                this.IgnoreBodyGesture = IgnoreBodyGesture;
                this.IsAmbient = IsAmbient;
                this.CameraIntimacy = CameraIntimacy;
                this.HideSubtitle = HideSubtitle;
                this.Script = Script;
                this.GUIStyle = GUIStyle;
                this.ReplyType = ReplyType;
                this.InterpLength = InterpLength;
            }

            public DialogueNodeExtended(DialogueNodeExtended nodeExtended)
            {
                this.NodeProp = nodeExtended.NodeProp;
                this.IsReply = nodeExtended.IsReply;
                this.NodeCount = nodeExtended.NodeCount;
                this.SpeakerIndex = nodeExtended.SpeakerIndex;
                this.LineStrRef = nodeExtended.LineStrRef;
                this.Line = nodeExtended.Line;
                this.FiresConditional = nodeExtended.FiresConditional;
                this.ConditionalOrBool = nodeExtended.ConditionalOrBool;
                this.Transition = nodeExtended.Transition;
                this.SpeakerTag = nodeExtended.SpeakerTag;
                this.Interpdata = nodeExtended.Interpdata;
                this.WwiseStream_Male = nodeExtended.WwiseStream_Male;
                this.WwiseStream_Female = nodeExtended.WwiseStream_Female;
                this.FaceFX_Male = nodeExtended.FaceFX_Male;
                this.FaceFX_Female = nodeExtended.FaceFX_Female;
                this.Listener = nodeExtended.Listener;
                this.ConditionalParam = nodeExtended.ConditionalParam;
                this.TransitionParam = nodeExtended.TransitionParam;
                this.ExportID = nodeExtended.ExportID;
                this.IsSkippable = nodeExtended.IsSkippable;
                this.IsUnskippable = nodeExtended.IsUnskippable;
                this.IsDefaultAction = nodeExtended.IsDefaultAction;
                this.IsMajorDecision = nodeExtended.IsMajorDecision;
                this.IsNonTextLine = nodeExtended.IsNonTextLine;
                this.IgnoreBodyGesture = nodeExtended.IgnoreBodyGesture;
                this.IsAmbient = nodeExtended.IsAmbient;
                this.CameraIntimacy = nodeExtended.CameraIntimacy;
                this.HideSubtitle = nodeExtended.HideSubtitle;
                this.Script = nodeExtended.Script;
                this.GUIStyle = nodeExtended.GUIStyle;
                this.ReplyType = nodeExtended.ReplyType;
            }

            public new event PropertyChangedEventHandler PropertyChanged;
            protected override void OnPropertyChanged(string PropertyName)
            {

                if (PropertyChanged != null)

                    PropertyChanged(this, new PropertyChangedEventArgs(PropertyName));
            }

        }

        public class ReplyChoiceNode : BioConversationExtended
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

            public ReplyChoiceNode(int Order, int Index, string Paraphrase, int ReplyStrRef, EReplyCategory RCategory, string ReplyLine, string NodeIDLink, string Ordinal, int TgtCondition, string TgtFireCnd, string TgtLine, string TgtSpeaker)
            {
                this.Order = Order;
                this.Index = Index;
                this.Paraphrase = Paraphrase;
                this.ReplyStrRef = ReplyStrRef;
                this.RCategory = RCategory;
                this.ReplyLine = ReplyLine;
                this.NodeIDLink = NodeIDLink;
                this.Ordinal = Ordinal;
                this.TgtCondition = TgtCondition;
                this.TgtFireCnd = TgtFireCnd;
                this.TgtLine = TgtLine;
                this.TgtSpeaker = TgtSpeaker;
            }
        }

        public class StageDirection : BioConversationExtended
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

            public new event PropertyChangedEventHandler PropertyChanged;
            protected override void OnPropertyChanged(string PropertyName)
            {

                if (PropertyChanged != null)

                    PropertyChanged(this, new PropertyChangedEventArgs(PropertyName));
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
        #endregion Convo
    }

    #region GraphObjects
    public abstract class DiagEdEdge : PPath
    {
        public PNode start;
        public PNode end;
        public DBox originator;
    }
    public class VarEdge : DiagEdEdge
    {
    }
    public class EventEdge : VarEdge
    {
    }

    [DebuggerDisplay("ActionEdge | {originator} to {inputIndex}")]
    public class ActionEdge : DiagEdEdge
    {
        public int inputIndex;
    }

    [DebuggerDisplay("DObj | #{UIndex}: {export.ObjectName}")]
    public abstract class DObj : PNode, IDisposable
    {
        public IMEPackage pcc;
        public ConvGraphEditor g;
        public static Color paraintColor = Color.Blue;
        public static Color renintColor = Color.Red;
        public static Color agreeColor = Color.DodgerBlue;
        public static Color disagreeColor = Color.Tomato;
        public static Color friendlyColor = Color.FromArgb(3, 3, 116);//dark blue
        public static Color hostileColor = Color.FromArgb(116, 3, 3);//dark red
        public static Color entryColor = Color.DarkGoldenrod;
        public static Color entryPenColor = Color.Black;
        public static Color replyColor = Color.CadetBlue;
        public static Color replyPenColor = Color.Black;
        protected static readonly Color EventColor = Color.FromArgb(214, 30, 28);
        protected static readonly Color titleColor = Color.FromArgb(255, 255, 128);
        protected static readonly Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));
        protected static readonly Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static readonly Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static readonly Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public RectangleF posAtDragStart;

        protected string listname;
        public string ListName => listname;
        public int NodeUID = 0;
        public IExportEntry Export => export;
        public virtual bool IsSelected { get; set; }

        protected IExportEntry export;
        protected Pen outlinePen;
        protected DText comment;

        protected DObj(ConvGraphEditor ConvGraphEditor)
        {
            g = ConvGraphEditor;
        }

        public virtual void CreateConnections(IList<DObj> objects) { }
        public virtual void Layout() { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);
        public virtual IEnumerable<DiagEdEdge> Edges => Enumerable.Empty<DiagEdEdge>();

        protected Color getColor(BioConversationExtended.EReplyCategory t)
        {
            switch (t)
            {
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_PARAGON_INTERRUPT:
                    return paraintColor;
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_RENEGADE_INTERRUPT:
                    return renintColor;
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_AGREE:
                    return agreeColor;
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DISAGREE:
                    return disagreeColor;
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_FRIENDLY:
                    return friendlyColor;
                case BioConversationExtended.EReplyCategory.REPLY_CATEGORY_HOSTILE:
                    return hostileColor;
                default:
                    return Color.Black;
            }
        }
        public virtual void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("DBox | #{UIndex}: {export.ObjectName}")]
    public abstract class DBox : DObj
    {
        public override IEnumerable<DiagEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Cast<DiagEdEdge>();
        protected static Brush outputBrush = new SolidBrush(Color.Black);
        public static float LineScaleOption = 1.0f;
        public static Color lineColor = Color.FromArgb(74, 63, 190);
        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public int InputIndices;
            public string Desc;
            public List<ActionEdge> Edges;
            public BioConversationExtended.EReplyCategory RCat;
        }

        public struct InputLink
        {
            public PPath node;
            public string Desc;
            public int index;
            public bool hasName;
            public List<ActionEdge> Edges;
        }

        protected PPath titleBox;
        protected PPath outLinkBox;
        public readonly List<OutputLink> Outlinks = new List<OutputLink>();
        protected readonly OutputDragHandler outputDragHandler;

        private static readonly PointF[] downwardTrianglePoly = { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) };
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);

        protected DBox(float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(ConvGraphEditor)
        {
            outputDragHandler = new OutputDragHandler(ConvGraphEditor, this);
        }

        public override void CreateConnections(IList<DObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                for (int j = 0; j < outLink.Links.Count; j++)
                {
                    foreach (DiagNode destAction in objects.OfType<DiagNode>())
                    {
                        if (destAction.NodeID == outLink.Links[j])
                        {
                            PPath p1 = outLink.node;
                            var edge = new ActionEdge();
                            if (p1.Tag == null)
                                p1.Tag = new List<ActionEdge>();
                            ((List<ActionEdge>)p1.Tag).Add(edge);
                            destAction.InputEdges.Add(edge);
                            edge.Pen = new Pen(getColor(outLink.RCat));
                            edge.start = p1;
                            edge.end = destAction;
                            edge.originator = this;
                            edge.inputIndex = outLink.InputIndices;
                            g.addEdge(edge);
                            outLink.Edges.Add(edge);
                        }
                    }
                }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            //s = $"#{UIndex} : {s}";
            DText title = new DText(s, titleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            if (title.Width + 20 > w)
            {
                w = title.Width + 20;
            }
            title.Width = w;
            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
            return w;
        }

        protected float GetTitlePlusLineBox(string s, string l, string n, float w)
        {
            //s = $"#{UIndex} : {s}";
            DText title = new DText(s, titleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            if (title.Width + 60 > w)
            {
                w = title.Width + 60;
            }
            title.Width = w;

            DText line = null;
            if(LineScaleOption > 0)
            {
                line = new DText(l, lineColor, false, LineScaleOption) //Add line string to right side
                {
                    TextAlignment = StringAlignment.Near,
                    ConstrainWidthToTextWidth = false,
                    ConstrainHeightToTextHeight = false,
                    X = w / LineScaleOption + 5,
                    Y = 3,
                    Pickable = false
                };
            }


            DText nodeID = new DText(n, titleColor) //Add node count to left side
            {
                TextAlignment = StringAlignment.Near,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };

            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = new Pen(entryPenColor);
            if(NodeUID < 1000)
            {
                titleBox.Brush = new SolidBrush(entryColor); ;
            }
            else if(NodeUID < 2000)
            {
                titleBox.Brush = new SolidBrush(replyColor); ;
            }
            else 
            {
                titleBox.Brush = titleBoxBrush;
            }
            titleBox.AddChild(nodeID);
            titleBox.AddChild(title);
            if (LineScaleOption > 0)
            {
                titleBox.AddChild(line);
            }
            titleBox.Pickable = false;
            return w;
        }

        protected class OutputDragHandler : PDragEventHandler
        {
            private readonly ConvGraphEditor ConvGraphEditor;
            private readonly DBox DObj;
            public OutputDragHandler(ConvGraphEditor graph, DBox obj)
            {
                ConvGraphEditor = graph;
                DObj = obj;
            }
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                DObj.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                var edge = new ActionEdge();
                if (p1.Tag == null)
                    p1.Tag = new List<ActionEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<ActionEdge>();
                ((List<ActionEdge>)p1.Tag).Add(edge);
                ((List<ActionEdge>)p2.Tag).Add(edge);
                edge.start = p1;
                edge.end = p2;
                edge.originator = DObj;
                ConvGraphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                ConvGraphEditor.UpdateEdge(((List<ActionEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                ActionEdge edge = ((List<ActionEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<ActionEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                ConvGraphEditor.edgeLayer.RemoveChild(edge);
                ((List<ActionEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingOutlink = false;
                if (dragTarget != null)
                {
                    DObj.CreateOutlink(((PPath)sender).Parent, dragTarget);
                    dragTarget = null;
                }
            }
        }

        public virtual void CreateOutlink(PNode n1, PNode n2) { }

        public void RemoveOutlink(ActionEdge edge)
        {
            for (int i = 0; i < Outlinks.Count; i++)
            {
                OutputLink outLink = Outlinks[i];
                for (int j = 0; j < outLink.Edges.Count; j++)
                {
                    ActionEdge actionEdge = outLink.Edges[j];
                    if (actionEdge == edge)
                    {
                        RemoveOutlink(i, j);
                        return;
                    }
                }
            }
        }

        public virtual void RemoveOutlink(int linkconnection, int linkIndex) { }

        public override void Dispose()
        {
            base.Dispose();
            if (outputDragHandler != null)
            {
                foreach (var x in Outlinks) x.node[0].RemoveInputEventListener(outputDragHandler);
            }
        }
    }

    [DebuggerDisplay("DStart | #{UIndex}: {export.ObjectName}")]
    public class DStart : DBox
    {
        public int StartNumber;
        public int Order;
        private string Ordinal;
        private DialogueEditorWPF Editor;
        public List<EventEdge> connections = new List<EventEdge>();
        public override IEnumerable<DiagEdEdge> Edges => connections.Union(base.Edges);

        public DStart(DialogueEditorWPF editor, int orderKey, int StartNbr, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(x, y, ConvGraphEditor)
        {
            NodeUID = 2000 + StartNbr;
            Editor = editor;
            Order = orderKey;
            Ordinal = DialogueEditorWPF.AddOrdinal(orderKey + 1);
            StartNumber = StartNbr;
            outlinePen = new Pen(EventColor);
            listname = $"{Ordinal} Start Node: {StartNbr}"; ;

            float starty = 0;
            float w = 15;
            float midW = 50;
            GetTitleBox(listname, 20);

            w += titleBox.Width;
            OutputLink l = new OutputLink
            {
                Links = new List<int>(StartNbr),
                InputIndices = new int(),
                Edges = new List<ActionEdge>(),
                Desc =$"Out {StartNbr}",
                RCat = BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT
            };
            int linkedOp = StartNbr;
            l.Links.Add(linkedOp);
            l.InputIndices = 0;
            l.node = CreateActionLinkBox();
            l.node.Brush = outputBrush;
            l.node.Pickable = false;

            PPath dragger = CreateActionLinkBox();
            dragger.Brush = mostlyTransparentBrush;
            dragger.X = l.node.X;
            dragger.Y = l.node.Y;
            dragger.AddInputEventListener(outputDragHandler);
            l.node.AddChild(dragger);
            Outlinks.Add(l);
            outLinkBox = new PPath();
            DText t2 = new DText($"{StartNbr} :");
            if (t2.Width + 10 > midW) midW = t2.Width + 10;
            t2.X = 0 - t2.Width;
            t2.Y = starty - 10;
            t2.Pickable = false;
            t2.AddChild(l.node);
            outLinkBox.AddChild(t2);
            outLinkBox.AddPolygon(new[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f * midW, starty + 30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW / -2, -30) });
            outLinkBox.Pickable = false;
            outLinkBox.Pen = outlinePen;
            outLinkBox.Brush = nodeBrush;
            float h = titleBox.Height + 1;
            outLinkBox.TranslateBy(titleBox.Width / 2 + midW / 2, h + 30);
            
            h += outLinkBox.Height + 1;
            bounds = new RectangleF(0, 0, w, h);
            AddChild(titleBox);
            AddChild(outLinkBox);
            this.Pickable = true;
            SetOffset(x, y);
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
        }

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    outLinkBox.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    outLinkBox.Pen = outlinePen;
                }
            }
        }

        public override void CreateOutlink(PNode n1, PNode n2)
        {
            DStart start = (DStart)n1.Parent.Parent.Parent;
            DiagNode end = (DiagNode)n2.Parent.Parent.Parent;
            if (end.GetType() != typeof(DiagNodeEntry))
            {
                MessageBox.Show("You cannot link start nodes to replies.\r\nStarts must link to entries.", "Dialogue Editor");
                return;
            }
            Editor.SelectedConv.StartingList[Order] = end.NodeID;
            Editor.RecreateNodesToProperties(Editor.SelectedConv);
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {

        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {

        }


    }

    [DebuggerDisplay("DiagNode | #{UIndex}: {export.ObjectName}")]
    public class DiagNode : DBox
    {
        public override IEnumerable<DiagEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public List<ActionEdge> InputEdges = new List<ActionEdge>();
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;
        public StructProperty NodeProp;
        public BioConversationExtended.DialogueNodeExtended Node;
        public int NodeID;
        public ObservableCollectionExtended<BioConversationExtended.ReplyChoiceNode> Links = new ObservableCollectionExtended<BioConversationExtended.ReplyChoiceNode>();
        static readonly Color insideTextColor = Color.FromArgb(213, 213, 213);//white
        protected InputDragHandler inputDragHandler = new InputDragHandler();
        protected DialogueEditorWPF Editor;
        public DiagNode(DialogueEditorWPF editor, BioConversationExtended.DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(x, y, ConvGraphEditor)
        {
            Editor = editor;
            Node = node;
            NodeProp = node.NodeProp;
            NodeID = Node.NodeCount;
            originalX = x;
            originalY = y;
        }

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    box.Pen = selectedPen;
                    ((PPath)this[1]).Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    box.Pen = outlinePen;
                    ((PPath)this[1]).Pen = outlinePen;
                }
            }
        }

        public override void Layout()
        {
            Layout(originalX, originalY);
        }

        public override void Layout(float x, float y)
        {
            if(NodeUID < 1000)
            {
                outlinePen = new Pen(entryPenColor);
            }
            else if(NodeUID < 2000)
            {
                outlinePen = new Pen(replyPenColor);
            }
            else
            {
                outlinePen = new Pen(Color.Black);
            }
            float starty = 8;
            float w = 160;

            //OutputLinks
            outLinkBox = new PPath();
            float outW = 0;
            for (int i = 0; i < Outlinks.Count; i++)
            {
                DText t2 = new DText(Outlinks[i].Desc);
                if (t2.Width + 10 > outW) outW = t2.Width + 10;
                t2.X = 0 - t2.Width;
                t2.Y = starty;
                starty += t2.Height;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.Pickable = false;
            float outY = starty;

            //InputLinks
            inputLinkBox = new PNode();
            GetInputLinks(Node);
            float inW = 0;
            float inY = 8;
            for (int i = 0; i < InLinks.Count; i++)
            {
                DText t2 = new DText(InLinks[i].Desc);
                if (t2.Width > inW) inW = t2.Width;
                t2.X = 3;
                t2.Y = inY;
                inY += t2.Height;
                t2.Pickable = false;
                InLinks[i].node.X = -10;
                InLinks[i].node.Y = t2.Y + t2.Height / 2 - 5;
                t2.AddChild(InLinks[i].node);
                inputLinkBox.AddChild(t2);
            }
            inputLinkBox.Pickable = false;
            if (inY > outY) starty = inY;
            if (inW + outW + 10 > w) w = inW + outW + 10;

            //TitleBox
            string s = $"{Node.SpeakerTag.SpeakerName}";
            string l = $"{Node.Line}";
            string n = $"E{Node.NodeCount}";
            if(Node.IsReply)
            { n = $"R{Node.NodeCount}"; }
            float tW = GetTitlePlusLineBox(s, l, n, w);
            if (tW > w)
            {
                w = tW;
                titleBox.Width = w;
            }
            titleBox.X = 0;
            titleBox.Y = 0;
            float h = titleBox.Height + 2;
            inputLinkBox.TranslateBy(0, h);
            outLinkBox.TranslateBy(w, h);
            h += starty + 8;

            //Inside Text +  Box
            string plotCnd = "";
            string trans = "";
            string type = "";
            if(Node.ConditionalOrBool >= 0)
            {
                string cnd = "Cnd:";
                if (Node.FiresConditional == false)
                    cnd = "Bool:";
                plotCnd = $"{cnd} {Node.ConditionalOrBool}\r\n";
            }
            if (Node.Transition >= 0)
            {
                trans = $"Trans:{Node.Transition}\r\n";
            }
            if (Node.IsReply)
            {
                string t = Node.ReplyType.ToString().Substring(6);
                type = $"{t}";
            }
            string d = $"{Node.LineStrRef}\r\n{plotCnd}{trans}{type}";

            DText insidetext = new DText(d, insideTextColor, true)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                ConstrainHeightToTextHeight = true,
                X = 0,
                Y = titleBox.Height + starty + 5,
                Pickable = false
            };
            h += insidetext.Height;
            float iw = insidetext.Width;
            if(iw > w) { w = iw; }
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            insidetext.TranslateBy((w - iw)/2, 0);
            box.AddChild(insidetext);
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(titleBox);
            this.AddChild(outLinkBox);
            this.AddChild(inputLinkBox);
            SetOffset(x, y);
        }

        private void GetInputLinks(BioConversationExtended.DialogueNodeExtended node = null)
        {
            InLinks = new List<InputLink>();

            void CreateInputLink(string desc, int idx, bool hasName = true)
            {
                InputLink l = new InputLink
                {
                    Desc = desc,
                    hasName = hasName,
                    index = idx,
                    node = CreateActionLinkBox(),
                    Edges = new List<ActionEdge>()
                };
                l.node.Brush = outputBrush;
                l.node.MouseEnter += OnMouseEnter;
                l.node.MouseLeave += OnMouseLeave;
                l.node.AddInputEventListener(inputDragHandler);
                InLinks.Add(l);
            }

            if(node != null && !node.IsReply)
            {
                CreateInputLink("Start", 0, true);
            }
            CreateInputLink("In", 1, true);

            //if (inputLinksProp != null)
            //{
            //    for (int i = 0; i < inputLinksProp.Count; i++)
            //    {
            //        CreateInputLink(inputLinksProp[i].GetProp<StrProperty>("LinkDesc"), i);
            //    }
            //}

            if (InputEdges.Any())
            {
                int numInputs = InLinks.Count;
                foreach (ActionEdge edge in InputEdges)
                {
                    int inputNum = edge.inputIndex;
                    //if there are inputs with an index greater than is accounted for by
                    //the current number of inputs, create enough inputs to fill up to that index
                    //With current toolset advances this is unlikely to occur, but no harm in leaving it in
                    if (inputNum + 1 > numInputs)
                    {
                        for (int i = numInputs; i <= inputNum; i++)
                        {
                            CreateInputLink($":{i}", i, false);
                        }
                        numInputs = inputNum + 1;
                    }
                    //change the end of the edge to the input box, not the DiagNode
                    if (inputNum >= 0)
                    {
                        edge.end = InLinks[inputNum].node;
                        InLinks[inputNum].Edges.Add(edge);
                    }
                }
            }
        }

        public override void CreateOutlink(PNode n1, PNode n2)
        {

        }

        public class InputDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingOutlink)
            {
                ((PPath)sender).Pen = selectedPen;
                dragTarget = (PPath)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            ((PPath)sender).Pen = outlinePen;
            dragTarget = null;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (inputDragHandler != null)
            {
                InLinks.ForEach(x => x.node.RemoveInputEventListener(inputDragHandler));
            }
        }
    }

    public class DiagNodeEntry : DiagNode
    {
        public DiagNodeEntry(DialogueEditorWPF editor, BioConversationExtended.DialogueNodeExtended node, MEGame game, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(editor, node, x, y, ConvGraphEditor)
        {
            Node = node;
            NodeProp = node.NodeProp;
            NodeID = node.NodeCount;
            NodeUID = NodeID;
            originalX = x;
            originalY = y;
            listname = $"E{NodeID} {node.Line}";
            var rcarray = NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
            if (rcarray != null)
            {
                try
                {

                    foreach (var rc in rcarray)
                    {
                        var replychoice = new BioConversationExtended.ReplyChoiceNode(-1, "", -1, BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT, "No data");
                        var nIDprop = rc.GetProp<IntProperty>("nIndex");
                        if (nIDprop != null)
                        {
                            replychoice.Index = nIDprop.Value;
                        }

                        var strRefPara = rc.GetProp<StringRefProperty>("srParaphrase");
                        if (strRefPara != null)
                        {
                            replychoice.ReplyStrRef = strRefPara.Value;
                            replychoice.ReplyLine = GlobalFindStrRefbyID(replychoice.ReplyStrRef, game);
                        }

                        var rcatprop = rc.GetProp<EnumProperty>("Category");
                        if (rcatprop != null)
                        {
                            Enum.TryParse(rcatprop.Value.Name, out BioConversationExtended.EReplyCategory eReply);
                            replychoice.RCategory = eReply;
                        }
                        Links.Add(replychoice);
                    }
                }
                catch
                {
                    //ignore
                }
            }

            GetEReplyLinks(Node);
        }

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    box.Pen = selectedPen;
                    ((PPath)this[1]).Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    var entryPen = new Pen(entryPenColor);
                    titleBox.Pen = entryPen;
                    box.Pen = entryPen;
                    ((PPath)this[1]).Pen = entryPen;
                }
            }
        }
        protected void GetEReplyLinks(BioConversationExtended.DialogueNodeExtended node)
        {
            if (node != null)
            {
                if (Links.Count > 0)
                {
                    int n = 0;
                    foreach (var reply in Links)
                    {

                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new int(),
                            Edges = new List<ActionEdge>(),
                            Desc = n.ToString(),
                            RCat = reply.RCategory
                        };

                        int linkedOp = reply.Index + 1000;
                        l.Links.Add(linkedOp);
                        l.InputIndices = 0;
                        if (OutputNumbers)
                            l.Desc = "R" + reply.Index;
                        l.node = CreateActionLinkBox();
                        var linkcolor = getColor(reply.RCategory);
                        l.node.Brush = new SolidBrush(linkcolor);
                        l.node.Pen = new Pen(getColor(reply.RCategory));
                        l.node.Pickable = false;

                        DText paraphrase = new DText(reply.ReplyLine, linkcolor, false, 0.8f)
                        {
                            TextAlignment = StringAlignment.Near,
                            ConstrainWidthToTextWidth = true,
                            ConstrainHeightToTextHeight = true,
                            X = 15,
                            Y = -8,
                            Pickable = false
                        };
                        l.node.AddChild(paraphrase);
                        paraphrase.TranslateBy(0,0);

                        PPath dragger = CreateActionLinkBox();
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.Pen = new Pen(getColor(reply.RCategory));
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(outputDragHandler);
                        l.node.AddChild(dragger);
                        Outlinks.Add(l);
                        n++;
                    }
                }
                else //Create default node.
                {
                    OutputLink l = new OutputLink
                    {
                        Links = new List<int>(),
                        InputIndices = new int(),
                        Edges = new List<ActionEdge>(),
                        Desc = "Out:",
                        RCat = BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT
                    };

                    l.node = CreateActionLinkBox();
                    l.node.Brush = outputBrush;
                    l.node.Pickable = false;
                    PPath dragger = CreateActionLinkBox();
                    dragger.Brush = mostlyTransparentBrush;
                    dragger.X = l.node.X;
                    dragger.Y = l.node.Y;
                    dragger.AddInputEventListener(outputDragHandler);
                    l.node.AddChild(dragger);
                    Outlinks.Add(l);
                }
            }
        }
        public override void CreateOutlink(PNode n1, PNode n2)
        {
            DiagNode start = (DiagNode)n1.Parent.Parent.Parent;
            DiagNode end = (DiagNode)n2.Parent.Parent.Parent;
            if (end.GetType() != typeof(DiagNodeReply))
            {
                MessageBox.Show("You cannot link entry nodes to entries.\r\nEntries must link to replies.", "Dialogue Editor");
                return;
            }
            var startNode = start.NodeID;
            var endNode = end.NodeID;

            var newReplyListProp = new ArrayProperty<StructProperty>(ArrayType.Struct, "ReplyListNew");
            var oldReplyListProp = start.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");


            if(oldReplyListProp != null && oldReplyListProp.Count > 0)
            {
                foreach(var rprop in oldReplyListProp)
                {
                    newReplyListProp.Add(rprop);
                }
            }

            var newProps = new PropertyCollection();
            newProps.Add(new IntProperty(endNode - 1000, "nIndex"));
            newProps.Add(new StringRefProperty(0, "srParaphrase"));
            newProps.Add(new StrProperty("", "sParaphrase"));
            newProps.Add(new EnumProperty("REPLY_CATEGORY_DEFAULT", "EReplyCategory", Editor.Pcc.Game,"Category"));
            newProps.Add(new NoneProperty());

            var newstruct = new StructProperty("BioDialogReplyListDetails", newProps);
            newReplyListProp.Add(newstruct);

            Node.NodeProp.Properties.AddOrReplaceProp(newReplyListProp);
            Editor.RecreateNodesToProperties(Editor.SelectedConv);

            //Add new entry2ReplyNode
            //Reload
        }
    }

    public class DiagNodeReply : DiagNode
    {

        public DiagNodeReply(DialogueEditorWPF editor, BioConversationExtended.DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(editor, node, x, y, ConvGraphEditor)
        {

            Editor = editor;
            Node = node;
            NodeProp = node.NodeProp;
            NodeID = Node.NodeCount + 1000;
            NodeUID = NodeID;
            listname = $"R{Node.NodeCount} {node.Line}";
            GetOutputLinks(Node);
            originalX = x;
            originalY = y;
        }
        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    box.Pen = selectedPen;
                    ((PPath)this[1]).Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    var replyPen = new Pen(replyPenColor);
                    titleBox.Pen = replyPen;
                    box.Pen = replyPen;
                    ((PPath)this[1]).Pen = replyPen;
                }
            }
        }
        protected void GetOutputLinks(BioConversationExtended.DialogueNodeExtended node)
        {
            if (node != null)
            {
                var replytoEntryList = node.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                if (replytoEntryList != null)
                {
                    if(replytoEntryList.Count >0)
                    {
                        int n = 0;
                        foreach (var prop in replytoEntryList)
                        {
                            OutputLink l = new OutputLink
                            {
                                Links = new List<int>(),
                                InputIndices = new int(),
                                Edges = new List<ActionEdge>(),
                                Desc = n.ToString(),
                                RCat = BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT
                            };

                            int linkedOp = prop.Value;
                            l.Links.Add(linkedOp);
                            l.InputIndices = 1;
                            if (OutputNumbers)
                                l.Desc = "E" + linkedOp;

                            l.node = CreateActionLinkBox();
                            l.node.Brush = outputBrush;
                            l.node.Pickable = false;
                            PPath dragger = CreateActionLinkBox();
                            dragger.Brush = mostlyTransparentBrush;
                            dragger.X = l.node.X;
                            dragger.Y = l.node.Y;
                            dragger.AddInputEventListener(outputDragHandler);
                            l.node.AddChild(dragger);
                            Outlinks.Add(l);
                            n++;

                            //Add to links package
                            var replychoice = new BioConversationExtended.ReplyChoiceNode(linkedOp, "", -1, BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT, "No data");
                            Links.Add(replychoice);
                        }
                    }
                    else //Create default node.
                    {
                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new int(),
                            Edges = new List<ActionEdge>(),
                            Desc ="Out:",
                            RCat = BioConversationExtended.EReplyCategory.REPLY_CATEGORY_DEFAULT
                        };

                        l.node = CreateActionLinkBox();
                        l.node.Brush = outputBrush;
                        l.node.Pickable = false;
                        PPath dragger = CreateActionLinkBox();
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(outputDragHandler);
                        l.node.AddChild(dragger);
                        Outlinks.Add(l);
                    }
                }
            }
        }
        public override void CreateOutlink(PNode n1, PNode n2)
        {

            /// NEED TO ADD CHECK TO STOP ATTACH TO SAME TYPE.
            DiagNode start = (DiagNode)n1.Parent.Parent.Parent;
            DiagNode end = (DiagNode)n2.Parent.Parent.Parent;
            if (end.GetType() != typeof(DiagNodeEntry))
            {
                MessageBox.Show("You cannot link reply nodes to replies.\r\nReplies must link to entries.", "Dialogue Editor");
                return;
            }

            var startNode = start.NodeID;
            var endNode = end.NodeID;

            var newEntriesProp = new ArrayProperty<IntProperty>(ArrayType.Int, "EntryList");
            var oldEntriesProp = start.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
            if(oldEntriesProp != null)
            {
                foreach (var i in oldEntriesProp)
                {
                    newEntriesProp.Add(i);
                }
            }

            newEntriesProp.Add(new IntProperty(endNode));

            start.NodeProp.Properties.AddOrReplaceProp(newEntriesProp);

            Editor.RecreateNodesToProperties(Editor.SelectedConv);

        }

        public override void RemoveOutlink(int linkconnection, int linkIndex)
        {
            var oldEntriesProp = NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
            oldEntriesProp.RemoveAt(linkconnection);
            NodeProp.Properties.AddOrReplaceProp(oldEntriesProp);
            Editor.RecreateNodesToProperties(Editor.SelectedConv);

        }
    }
    public class DText : PText
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);
        
        private readonly Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }
        private static PrivateFontCollection fontcollection;
        private static Font kismetFont;

        public DText(string s, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        public DText(string s, Color c, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        //must be called once in the program before DText can be used
        public static void LoadFont()
        {
            if (fontcollection == null || fontcollection.Families.Length < 1)
            {
                fontcollection = new PrivateFontCollection();
                byte[] fontData = Properties.Resources.KismetFont;
                IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                fontcollection.AddMemoryFont(fontPtr, fontData.Length);
                uint tmp = 0;
                AddFontMemResourceEx(fontPtr, (uint)(fontData.Length), IntPtr.Zero, ref tmp);
                Marshal.FreeCoTaskMem(fontPtr);
                kismetFont = new Font(fontcollection.Families[0], 6, GraphicsUnit.Pixel);
            }
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            //paints dropshadow
            if (shadowRendering && paintContext.Scale >= 1 && base.Text != null && base.TextBrush != null && base.Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = base.Font.SizeInPoints * paintContext.Scale;
                if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                {
                    RectangleF shadowbounds = Bounds;
                    shadowbounds.Offset(1, 1);
                    StringFormat stringformat = new StringFormat { Alignment = base.TextAlignment };
                    g.DrawString(base.Text, base.Font, black, shadowbounds, stringformat);
                }
            }
            base.Paint(paintContext);
        }
    }


    #endregion Graphobjects
}