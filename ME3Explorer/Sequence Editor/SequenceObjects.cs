using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;
using System.Runtime.InteropServices;
using ME1Explorer;

namespace ME3Explorer.SequenceObjects
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };
    public abstract class SeqEdEdge : PPath
    {
        public PNode start;
        public PNode end;
    }
    public class VarEdge : SeqEdEdge
    {
    }

    public class ActionEdge : SeqEdEdge
    {
        public int inputIndex;
    }

    public abstract class SObj : PNode, IDisposable
    {
        public IMEPackage pcc;
        public GraphEditor g;
        static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        static readonly Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static readonly Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static readonly Color boolColor = Color.FromArgb(215, 37, 33); //red
        static readonly Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static readonly Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        protected static Color titleColor = Color.FromArgb(255, 255, 128);
        protected static Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));
        protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink = false;
        public static bool draggingVarlink = false;
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
        protected SText comment;

        protected SObj(IExportEntry entry, GraphEditor grapheditor)
        {
            pcc = entry.FileRef;
            export = entry;
            g = grapheditor;
            comment = new SText(GetComment(), commentColor, false)
            {
                Pickable = false,
                X = 0
            };
            comment.Y = 0 - comment.Height;
            AddChild(comment);
            Pickable = true;
        }

        public virtual void CreateConnections(IList<SObj> objects) { }
        public virtual void Layout() { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);

        public virtual IEnumerable<SeqEdEdge> Edges => Enumerable.Empty<SeqEdEdge>();
        protected string GetComment()
        {
            string res = "";
            var comments = export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
            if (comments != null)
            {
                foreach (var s in comments)
                {
                    res += s + "\n";
                }
            }
            return res;
        }

        protected Color getColor(VarTypes t)
        {
            switch (t)
            {
                case VarTypes.Int:
                    return intColor;
                case VarTypes.Float:
                    return floatColor;
                case VarTypes.Bool:
                    return boolColor;
                case VarTypes.Object:
                    return objectColor;
                case VarTypes.MatineeData:
                    return interpDataColor;
                default:
                    return Color.Black;
            }
        }

        protected VarTypes getType(string s)
        {
            if (s.Contains("InterpData"))
                return VarTypes.MatineeData;
            if (s.Contains("Int"))
                return VarTypes.Int;
            if (s.Contains("Bool"))
                return VarTypes.Bool;
            if (s.Contains("Object") || s.Contains("Player"))
                return VarTypes.Object;
            if (s.Contains("Float"))
                return VarTypes.Float;
            if (s.Contains("StrRef"))
                return VarTypes.StrRef;
            if (s.Contains("String"))
                return VarTypes.String;
            return VarTypes.Extern;
        }

        public virtual void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    public class SVar : SObj
    {
        public const float RADIUS = 30;

        public List<VarEdge> connections = new List<VarEdge>();
        public override IEnumerable<SeqEdEdge> Edges => connections;
        public VarTypes type { get; set; }
        readonly SText val;
        protected PPath shape;
        public string Value
        {
            get => val.Text;
            set => val.Text = value;
        }

        public SVar(IExportEntry entry, float x, float y, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            string s = export.ObjectName;
            s = s.Replace("BioSeqVar_", "");
            s = s.Replace("SFXSeqVar_", "");
            s = s.Replace("SeqVar_", "");
            type = getType(s);
            const float w = RADIUS * 2;
            const float h = RADIUS * 2;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(getColor(type));
            shape.Pen = outlinePen;
            shape.Brush = nodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if ((prop.Name == "VarName" || prop.Name == "varName")
                    && prop is NameProperty nameProp)
                {
                    SText VarName = new SText(nameProp.Value, Color.Red, false)
                    {
                        Pickable = false,
                        TextAlignment = StringAlignment.Center,
                        Y = h
                    };
                    VarName.X = w / 2 - VarName.Width / 2;
                    AddChild(VarName);
                    break;
                }
            }
            SetOffset(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        public string GetValue()
        {
            try
            {
                var props = export.GetProperties();
                switch (type)
                {
                    case VarTypes.Int:
                        if (export.ObjectName == "BioSeqVar_StoryManagerInt")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Int\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<IntProperty>("IntValue") is IntProperty intValue)
                        {
                            return intValue.Value.ToString();
                        }
                        return "0";
                    case VarTypes.Float:
                        if (export.ObjectName == "BioSeqVar_StoryManagerFloat")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Float\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<FloatProperty>("FloatValue") is FloatProperty floatValue)
                        {
                            return floatValue.Value.ToString();
                        }
                        return "0.00";
                    case VarTypes.Bool:
                        if (export.ObjectName == "BioSeqVar_StoryManagerBool")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Bool\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<IntProperty>("bValue") is IntProperty bValue)
                        {
                            return (bValue.Value == 1).ToString();
                        }
                        return "False";
                    case VarTypes.Object:
                        if (export.ObjectName == "SeqVar_Player")
                            return "Player";
                        foreach (var prop in props)
                        {
                            switch (prop)
                            {
                                case NameProperty nameProp when nameProp.Name == "m_sObjectTagToFind":
                                    return nameProp.Value;
                                case StrProperty strProp when strProp.Name == "m_sObjectTagToFind":
                                    return strProp.Value;
                                case ObjectProperty objProp when objProp.Name == "ObjValue":
                                    {
                                        IEntry entry = pcc.getEntry(objProp.Value);
                                        if (entry == null) return "???";
                                        if (entry is IExportEntry objValueExport && objValueExport.GetProperty<NameProperty>("Tag") is NameProperty tagProp && tagProp.Value != objValueExport.ObjectName)
                                        {
                                            return $"{entry.ObjectName}\n{ tagProp.Value}";
                                        }
                                        else
                                        {
                                            return entry.ObjectName;
                                        }
                                    }
                            }
                        }
                        return "???";
                    case VarTypes.StrRef:
                        foreach (var prop in props)
                        {
                            if ((prop.Name == "m_srValue" || prop.Name == "m_srStringID")
                                && prop is StringRefProperty strRefProp)
                            {
                                switch (pcc.Game)
                                {
                                    case MEGame.ME1:
                                        return ME1TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.ME2:
                                        return ME2Explorer.ME2TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.ME3:
                                        return ME3TalkFiles.findDataById(strRefProp.Value);
                                    case MEGame.UDK:
                                        return "UDK StrRef not supported";
                                }
                            }
                        }
                        return "???";
                    case VarTypes.String:
                        var strValue = props.GetProp<StrProperty>("StrValue");
                        if (strValue != null)
                        {
                            return strValue.Value;
                        }
                        return "???";
                    case VarTypes.Extern:
                        foreach (var prop in props)
                        {
                            switch (prop)
                            {
                                //Named Variable
                                case NameProperty nameProp when nameProp.Name == "FindVarName":
                                    return $"< {nameProp.Value} >";
                                //SeqVar_Name
                                case NameProperty nameProp when nameProp.Name == "NameValue":
                                    return nameProp.Value;
                                //External
                                case StrProperty strProp when strProp.Name == "VariableLabel":
                                    return $"Extern:\n{strProp.Value}";
                            }
                        }
                        return "???";
                    case VarTypes.MatineeData:
                        return $"#{UIndex}\nInterpData";
                    default:
                        return "???";
                }
            }
            catch (Exception)
            {
                return "???";
            }
        }

        void appendToComment(string s)
        {
            if (comment.Text.Length > 0)
            {
                comment.TranslateBy(0, -1 * comment.Height);
                comment.Text += s + "\n";
            }
            else
            {
                comment.Text += s + "\n";
                comment.TranslateBy(0, -1 * comment.Height);
            }
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
                    shape.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    shape.Pen = outlinePen;
                }
            }
        }

        public override bool Intersects(RectangleF bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }

        public override void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    public class SFrame : SObj
    {
        protected PPath shape;
        protected PPath titleBox;
        public SFrame(IExportEntry entry, float x, float y, GraphEditor grapheditor)
            : base(entry, grapheditor)
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
            SText title = new SText(s, titleColor)
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

    public abstract class SBox : SObj
    {
        public override IEnumerable<SeqEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Union(Varlinks.SelectMany(l => l.Edges).Cast<SeqEdEdge>());

        protected static Brush outputBrush = new SolidBrush(Color.Black);

        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
            public List<ActionEdge> Edges;
        }

        public struct VarLink
        {
            public PPath node;
            public List<int> Links;
            public string Desc;
            public VarTypes type;
            public bool writeable;
            public List<VarEdge> Edges;
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
        public readonly List<VarLink> Varlinks = new List<VarLink>();
        protected readonly VarDragHandler varDragHandler;
        protected readonly OutputDragHandler outputDragHandler;
        private static readonly PointF[] downwardTrianglePoly = { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) };
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);

        protected SBox(IExportEntry entry, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            varDragHandler = new VarDragHandler(grapheditor, this);
            outputDragHandler = new OutputDragHandler(grapheditor, this);
        }

        public override void CreateConnections(IList<SObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                foreach (SAction destAction in objects.OfType<SAction>())
                {
                    for (int j = 0; j < outLink.Links.Count; j++)
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
                            edge.inputIndex = outLink.InputIndices[j];
                            g.addEdge(edge);
                            outLink.Edges.Add(edge);
                        }
                    }
                }
            }
            foreach (VarLink varLink in Varlinks)
            {
                foreach (SVar destVar in objects.OfType<SVar>())
                {
                    foreach (int link in varLink.Links)
                    {
                        if (destVar.UIndex == link)
                        {
                            PPath p1 = varLink.node;
                            var edge = new VarEdge();
                            if (destVar.ChildrenCount > 1)
                                edge.Pen = ((PPath)destVar[1]).Pen;
                            if (p1.Tag == null)
                                p1.Tag = new List<VarEdge>();
                            ((List<VarEdge>)p1.Tag).Add(edge);
                            destVar.connections.Add(edge);
                            edge.start = p1;
                            edge.end = destVar;
                            g.addEdge(edge);
                            varLink.Edges.Add(edge);
                        }
                    }
                }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            s = $"#{UIndex} : {s}";
            SText title = new SText(s, titleColor)
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

        protected void GetVarLinks()
        {
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linkedVars = props.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                    if (linkedVars != null)
                    {
                        VarLink l = new VarLink
                        {
                            Links = new List<int>(),
                            Edges = new List<VarEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc"),
                            type = getType(pcc.getObjectName(props.GetProp<ObjectProperty>("ExpectedType").Value))
                        };
                        foreach (var objProp in linkedVars)
                        {
                            l.Links.Add(objProp.Value);
                        }
                        PPath dragger;
                        if (props.GetProp<BoolProperty>("bWriteable").Value)
                        {
                            //downward pointing triangle
                            l.node = PPath.CreatePolygon(downwardTrianglePoly);
                            dragger = PPath.CreatePolygon(downwardTrianglePoly);
                        }
                        else
                        {
                            l.node = PPath.CreateRectangle(-4, 0, 8, 10);
                            dragger = PPath.CreateRectangle(-4, 0, 8, 10);
                        }
                        l.node.Brush = new SolidBrush(getColor(l.type));
                        l.node.Pen = new Pen(getColor(l.type));
                        l.node.Pickable = false;
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.Pen = l.node.Pen;
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(varDragHandler);
                        l.node.AddChild(dragger);
                        Varlinks.Add(l);
                    }
                }
            }
        }

        protected void GetOutputLinks()
        {
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linksProp = props.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (linksProp != null)
                    {
                        OutputLink l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new List<int>(),
                            Edges = new List<ActionEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc")
                        };
                        for (int i = 0; i < linksProp.Count; i++)
                        {
                            int linkedOp = linksProp[i].GetProp<ObjectProperty>("LinkedOp").Value;
                            l.Links.Add(linkedOp);
                            l.InputIndices.Add(linksProp[i].GetProp<IntProperty>("InputLinkIdx"));
                            if (OutputNumbers)
                                l.Desc = l.Desc + (i > 0 ? "," : ": ") + "#" + linkedOp;
                        }
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

        protected class OutputDragHandler : PDragEventHandler
        {
            private readonly GraphEditor graphEditor;
            private readonly SBox sObj;
            public OutputDragHandler(GraphEditor graph, SBox obj)
            {
                graphEditor = graph;
                sObj = obj;
            }
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                sObj.MoveToBack();
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
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge(((List<ActionEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                ActionEdge edge = ((List<ActionEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<ActionEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                graphEditor.edgeLayer.RemoveChild(edge);
                ((List<ActionEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingOutlink = false;
                if (dragTarget != null)
                {
                    sObj.CreateOutlink(((PPath)sender).Parent, dragTarget);
                    dragTarget = null;
                }
            }
        }

        protected class VarDragHandler : PDragEventHandler
        {
            private readonly GraphEditor graphEditor;
            private readonly SBox sObj;
            public VarDragHandler(GraphEditor graph, SBox obj)
            {
                graphEditor = graph;
                sObj = obj;
            }

            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                sObj.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                var edge = new VarEdge();
                if (p1.Tag == null)
                    p1.Tag = new List<VarEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<VarEdge>();
                ((List<VarEdge>)p1.Tag).Add(edge);
                ((List<VarEdge>)p2.Tag).Add(edge);
                edge.start = p1;
                edge.end = p2;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingVarlink = true;

            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge(((List<VarEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                VarEdge edge = ((List<VarEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<VarEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                graphEditor.edgeLayer.RemoveChild(edge);
                ((List<VarEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingVarlink = false;
                if (dragTarget != null)
                {
                    sObj.CreateVarlink(((PPath)sender).Parent, (SVar)dragTarget);
                    dragTarget = null;
                }
            }
        }

        public void CreateOutlink(PNode n1, PNode n2)
        {
            SBox start = (SBox)n1.Parent.Parent.Parent;
            SAction end = (SAction)n2.Parent.Parent.Parent;
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

        public void CreateVarlink(PNode p1, SVar end)
        {
            SBox start = (SBox)p1.Parent.Parent.Parent;
            IExportEntry startExport = start.export;
            string linkDesc = null;
            foreach (VarLink l in start.Varlinks)
            {
                if (l.node == p1)
                {
                    if (l.Links.Contains(end.UIndex))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            var varLinksProp = startExport.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Add(new ObjectProperty(end.Export));
                        startExport.WriteProperty(varLinksProp);
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

        public void RemoveVarlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Varlinks[linkconnection].Desc;
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").RemoveAt(linkIndex);
                        export.WriteProperty(varLinksProp);
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
                Outlinks.ForEach(x => x.node[0].RemoveInputEventListener(outputDragHandler));
            }
            if (varDragHandler != null)
            {
                Varlinks.ForEach(x => x.node[0].RemoveInputEventListener(varDragHandler));
            }
        }
    }

    public class SEvent : SBox
    {

        public SEvent(IExportEntry entry, float x, float y, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            outlinePen = new Pen(Color.FromArgb(214, 30, 28));
            string s = export.ObjectName;
            s = s.Replace("BioSeqEvt_", "");
            s = s.Replace("SFXSeqEvt_", "");
            s = s.Replace("SeqEvt_", "");
            s = s.Replace("SeqEvent_", "");
            s += "_" + export.indexValue;
            float starty = 0;
            float w = 15;
            float midW = 0;
            varLinkBox = new PPath();
            GetVarLinks();
            foreach (var varLink in Varlinks)
            {
                string d = string.Join(",", varLink.Links.Select(l => $"#{l}"));
                SText t2 = new SText(d + "\n" + varLink.Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                varLink.node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(varLink.node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.AddRectangle(0, 0, w, varLinkBox[0].Height);
            varLinkBox.Pickable = false;
            varLinkBox.Pen = outlinePen;
            varLinkBox.Brush = nodeBrush;
            GetOutputLinks();
            outLinkBox = new PPath();
            for (int i = 0; i < Outlinks.Count; i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
                if (t2.Width + 10 > midW) midW = t2.Width + 10;
                //t2.TextAlignment = StringAlignment.Far;
                //t2.ConstrainWidthToTextWidth = false;
                t2.X = 0 - t2.Width;
                t2.Y = starty + 3;
                starty += t2.Height + 6;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.AddPolygon(new[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f * midW, starty + 30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW / -2, -30) });
            outLinkBox.Pickable = false;
            outLinkBox.Pen = outlinePen;
            outLinkBox.Brush = nodeBrush;
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name.Name.Contains("EventName") || prop.Name == "sScriptName")
                    s += "\n\"" + (prop as NameProperty) + "\"";
                else if (prop.Name == "InputLabel" || prop.Name == "sEvent")
                    s += "\n\"" + (prop as StrProperty) + "\"";
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                if (midW > tW)
                {
                    w = midW;
                    titleBox.Width = w;
                }
                else
                {
                    w = tW;
                }
                varLinkBox.Width = w;
            }
            float h = titleBox.Height + 1;
            outLinkBox.TranslateBy(titleBox.Width / 2 + midW / 2, h + 30);
            h += outLinkBox.Height + 1;
            varLinkBox.TranslateBy(0, h);
            h += varLinkBox.Height;
            this.bounds = new RectangleF(0, 0, w, h);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.SetOffset(x, y);
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
                    varLinkBox.Pen = selectedPen;
                    outLinkBox.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    varLinkBox.Pen = outlinePen;
                    outLinkBox.Pen = outlinePen;
                }
            }
        }

        //public override bool Intersects(RectangleF bounds)
        //{
        //    Region hitregion = new Region(outLinkBox.PathReference);
        //    //hitregion.Union(titleBox.PathReference);
        //    //hitregion.Union(varLinkBox.PathReference);
        //    return hitregion.IsVisible(bounds);
        //}
    }

    public class SAction : SBox
    {
        public override IEnumerable<SeqEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public List<ActionEdge> InputEdges = new List<ActionEdge>();
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;

        protected InputDragHandler inputDragHandler = new InputDragHandler();

        public SAction(IExportEntry entry, float x, float y, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            GetVarLinks();
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
                    ((PPath)this[1]).Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
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
            string s = export.ObjectName;
            s = s.Replace("BioSeqAct_", "").Replace("SFXSeqAct_", "")
                 .Replace("SeqAct_", "").Replace("SeqCond_", "");
            float starty = 8;
            float w = 20;
            varLinkBox = new PPath();
            for (int i = 0; i < Varlinks.Count; i++)
            {
                string d = string.Join(",", Varlinks[i].Links.Select(l => $"#{l}"));
                SText t2 = new SText(d + "\n" + Varlinks[i].Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.Height = varLinkBox[0].Height;
            varLinkBox.Width = w;
            varLinkBox.Pickable = false;
            outLinkBox = new PPath();
            float outW = 0;
            for (int i = 0; i < Outlinks.Count; i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
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
            inputLinkBox = new PNode();
            GetInputLinks();
            float inW = 0;
            float inY = 8;
            for (int i = 0; i < InLinks.Count; i++)
            {
                SText t2 = new SText(InLinks[i].Desc);
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
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case ObjectProperty objProp when objProp.Name == "oSequenceReference":
                        {
                            string seqName = pcc.getEntry(objProp.Value).ObjectName;
                            if (pcc.Game == MEGame.ME1
                                && objProp.Value > 0
                                && seqName == "Sequence"
                                && pcc.getExport(objProp.Value - 1).GetProperty<StrProperty>("ObjName") is StrProperty objNameProp)
                            {
                                seqName = objNameProp;
                            }
                            s += $"\n\"{seqName}\"";
                            break;
                        }
                    case NameProperty nameProp when nameProp.Name == "EventName" || nameProp.Name == "StateName":
                        s += $"\n\"{nameProp}\"";
                        break;
                    case StrProperty strProp when strProp.Name == "OutputLabel" || strProp.Name == "m_sMovieName":
                        s += $"\n\"{strProp}\"";
                        break;
                    case ObjectProperty objProp when objProp.Name == "m_pEffect":
                        s += $"\n\"{pcc.getEntry(objProp.Value).ObjectName}\"";
                        break;
                }
            }
            float tW = GetTitleBox(s, w);
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
            varLinkBox.TranslateBy(varLinkBox.Width < w ? (w - varLinkBox.Width) / 2 : 0, h);
            h += varLinkBox.Height;
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.AddChild(inputLinkBox);
            SetOffset(x, y);
        }

        private void GetInputLinks()
        {
            InLinks = new List<InputLink>();
            var inputLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (export.ClassName == "SequenceReference")
            {
                var oSequenceReference = export.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSequenceReference != null)
                {
                    inputLinksProp = pcc.getExport(oSequenceReference.Value - 1).GetProperty<ArrayProperty<StructProperty>>("InputLinks");
                }
            }

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

            if (inputLinksProp != null)
            {
                for (int i = 0; i < inputLinksProp.Count; i++)
                {
                    CreateInputLink(inputLinksProp[i].GetProp<StrProperty>("LinkDesc"), i);
                }
            }
            else if (pcc.Game == MEGame.ME3)
            {
                try
                {
                    if (ME3UnrealObjectInfo.getSequenceObjectInfo(export.ClassName)?.inputLinks is List<string> inputLinks)
                    {
                        for (int i = 0; i < inputLinks.Count; i++)
                        {
                            CreateInputLink(inputLinks[i], i);
                        }
                    }
                }
                catch (Exception)
                {
                    InLinks.Clear();
                }
            }
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
                    //change the end of the edge to the input box, not the SAction
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

    public class SText : PText
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private readonly Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }
        private static PrivateFontCollection fontcollection;
        private static Font kismetFont;

        public SText(string s, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;

            shadowRendering = shadows;
        }

        public SText(string s, Color c, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
            shadowRendering = shadows;
        }

        //must be called once in the program before SText can be used
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
}