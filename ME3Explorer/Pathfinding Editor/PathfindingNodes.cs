using System;
using System.Collections.Generic;
using System.Collections;
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
using UMD.HCIL.PathingGraphEditor;
using ME3Explorer.Pathfinding_Editor;

namespace ME3Explorer.PathfindingNodes
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };

    public abstract class PathfindingNode : PathfindingNodeMaster
    {
        //protected PPath shape;
        //public IMEPackage pcc;
        //public PathingGraphEditor g;
        //public static ME1Explorer.TalkFiles talkfiles { get; set; }
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        static Pen blackPen = Pens.Black;
        static Pen halfReachSpecPen = Pens.Gray;
        static Pen slotToSlotPen = Pens.DarkOrange;
        static Pen sfxLadderPen = Pens.Purple;
        static Pen sfxBoostPen = Pens.Blue;
        static Pen sfxJumpDownPen = Pens.Maroon;
        static Pen sfxLargeBoostPen = Pens.DeepPink;

        private Pen edgePen = blackPen;
        //protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        //protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        //public static bool draggingOutlink = false;
        //public static bool draggingVarlink = false;
        //public static PNode dragTarget;
        //public static bool OutputNumbers;

        //public int Index { get { return index; } }
        ////public float Width { get { return shape.Width; } }
        ////public float Height { get { return shape.Height; } }

        //protected int index;
        //public IExportEntry export;
        //protected Pen outlinePen;
        //protected SText comment;
        public List<IExportEntry> ReachSpecs = new List<IExportEntry>();


        protected PathfindingNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
            Tag = new ArrayList(); //outbound reachspec edges.
            pcc = p;
            g = grapheditor;
            index = idx;
            export = pcc.getExport(index);
            comment = new SText(GetComment(), commentColor, false);
            comment.X = 0;
            comment.Y = 65 - comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
        }

        protected PathfindingNode(int idx, IMEPackage p)
        {
            pcc = p;
            index = idx;
            export = pcc.getExport(index);
            comment = new SText(GetComment(), commentColor, false);
            comment.X = 0;
            comment.Y = 0 - comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
        }



        /// <summary>
        /// Creates the reachspec connections from this pathfinding node to others.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            var outLinksProp = export.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    int reachspecexport = prop.Value;
                    ReachSpecs.Add(pcc.Exports[reachspecexport - 1]);
                }

                foreach (IExportEntry spec in ReachSpecs)
                {
                    Pen penToUse = halfReachSpecPen;
                    switch (spec.ObjectName)
                    {
                        case "SlotToSlotReachSpec":
                            penToUse = slotToSlotPen;
                            break;
                        case "SFXLadderReachSpec":
                            penToUse = sfxLadderPen;
                            break;
                        case "SFXLargeBoostReachSpec":
                            penToUse = sfxLargeBoostPen;
                            break;
                        case "SFXBoostReachSpec":
                            penToUse = sfxBoostPen;
                            break;
                        case "SFXJumpDownReachSpec":
                            penToUse = sfxJumpDownPen;
                            break;
                    }
                    //Get ending
                    PNode othernode = null;
                    int othernodeidx = 0;
                    PropertyCollection props = spec.GetProperties();
                    foreach (var prop in props)
                    {
                        if (prop.Name == "End")
                        {
                            PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                            foreach (var rprop in reachspecprops)
                            {
                                if (rprop.Name == "Actor")
                                {
                                    othernodeidx = (rprop as ObjectProperty).Value;
                                    break;
                                }
                            }
                        }
                    }

                    if (othernodeidx != 0)
                    {
                        foreach (PathfindingNodeMaster node in Objects)
                        {
                            if (node.export.UIndex == othernodeidx)
                            {
                                othernode = node;

                                //Check for returning reachspec for pen drawing. This is going to incur a significant performance penalty...
                                IExportEntry otherNode = node.export;
                                var otherNodePathList = otherNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                                if (otherNodePathList != null)
                                {
                                    bool keepParsing = true;
                                    foreach (var path in otherNodePathList)
                                    {
                                        int reachspecexport = path.Value;
                                        IExportEntry possibleIncomingSpec = pcc.Exports[reachspecexport - 1];
                                        PropertyCollection otherSpecProperties = possibleIncomingSpec.GetProperties();
                                        foreach (var otherSpecProp in otherSpecProperties)
                                        {
                                            if (otherSpecProp.Name == "End")
                                            {
                                                PropertyCollection reachspecprops = (otherSpecProp as StructProperty).Properties;
                                                foreach (var rprop in reachspecprops)
                                                {
                                                    if (rprop.Name == "Actor")
                                                    {
                                                        othernodeidx = (rprop as ObjectProperty).Value;
                                                        if (othernodeidx == export.UIndex)
                                                        {
                                                            keepParsing = false;
                                                            if (penToUse == halfReachSpecPen)
                                                            {
                                                                penToUse = blackPen;
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (!keepParsing)
                                        {
                                            break;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        if (othernode != null)
                        {
                            IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
                            IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

                            if (radius != null && height != null && (radius >= PathfindingNodeInfoPanel.MINIBOSS_RADIUS || height >= PathfindingNodeInfoPanel.MINIBOSS_HEIGHT))
                            {
                                penToUse = (Pen)penToUse.Clone();
                                if (radius >= PathfindingNodeInfoPanel.BOSS_RADIUS && height >= PathfindingNodeInfoPanel.BOSS_HEIGHT)
                                {
                                    penToUse.Width = 3;
                                }
                                else
                                {
                                    penToUse.Width = 2;
                                }
                            }

                            PPath edge = new PPath();
                            edge.Pen = penToUse;
                            ((ArrayList)Tag).Add(edge);
                            ((ArrayList)othernode.Tag).Add(edge);
                            edge.Tag = new ArrayList();
                            ((ArrayList)edge.Tag).Add(this);
                            ((ArrayList)edge.Tag).Add(othernode);

                            g.edgeLayer.AddChild(edge);
                        }
                    }
                }
            }
        }

        public virtual void Layout(float x, float y) { }


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
            else if (s.Contains("Int"))
                return VarTypes.Int;
            else if (s.Contains("Bool"))
                return VarTypes.Bool;
            else if (s.Contains("Object") || s.Contains("Player"))
                return VarTypes.Object;
            else if (s.Contains("Float"))
                return VarTypes.Float;
            else if (s.Contains("StrRef"))
                return VarTypes.StrRef;
            else if (s.Contains("String"))
                return VarTypes.String;
            else
                return VarTypes.Extern;
        }
    }

    public class PathNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(34, 218, 218);

        public PathNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class BioPathPoint : PathNode
    {
        public BioPathPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }         //SFXDynamicCoverLink, SFXDynamicCoverSlotMarker

    public class CoverLink : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(63, 102, 207);
        PointF[] coverShape = new PointF[] { new PointF(0, 50), new PointF(0, 35), new PointF(15, 35), new PointF(15, 0), new PointF(35, 0), new PointF(35, 35), new PointF(50, 35), new PointF(50, 50) };

        public CoverLink(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(coverShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXDynamicCoverLink : CoverLink
    {
        public SFXDynamicCoverLink(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }



    public class SFXNav_BoostNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(17, 189, 146);
        PointF[] boostdownshape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 50), new PointF(40, 40), new PointF(30, 50), new PointF(20, 40), new PointF(10, 50), new PointF(0, 40) };
        PointF[] boostbottomshape = new PointF[] { new PointF(0, 50), new PointF(50, 50), new PointF(50, 0), new PointF(40, 10), new PointF(30, 0), new PointF(20, 10), new PointF(10, 0), new PointF(0, 10) };

        public SFXNav_BoostNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = boostdownshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = boostbottomshape;
            }
            shape = PPath.CreatePolygon(shapetouse);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_JumpDownNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(191, 82, 93);
        //        PointF[] mantleshape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        PointF[] jumptopshape = new PointF[] { new PointF(0, 0), new PointF(40, 0), new PointF(40, 35), new PointF(50, 35), new PointF(35, 50), new PointF(20, 35), new PointF(30, 35), new PointF(30, 10), new PointF(0, 10) };
        PointF[] jumplandingshape = new PointF[] { new PointF(15, 0), new PointF(35, 0), new PointF(35, 20), new PointF(50, 20), new PointF(25, 40), new PointF(50, 40), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(25, 40), new PointF(0, 20), new PointF(15, 20) };

        public SFXNav_JumpDownNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = jumptopshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = jumplandingshape;
            }
            shape = PPath.CreatePolygon(shapetouse);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_LadderNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(127, 76, 186);
        //        PointF[] mantleshape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        PointF[] laddertopshape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(30, 10), new PointF(30, 20), new PointF(50, 20), new PointF(50, 30), new PointF(30, 30), new PointF(30, 40),
            new PointF(35, 40), new PointF(25, 50), new PointF(15, 40),
            new PointF(20, 40), new PointF(20, 30), new PointF(0, 30),new PointF(0, 20), new PointF(20, 20),new PointF(20, 10), new PointF(0, 10) };

        PointF[] ladderbottomshape = new PointF[] { new PointF(15, 10), new PointF(25, 0), new PointF(35, 10), new PointF(30, 10), new PointF(30, 10), new PointF(30, 20), new PointF(50, 20), new PointF(50, 30), new PointF(30, 30), new PointF(30, 40), new PointF(50, 40), new PointF(50, 50),
            new PointF(0, 50), new PointF(0, 40), new PointF(20, 40), new PointF(20, 30), new PointF(0, 30),new PointF(0, 20), new PointF(20, 20),new PointF(20, 10) };

        public SFXNav_LadderNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = laddertopshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = ladderbottomshape;
            }
            shape = PPath.CreatePolygon(shapetouse);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_LargeBoostNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(219, 112, 147);
        PointF[] doubleboostshape = new PointF[] { new PointF(0, 10), new PointF(10, 0), new PointF(20, 10), new PointF(30, 0), new PointF(40, 10), new PointF(50, 0), new PointF(50, 50), new PointF(40, 40), new PointF(30, 50), new PointF(20, 40), new PointF(10, 50), new PointF(0, 40) };

        public SFXNav_LargeBoostNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(doubleboostshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class WwiseAmbientSound : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 255, 0);
        PointF[] soundShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(40, 0), new PointF(50, 0), new PointF(50, 10), new PointF(40, 10), new PointF(25, 50), new PointF(10, 10), new PointF(0, 10), new PointF(0, 0), new PointF(10, 0) };

        public WwiseAmbientSound(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(soundShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        /// <summary>
        /// Creates the connection to the annex node.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            var annexZoneLocProp = export.GetProperty<ObjectProperty>("AnnexZoneLocation");
            if (annexZoneLocProp != null)
            {
                //IExportEntry annexzonelocexp = pcc.Exports[annexZoneLocProp.Value - 1];

                PathfindingNode othernode = null;
                int othernodeidx = annexZoneLocProp.Value - 1;
                if (othernodeidx != 0)
                {
                    foreach (PathfindingNode node in Objects)
                    {
                        if (node.export.Index == othernodeidx)
                        {
                            othernode = node;
                            break;
                        }
                    }
                }

                if (othernode != null)
                {
                    PPath edge = new PPath();
                    ((ArrayList)Tag).Add(edge);
                    ((ArrayList)othernode.Tag).Add(edge);
                    edge.Tag = new ArrayList();
                    ((ArrayList)edge.Tag).Add(this);
                    ((ArrayList)edge.Tag).Add(othernode);
                    g.edgeLayer.AddChild(edge);
                }
            }
        }
    }

    public class SFXDoorMarker : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 100, 0);
        PointF[] doorshape = new PointF[] { new PointF(0, 25), new PointF(10, 0), new PointF(10, 13), new PointF(40, 13), new PointF(40, 0), new PointF(50, 25), new PointF(40, 50), new PointF(40, 37), new PointF(10, 37), new PointF(10, 50) };

        public SFXDoorMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(doorshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    /*public class BioPathPoint : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 165, 0);
        PointF[] upleftarrowshape = new PointF[] { new PointF(0, 0), new PointF(39, 0), new PointF(27, 13), new PointF(49, 35), new PointF(35, 49), new PointF(13, 27), new PointF(0, 39) };

        public BioPathPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(upleftarrowshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }*/

    //This is technically not a pathnode...
    public class SFXObjectiveSpawnPoint : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 255, 0);
        PointF[] triangleshape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50) };

        public SFXObjectiveSpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            string commentText = comment.Text + "\n";

            var spawnTagsProp = export.GetProperty<ArrayProperty<StrProperty>>("SupportedSpawnTags");
            if (spawnTagsProp != null)
            {
                foreach (string str in spawnTagsProp)
                {
                    commentText += str + "\n";
                }
            }
            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(triangleshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            comment.Text = commentText;

            this.AddChild(val);
            //var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        /// <summary>
        /// Creates the connection to the annex node.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            var annexZoneLocProp = export.GetProperty<ObjectProperty>("AnnexZoneLocation");
            if (annexZoneLocProp != null)
            {
                //IExportEntry annexzonelocexp = pcc.Exports[annexZoneLocProp.Value - 1];

                PathfindingNodeMaster othernode = null;
                int othernodeidx = annexZoneLocProp.Value - 1;
                if (othernodeidx != 0)
                {
                    foreach (PathfindingNodeMaster node in Objects)
                    {
                        if (node.export.Index == othernodeidx)
                        {
                            othernode = node;
                            break;
                        }
                    }
                }

                if (othernode != null)
                {
                    PPath edge = new PPath();
                    ((ArrayList)Tag).Add(edge);
                    ((ArrayList)othernode.Tag).Add(edge);
                    edge.Tag = new ArrayList();
                    ((ArrayList)edge.Tag).Add(this);
                    ((ArrayList)edge.Tag).Add(othernode);
                    g.edgeLayer.AddChild(edge);
                }
            }
        }
    }

    //This is technically not a pathnode...
    public class AnnexNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);

        public AnnexNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {

        }
    }

    public class PendingNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);

        public PendingNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class CoverSlotMarker : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(153, 153, 0);
        PointF[] shieldshape = new PointF[] { new PointF(0, 15), new PointF(25, 0), new PointF(50, 15), new PointF(25, 50) };

        public CoverSlotMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(shieldshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXDynamicCoverSlotMarker : CoverSlotMarker
    {
        public SFXDynamicCoverSlotMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }

    public class MantleMarker : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(85, 59, 255);
        PointF[] mantleshape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        public MantleMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(mantleshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_LargeMantleNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(185, 59, 55);
        PointF[] mantleshape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        public SFXNav_LargeMantleNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(mantleshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXEnemySpawnPoint : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);
        PointF[] starshape = new PointF[] { new PointF(0, 0), new PointF(25, 12), new PointF(50, 0), new PointF(37, 25), new PointF(50, 50), new PointF(25, 37), new PointF(0, 50), new PointF(12, 25) };

        public SFXEnemySpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(starshape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_JumpNode : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(148, 0, 211);
        PointF[] forkshape = new PointF[] { new PointF(25, 0), new PointF(50, 50), new PointF(25, 37), new PointF(0, 50) };

        public SFXNav_JumpNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            PPath nodeShape = PPath.CreatePolygon(forkshape);
            shape = nodeShape;
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_TurretPoint : PathfindingNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(139, 69, 19);
        PointF[] diamondshape = new PointF[] { new PointF(25, 0), new PointF(50, 25), new PointF(25, 50), new PointF(0, 25) };

        public SFXNav_TurretPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            PPath nodeShape = PPath.CreatePolygon(diamondshape);
            shape = nodeShape;
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }



}