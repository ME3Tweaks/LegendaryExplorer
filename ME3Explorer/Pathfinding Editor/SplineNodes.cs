using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal.ME3Enums;
using System;
using System.Windows;

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
            //Region ellipseRegion = new Region(shape.PathReference);
            //return ellipseRegion.IsVisible(_bounds);
            return true;
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
            if (splprop != null)
            {
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

                        spline.Pickable = false;
                        this.AddChild(spline);
                    }
                    connections.Add(cnctn);
                }
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

            //StructProperty splineInfo = export.GetProperty<StructProperty>("SplineInfo");
            //if (splineInfo != null)
            //{
            //    var pointsProp = splineInfo.GetProp<ArrayProperty<StructProperty>>("Points");
            //    StructProperty point0 = pointsProp[0];
            //    StructProperty point10 = pointsProp[1];
            //    SharpDX.Vector3 p0 = CommonStructs.GetVector3(point0.GetProp<StructProperty>("OutVal")); //start => should equal splineactor location
            //    SharpDX.Vector3 tan1 = CommonStructs.GetVector3(point0.GetProp<StructProperty>("LeaveTangent"));
            //    SharpDX.Vector3 tan2 = CommonStructs.GetVector3(point10.GetProp<StructProperty>("ArriveTangent"));
            //    SharpDX.Vector3 p3 = CommonStructs.GetVector3(point10.GetProp<StructProperty>("OutVal")); //end => should equal target next splineactor location
            //    var reparamProp = component.GetProperty<StructProperty>("SplineReparamTable");
            //    var reparamPoints = reparamProp.GetProp<ArrayProperty<StructProperty>>("Points");

            //    //Get P2 at (1/3 of control)   
            //    double m = SharpDX.Vector3.Distance(p3, p0); // use as approximate for distance of control point
            //    float magnitude = (p3 - p0).Length();
            //    const float THIRD = (float) 1/3;

            //    var linearPath = p3 - p0;
            //    var nlinPath = SharpDX.Vector3.Normalize(linearPath);
            //    var nTan1Path = SharpDX.Vector3.Normalize(tan1);
            //    var p1 = p0 + SharpDX.Vector3.Multiply(nlinPath + nTan1Path, magnitude * THIRD);
            //    this.AddChild(new SplinePointControlNode(2, p1.X, p1.Y, "P2", p, grapheditor));


            //    //Get P3 at (1/3 of control)
            //    var nTan2Path = SharpDX.Vector3.Normalize(tan2);
            //    var p2 = p3 - SharpDX.Vector3.Multiply(nlinPath * nTan2Path, magnitude * THIRD);
            //    this.AddChild(new SplinePointControlNode(3, p2.X, p2.Y, "P3", p, grapheditor));

            //    for(int n = 1; n < 10; n++)  // this adds the path of reparamble intermediate steps
            //    {

                    
            //        float t = (float)n / (float)9;
            //        var paramPos = GetPointOnBezierCurve(p0, p1, p2, p3, t);
            //        var param = new SplineParambleNode(n, paramPos.X, paramPos.Y, paramPos, t, 0, 0, 0, EInterpCurveMode.CIM_Linear, p, grapheditor);
            //        nodes.Add(param);
            //        this.AddChild(param);
                                       
            //    }


                //Return co-ordinates DEBUG
                //string StrP1 = $"P0: {p1.X.ToString()}, {p1.Y.ToString()}, {p1.Z.ToString()}";
                //string StrP2 = $"P1: {p2.X.ToString()}, {p2.Y.ToString()}, {p2.Z.ToString()}";
                //string StrP3 = $"P3: {p3.X.ToString()}, {p3.Y.ToString()}, {p3.Z.ToString()}";
                //string StrP4 = $"P4: {p4.X.ToString()}, {p4.Y.ToString()}, {p4.Z.ToString()}";
                //string StrNout = null; /*$"Nout: {nout.X.ToString()}, {nout.Y.ToString()}, {nout.Z.ToString()}";*/
                //MessageBox.Show($"Magnitude: {magnitude}\n{StrP1}\n{StrP2}\n{StrP3}\n{StrP4}\n{StrNout}");




                //var atan =  reparamPoints[0].GetProp<FloatProperty>("ArriveTangent").Value;
                //var ltan = reparamPoints[0].GetProp<FloatProperty>("LeaveTangent").Value;
                //reparamPoints[0].GetProp<EnumProperty>("InterpMode");
                //EInterpCurveMode ipmode = EInterpCurveMode.CIM_Linear;
                //nodes.Add(new SplineParambleNode(0, a.X, a.Y, a, 0, 0, atan, ltan, ipmode, pcc, grapheditor));

                //float distance = 0;
                //for (int n = 0; n < 10; n++)
                //{
                //    //find on bezier point n
                //    //
                //    float inval = n / 9;
                //    var outval = reparamPoints[n].GetProp<FloatProperty>("OutVal").Value;
                //    atan = reparamPoints[0].GetProp<FloatProperty>("ArriveTangent").Value;
                //    ltan = reparamPoints[0].GetProp<FloatProperty>("LeaveTangent").Value;

                    

                //    nodes.Add(new SplineParambleNode(0, a.X, a.Y, a, 0, 0, atan, ltan, ipmode, pcc, grapheditor));
                //}

                //var terminator = new SplineParambleNode(10, d.X, d.Y, d, 1, 0, atan, ltan, ipmode, pcc, grapheditor);
                //nodes.Add(terminator);
                ////const float w = 25;
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
            //}
        }

        private SharpDX.Vector3 GetPointOnBezierCurve(SharpDX.Vector3 p0, SharpDX.Vector3 p1, SharpDX.Vector3 p2, SharpDX.Vector3 p3, float t)
        {
            float u = 1f - t;
            float t2 = t * t;
            float u2 = u * u;
            float u3 = u2 * u;
            float t3 = t2 * t;

            SharpDX.Vector3 result =
                (u3) * p0 +
                (3f * u2 * t) * p1 +
                (3f * u * t2) * p2 +
                (t3) * p3;

            return result;
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


        public SplineParambleNode(int idx, float x, float y, SharpDX.Vector3 loc, float inval, float outval, float inTan, float outTan, EInterpCurveMode interpMode, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Loc = loc;
            Time = inval;
            Distance = outval;
            ArriveTangent = inTan;
            LeaveTangent = outTan;
            InterpMode = interpMode;
            const float w = 5;
            const float h = 5;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            this.TranslateBy(x, y);
        }
    }

    public class SplinePointControlNode : SplineNode
    {
        private static readonly Color color = Color.FromArgb(0, 0, 255);
        private SplinePoint1Node destinationPoint;

        public SplinePointControlNode(int idx, float x, float y, string name, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {

            const float w = 10;
            const float h = 10;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText($"Ctrl {name} {x},{y}");
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);


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