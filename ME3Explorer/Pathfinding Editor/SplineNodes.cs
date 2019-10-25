using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal.ME3Enums;

namespace ME3Explorer.SplineNodes
{
    public class SplineNode : PathfindingNodeMaster
    {
        static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        public static Pen splineconnnectorPen = Pens.DeepPink;
        protected SplineNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
            Tag = new ArrayList(); //outbound reachspec edges.
            pcc = p;
            g = grapheditor;
            index = idx;
            export = pcc.GetUExport(index);

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
                export = pcc.GetUExport(index);
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

        public override bool Intersects(RectangleF _bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(_bounds);
        }
    }


    public class PendingSplineNode : SplineNode
    {
        private static readonly Color color = Color.FromArgb(255, 0, 0);

        public PendingSplineNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            const float w = 50;
            const float h = 50;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText(idx.ToString());
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
        public List<Spline> Splines = new List<Spline>();
        public List<ExportEntry> connections = new List<ExportEntry>();

        private static readonly Color color = Color.FromArgb(255, 30, 30);
        readonly PointF[] edgeShape = { new PointF(21, 25), new PointF(29, 25), new PointF(27, 5), new PointF(40, 5), new PointF(40, 15), new PointF(50, 0), new PointF(40, -15), new PointF(40, -5), new PointF(0, -5), new PointF(0, 5), new PointF(23, 5) };
        public SplineActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            var splprop = export.GetProperty<ArrayProperty<StructProperty>>("Connections");
            foreach (var prop in splprop)
            {
                var spcomp = prop.GetProp<ObjectProperty>("SplineComponent");
                var cmpidx = spcomp?.Value ?? 0;
                var cntcomp = prop.GetProp<ObjectProperty>("ConnectTo");
                var cnctn = pcc.GetUExport(cntcomp?.Value ?? 0);
                if (spcomp != null && cmpidx > 0)
                {
                    var component = pcc.GetUExport(cmpidx);
                    var spline = new Spline(cmpidx, component, cnctn, p, grapheditor);
                    Splines.Add(spline);
                }
                connections.Add(cnctn);
            }

