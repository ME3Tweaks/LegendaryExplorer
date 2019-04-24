using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;

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
            export = pcc.getUExport(index);
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
                export = pcc.getUExport(index);
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
                    //PPath edge = new PPath();
                    //edge.Add
                    //((ArrayList)Tag).Add(edge);
                    //edge.Tag = new ArrayList();
                    //((ArrayList)edge.Tag).Add(this);
                    //((ArrayList)edge.Tag).Add(othernode);
                    //g.edgeLayer.AddChild(edge);
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
        }
    }

    public class SplineActorNode : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 30, 30);
        PointF[] edgeShape = new PointF[] { new PointF(0, 50), new PointF(0, 25), new PointF(10, 15), new PointF(15, 10), new PointF(30, 5), new PointF(40, 0), new PointF(50, 0), new PointF(40,5), new PointF(30, 10), new PointF(15, 15), new PointF(5, 25) };
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
        }
    }

    public class SplinePoint0Node : SplineNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        private SplinePoint1Node destinationPoint;

        SharpDX.Vector2 a;
        SharpDX.Vector2 tan1;
        SharpDX.Vector2 tan2;
        SharpDX.Vector2 d;

        public SplinePoint0Node(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            StructProperty splineInfo = export.GetProperty<StructProperty>("SplineInfo");
            if (splineInfo != null)
            {
                ArrayProperty<StructProperty> pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                StructProperty point0 = pointsProp[0];
                StructProperty point1 = pointsProp[1];
                a = SharedPathfinding.GetVector2(point0.GetProp<StructProperty>("OutVal"));
                tan1 = SharedPathfinding.GetVector2(point0.GetProp<StructProperty>("LeaveTangent"));
                tan2 = SharedPathfinding.GetVector2(point1.GetProp<StructProperty>("ArriveTangent"));
                d = SharedPathfinding.GetVector2(point1.GetProp<StructProperty>("OutVal"));
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
                val = new SText(export.Index + "\nSpline Start");
                val.Pickable = false;
                val.TextAlignment = StringAlignment.Center;
                val.X = w / 2 - val.Width / 2;
                val.Y = h / 2 - val.Height / 2;
                this.AddChild(val);
                var props = export.GetProperties();
                this.TranslateBy(x, y);
            }
        }

        internal void SetDestinationPoint(SplinePoint1Node point1node)
        {
            destinationPoint = point1node;
        }

        /// <summary>
        /// This beginning node of the spline connects to the destination point over a bezier curve.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {
            PathfindingEditorEdge edge = new PathfindingEditorEdge();
            edge.Pen = splineconnnectorPen;

            //TODO: Calculate where points B and C lie in actual space for calculating the overhead curve
            //edge.BezierPoints = new float[2];
            //SharpDX.Vector2 b = a + tan1 / 3.0f; // Operator overloading is lovely. Java can go die in a hole.
            //SharpDX.Vector2 c = d - tan2 / 3.0f;

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
            val = new SText(export.Index + "\nSpline End");
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            var props = export.GetProperties();
            this.TranslateBy(x, y);
        }

        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {

        }
    }
}