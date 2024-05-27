using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.SequenceObjects;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;
using InterpCurvePointFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<float>;

namespace LegendaryExplorer.Tools.PathfindingEditor
{
    public class SplineNode : PathfindingNodeMaster
    {
        new static readonly Color commentColor = Color.FromArgb(74, 63, 190);
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

        public override bool Intersects(RectangleF _bounds)
        {
            Region ellipseRegion = new(shape.PathReference);
            return ellipseRegion.IsVisible(_bounds);
        }
    }

    public sealed class SplineActorNode : SplineNode
    {
        public List<Spline> Splines = new();
        public List<ExportEntry> connections = new();
        public SplinePointControlNode ArriveTangentControlNode;
        public SplinePointControlNode LeaveTangentControlNode;
        private readonly List<SplineActorNode> LinksFrom = new();

        private static readonly Color color = Color.FromArgb(255, 30, 30);
        readonly PointF[] edgeShape = { new(21, 25), new(29, 25), new(27, 5), new(40, 5), new(40, 15), new(50, 0), new(40, -15), new(40, -5), new(0, -5), new(0, 5), new(23, 5) };
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
                    pcc.TryGetUExport(cntcomp?.Value ?? 0, out ExportEntry cnctn);
                    if (spcomp != null && cmpidx > 0)
                    {
                        var component = pcc.GetUExport(cmpidx);
                        var spline = new Spline(component, grapheditor, cnctn);
                        Splines.Add(spline);

                        spline.Pickable = false;
                        g.nodeLayer.AddChild(spline);
                    }
                    connections.Add(cnctn);
                }
            }

            Vector3 tangent = CommonStructs.GetVector3(export, "SplineActorTangent", new Vector3(300f, 0f, 0f));

            //(float controlPointX, float controlPointY, _) = tangent;

            (float controlPointX, float controlPointY, float controlPointZ) = ActorUtils.GetLocalToWorld(export).TransformNormal(tangent);

            LeaveTangentControlNode = new SplinePointControlNode(this, controlPointX, controlPointY, controlPointZ, UpdateMode.LeaveTangent);
            ArriveTangentControlNode = new SplinePointControlNode(this, -controlPointX, -controlPointY, controlPointZ, UpdateMode.ArriveTangent);
            AddChild(LeaveTangentControlNode);
            AddChild(ArriveTangentControlNode);

            shape = PPath.CreatePolygon(edgeShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = splineNodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            float w = shape.Width;
            float h = shape.Height;
            Bounds = new RectangleF(0, 0, w, h);
            SText val = new(idx.ToString())
            {
                Pickable = false,
                TextAlignment = StringAlignment.Center
            };
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            SetOffset(x, y);
        }

        public override void CreateConnections(List<PathfindingNodeMaster> Objects)
        {
            g.edgeLayer.AddChild(ArriveTangentControlNode.CreateConnection(g));
            g.edgeLayer.AddChild(LeaveTangentControlNode.CreateConnection(g));

            ArriveTangentControlNode.Hidden = true;
            LeaveTangentControlNode.Hidden = true;

            LinksFrom.Clear();
            if (export.GetProperty<ArrayProperty<ObjectProperty>>("LinksFrom") is {} linksFromProp)
            {
                foreach (ObjectProperty objProp in linksFromProp)
                {
                    if (pcc.TryGetUExport(objProp.Value, out ExportEntry exp) && Objects.FirstOrDefault(node => node.export == exp) is SplineActorNode splineActorNode)
                    {
                        LinksFrom.Add(splineActorNode);
                    }
                }
            }
        }

        public enum UpdateMode
        {
            ArriveTangent,
            LeaveTangent,
            Movement
        }

        public override void OnMouseDrag(PInputEventArgs e)
        {
            base.OnMouseDrag(e);
            UpdateSplines(UpdateMode.Movement);
            dragging = true;
        }

        private bool dragging;
        public override void OnMouseUp(PInputEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                UpdateSplines(UpdateMode.Movement, true);
                dragging = false;
            }
        }

        public void UpdateSplines(UpdateMode mode, bool save = false)
        {
            if (mode == UpdateMode.ArriveTangent || mode == UpdateMode.LeaveTangent)
            {
                Vector3 worldCoords;
                if (mode == UpdateMode.ArriveTangent)
                {
                    worldCoords = new Vector3(ArriveTangentControlNode.OffsetX, ArriveTangentControlNode.OffsetY, ArriveTangentControlNode.Z);
                    LeaveTangentControlNode.SetOffset(-worldCoords.X, -worldCoords.Y);

                    worldCoords = -worldCoords;
                }
                else
                {
                    worldCoords = new Vector3(LeaveTangentControlNode.OffsetX, LeaveTangentControlNode.OffsetY, LeaveTangentControlNode.Z);
                    ArriveTangentControlNode.SetOffset(-worldCoords.X, -worldCoords.Y);
                }

                if (save)
                {
                    //will cause a refresh,so no need to update UI
                    export.WriteProperty(CommonStructs.Vector3Prop(ActorUtils.GetWorldToLocal(export).TransformNormal(worldCoords), "SplineActorTangent"));
                    return;
                }

                foreach (Spline spline in Splines)
                {
                    spline.SplineInfo.Points[0].ArriveTangent = -worldCoords;
                    spline.SplineInfo.Points[0].LeaveTangent = worldCoords;
                    UpdateSpline(spline);
                }

                foreach (SplineActorNode prevSplineActor in LinksFrom)
                {
                    foreach (Spline spline in prevSplineActor.Splines)
                    {
                        if (spline.NextActor == export)
                        {
                            spline.SplineInfo.Points[1].ArriveTangent = worldCoords;
                            spline.SplineInfo.Points[1].LeaveTangent = -worldCoords;
                            UpdateSpline(spline);
                        }
                    }
                }
            }
            else
            {
                float z = (float)PathEdUtils.GetLocation(export).Z;
                Vector3 location = new(OffsetX, OffsetY, z);

                if (save)
                {
                    //will cause a refresh,so no need to update UI
                   PathEdUtils.SetLocation(export, location.X, location.Y, location.Z);
                   return;
                }

                foreach (Spline spline in Splines)
                {
                    spline.SplineInfo.Points[0].OutVal = location;
                    UpdateSpline(spline);
                }

                foreach(SplineActorNode prevSplineActor in LinksFrom)
                {
                    foreach (Spline spline in prevSplineActor.Splines)
                    {
                        if (spline.NextActor == export)
                        {
                            spline.SplineInfo.Points[1].OutVal = location;
                            UpdateSpline(spline);
                        }
                    }
                }
            }

            PathingGraphEditor.UpdateEdgeStraight(ArriveTangentControlNode.Edge);
            PathingGraphEditor.UpdateEdgeStraight(LeaveTangentControlNode.Edge);

            void UpdateSpline(Spline spline)
            {
                spline.RegenerateReparamTable();
                for (int i = 0; i < spline.ReparamTable.Points.Count; i++)
                {
                    (float x, float y, _) = spline.SplineInfo.Eval(spline.ReparamTable.Points[i].OutVal, Vector3.Zero);
                    spline.nodes[i].SetOffset(x, y);
                }

                if (spline.nodes.Count > 7)
                {
                    var directionVector = new Vector2(spline.nodes[7].OffsetX, spline.nodes[7].OffsetY) - new Vector2(spline.nodes[5].OffsetX, spline.nodes[5].OffsetY);
                    directionVector = Vector2.Normalize(directionVector);
                    spline.nodes[6].Rotation = (float)(Math.Atan2(directionVector.X, -directionVector.Y) * 180 / Math.PI);
                }

                foreach (PathfindingEditorEdge edge in spline.edges)
                {
                    PathingGraphEditor.UpdateEdgeStraight(edge);
                }
            }
        }

        public override void Select()
        {
            base.Select();
            ArriveTangentControlNode.Hidden = false;
            LeaveTangentControlNode.Hidden = false;
        }

        public override void Deselect()
        {
            base.Deselect();
            ArriveTangentControlNode.Hidden = true;
            LeaveTangentControlNode.Hidden = true;
        }
    }
    public sealed class Spline : PNode
    {
        public List<SplineParambleNode> nodes = new();
        public List<PathfindingEditorEdge> edges = new();
        public InterpCurveVector SplineInfo;
        public InterpCurveFloat ReparamTable;
        public ExportEntry NextActor;

        public Spline(ExportEntry component, PathingGraphEditor g, ExportEntry nextActor)
        {
            Pickable = false;
            NextActor = nextActor;
            var splineInfoProp = component.GetProperty<StructProperty>("SplineInfo");
            if (splineInfoProp != null)
            {
                SplineInfo = InterpCurveVector.FromStructProperty(splineInfoProp, component.Game);
                var reparamProp = component.GetProperty<StructProperty>("SplineReparamTable");
                if (reparamProp != null)
                {
                    ReparamTable = InterpCurveFloat.FromStructProperty(reparamProp, component.Game);
                }
                else
                {
                    ReparamTable = new InterpCurveFloat();
                    RegenerateReparamTable();
                }

                Draw(g);
            }
        }

        public void Draw(PathingGraphEditor g)
        {
            for (int i = 0; i < ReparamTable.Points.Count; i++)
            {
                InterpCurvePointFloat reparamTablePoint = ReparamTable.Points[i];
                (float x, float y, _) = SplineInfo.Eval(reparamTablePoint.OutVal, Vector3.Zero);
                var node = new SplineParambleNode(x, y, i == 6);
                nodes.Add(node);
            }

            if (nodes.Count > 7)
            {
                var directionVector = new Vector2(nodes[7].OffsetX, nodes[7].OffsetY) - new Vector2(nodes[5].OffsetX, nodes[5].OffsetY);
                directionVector = Vector2.Normalize(directionVector);
                nodes[6].RotateBy((float)(Math.Atan2(directionVector.X, -directionVector.Y) * 180 / Math.PI));
            }

            AddChildren(nodes);
            
            for (int i = 1; i < nodes.Count; i++)
            {
                PathfindingEditorEdge edge = new()
                {
                    EndPoints = { [0] = nodes[i - 1], [1] = nodes[i] },
                    Pen = Pens.Purple
                };
                edges.Add(edge);
                g.edgeLayer.AddChild(edge);
            }
        }

        public void RegenerateReparamTable()
        {
            ReparamTable.Points.Clear();
            const int steps = 10;
            float totalDist = 0;

            float input = SplineInfo.Points[0].InVal;
            float end = SplineInfo.Points.Last().InVal;
            float interval = (end - input) / (steps - 1);
            Vector3 oldPos = SplineInfo.Eval(input, Vector3.Zero);
            ReparamTable.AddPoint(totalDist, input);
            input += interval;
            for (int i = 1; i < steps; i++)
            {
                Vector3 newPos = SplineInfo.Eval(input, Vector3.Zero);
                totalDist += (newPos - oldPos).Length();
                oldPos = newPos;
                ReparamTable.AddPoint(totalDist, input);
                input += interval;
            }
        }
    }

    public sealed class SplineParambleNode : PNode
    {
        private static readonly Color color = Color.Purple;

        public SplineParambleNode(float x, float y, bool arrow = false)
        {
            const float w = 5;
            PPath shape = arrow ? PPath.CreatePolygon(0,0, 3*w,3*w, 0, w, -3*w,3*w) : PPath.CreateRectangle(0, 0, w, w);
            shape.Pen = new Pen(color);
            shape.Brush = new SolidBrush(color);
            shape.Pickable = false;
            AddChild(shape);
            TranslateBy(x, y);
            Pickable = false;
        }
    }

    public sealed class SplinePointControlNode : PNode
    {
        private static readonly Color color = Color.White;
        private readonly SplineActorNode splineActor;
        public readonly float Z;
        public SplineActorNode.UpdateMode UpdateMode;
        public PathfindingEditorEdge Edge;

        public SplinePointControlNode(SplineActorNode actor, float x, float y, float z, SplineActorNode.UpdateMode updateMode)
        {
            UpdateMode = updateMode;
            Z = z;
            splineActor = actor;
            const float w = 15;
            const float h = 15;
            PPath shape = PPath.CreateEllipse(0, 0, w, h);
            shape.Pen = new Pen(color);
            shape.Brush = PathfindingNodeMaster.pathfindingNodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);

            TranslateBy(x, y);
        }

        public bool Hidden
        {
            set
            {
                Visible = !value;
                Pickable = !value;
                Edge.Visible =! value;
            }
        }

        public PathfindingEditorEdge CreateConnection(PathingGraphEditor g)
        {
            if (Edge != null)
            {
                g.edgeLayer.RemoveChild(Edge);
            }
            return Edge = new PathfindingEditorEdge
            {
                EndPoints = { [0] = this, [1] = splineActor },
                Pen = Pens.White
            };
        }

        public override void OnMouseUp(PInputEventArgs e)
        {
            base.OnMouseUp(e);
            splineActor.UpdateSplines(UpdateMode, true);
        }

        public override void OnMouseDrag(PInputEventArgs e)
        {
            base.OnMouseDrag(e);
            splineActor.UpdateSplines(UpdateMode);
        }
    }
}