            const float w = 50;
            const float h = 50;
            shape = PPath.CreatePolygon(edgeShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);
        }
    }
    public class Spline : SplineNode
    {
        private static readonly Color color = Color.FromArgb(255, 30, 30);
        public List<SplineParambleNode> nodes = new List<SplineParambleNode>();

        public Spline(int idx, ExportEntry component, ExportEntry target, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {

            StructProperty splineInfo = export.GetProperty<StructProperty>("SplineInfo");
            if (splineInfo != null)
            {
                var pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                StructProperty point0 = pointsProp[0];
                StructProperty point10 = pointsProp[1];
                SharpDX.Vector3 a = CommonStructs.GetVector3(point0.GetProp<StructProperty>("OutVal"));
                SharpDX.Vector3 tan1 = CommonStructs.GetVector3(point0.GetProp<StructProperty>("LeaveTangent"));
                SharpDX.Vector3 tan2 = CommonStructs.GetVector3(point10.GetProp<StructProperty>("ArriveTangent"));
                SharpDX.Vector3 d = CommonStructs.GetVector3(point10.GetProp<StructProperty>("OutVal"));
                var reparamProp = component.GetProperty<StructProperty>("SplineReparamTable");
                var reparamPoints = reparamProp.GetProp<ArrayProperty<StructProperty>>("Points");

                var atan =  reparamPoints[0].GetProp<FloatProperty>("ArriveTangent").Value;
                var ltan = reparamPoints[0].GetProp<FloatProperty>("LeaveTangent").Value;
                reparamPoints[0].GetProp<EnumProperty>("InterpMode");
                EInterpCurveMode ipmode = EInterpCurveMode.CIM_Linear;
                nodes.Add(new SplineParambleNode(0, a.X, a.Y, a, 0, 0, atan, ltan, ipmode, pcc, grapheditor));

                float distance = 0;
                for (int n = 0; n < 10; n++)
                {
                    //find on bezier point n
                    //
                    float inval = n / 9;
                    var outval = reparamPoints[n].GetProp<FloatProperty>("OutVal").Value;
                    atan = reparamPoints[0].GetProp<FloatProperty>("ArriveTangent").Value;
                    ltan = reparamPoints[0].GetProp<FloatProperty>("LeaveTangent").Value;

                    

                    nodes.Add(new SplineParambleNode(0, a.X, a.Y, a, 0, 0, atan, ltan, ipmode, pcc, grapheditor));
                }

                var terminator = new SplineParambleNode(10, d.X, d.Y, d, 1, 0, atan, ltan, ipmode, pcc, grapheditor);
                nodes.Add(terminator);
                //const float w = 25;
                //const float h = 25;
                //shape = PPath.CreateEllipse(0, 0, w, h);
                //outlinePen = new Pen(color);
                //shape.Pen = outlinePen;
                //shape.Brush = pathfindingNodeBrush;
                //shape.Pickable = false;
                //this.AddChild(shape);
                //this.Bounds = new RectangleF(0, 0, w, h);
                //SText val = new SText($"{export.Index}\nSpline Start");
                //val.Pickable = false;
                //val.TextAlignment = StringAlignment.Center;
                //val.X = w / 2 - val.Width / 2;
                //val.Y = h / 2 - val.Height / 2;
                //this.AddChild(val);
                //this.TranslateBy(x, y);
            }
        }
    }

    public class SplineParambleNode : SplineNode
    {
        private static readonly Color color = Color.FromArgb(255, 30, 30);
        public float Time;
        public float Distance;
        public float ArriveTangent;
        public float LeaveTangent;
        public EInterpCurveMode InterpMode;
        SharpDX.Vector3 Loc;
        SharpDX.Vector3 tan1;


        public SplineParambleNode(int idx, float x, float y, SharpDX.Vector3 loc, float inval, float outval, float inTan = 0, float outTan = 0, EInterpCurveMode interpMode = EInterpCurveMode.CIM_Linear, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Loc = loc;
            Time = inval;
            Distance = outval;
            ArriveTangent = inTan;
            LeaveTangent = outTan;
            InterpMode = interpMode;
            const float w = 10;
            const float h = 10;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText(idx.ToString());
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
        private static readonly Color color = Color.FromArgb(0, 0, 255);
        private SplinePoint1Node destinationPoint;

        SharpDX.Vector2 a;
        SharpDX.Vector2 tan1;
        SharpDX.Vector2 tan2;
        SharpDX.Vector2 d;

        public SplinePoint0Node(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            StructProperty splineInfo = export.GetProperty<StructProperty>("SplineInfo");
            if (splineInfo != null)
            {
                var pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
                StructProperty point0 = pointsProp[0];
                StructProperty point1 = pointsProp[1];
                a = CommonStructs.GetVector2(point0.GetProp<StructProperty>("OutVal"));
                tan1 = CommonStructs.GetVector2(point0.GetProp<StructProperty>("LeaveTangent"));
                tan2 = CommonStructs.GetVector2(point1.GetProp<StructProperty>("ArriveTangent"));
                d = CommonStructs.GetVector2(point1.GetProp<StructProperty>("OutVal"));
                const float w = 25;
                const float h = 25;
                shape = PPath.CreateEllipse(0, 0, w, h);
                outlinePen = new Pen(color);
                shape.Pen = outlinePen;
                shape.Brush = pathfindingNodeBrush;
                shape.Pickable = false;
                this.AddChild(shape);
                this.Bounds = new RectangleF(0, 0, w, h);
                SText val = new SText($"{export.Index}\nSpline Start");
                val.Pickable = false;
                val.TextAlignment = StringAlignment.Center;
                val.X = w / 2 - val.Width / 2;
                val.Y = h / 2 - val.Height / 2;
                this.AddChild(val);
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
        public override void CreateConnections(List<PathfindingNodeMaster> graphNodes)
        {
            PathfindingEditorEdge edge = new PathfindingEditorEdge()
            {
                EndPoints = { [0] = this, [1] = destinationPoint },
                OutboundConnections = { [0] = true, [1] = true},
                Pen = splineconnnectorPen
            };

            //TODO: Calculate where points B and C lie in actual space for calculating the overhead curve
            //edge.BezierPoints = new float[2];
            //SharpDX.Vector2 b = a + tan1 / 3.0f; // Operator overloading is lovely. Java can go die in a hole.
            //SharpDX.Vector2 c = d - tan2 / 3.0f;

            //((ArrayList)Tag).Add(edge);
            //((ArrayList)destinationPoint.Tag).Add(edge);
            //edge.Tag = new ArrayList();
            //((ArrayList)edge.Tag).Add(this);
            //((ArrayList)edge.Tag).Add(destinationPoint);
            g.edgeLayer.AddChild(edge);
        }
    }

    public class SplinePoint1Node : SplineNode
    {
        private static readonly Color color = Color.FromArgb(0, 0, 255);
        public SplinePoint1Node(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            const float w = 20;
            const float h = 20;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText(export.Index + "\nSpline End");
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);
        }

        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(List<PathfindingNodeMaster> Objects)
        {

        }
    }
}