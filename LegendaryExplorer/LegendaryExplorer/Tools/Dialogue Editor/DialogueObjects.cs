using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using Piccolo.Util;
using static LegendaryExplorer.Tools.TlkManagerNS.TLKManagerWPF;

namespace LegendaryExplorer.DialogueEditor
{
    [DebuggerDisplay("DiagEdEdge | {originator} to {inputIndex}")]
    public class DiagEdEdge : PPath
    {
        public PNode start;
        public PNode end;
        public DBox originator;
        public int inputIndex;
    }

    [DebuggerDisplay("DObj | #{" + nameof(NodeUID) + "}")]
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
        public int NodeUID;
        public ExportEntry Export => export;
        public virtual bool IsSelected { get; set; }

        protected ExportEntry export;
        protected Pen outlinePen;
        protected DText comment;

        protected DObj(ConvGraphEditor ConvGraphEditor)
        {
            g = ConvGraphEditor;
        }

        public virtual void CreateConnections(IList<DObj> objects) { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);
        public virtual IEnumerable<DiagEdEdge> Edges => Enumerable.Empty<DiagEdEdge>();

        protected Color getColor(EReplyCategory t) =>
            t switch
            {
                EReplyCategory.REPLY_CATEGORY_PARAGON_INTERRUPT => paraintColor,
                EReplyCategory.REPLY_CATEGORY_RENEGADE_INTERRUPT => renintColor,
                EReplyCategory.REPLY_CATEGORY_AGREE => agreeColor,
                EReplyCategory.REPLY_CATEGORY_DISAGREE => disagreeColor,
                EReplyCategory.REPLY_CATEGORY_FRIENDLY => friendlyColor,
                EReplyCategory.REPLY_CATEGORY_HOSTILE => hostileColor,
                _ => Color.Black
            };

