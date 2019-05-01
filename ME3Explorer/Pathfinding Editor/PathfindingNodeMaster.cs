using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.SequenceObjects;
using System.Numerics;
using System.Diagnostics;
using ME3Explorer.PathfindingNodes;

namespace ME3Explorer.Pathfinding_Editor
{
    public abstract class PathfindingNodeMaster : PNode
    {
        public PPath shape;
        public IMEPackage pcc;
        public PathingGraphEditor g;
        //public static ME1Explorer.TalkFiles talkfiles { get; set; }
        protected static Color commentColor = Color.FromArgb(74, 63, 190);
        public static Brush sfxCombatZoneBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
        public static Brush highlightedCoverSlotBrush = new SolidBrush(Color.FromArgb(219, 137, 6));

        protected static Brush actorNodeBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
        protected static Brush splineNodeBrush = new SolidBrush(Color.FromArgb(255, 60, 200));
        public static Brush pathfindingNodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Brush dynamicPathfindingNodeBrush = new SolidBrush(Color.FromArgb(46, 184, 25));
        protected static Brush medkitBrush = new SolidBrush(Color.FromArgb(200, 15, 15));
        protected static Brush dynamicPathnodefindingNodeBrush = new SolidBrush(Color.FromArgb(80, 184, 25));

        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public List<IExportEntry> SequenceReferences = new List<IExportEntry>();

        public int UIndex => index;

        protected int index;
        public IExportEntry export;
        protected Pen outlinePen;
        public SText comment;
        public string NodeTag;
        internal bool Selected;
        /// <summary>
        /// List of all outbound connections between two PNodes (since some code requires this)
        /// </summary>
        public List<PathfindingEditorEdge> Edges = new List<PathfindingEditorEdge>();

        public void Select()
        {
            Selected = true;
            shape.Pen = selectedPen;
            MoveToFront();
        }

        public void Deselect()
        {
            Selected = false;
            if (shape.Pen != outlinePen)
            {
                shape.Pen = outlinePen;
            }
        }

        public override bool Intersects(RectangleF _bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(_bounds);
        }

        //Empty implementation
        public virtual void CreateConnections(List<PathfindingNodeMaster> Objects)
        {

        }

