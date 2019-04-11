using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UMD.HCIL.PathingGraphEditor;
using UMD.HCIL.Piccolo.Nodes;

namespace ME3Explorer.ActorNodes
{
    public class ActorNode : PathfindingNodeMaster
    {
        public PathingGraphEditor g;
        static Color commentColor = Color.FromArgb(74, 63, 190);
        protected static Pen annexZoneLocPen = Pens.Lime;

        protected ActorNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
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

        protected ActorNode(int idx, IMEPackage p)
        {
            pcc = p;
            index = idx;
            export = pcc.getUExport(index);
            comment = new SText(GetComment(), commentColor, false);
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

        public virtual void CreateConnections(ref List<ActorNode> Objects)
        {

        }
        public virtual void Layout(float x, float y) { }
    }

    public class BlockingVolumeNode : ActorNode
    {
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);

        public BlockingVolumeNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_BlockingVolume)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreateRectangle(0, 0, 50, 50);
            }
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, 50, 50);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = 50 / 2 - val.Width / 2;
            val.Y = 50 / 2 - val.Height / 2;
            this.AddChild(val);
            this.TranslateBy(x, y);
        }
    }

    public class DynamicBlockingVolume : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);
        public DynamicBlockingVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_DynamicBlockingVolume)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreateRectangle(0, 0, 50, 50);
            }
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = dynamicPathfindingNodeBrush;
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


    //This is technically not a pathnode...
    public class SFXObjectiveSpawnPoint : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 255, 0);
        PointF[] triangleshape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50) };

        public SFXObjectiveSpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            string commentText = comment.Text + "\nSpawnLocation: ";

            var spawnLocation = export.GetProperty<EnumProperty>("SpawnLocation");
            if (spawnLocation == null)
            {
                commentText += "Table";
            }
            else
            {
                commentText += spawnLocation.Value;
            }
            commentText += "\n";
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
            this.TranslateBy(x, y);


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
                int othernodeidx = annexZoneLocProp.Value;
                if (othernodeidx != 0)
                {
                    foreach (PathfindingNodeMaster node in Objects)
                    {
                        if (node.export.UIndex == othernodeidx)
                        {
                            othernode = node;
                            break;
                        }
                    }
                }

                if (othernode != null)
                {
                    PPath edge = new PPath();
                    edge.Pen = annexZoneLocPen;
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

    public class SFXBlockingVolume_Ledge : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 30, 30);
        private static PointF[] edgeShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(30, 50), new PointF(0, 50) };
        public SFXBlockingVolume_Ledge(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_SFXBlockingVolume_Ledge)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreatePolygon(edgeShape);
            }

            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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


    public class TargetPoint : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(30, 255, 30);
        PointF[] targetShape = new PointF[] { new PointF(20, 0), new PointF(30, 0), //top side
            new PointF(30, 15),new PointF(35, 15),new PointF(35, 20), //top right
            new PointF(50, 20), new PointF(50, 30), //right side
            new PointF(35, 30),new PointF(35, 35),new PointF(30, 35), //bottom right

            new PointF(30, 50), new PointF(20, 50), //bottom
            new PointF(20, 35),new PointF(15, 35),new PointF(15, 30), //bottom left
            new PointF(0, 30), new PointF(0, 20), //left
            new PointF(15, 20),new PointF(15, 15), new PointF(20, 15) };

        public TargetPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(targetShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = dynamicPathfindingNodeBrush;
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
        }
    }

    public class SFXMedKit : ActorNode
    {
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 10, 10);
        PointF[] plusShape = new PointF[] {
            new PointF(35, 0), new PointF(35, 15),new PointF(50, 15), //top right
            new PointF(50, 35),new PointF(35, 35), new PointF(35, 50), //bottom right
            new PointF(15, 50),new PointF(15, 35),new PointF(0, 35), //bottom left
            new PointF(0, 15), new PointF(15, 15), new PointF(15, 0), }; //top left

        public SFXMedKit(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(plusShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = medkitBrush;
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
        }
    }

    public class BioStartLocation : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(128, 255, 60);

        public BioStartLocation(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateEllipse(0, 10, w, h - 10);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    public class SFXStuntActor : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(128, 255, 60);
        PointF[] sShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(10, 10), new PointF(10, 20), new PointF(10, 25), new PointF(50, 25), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(40, 40), new PointF(40, 30), new PointF(0, 30) };

        public SFXStuntActor(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            base.shape = PPath.CreatePolygon(sShape);
            outlinePen = new Pen(color);
            base.shape.Pen = outlinePen;
            base.shape.Brush = actorNodeBrush;
            base.shape.Pickable = false;
            this.AddChild(base.shape);
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

    public class SkeletalMeshActor : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(200, 200, 200);
        PointF[] kShape = new PointF[] { new PointF(0, 0), new PointF(5, 0), new PointF(5, 20), new PointF(45, 0), new PointF(50, 0), new PointF(10, 25), new PointF(50, 50), new PointF(45, 50), new PointF(5, 35), new PointF(5, 50), new PointF(0, 50) };

        public SkeletalMeshActor(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(kShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    public class PendingActorNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(34, 218, 218);
        PointF[] aShape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50), new PointF(25, 30) };

        public PendingActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(aShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    /// <summary>
    /// This node is used on the Everything Else option. Technically not an actor, but I don't want to make a new class file for a single node type.
    /// </summary>
    public class EverythingElseNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(34, 218, 218);
        protected Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 120, 0));

        public EverythingElseNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreateRectangle(0, 0, w, h);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = backgroundBrush;
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
            if (comment.Text != "")
            {
                s += "\n";
            }
            comment.Text = s + comment.Text;
        }
    }

    public class StaticMeshActorNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(34, 218, 218);
        PointF[] aShape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50), new PointF(25, 30) };

        public StaticMeshActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(aShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

            ObjectProperty smc = export.GetProperty<ObjectProperty>("StaticMeshComponent");
            if (smc != null)
            {
                IExportEntry smce = pcc.Exports[smc.Value - 1];
                //smce.GetProperty<ObjectProperty>("St")
                var meshObj = smce.GetProperty<ObjectProperty>("StaticMesh");
                if (meshObj != null)
                {
                    IExportEntry sme = pcc.Exports[meshObj.Value - 1];
                    comment.Text = sme.ObjectName;
                }
            }
        }
    }

    public class WwiseAmbientSound : ActorNode
    {

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
            shape.Brush = actorNodeBrush;
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

    public class WwiseAudioVolume : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 255, 0);
        PointF[] soundShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(40, 0), new PointF(50, 0), new PointF(50, 10), new PointF(40, 10), new PointF(25, 50), new PointF(10, 10), new PointF(0, 10), new PointF(0, 0), new PointF(10, 0) };

        public WwiseAudioVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_WwiseAudioVolume)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreatePolygon(soundShape);
            }
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    public class SFXTreasureNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(100, 155, 0);
        protected static Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 120, 0));

        PointF[] soundShape = new PointF[] { new PointF(0, 50), new PointF(0, 15), new PointF(15, 0), new PointF(35, 0), new PointF(50, 15), new PointF(50, 50) };

        public SFXTreasureNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(soundShape);

            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = backgroundBrush;
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

    public class SFXAmmoContainer : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(178, 34, 34);
        PointF[] ammoShape = new PointF[] { new PointF(0, 10), new PointF(10, 10), new PointF(10, 0), new PointF(50, 0), new PointF(50, 50), new PointF(10, 50), new PointF(10, 40), new PointF(0, 40) };

        public SFXAmmoContainer(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(ammoShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

            var bRespawns = export.GetProperty<BoolProperty>("bRespawns");
            var respawnTime = export.GetProperty<IntProperty>("RespawnTime");
            string commentText = "Respawns: ";
            commentText += bRespawns != null ? bRespawns.Value.ToString() : "False";
            if (respawnTime != null)
            {
                commentText += "\nRespawn time: " + respawnTime.Value + "s";
            }
            else if (bRespawns != null && bRespawns.Value == true)
            {
                commentText += "\nRespawn time: 20s";
            }

            comment.Text = commentText;
        }
    }

    public class SFXAmmoContainer_Simulator : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(178, 34, 34);
        PointF[] ammoShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(50, 20), new PointF(50, 40), new PointF(0, 40), new PointF(0, 20) };

        public SFXAmmoContainer_Simulator(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(ammoShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

            var bRespawns = export.GetProperty<BoolProperty>("bRespawns");
            var respawnTime = export.GetProperty<IntProperty>("RespawnTime");
            string commentText = "Respawns: ";
            commentText += bRespawns != null ? bRespawns.Value.ToString() : "False";
            if (respawnTime != null)
            {
                commentText += "\nRespawn time: " + respawnTime.Value + "s";
            }
            else if (bRespawns != null && bRespawns.Value == true)
            {
                commentText += "\nRespawn time: 20s";
            }

            comment.Text = commentText;
        }
    }

    public class SFXGrenadeContainer : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 100, 0);
        PointF[] grenadeShape = new PointF[] { new PointF(0, 10), new PointF(15, 10), new PointF(15, 0), new PointF(35, 0), new PointF(35, 10), new PointF(50, 10), new PointF(50, 50), new PointF(0, 50) };

        public SFXGrenadeContainer(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(grenadeShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

            var bRespawns = export.GetProperty<BoolProperty>("bRespawns");
            var respawnTime = export.GetProperty<IntProperty>("RespawnTime");
            string commentText = "Respawns: ";
            commentText += bRespawns != null ? bRespawns.Value.ToString() : "False";
            if (respawnTime != null)
            {
                commentText += "\nRespawn time: " + respawnTime.Value + "s";
            }
            else if (bRespawns != null && bRespawns.Value == true)
            {
                commentText += "\nRespawn time: 20s";
            }
            comment.Text = commentText;
        }
    }

    public class SFXCombatZone : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(20, 34, 34);
        PointF[] cShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 8), new PointF(8, 8), new PointF(8, 42), new PointF(50, 42), new PointF(50, 50), new PointF(0, 50) };

        public SFXCombatZone(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_SFXCombatZones)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreatePolygon(cShape);
            }

            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    public class SFXPlaceable : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(20, 200, 34);
        PointF[] pShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 20), new PointF(10, 20), new PointF(10, 50), new PointF(0, 50) };

        public SFXPlaceable(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(pShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(idx.ToString());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            comment.Text = s;
            this.AddChild(val);
            this.TranslateBy(x, y);


        }
    }

    public class SFXDoorMarker : ActorNode
    {

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
            shape.Brush = actorNodeBrush;
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

    public class InterpActorNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 130, 255);
        PointF[] iShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(35, 10), new PointF(35, 40), new PointF(50, 40), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(10, 40), new PointF(10, 10), new PointF(0, 10) };

        public InterpActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(iShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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

    //This is technically not a BlockingVolumeNode...
    public class SMAC_ActorNode : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 255, 0);
        PointF[] SShape = new PointF[] { new PointF(50, 0), new PointF(0, 17), new PointF(35, 33), new PointF(0, 50), new PointF(50, 33), new PointF(15, 17) };

        public SMAC_ActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(SShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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


            ObjectProperty sm = export.GetProperty<ObjectProperty>("StaticMesh");
            if (sm != null)
            {
                IEntry meshexp = pcc.getEntry(sm.Value);
                string text = comment.Text;
                if (text != "")
                {
                    text += "\n";
                }
                comment.Text = text + meshexp.ObjectName;
            }
        }
    }

    public class BioTriggerVolume : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        //private static PointF[] TShape = ;
        private readonly static PointF[] TShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 15), new PointF(35, 15), new PointF(35, 50), new PointF(15, 50), new PointF(15, 15), new PointF(0, 15) };

        public BioTriggerVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
        : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;
            float w = 50;
            float h = 50;

            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_BioTriggerVolume)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreatePolygon(TShape);
            }

            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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


            /*if (comment != null)
            {
                NameProperty tagProp = export.GetProperty<NameProperty>("Tag");
                if (tagProp != null)
                {
                    string name = tagProp.Value;
                    if (name != export.ObjectName)
                    {
                        comment.Text = name;
                    }
                }
            }*/
        }

        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(ref List<ActorNode> Objects)
        {

        }
    }

    public class BioTriggerStream : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        private readonly static PointF[] TShape = new PointF[] {
            new PointF(15, 0), //top left of S, top left of T column
            new PointF(50, 0),//top right of S
            new PointF(50, 10), //going down
            new PointF(25, 10), //going left
            new PointF(25, 20), //top right of T center
            new PointF(50, 20), //top right of middle S
            new PointF(50, 50), //bottom right of S

            new PointF(25, 50),//bottom left of S
            new PointF(25, 40),

            new PointF(40, 40),
            new PointF(40, 30),
            new PointF(25, 30),
            new PointF(25, 50),//bottom right of center T column
            new PointF(15, 50),
            new PointF(15, 30),
            new PointF(0, 30),
            new PointF(0, 20),
            new PointF(15, 20)
        };

        public BioTriggerStream(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            outlinePen = new Pen(color);
            if (grapheditor.showVolumeBrushes && grapheditor.showVolume_BioTriggerStream)
            {
                var TShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (TShape != null)
                {
                    shape = PPath.CreatePolygon(TShape);
                }
                else
                {
                    shape = PPath.CreateRectangle(0, 0, 50, 50);
                }
                if (calculatedHeight >= 0)
                {
                    comment.Text += "\nBrush total height: " + calculatedHeight;
                }
            }
            else
            {
                shape = PPath.CreatePolygon(TShape);
            }

            shape.Pen = outlinePen;
            shape.Brush = actorNodeBrush;
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


            var exportProps = export.GetProperties();


            var streamingStates = exportProps.GetProp<ArrayProperty<StructProperty>>("StreamingStates");
            if (streamingStates != null)
            {
                string commentText = "";
                var tierName = exportProps.GetProp<NameProperty>("TierName");
                commentText += "Tier: " + tierName + "\n";
                foreach (StructProperty state in streamingStates)
                {
                    //List<string> visibleItems = 
                    var stateName = state.GetProp<NameProperty>("StateName");
                    var items = new List<string>();
                    commentText += "State: " + stateName + "\n";
                    var inChunkName = state.GetProp<NameProperty>("InChunkName");
                    commentText += "InChunkName: " + inChunkName + "\n";
                    var visibleChunkNames = state.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                    if (visibleChunkNames != null)
                    {
                        foreach (NameProperty name in visibleChunkNames)
                        {
                            items.Add(name.Value);
                        }
                        items.Sort();
                        foreach (string item in items)
                        {
                            commentText += "   " + item + "\n";

                        }
                    }
                }
                comment.Text = commentText;
            }
        }


        /// <summary>
        /// This has no outbound connections.
        /// </summary>
        public override void CreateConnections(ref List<ActorNode> Objects)
        {

        }
    }

    public class PendingNode : ActorNode
    {

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
            shape.Brush = actorNodeBrush;
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


        }
    }

    public class SFXMedStation : ActorNode
    {

        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);
        protected static Brush backgroundBrush = new SolidBrush(Color.FromArgb(128, 0, 0));
        protected static PointF[] medShape = new PointF[] { new PointF(17, 0), new PointF(33, 0), //top side
            new PointF(33, 17),new PointF(50, 17),new PointF(50, 33), //right side
            
            new PointF(33, 33),new PointF(33, 50),new PointF(17, 50), //bottom side
            new PointF(17, 33),new PointF(0, 33),new PointF(0, 17), new PointF(17,17) //left side
            };

        public SFXMedStation(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(medShape);
            outlinePen = new Pen(color);
            shape.Pen = outlinePen;
            shape.Brush = backgroundBrush;
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


        }
    }



    public class SFXNav_JumpNode : ActorNode
    {

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
            shape.Brush = actorNodeBrush;
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


        }
    }

    public class SFXNav_TurretPoint : ActorNode
    {

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
            shape.Brush = actorNodeBrush;
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


        }
    }
}