        public virtual void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("DBox | #{" + nameof(NodeUID) + "}")]
    public abstract class DBox : DObj
    {
        public override IEnumerable<DiagEdEdge> Edges => Outlinks.SelectMany(l => l.Edges);
        protected static readonly Brush outputBrush = new SolidBrush(Color.Black);
        public static Color lineColor = Color.FromArgb(74, 63, 190);
        public static float LineScaleOption = 1.0f;
        public static bool LinesAtTop;
        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public int InputIndices;
            public string Desc;
            public List<DiagEdEdge> Edges;
            public EReplyCategory RCat;
        }

        public struct InputLink
        {
            public PPath node;
            public string Desc;
            public int index;
            public bool hasName;
            public List<DiagEdEdge> Edges;
        }

        protected PPath titleBox;
        protected PPath outLinkBox;
        public readonly List<OutputLink> Outlinks = new List<OutputLink>();
        protected readonly OutputDragHandler outputDragHandler;
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);

        protected DBox(ConvGraphEditor ConvGraphEditor)
            : base(ConvGraphEditor)
        {
            outputDragHandler = new OutputDragHandler(ConvGraphEditor, this);
        }

        public override void CreateConnections(IList<DObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                foreach (int link in outLink.Links)
                {
                    foreach (DiagNode destAction in objects.OfType<DiagNode>())
                    {
                        if (destAction.NodeID == link)
                        {
                            PPath p1 = outLink.node;
                            var edge = new DiagEdEdge();
                            if (p1.Tag == null)
                                p1.Tag = new List<DiagEdEdge>();
                            ((List<DiagEdEdge>)p1.Tag).Add(edge);
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

        public void RecreateConnections(IList<DObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                foreach (int link in outLink.Links)
                {
                    foreach (DiagNode destAction in objects.OfType<DiagNode>())
                    {
                        if (destAction.NodeID == link)
                        {
                            PPath p1 = outLink.node;
                            var edge = new DiagEdEdge();
                            if (p1.Tag == null)
                                p1.Tag = new List<DiagEdEdge>();
                            ((List<DiagEdEdge>)p1.Tag).Add(edge);
                            destAction.InputEdges.Add(edge);
                            edge.Pen = new Pen(getColor(outLink.RCat));
                            edge.start = p1;
                            edge.end = destAction;
                            edge.originator = this;
                            edge.inputIndex = outLink.InputIndices;
                            g.addEdge(edge);
                            outLink.Edges.Add(edge);
                            destAction.RefreshInputLinks();
                        }
                    }
                }
            }
        }

        public void RemoveConnections()
        {
            foreach (OutputLink outLink in Outlinks)
            {
                DiagEdEdge[] edges = outLink.Edges.ToArray();
                foreach (var e in edges)
                {
                    g.edgeLayer.RemoveChild(e);
                }
                outLink.Edges.Clear();
            }
        }

        protected float GetTitleBox(string s, float w)
        {
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

            float lineX = w / LineScaleOption + 5;
            float lineY = 3;
            if (LinesAtTop)
            {
                lineX = 2;
                lineY = -title.Height + 2;
            }
            DText line = null;
            if (LineScaleOption > 0)
            {
                line = new DText(l, lineColor, false, LineScaleOption) //Add line string to right side
                {
                    TextAlignment = StringAlignment.Near,
                    ConstrainWidthToTextWidth = false,
                    ConstrainHeightToTextHeight = false,
                    X = lineX,
                    Y = lineY,
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
            if (NodeUID < 1000)
            {
                titleBox.Brush = new SolidBrush(entryColor); ;
            }
            else if (NodeUID < 2000)
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
                var edge = new DiagEdEdge();
                if (p1.Tag == null)
                    p1.Tag = new List<DiagEdEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<DiagEdEdge>();
                ((List<DiagEdEdge>)p1.Tag).Add(edge);
                ((List<DiagEdEdge>)p2.Tag).Add(edge);
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
                ConvGraphEditor.UpdateEdge(((List<DiagEdEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                DiagEdEdge edge = ((List<DiagEdEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<DiagEdEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                ConvGraphEditor.edgeLayer.RemoveChild(edge);
                ((List<DiagEdEdge>)((PNode)sender).Tag).RemoveAt(0);
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

        public void RemoveOutlink(DiagEdEdge edge)
        {
            for (int i = 0; i < Outlinks.Count; i++)
            {
                OutputLink outLink = Outlinks[i];
                for (int j = 0; j < outLink.Edges.Count; j++)
                {
                    DiagEdEdge DiagEdEdge = outLink.Edges[j];
                    if (DiagEdEdge == edge)
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

    [DebuggerDisplay("DStart | #{" + nameof(NodeUID) + "}")]
    public sealed class DStart : DBox
    {
        public int StartNumber;
        public int Order;
        private readonly DialogueEditorWindow Editor;

        public DStart(DialogueEditorWindow editor, int orderKey, int StartNbr, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(ConvGraphEditor)
        {
            NodeUID = 2000 + StartNbr;
            Editor = editor;
            Order = orderKey;
            string ordinal = DialogueEditorWindow.AddOrdinal(orderKey + 1);
            StartNumber = StartNbr;
            outlinePen = new Pen(EventColor);
            listname = $"{ordinal} Start Node: {StartNbr}"; ;

            float starty = 0;
            float w = 15;
            float midW = 50;
            GetTitleBox(listname, 20);

            w += titleBox.Width;
            OutputLink l = new OutputLink
            {
                Links = new List<int>(StartNbr),
                InputIndices = new int(),
                Edges = new List<DiagEdEdge>(),
                Desc = $"Out {StartNbr}",
                RCat = EReplyCategory.REPLY_CATEGORY_DEFAULT
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
            Pickable = true;
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

    [DebuggerDisplay("DiagNode | #{NodeUID}")]
    public class DiagNode : DBox
    {
        public override IEnumerable<DiagEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public List<DiagEdEdge> InputEdges = new List<DiagEdEdge>();
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;
        public StructProperty NodeProp;
        public DialogueNodeExtended Node;
        public int NodeID;
        public ObservableCollectionExtended<ReplyChoiceNode> Links = new ObservableCollectionExtended<ReplyChoiceNode>();
        static readonly Color insideTextColor = Color.FromArgb(213, 213, 213);//white
        protected InputDragHandler inputDragHandler = new InputDragHandler();
        protected DialogueEditorWindow Editor;

        public DiagNode(DialogueEditorWindow editor, DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(ConvGraphEditor)
        {
            Editor = editor;
            Node = node;
            NodeProp = node.NodeProp;
            NodeID = Node.NodeCount;
            pcc = editor.Pcc;
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

        public override void Layout(float x, float y)
        {
            if (NodeUID < 1000)
            {
                outlinePen = new Pen(entryPenColor);
            }
            else if (NodeUID < 2000)
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
            string s = $"{Node.SpeakerTag?.SpeakerName ?? "Unknown"}";
            string l = $"{Node.Line}";
            string n = $"E{Node.NodeCount}";
            if (Node.IsReply)
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
            if (Node.ConditionalOrBool >= 0)
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
            if (iw > w) { w = iw; }
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            insidetext.TranslateBy((w - iw) / 2, 0);
            box.AddChild(insidetext);
            Bounds = new RectangleF(0, 0, w, h);
            AddChild(box);
            AddChild(titleBox);
            AddChild(outLinkBox);
            AddChild(inputLinkBox);
            SetOffset(x, y);
        }
        public virtual void GetOutputLinks(DialogueNodeExtended node) { }
        public void GetInputLinks(DialogueNodeExtended node = null)
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
                    Edges = new List<DiagEdEdge>()
                };
                l.node.Brush = outputBrush;
                l.node.MouseEnter += OnMouseEnter;
                l.node.MouseLeave += OnMouseLeave;
                l.node.AddInputEventListener(inputDragHandler);
                InLinks.Add(l);
            }

            if (node != null && !node.IsReply)
            {
                CreateInputLink("Start", 0, true);
            }
            CreateInputLink("In", 1, true);

            if (InputEdges.Any())
            {
                int numInputs = InLinks.Count;
                foreach (DiagEdEdge edge in InputEdges)
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
        public void RefreshInputLinks()
        {
            if (InputEdges.Any() && InLinks != null)
            {
                foreach (DiagEdEdge edge in InputEdges)
                {
                    int inputNum = edge.inputIndex;
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

    public sealed class DiagNodeEntry : DiagNode
    {
        public DiagNodeEntry(DialogueEditorWindow editor, DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
            : base(editor, node, x, y, ConvGraphEditor)
        {
            Node = node;
            NodeProp = node.NodeProp;
            NodeID = node.NodeCount;
            NodeUID = NodeID;
            originalX = x;
            originalY = y;
            listname = $"E{NodeID} {node.Line}";

            GetOutputLinks(Node);
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
        public override void GetOutputLinks(DialogueNodeExtended node)
        {
            if (node != null)
            {
                Links.Clear();
                Outlinks.Clear();
                var rcarray = NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                if (rcarray != null)
                {
                    try
                    {
                        foreach (var rc in rcarray)
                        {
                            var replychoice = new ReplyChoiceNode(-1, "", -1, EReplyCategory.REPLY_CATEGORY_DEFAULT, "No data");
                            var nIDprop = rc.GetProp<IntProperty>("nIndex");
                            if (nIDprop != null)
                            {
                                replychoice.Index = nIDprop.Value;
                            }

                            var strRefPara = rc.GetProp<StringRefProperty>("srParaphrase");
                            if (strRefPara != null)
                            {
                                replychoice.ReplyStrRef = strRefPara.Value;
                                replychoice.ReplyLine = GlobalFindStrRefbyID(replychoice.ReplyStrRef, pcc);
                            }

                            var rcatprop = rc.GetProp<EnumProperty>("Category");
                            if (rcatprop != null)
                            {
                                Enum.TryParse(rcatprop.Value.Name, out EReplyCategory eReply);
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
                if (Links.Count > 0)
                {
                    int n = 0;
                    foreach (var reply in Links)
                    {
                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new int(),
                            Edges = new List<DiagEdEdge>(),
                            Desc = n.ToString(),
                            RCat = reply.RCategory
                        };

                        int linkedOp = reply.Index + 1000;
                        l.Links.Add(linkedOp);
                        l.InputIndices = 0;

                        l.Desc = "R" + reply.Index;
                        l.node = CreateActionLinkBox();
                        var linkcolor = getColor(reply.RCategory);
                        l.node.Brush = new SolidBrush(linkcolor);
                        l.node.Pen = new Pen(getColor(reply.RCategory));
                        l.node.Pickable = false;
                        if (!OutputNumbers)
                        {
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
                            paraphrase.TranslateBy(0, 0);
                        }

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
                        Edges = new List<DiagEdEdge>(),
                        Desc = "Out:",
                        RCat = EReplyCategory.REPLY_CATEGORY_DEFAULT,
                        node = CreateActionLinkBox()
                    };

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

            var newReplyListProp = new ArrayProperty<StructProperty>("ReplyListNew");
            var oldReplyListProp = start.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");

            if (oldReplyListProp != null && oldReplyListProp.Count > 0)
            {
                foreach (var rprop in oldReplyListProp)
                {
                    newReplyListProp.Add(rprop);
                }
            }
            newReplyListProp.Add(new StructProperty("BioDialogReplyListDetails", new PropertyCollection
            {
                new IntProperty(endNode - 1000, "nIndex"),
                new StringRefProperty(663399, "srParaphrase"),
                new StrProperty("", "sParaphrase"),
                new EnumProperty("REPLY_CATEGORY_DEFAULT", "EReplyCategory", Editor.Pcc.Game, "Category"),
                new NoneProperty()
            }));

            Node.NodeProp.Properties.AddOrReplaceProp(newReplyListProp);
            Editor.PushLocalGraphChanges(this);
        }
        public override void RemoveOutlink(int linkconnection, int linkIndex)
        {
            var oldEntriesProp = NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
            oldEntriesProp.RemoveAt(linkconnection);
            NodeProp.Properties.AddOrReplaceProp(oldEntriesProp);
            //Editor.RecreateNodesToProperties(Editor.SelectedConv);
            Editor.PushLocalGraphChanges(this);
        }
    }

    public sealed class DiagNodeReply : DiagNode
    {
        public DiagNodeReply(DialogueEditorWindow editor, DialogueNodeExtended node, float x, float y, ConvGraphEditor ConvGraphEditor)
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
        public override void GetOutputLinks(DialogueNodeExtended node)
        {
            if (node != null)
            {
                Outlinks.Clear();
                Links.Clear();
                var replytoEntryList = node.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                if (replytoEntryList != null)
                {
                    if (replytoEntryList.Count > 0)
                    {
                        int n = 0;
                        foreach (var prop in replytoEntryList)
                        {
                            OutputLink l = new OutputLink
                            {
                                Links = new List<int>(),
                                InputIndices = new int(),
                                Edges = new List<DiagEdEdge>(),
                                Desc = n.ToString(),
                                RCat = EReplyCategory.REPLY_CATEGORY_DEFAULT
                            };

                            int linkedOp = prop.Value;
                            l.Links.Add(linkedOp);
                            l.InputIndices = 1;

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
                            var replychoice = new ReplyChoiceNode(linkedOp, "", -1, EReplyCategory.REPLY_CATEGORY_DEFAULT, "No data");
                            Links.Add(replychoice);
                        }
                    }
                    else //Create default node.
                    {
                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new int(),
                            Edges = new List<DiagEdEdge>(),
                            Desc = "Out:",
                            RCat = EReplyCategory.REPLY_CATEGORY_DEFAULT,
                            node = CreateActionLinkBox()
                        };

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
            DiagNode start = (DiagNode)n1.Parent.Parent.Parent;
            DiagNode end = (DiagNode)n2.Parent.Parent.Parent;
            if (end.GetType() != typeof(DiagNodeEntry))
            {
                MessageBox.Show("You cannot link reply nodes to replies.\r\nReplies must link to entries.", "Dialogue Editor");
                return;
            }

            var startNode = start.NodeID;
            var endNode = end.NodeID;

            var newEntriesProp = new ArrayProperty<IntProperty>("EntryList");
            var oldEntriesProp = start.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
            if (oldEntriesProp != null)
            {
                foreach (var i in oldEntriesProp)
                {
                    newEntriesProp.Add(i);
                }
            }

            newEntriesProp.Add(new IntProperty(endNode));
            start.NodeProp.Properties.AddOrReplaceProp(newEntriesProp);  //Push to Property

            Editor.PushLocalGraphChanges(this);
        }
        public override void RemoveOutlink(int linkconnection, int linkIndex)
        {
            var oldEntriesProp = NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
            oldEntriesProp.RemoveAt(linkconnection);
            NodeProp.Properties.AddOrReplaceProp(oldEntriesProp);
            Editor.PushLocalGraphChanges(this);
        }
    }
    public class DText : PText
    {
        private readonly Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }

        public DText(string s, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        public DText(string s, Color c, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            //paints dropshadow
            if (shadowRendering && paintContext.Scale >= 1 && base.Text != null && base.TextBrush != null && base.Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = FontSizeInPoints * paintContext.Scale;
                if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                {
                    RectangleF shadowbounds = Bounds;
                    shadowbounds.Offset(1, 1);
                    var stringformat = new StringFormat { Alignment = base.TextAlignment };
                    g.DrawString(base.Text, base.Font, black, shadowbounds, stringformat);
                }
            }
            base.Paint(paintContext);
        }
    }
}