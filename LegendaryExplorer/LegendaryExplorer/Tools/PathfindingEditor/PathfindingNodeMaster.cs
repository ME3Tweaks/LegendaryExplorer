using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Diagnostics;
using LegendaryExplorer.Tools.SequenceObjects;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Piccolo;
using Piccolo.Nodes;

namespace LegendaryExplorer.Tools.PathfindingEditor
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
        public static Brush directionBrush = new SolidBrush(Color.FromArgb(15, 95, 15));
        public static Brush OverlayBrush = new SolidBrush(Color.FromArgb(128, 128, 175));
        public static Brush OverlayOutlineBrush = new SolidBrush(Color.FromArgb(148, 148, 175));

        protected static Brush actorNodeBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
        protected static Brush splineNodeBrush = new SolidBrush(Color.FromArgb(255, 60, 200));
        public static Brush pathfindingNodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Brush dynamicPathfindingNodeBrush = new SolidBrush(Color.FromArgb(46, 184, 25));
        protected static Brush biopawnPathfindingNodeBrush = new SolidBrush(Color.FromArgb(255, 70, 70));
        protected static Brush medkitBrush = new SolidBrush(Color.FromArgb(200, 15, 15));
        protected static Brush dynamicPathnodefindingNodeBrush = new SolidBrush(Color.FromArgb(80, 184, 25));

        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public List<ExportEntry> SequenceReferences = new List<ExportEntry>();
        public int UIndex => index;

        protected int index;
        public ExportEntry export;
        protected Pen outlinePen;
        public SText comment;
        public string NodeTag;
        internal bool Selected;

        private bool isOverlay;
        public bool IsOverlay
        {
            //This is not used for NotifyPropertyChanged.
            get => isOverlay;
            set
            {
                if (value)
                {
                    shape.Pen = new Pen(OverlayOutlineBrush);
                    shape.Brush = OverlayBrush;
                }
                isOverlay = value;
            }
        }

        /// <summary>
        /// List of all outbound connections between two PNodes (since some code requires this)
        /// </summary>
        public List<PathfindingEditorEdge> Edges = new List<PathfindingEditorEdge>();

        public virtual void Select()
        {
            Selected = true;
            shape.Pen = selectedPen;
            MoveToFront();
        }

        public virtual void Deselect()
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
                ExportEntry brush = export.FileRef.GetUExport(brushComponent.Value);
                PropertyCollection brushProps = brush.GetProperties();
                var brushAggGeom = brushProps.GetProp<StructProperty>("BrushAggGeom");
                if (brushAggGeom == null)
                {
                    return -1;
                }
                int minZ = int.MaxValue;
                int maxZ = int.MinValue;
                var convexList = brushAggGeom.GetProp<ArrayProperty<StructProperty>>("ConvexElems");
                if (convexList.Count == 0)
                {
                    return -1;
                }
                foreach (StructProperty convexElem in convexList)
                {
                    var brushVertices = new List<Vector3>();
                    //Vertices
                    var verticiesList = convexElem.GetProp<ArrayProperty<StructProperty>>("VertexData");
                    foreach (StructProperty vertex in verticiesList)
                    {
                        var point = new Vector3 {Z = vertex.GetProp<FloatProperty>("Z") * zScalar};
                        brushVertices.Add(point);
                    }

                    //FaceTris
                    var faceTriData = convexElem.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
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

        protected IEnumerable<PointF[]> get3DBrushShape()
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
                ExportEntry brush = export.FileRef.GetUExport(brushComponent.Value);
                PropertyCollection brushProps = brush.GetProperties();
                var brushAggGeom = brushProps.GetProp<StructProperty>("BrushAggGeom");
                if (brushAggGeom == null)
                {
                    return null;
                }
                var convexList = brushAggGeom.GetProp<ArrayProperty<StructProperty>>("ConvexElems");
                if (convexList.Count is 0)
                {
                    return null;
                }
                var polys = new List<PointF[]>(convexList.Count);
                foreach (StructProperty convexElem in convexList)
                {
                    var verticiesList = convexElem.GetProp<ArrayProperty<StructProperty>>("VertexData");
                    var verts = new PointF[verticiesList.Count];
                    //Vertices
                    for (int i = 0; i < verticiesList.Count; i++)
                    {
                        StructProperty vertex = verticiesList[i];
                        verts[i] = new PointF
                        {
                            X = vertex.GetProp<FloatProperty>("X") * xScalar,
                            Y = vertex.GetProp<FloatProperty>("Y") * yScalar,
                        };
                    }
                    //2D convex hull algorithm from https://en.wikibooks.org/wiki/Algorithm_Implementation/Geometry/Convex_hull/Monotone_chain
                    int n = verts.Length;
                    if (n <= 3)
                    {
                        polys.Add(verts);
                        continue;
                    }
                    // Sort points lexicographically
                    Array.Sort(verts, new PointFLexicalComparer());
                    int k = 0;
                    var hullVerts = new PointF[n * 2];

                    // Build lower hull
                    for (int i = 0; i < n; ++i)
                    {
                        while (k >= 2 && Cross(hullVerts[k - 2], hullVerts[k - 1], verts[i]) <= 0) --k;
                        hullVerts[k++] = verts[i];
                    }

                    // Build upper hull
                    for (int i = n - 1, t = k + 1; i > 0; --i)
                    {
                        while (k >= t && Cross(hullVerts[k - 2], hullVerts[k - 1], verts[i - 1]) <= 0) --k;
                        hullVerts[k++] = verts[i - 1];
                    }

                    var hullPoints = new PointF[k];
                    Array.Copy(hullVerts, hullPoints, k);
                    polys.Add(hullPoints);
                }
                return polys;

                float Cross(PointF p1, PointF p2, PointF p3)
                {
                    return (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private struct PointFLexicalComparer : IComparer<PointF>
        {
            public int Compare(PointF x, PointF y)
            {
                int xComparison = x.X.CompareTo(y.X);
                return xComparison != 0 ? xComparison : x.Y.CompareTo(y.Y);
            }
        }

        protected Tuple<float, float, float> getCylinderDimensions()
        {
            try
            {
                PropertyCollection props = export.GetProperties();
                float xScalar = 1;
                float yScalar = 1;
                float zScalar = 1;

                var drawScale = props.GetProp<FloatProperty>("DrawScale");
                var drawScale3d = props.GetProp<StructProperty>("DrawScale3D");
                if (drawScale != null)
                {
                    xScalar = yScalar = zScalar = drawScale.Value;
                }
                if (drawScale3d != null)
                {
                    xScalar *= drawScale3d.GetProp<FloatProperty>("X").Value;
                    yScalar *= drawScale3d.GetProp<FloatProperty>("Y").Value;
                    zScalar *= drawScale3d.GetProp<FloatProperty>("Z").Value;
                }
                var cylinderComponent = props.GetProp<ObjectProperty>("CylinderComponent");
                if (cylinderComponent == null)
                {
                    return null;
                }
                ExportEntry cylinder = export.FileRef.GetUExport(cylinderComponent.Value);
                var graphVertices = new List<PointF>();
                var brushVertices = new List<Vector3>();
                PropertyCollection cylinderProps = cylinder.GetProperties();
                float cylinderradius = 0;
                float cylinderheight = 0;
                if (export.IsA("Trigger")) //default Unreal values
                {
                    cylinderradius = 40;
                    cylinderheight = 40;
                }
                var radiusprop = cylinderProps.GetProp<FloatProperty>("CollisionRadius");
                if (radiusprop != null)
                {
                    cylinderradius = radiusprop.Value;
                }
                var heightprop = cylinderProps.GetProp<FloatProperty>("CollisionHeight");
                if (heightprop != null)
                {
                    cylinderheight = heightprop.Value;
                }

                var xradius = cylinderradius * xScalar;
                var yradius = cylinderradius * yScalar;
                cylinderheight = cylinderheight * zScalar;

                return new Tuple<float, float, float>(xradius, yradius, cylinderheight);
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
