using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;
using static ME3Explorer.Dialogue_Editor.DialogueEditorWPF;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using System.Runtime.InteropServices;
using ME1Explorer;
using System.ComponentModel;
using ME3Explorer.SharedUI.Interfaces;

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

            public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; set; }
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

            public ConversationExtended(int ExportUID, string ConvName, PropertyCollection BioConvo, IExportEntry Export, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList)
            {
                this.ExportUID = ExportUID;
                this.ConvName = ConvName;
                this.BioConvo = BioConvo;
                this.Export = Export;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
            }

            public ConversationExtended(int ExportUID, string ConvName, PropertyCollection BioConvo, IExportEntry Export, bool IsParsed, bool IsFirstParsed, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList, IExportEntry WwiseBank, IEntry Sequence, IEntry NonSpkrFFX)
            {
                this.ExportUID = ExportUID;
                this.ConvName = ConvName;
                this.BioConvo = BioConvo;
                this.Export = Export;
                this.IsParsed = IsParsed;
                this.IsFirstParsed = IsFirstParsed;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
                this.WwiseBank = WwiseBank;
                this.Sequence = Sequence;
                this.NonSpkrFFX = NonSpkrFFX;
            }
        }


        #endregion Convo



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

            public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition)
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
            }

            public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition, SpeakerExtended SpeakerTag,
                IExportEntry Interpdata, IExportEntry WwiseStream_Male, IExportEntry WwiseStream_Female, string FaceFX_Male, string FaceFX_Female, int Listener, int ConditionalParam, int TransitionParam, int ExportID,
                bool IsSkippable, bool IsUnskippable, bool IsDefaultAction, bool IsMajorDecision, bool IsNonTextLine, bool IgnoreBodyGesture, bool IsAmbient, int CameraIntimacy, bool HideSubtitle, EConvGUIStyles GUIStyle, EReplyTypes ReplyType)
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
                this.GUIStyle = GUIStyle;
                this.ReplyType = ReplyType;
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

    #region GraphObjects
    public abstract class SeqEdEdge : PPath
    {
        public PNode start;
        public PNode end;
        public DBox originator;
    }
    public class VarEdge : SeqEdEdge
    {
    }

    public class EventEdge : VarEdge
    {
    }

    [DebuggerDisplay("ActionEdge | {originator} to {inputIndex}")]
    public class ActionEdge : SeqEdEdge
    {
        public int inputIndex;
    }

    [DebuggerDisplay("DObj | #{UIndex}: {export.ObjectName}")]
    public abstract class DObj : PNode, IDisposable
    {
        public IMEPackage pcc;
        public ConvGraphEditor g;
        static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        static readonly Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static readonly Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static readonly Color boolColor = Color.FromArgb(215, 37, 33); //red
        static readonly Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static readonly Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
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

        public int Index => export.Index;

        public int UIndex => export.UIndex;
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }
        public IExportEntry Export => export;
        public virtual bool IsSelected { get; set; }

        protected IExportEntry export;
        protected Pen outlinePen;
        protected DText comment;

        protected DObj(ConvGraphEditor ConvGraphEditor)
        {
            //pcc = convoexport.FileRef;
            //export = convoexport;
            g = ConvGraphEditor;
 
        }

        public virtual void CreateConnections(IList<DObj> objects) { }
        public virtual void Layout() { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);
        public virtual IEnumerable<SeqEdEdge> Edges => Enumerable.Empty<SeqEdEdge>();

        public virtual void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("SFrame | #{UIndex}: {export.ObjectName}")]
    public class SFrame : DObj
    {
        protected PPath shape;
        protected PPath titleBox;
        public SFrame(float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(ConvGraphEditor)
        {
            string s = $"{export.ObjectName}_{export.indexValue}";
            float w = 0;
            float h = 0;
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "SizeX")
                {
                    w = (prop as IntProperty);
                }
                if (prop.Name == "SizeY")
                {
                    h = (prop as IntProperty);
                }
            }
            MakeTitleBox(s);
            shape = PPath.CreateRectangle(0, -titleBox.Height, w, h + titleBox.Height);
            outlinePen = new Pen(Color.Black);
            shape.Pen = outlinePen;
            shape.Brush = new SolidBrush(Color.Transparent);
            shape.Pickable = false;
            this.AddChild(shape);
            titleBox.TranslateBy(0, -titleBox.Height);
            this.AddChild(titleBox);
            comment.Y -= titleBox.Height;
            this.Bounds = new RectangleF(0, -titleBox.Height, titleBox.Width, titleBox.Height);
            SetOffset(x, y);
        }

        public override void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }

        protected void MakeTitleBox(string s)
        {
            s = $"#{UIndex} : {s}";
            DText title = new DText(s, titleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            title.Width += 20;
            titleBox = PPath.CreateRectangle(0, 0, title.Width, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
        }
    }

    [DebuggerDisplay("DBox | #{UIndex}: {export.ObjectName}")]
    public abstract class DBox : DObj
    {
        public override IEnumerable<SeqEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Cast<SeqEdEdge>();
        static readonly Color lineColor = Color.FromArgb(74, 63, 190);
        protected static Brush outputBrush = new SolidBrush(Color.Black);

        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
            public List<ActionEdge> Edges;
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
        protected PPath varLinkBox;
        protected PPath outLinkBox;
        public readonly List<OutputLink> Outlinks = new List<OutputLink>();
        protected readonly OutputDragHandler outputDragHandler;

        private static readonly PointF[] downwardTrianglePoly = { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) };
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);
        protected PPath CreateVarLinkBox() => PPath.CreateRectangle(-4, 0, 8, 10);

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
                        if (destAction.UIndex == outLink.Links[j])
                        {
                            PPath p1 = outLink.node;
                            var edge = new ActionEdge();
                            if (p1.Tag == null)
                                p1.Tag = new List<ActionEdge>();
                            ((List<ActionEdge>)p1.Tag).Add(edge);
                            destAction.InputEdges.Add(edge);
                            edge.start = p1;
                            edge.end = destAction;
                            edge.originator = this;
                            edge.inputIndex = outLink.InputIndices[j];
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
            if (title.Width + 40 > w)
            {
                w = title.Width + 40;
            }
            title.Width = w;

            DText line = new DText(l, lineColor, false) //Add line string to right side
            {
                TextAlignment = StringAlignment.Near,
                ConstrainWidthToTextWidth = false,
                ConstrainHeightToTextHeight = false,
                X = w + 5,
                Y = 3,
                Pickable = false
            };

            DText nodeID = new DText(n, titleColor) //Add node count to left side
            {
                TextAlignment = StringAlignment.Near,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };

            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(nodeID);
            titleBox.AddChild(title);
            titleBox.AddChild(line);
            titleBox.Pickable = false;
            return w;
        }

        protected void GetOutputLinks()
        {
            //var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            //if (outLinksProp != null)
            //{
            //    foreach (var prop in outLinksProp)
            //    {
            //        PropertyCollection props = prop.Properties;
            //        var linksProp = props.GetProp<ArrayProperty<StructProperty>>("Links");
            //        if (linksProp != null)
            //        {
            //            OutputLink l = new OutputLink
            //            {
            //                Links = new List<int>(),
            //                InputIndices = new List<int>(),
            //                Edges = new List<ActionEdge>(),
            //                Desc = props.GetProp<StrProperty>("LinkDesc")
            //            };
            //            for (int i = 0; i < linksProp.Count; i++)
            //            {
            //                int linkedOp = linksProp[i].GetProp<ObjectProperty>("LinkedOp").Value;
            //                l.Links.Add(linkedOp);
            //                l.InputIndices.Add(linksProp[i].GetProp<IntProperty>("InputLinkIdx"));
            //                if (OutputNumbers)
            //                    l.Desc = l.Desc + (i > 0 ? "," : ": ") + "#" + linkedOp;
            //            }
            //            l.node = CreateActionLinkBox();
            //            l.node.Brush = outputBrush;
            //            l.node.Pickable = false;
            //            PPath dragger = CreateActionLinkBox();
            //            dragger.Brush = mostlyTransparentBrush;
            //            dragger.X = l.node.X;
            //            dragger.Y = l.node.Y;
            //            dragger.AddInputEventListener(outputDragHandler);
            //            l.node.AddChild(dragger);
            //            Outlinks.Add(l);
            //        }
            //    }
            //}
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

        public void CreateOutlink(PNode n1, PNode n2)
        {
            DBox start = (DBox)n1.Parent.Parent.Parent;
            DiagNode end = (DiagNode)n2.Parent.Parent.Parent;
            IExportEntry startExport = start.export;
            string linkDesc = null;
            foreach (OutputLink l in start.Outlinks)
            {
                if (l.node == n1)
                {
                    if (l.Links.Contains(end.UIndex))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            linkDesc = OutputNumbers ? linkDesc.Substring(0, linkDesc.LastIndexOf(":")) : linkDesc;
            int inputIndex = -1;
            foreach (InputLink l in end.InLinks)
            {
                if (l.node == n2)
                {
                    inputIndex = l.index;
                }
            }
            if (inputIndex == -1)
                return;
            var outLinksProp = startExport.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        var linksProp = prop.GetProp<ArrayProperty<StructProperty>>("Links");
                        linksProp.Add(new StructProperty("SeqOpOutputInputLink", false,
                            new ObjectProperty(end.export, "LinkedOp"),
                            new IntProperty(inputIndex, "InputLinkIdx")));
                        startExport.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

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

        public void RemoveOutlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Outlinks[linkconnection].Desc;
            linkDesc = (OutputNumbers ? linkDesc.Substring(0, linkDesc.LastIndexOf(":")) : linkDesc);
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<StructProperty>>("Links").RemoveAt(linkIndex);
                        export.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

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
        public List<EventEdge> connections = new List<EventEdge>();
        public override IEnumerable<SeqEdEdge> Edges => connections.Union(base.Edges);

        public DStart(int StartNbr, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(x, y, ConvGraphEditor)
        {
            StartNumber = StartNbr;
            outlinePen = new Pen(EventColor);
            string s = $"Start Node: {StartNbr}";
            float starty = 0;
            float w = 15;
            float midW = 50;
            varLinkBox = new PPath();
            GetTitleBox(s, 20);
            OutputLink l = new OutputLink
            {
                Links = new List<int>(StartNbr),
                InputIndices = new List<int>(),
                Edges = new List<ActionEdge>(),
                Desc =$"Out {StartNbr}"
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
        public override IEnumerable<SeqEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public List<ActionEdge> InputEdges = new List<ActionEdge>();
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;
        public StructProperty NodeProp;
        public BioConversationExtended.DialogueNodeExtended Node;
        static readonly Color insideTextColor = Color.FromArgb(213, 213, 213);//white
        protected InputDragHandler inputDragHandler = new InputDragHandler();

        public DiagNode(BioConversationExtended.DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(x, y, ConvGraphEditor)
        {
            Node = node;
            NodeProp = node.NodeProp;

            GetOutputLinks();
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
            outlinePen = new Pen(Color.Black);
            float starty = 8;
            float w = 20;

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


            //InputLinks
            inputLinkBox = new PNode();
            GetInputLinks();
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
            if (inY > starty) starty = inY;
            if (inW + outW + 10 > w) w = inW + outW + 10;

            //TitleBox
            string s = $"{Node.SpeakerTag.SpeakerName}";
            string l = $"{Node.Line}";
            string n = $"{Node.NodeCount}";
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
            if(Node.ConditionalOrBool >= 0)
            {
                string cnd = "Cnd:";
                if (Node.FiresConditional == false)
                    cnd = "Bool:";
                plotCnd = $"{cnd} {Node.ConditionalOrBool}\r\n";
            }
            if (Node.Transition >= 0)
            {
                trans = $"Trans:{Node.Transition}";
            }
            string d = $"{Node.LineStrRef}\r\n{plotCnd}{trans}";

            DText insidetext = new DText(d, insideTextColor, true)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                ConstrainHeightToTextHeight = true,
                X = w / 4,
                Y = titleBox.Height + inY + 5,
                Pickable = false
            };
            h += insidetext.Height;
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            box.AddChild(insidetext);
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(titleBox);
            this.AddChild(outLinkBox);
            this.AddChild(inputLinkBox);
            SetOffset(x, y);
        }

        private void GetInputLinks()
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

            CreateInputLink("Start", 0, true);

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
        public DiagNodeEntry(BioConversationExtended.DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(node, x, y, ConvGraphEditor)
        {
            Node = node;
            NodeProp = node.NodeProp;

            GetOutputLinks();
            originalX = x;
            originalY = y;
        }
    }

    public class DiagNodeReply : DiagNode
    {
        public DiagNodeReply(BioConversationExtended.DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(node, x, y, ConvGraphEditor)
        {
            Node = node;
            NodeProp = node.NodeProp;

            GetOutputLinks();
            originalX = x;
            originalY = y;
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

        public DText(string s, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;

            shadowRendering = shadows;
        }

        public DText(string s, Color c, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
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