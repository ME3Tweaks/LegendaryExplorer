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
using ME3Explorer.SequenceObjects;
using System.Numerics;
using System.Diagnostics;

namespace ME3Explorer.Pathfinding_Editor
{
    public abstract class PathfindingNodeMaster : PNode
    {
        public PPath shape;
        public IMEPackage pcc;
        public PathingGraphEditor g;
        //public static ME1Explorer.TalkFiles talkfiles { get; set; }
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        public static Brush sfxCombatZoneBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
        public static Brush highlightedCoverSlotBrush = new SolidBrush(Color.FromArgb(219, 137, 6));

        protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static Brush actorNodeBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
        protected static Brush splineNodeBrush = new SolidBrush(Color.FromArgb(255, 60, 200));
        public static Brush pathfindingNodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Brush dynamicPathfindingNodeBrush = new SolidBrush(Color.FromArgb(46, 184, 25));
        protected static Brush dynamicPathnodefindingNodeBrush = new SolidBrush(Color.FromArgb(80, 184, 25));

        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool OutputNumbers;

        public int UIndex { get { return index; } }
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }

        protected int index;
        public IExportEntry export;
        protected Pen outlinePen;
        public SText comment;
        public List<IExportEntry> ReachSpecs = new List<IExportEntry>();
        public string NodeTag;

        public void Select()
        {
            shape.Pen = selectedPen;
            MoveToFront();
        }

        public void Deselect()
        {
            if (shape.Pen != outlinePen)
            {
                shape.Pen = outlinePen;
            }
        }

        public override bool Intersects(RectangleF bounds)
        {
            Region ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        //Empty implementation
        public virtual void CreateConnections(ref List<PathfindingNodeMaster> Objects)
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
                List<PointF> graphVertices = new List<PointF>();
                List<Vector3> brushVertices = new List<Vector3>();
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
                    Vector3 point = new Vector3();
                    point.X = vertex.GetProp<FloatProperty>("X") * xScalar;
                    point.Y = vertex.GetProp<FloatProperty>("Y") * yScalar;
                    point.Z = vertex.GetProp<FloatProperty>("Z");
                    brushVertices.Add(point);
                }

                //FaceTris
                var faceTriData = convexList[0].Properties.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                Vector3 previousVertex = new Vector3();
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

        protected virtual string GetComment()
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
                        retval += "_" + tagProp.Value.Number;
                    }
                    NodeTag = retval;
                    return retval;
                }
            }
            return "";
        }

    }
}