        protected int get3DBrushHeight()
        {
            try
            {
                PropertyCollection props = export.GetProperties();
                float zScalar = 1;

                var drawScale = props.GetProp<FloatProperty>("DrawScale");
                var drawScale3d = props.GetProp<StructProperty>("DrawScale3D");
                if (drawScale != null)
                {
                    zScalar = drawScale.Value;
                }
                if (drawScale3d != null)
                {
                    zScalar *= drawScale3d.GetProp<FloatProperty>("Z").Value;
                }

                //Todo: figure out how to do rotation properly for yaw. rotationpoint seems to be the issue.
                //var rotation = props.GetProp<StructProperty>("Rotation");
                //if (rotation != null)
                //{
                //    var yaw = rotation.GetProp<IntProperty>("Yaw");
                //    if (yaw != 0)
                //    {
                //        var translatedYaw = yaw * 360f / 65536f;
                //        RotateInPlace(translatedYaw);
                //        Debug.WriteLine("Rotation YAW found on " + export.UIndex + " " + translatedYaw);
                //    }
                //}
                var brushComponent = props.GetProp<ObjectProperty>("BrushComponent");
                if (brushComponent == null)
                {
                    return -1;
                }
                IExportEntry brush = export.FileRef.getExport(brushComponent.Value - 1);
                List<PointF> graphVertices = new List<PointF>();
                List<Vector3> brushVertices = new List<Vector3>();
                PropertyCollection brushProps = brush.GetProperties();
                var brushAggGeom = brushProps.GetProp<StructProperty>("BrushAggGeom");
                if (brushAggGeom == null)
                {
                    return -1;
                }
                var convexList = brushAggGeom.GetProp<ArrayProperty<StructProperty>>("ConvexElems");

                //Vertices
                var verticiesList = convexList[0].Properties.GetProp<ArrayProperty<StructProperty>>("VertexData");
                foreach (StructProperty vertex in verticiesList)
                {
                    Vector3 point = new Vector3();
                    point.Z = vertex.GetProp<FloatProperty>("Z") * zScalar;
                    brushVertices.Add(point);
                }

                int minZ = int.MaxValue;
                int maxZ = int.MinValue;
                //FaceTris
                var faceTriData = convexList[0].Properties.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                foreach (IntProperty triPoint in faceTriData)
                {
                    Vector3 vertex = brushVertices[triPoint];
                    //if (vertex.X == prevX && vertex.Y == prevY)
                    //{
                    //    continue; //Z is on the difference
                    //}

                    //float x = vertex.X;
                    //float y = vertex.Y;
                    float z = vertex.Z;
                    minZ = Math.Min((int)z, minZ);
                    maxZ = Math.Max((int)z, maxZ);
                }
                if (minZ != int.MaxValue && maxZ != int.MinValue)
                {
                    return maxZ - minZ;
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        protected PointF[] get3DBrushShape()
        {
            try
            {
                PropertyCollection props = export.GetProperties();
                float xScalar = 1;
                float yScalar = 1;
                //float zScalar = 1;

                var drawScale = props.GetProp<FloatProperty>("DrawScale");
                var drawScale3d = props.GetProp<StructProperty>("DrawScale3D");
                if (drawScale != null)
                {
                    xScalar = yScalar = drawScale.Value;
                }
                if (drawScale3d != null)
                {
                    xScalar *= drawScale3d.GetProp<FloatProperty>("X").Value;
                    yScalar *= drawScale3d.GetProp<FloatProperty>("Y").Value;
                }

                //Todo: figure out how to do rotation properly for yaw. rotationpoint seems to be the issue.
                //var rotation = props.GetProp<StructProperty>("Rotation");
                //if (rotation != null)
                //{
                //    var yaw = rotation.GetProp<IntProperty>("Yaw");
                //    if (yaw != 0)
                //    {
                //        var translatedYaw = yaw * 360f / 65536f;
                //        RotateInPlace(translatedYaw);
                //        Debug.WriteLine("Rotation YAW found on " + export.UIndex + " " + translatedYaw);
                //    }
                //}
                var brushComponent = props.GetProp<ObjectProperty>("BrushComponent");
                if (brushComponent == null)
                {
                    return null;
                }
                IExportEntry brush = export.FileRef.getExport(brushComponent.Value - 1);
                var graphVertices = new List<PointF>();
                var brushVertices = new List<Vector3>();
                PropertyCollection brushProps = brush.GetProperties();
                var brushAggGeom = brushProps.GetProp<StructProperty>("BrushAggGeom");
                if (brushAggGeom == null)
                {
                    return null;
                }
                var convexList = brushAggGeom.GetProp<ArrayProperty<StructProperty>>("ConvexElems");

                //Vertices
                var verticiesList = convexList[0].Properties.GetProp<ArrayProperty<StructProperty>>("VertexData");
                foreach (StructProperty vertex in verticiesList)
                {
                    brushVertices.Add(new Vector3
                    {
                        X = vertex.GetProp<FloatProperty>("X") * xScalar,
                        Y = vertex.GetProp<FloatProperty>("Y") * yScalar,
                        Z = vertex.GetProp<FloatProperty>("Z")
                    });
                }

                //FaceTris
                var faceTriData = convexList[0].Properties.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                float prevX = float.MinValue;
                float prevY = float.MinValue;
                foreach (IntProperty triPoint in faceTriData)
                {
                    Vector3 vertex = brushVertices[triPoint];
                    if (vertex.X == prevX && vertex.Y == prevY)
                    {
                        continue; //Z is on the difference
                    }

                    float x = vertex.X;
                    float y = vertex.Y;

                    prevX = x;
                    prevY = y;
                    PointF graphPoint = new PointF(x, y);
                    graphVertices.Add(graphPoint);
                }
                return graphVertices.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected string GetComment()
        {
            NameProperty tagProp = export.GetProperty<NameProperty>("Tag");
            if (tagProp != null)
            {
                string name = tagProp.Value;
                if (name != export.ObjectName)
                {
                    string retval = name;

                    if (tagProp.Value.Number != 0)
                    {
                        retval += $"_{tagProp.Value.Number}";
                    }
                    NodeTag = retval;
                    return retval;
                }
            }
            return "";
        }

        public override string ToString()
        {
            return $"{GetType().Name} - {UIndex}";
        }
    }

    [DebuggerDisplay("PathfindingEdge - {" + nameof(DebugTarget) + "}")]
    public class PathfindingEditorEdge : PPath
    {
        public bool[] OutboundConnections = new bool[2];
        public PNode[] EndPoints = new PNode[2];

        public string DebugTarget => $"{EndPoints[0]} to {EndPoints[1]}, {EndPoints.Length} tags";

        public bool DoesEdgeConnectSameNodes(PathfindingEditorEdge otherEdge)
        {
            return EndPoints.All(otherEdge.EndPoints.Contains);
        }

        internal bool HasAnyOutboundConnections()
        {
            return OutboundConnections[0] || OutboundConnections[1];
        }

        internal bool IsOneWayOnly()
        {
            return OutboundConnections[0] != OutboundConnections[1]; //one has to be true here
        }

        internal PNode GetOtherEnd(PathfindingNodeMaster currentPoint)
        {
            return EndPoints[0] == currentPoint ? EndPoints[1] : EndPoints[0];
        }

        internal void RemoveOutboundFrom(PathfindingNode pn)
        {
            if (EndPoints[0] == pn)
            {
                OutboundConnections[0] = false;
            }
            else if (EndPoints[1] == pn)
            {
                OutboundConnections[1] = false;
            }
        }

        internal void ReAttachEdgesToEndpoints()
        {
            if (EndPoints[0] is PathfindingNode pn0 && !pn0.Edges.Contains(this))
            {
                pn0.Edges.Add(this);
            }
            if (EndPoints[1] is PathfindingNode pn1 && !pn1.Edges.Contains(this))
            {
                pn1.Edges.Add(this);
            }
        }
    }
}
