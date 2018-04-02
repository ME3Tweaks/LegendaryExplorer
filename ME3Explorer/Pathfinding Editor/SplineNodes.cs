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
using System.Diagnostics;

namespace ME3Explorer.SplineNodes
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };

    public class SplineNode : PathfindingNodeMaster
    {
        public PathingGraphEditor g;
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        public static Pen splineconnnectorPen = Pens.DeepPink;

        protected SplineNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
            Tag = new ArrayList(); //outbound reachspec edges.
            pcc = p;
            g = grapheditor;
            index = idx;
            export = pcc.getExport(index);
            comment = new SText(GetComment(), commentColor, false);
            comment.X = 0;
            comment.Y = 52 + comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
        }

        protected SplineNode(int idx, IMEPackage p)
        {
            pcc = p;
            index = idx;
            if (idx >= 0)
            {
                export = pcc.getExport(index);
                comment = new SText(GetComment(), commentColor, false);
            }

            comment.X = 0;
            comment.Y = 0 - comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
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

        public void Select()
        {
            shape.Pen = selectedPen;
        }

        public void Deselect()
        {
            shape.Pen = outlinePen;
        }

        public override bool Intersects(RectangleF bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        /*public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((BlockingVolumeNode)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((BlockingVolumeNode)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }*/

        /// <summary>
        /// Creates the reachspec connections from this pathfinding node to others.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("Connections");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {


                    PPath edge = new PPath();
                    //edge.Add
                    //((ArrayList)Tag).Add(edge);
                    //edge.Tag = new ArrayList();
                    //((ArrayList)edge.Tag).Add(this);
                    //((ArrayList)edge.Tag).Add(othernode);
                    //g.edgeLayer.AddChild(edge);
                }

                //foreach (IExportEntry spec in ReachSpecs)
                //{
                //    //Get ending
                //    PNode othernode = null;
                //    int othernodeidx = 0;
                //    PropertyCollection props = spec.GetProperties();
                //    foreach (var prop in props)
                //    {
                //        if (prop.Name == "End")
                //        {
                //            PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                //            foreach (var rprop in reachspecprops)
                //            {
                //                if (rprop.Name == "Actor")
                //                {
                //                    othernodeidx = (rprop as ObjectProperty).Value;
                //                    break;
                //                }
                //            }
                //        }
                //        if (othernodeidx != 0)
                //        {
                //            break;
                //        }
                //    }

                //    if (othernodeidx != 0)
                //    {
                //        foreach (SplineNode node in Objects)
                //        {
                //            if (node.export.UIndex == othernodeidx)
                //            {
                //                othernode = node;
                //                break;
                //            }
                //        }
                //    }
                //    if (othernode != null)
                //    {
                //        PPath edge = new PPath();
                //        ((ArrayList)Tag).Add(edge);
                //        ((ArrayList)othernode.Tag).Add(edge);
                //        edge.Tag = new ArrayList();
                //        ((ArrayList)edge.Tag).Add(this);
                //        ((ArrayList)edge.Tag).Add(othernode);
                //        g.edgeLayer.AddChild(edge);
                //    }
                //}
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

    public class PendingSplineNode : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);

        public PendingSplineNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SplineActorNode : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 30, 30);
        PointF[] edgeShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(30, 50), new PointF(0, 50) };
        public SplineActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(edgeShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SplinePoint0Node : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        private SplinePoint1Node destinationPoint;

        public SplinePoint0Node(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 25;
            float h = 25;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText("Spline Start");
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

        internal void SetDestinationPoint(SplinePoint1Node point1node)
        {
            destinationPoint = point1node;
        }

        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            PPath edge = new PPath();
            edge.Pen = splineconnnectorPen;
            ((ArrayList)Tag).Add(edge);
            ((ArrayList)destinationPoint.Tag).Add(edge);
            edge.Tag = new ArrayList();
            ((ArrayList)edge.Tag).Add(this);
            ((ArrayList)edge.Tag).Add(destinationPoint);

            g.edgeLayer.AddChild(edge);
        }
    }

    public class SplinePoint1Node : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        public SplinePoint1Node(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 20;
            float h = 20;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText("Spline End");
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
}