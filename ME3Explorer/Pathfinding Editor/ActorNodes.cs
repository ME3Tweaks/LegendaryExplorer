using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UMD.HCIL.Piccolo.Nodes;

namespace ME3Explorer.ActorNodes
{
    public abstract class ActorNode : PathfindingNodeMaster
    {
        //public PathingGraphEditor g;
        static Color commentColor = Color.FromArgb(74, 63, 190);
        protected static Pen annexZoneLocPen = Pens.Lime;
        internal bool ShowAsPolygon;
        internal SText val;

        //Allows colors and points to be static yet accessible
        public abstract Color GetDefaultShapeColor();
        public abstract PointF[] GetDefaultShapePoints();

        protected ActorNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
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

        public void SetShape(bool polygon)
        {
            if (shape != null)
            {
                RemoveChild(shape);
            }
            bool addVal = val == null;
            if (val == null)
            {
                val = new SText(index.ToString());
                val.Pickable = false;
                val.TextAlignment = StringAlignment.Center;
            }

            ShowAsPolygon = polygon;
            outlinePen = new Pen(GetDefaultShapeColor()); //Can't put this in a class variable becuase it doesn't seem to work for some reason.
            if (polygon)
            {
                var polygonShape = get3DBrushShape();
                int calculatedHeight = get3DBrushHeight();
                if (polygonShape != null)
                {
                    shape = PPath.CreatePolygon(polygonShape);
                    var AveragePoint = GetAveragePoint(polygonShape);
                    val.X = AveragePoint.X - val.Width / 2;
                    val.Y = AveragePoint.Y - val.Height / 2;
                    if (calculatedHeight >= 0)
                    {
                        SText brushText = new SText("Brush total height: " + calculatedHeight);
                        brushText.X = AveragePoint.X - brushText.Width / 2;
                        brushText.Y = AveragePoint.Y + 20 - brushText.Height / 2;
                        brushText.Pickable = false;
                        brushText.TextAlignment = StringAlignment.Center;
                        shape.AddChild(brushText);
                    }
                    shape.Pen = Selected ? selectedPen : outlinePen;
                    shape.Brush = actorNodeBrush;
                    shape.Pickable = false;
                }
                else
                {
                    SetDefaultShape();
                }
            }
            else
            {
                SetDefaultShape();
            }
            AddChild(0, shape);
            if (addVal)
            {
                AddChild(val);
            }
        }

        public virtual void CreateConnections(ref List<ActorNode> Objects)
        {

        }
        public virtual void Layout(float x, float y) { }

        public static PointF GetAveragePoint(PointF[] polygonShape)
        {
            double X = 0;
            double Y = 0;
            foreach (PointF point in polygonShape)
            {
                X += point.X;
                Y += point.Y;
            }
            return new PointF((float)(X / polygonShape.Length), (float)(Y / polygonShape.Length));
        }

        private void SetDefaultShape()
        {
            PPath defaultShape = PPath.CreatePolygon(GetDefaultShapePoints());
            shape = defaultShape;
            shape.Pen = Selected ? selectedPen : outlinePen;
            shape.Brush = actorNodeBrush;
            shape.Pickable = false;
            val.X = 50 / 2 - val.Width / 2;
            val.Y = 50 / 2 - val.Height / 2;
        }
    }

    public class BlockingVolume : ActorNode
    {
        private static Color outlinePenColor = Color.Red;
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 50), new PointF(0, 50) };

        public BlockingVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawAsPolygon)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawAsPolygon);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class BioPlaypenVolumeAdditive : ActorNode
    {

        private static Color outlinePenColor = Color.Orange;
        private static PointF[] outlineShape = new PointF[] { new PointF(10, 0), new PointF(50, 10), new PointF(40, 50), new PointF(0, 40) };

        public BioPlaypenVolumeAdditive(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawAsPolygon = false)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawAsPolygon);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class DynamicBlockingVolume : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(255, 0, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 50), new PointF(0, 50) };

        public DynamicBlockingVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawPolygon)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawPolygon);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }


    //This is technically not a pathnode...
    public class SFXObjectiveSpawnPoint : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 255, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50) };

        public SFXObjectiveSpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
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

            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            comment.Text = commentText;
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
                    PathfindingEditorEdge edge = new PathfindingEditorEdge();
                    edge.Pen = annexZoneLocPen;
                    edge.EndPoints.Add(this);
                    edge.EndPoints.Add(othernode);
                    if (!Edges.Any(x => x.DoesEdgeConnectSameNodes(edge)) && othernode.Edges.Any(x=>x.DoesEdgeConnectSameNodes(edge)))
                    {
                        Edges.Add(edge);
                        othernode.Edges.Add(edge);
                        g.edgeLayer.AddChild(edge);
                    }
                }
            }
        }
        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXBlockingVolume_Ledge : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(255, 30, 30);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(30, 50), new PointF(0, 50) };
        public SFXBlockingVolume_Ledge(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }


    public class TargetPoint : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(30, 255, 30);
        private static PointF[] outlineShape = new PointF[] { new PointF(20, 0), new PointF(30, 0), //top side
            new PointF(30, 15),new PointF(35, 15),new PointF(35, 20), //top right
            new PointF(50, 20), new PointF(50, 30), //right side
            new PointF(35, 30),new PointF(35, 35),new PointF(30, 35), //bottom right

            new PointF(30, 50), new PointF(20, 50), //bottom
            new PointF(20, 35),new PointF(15, 35),new PointF(15, 30), //bottom left
            new PointF(0, 30), new PointF(0, 20), //left
            new PointF(15, 20),new PointF(15, 15), new PointF(20, 15) };

        public TargetPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
            shape.Brush = dynamicPathfindingNodeBrush;
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXMedKit : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(255, 10, 10);
        private static PointF[] outlineShape = new PointF[] {
            new PointF(35, 0), new PointF(35, 15),new PointF(50, 15), //top right
            new PointF(50, 35),new PointF(35, 35), new PointF(35, 50), //bottom right
            new PointF(15, 50),new PointF(15, 35),new PointF(0, 35), //bottom left
            new PointF(0, 15), new PointF(15, 15), new PointF(15, 0), }; //top left

        public SFXMedKit(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class BioStartLocation : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(128, 255, 60);

        //TODO: UPDATE THIS. ORIGINALLY AN ELLIPSE.
        private static PointF[] outlineShape = new PointF[] {
            new PointF(35, 0), new PointF(35, 15),new PointF(50, 15), //top right
            new PointF(50, 35),new PointF(35, 35), new PointF(35, 50), //bottom right
            new PointF(15, 50),new PointF(15, 35),new PointF(0, 35), //bottom left
            new PointF(0, 15), new PointF(15, 15), new PointF(15, 0), }; //top left

        public BioStartLocation(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXStuntActor : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(128, 255, 60);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(10, 10), new PointF(10, 20), new PointF(10, 25), new PointF(50, 25), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(40, 40), new PointF(40, 30), new PointF(0, 30) };

        public SFXStuntActor(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SkeletalMeshActor : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(200, 200, 200);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(5, 0), new PointF(5, 20), new PointF(45, 0), new PointF(50, 0), new PointF(10, 25), new PointF(50, 50), new PointF(45, 50), new PointF(5, 35), new PointF(5, 50), new PointF(0, 50) };

        public SkeletalMeshActor(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }


        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class PendingActorNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(34, 218, 218);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50), new PointF(25, 30) };

        public PendingActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    /// <summary>
    /// This node is used on the Everything Else option. Technically not an actor, but I don't want to make a new class file for a single node type. (yet I did for splines!)
    /// </summary>
    public class EverythingElseNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(34, 218, 218);
        private static PointF[] outlineShape = new PointF[] { new PointF(15, 0), new PointF(35, 0), new PointF(50, 25), new PointF(35, 50), new PointF(15, 50), new PointF(0, 25) };

        protected Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 120, 0));

        public EverythingElseNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            shape.Brush = backgroundBrush;
            TranslateBy(x, y);

            string s = export.ObjectName;
            if (comment.Text != "")
            {
                s += "\n";
            }
            comment.Text = export.ObjectName + comment.Text;
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class StaticMeshActorNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(34, 218, 218);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(25, 0), new PointF(50, 50), new PointF(25, 30) };

        public StaticMeshActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);

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

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class WwiseAmbientSound : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 255, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(40, 0), new PointF(50, 0), new PointF(50, 10), new PointF(40, 10), new PointF(25, 50), new PointF(10, 10), new PointF(0, 10), new PointF(0, 0), new PointF(10, 0) };

        public WwiseAmbientSound(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class WwiseAudioVolume : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 255, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(40, 0), new PointF(50, 0), new PointF(50, 10), new PointF(40, 10), new PointF(25, 50), new PointF(10, 10), new PointF(0, 10), new PointF(0, 0), new PointF(10, 0) };

        public WwiseAudioVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawAsPolygon) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawAsPolygon);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXTreasureNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(100, 155, 0);
        protected static Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 120, 0));

        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(0, 15), new PointF(15, 0), new PointF(35, 0), new PointF(50, 15), new PointF(50, 50) };

        public SFXTreasureNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            shape.Brush = backgroundBrush;
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXAmmoContainer : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(178, 34, 34);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 10), new PointF(10, 10), new PointF(10, 0), new PointF(50, 0), new PointF(50, 50), new PointF(10, 50), new PointF(10, 40), new PointF(0, 40) };

        public SFXAmmoContainer(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);

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

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXAmmoContainer_Simulator : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(178, 34, 34);
        private static PointF[] outlineShape = new PointF[] { new PointF(10, 10), new PointF(40, 10), new PointF(50, 20), new PointF(50, 40), new PointF(0, 40), new PointF(0, 20) };

        public SFXAmmoContainer_Simulator(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);

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

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXGrenadeContainer : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 100, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 10), new PointF(15, 10), new PointF(15, 0), new PointF(35, 0), new PointF(35, 10), new PointF(50, 10), new PointF(50, 50), new PointF(0, 50) };

        public SFXGrenadeContainer(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);

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

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXCombatZone : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(20, 34, 34);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 8), new PointF(8, 8), new PointF(8, 42), new PointF(50, 42), new PointF(50, 50), new PointF(0, 50) };

        public SFXCombatZone(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawPolygon) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawPolygon);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXPlaceable : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(20, 200, 34);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 20), new PointF(10, 20), new PointF(10, 50), new PointF(0, 50) };

        public SFXPlaceable(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor) : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
            comment.Text = export.ObjectName + "_" + export.indexValue;
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXDoorMarker : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 100, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 25), new PointF(10, 0), new PointF(10, 13), new PointF(40, 13), new PointF(40, 0), new PointF(50, 25), new PointF(40, 50), new PointF(40, 37), new PointF(10, 37), new PointF(10, 50) };

        public SFXDoorMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class InterpActorNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 130, 255);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(35, 10), new PointF(35, 40), new PointF(50, 40), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(10, 40), new PointF(10, 10), new PointF(0, 10) };

        public InterpActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    //This is technically not a BlockingVolumeNode...
    public class SMAC_ActorNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 255, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(50, 0), new PointF(0, 17), new PointF(35, 33), new PointF(0, 50), new PointF(50, 33), new PointF(15, 17) };

        public SMAC_ActorNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);

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
        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }

    }

    public class BioTriggerVolume : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 0, 255);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 15), new PointF(35, 15), new PointF(35, 50), new PointF(15, 50), new PointF(15, 15), new PointF(0, 15) };

        public BioTriggerVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawPolygon)
        : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawPolygon);
            TranslateBy(x, y);
        }
        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class DynamicTriggerVolume : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(20, 255, 20);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 15), new PointF(35, 15), new PointF(35, 50), new PointF(15, 50), new PointF(15, 15), new PointF(0, 15) };

        public DynamicTriggerVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
        : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            shape.Brush = dynamicPathfindingNodeBrush;
            TranslateBy(x, y);
        }
        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class BioTriggerStream : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 0, 255);
        private static PointF[] outlineShape = new PointF[] {
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

        public BioTriggerStream(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor, bool drawPolygon)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(drawPolygon);
            TranslateBy(x, y);

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

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class PendingNode : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 0, 255);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 50), new PointF(0, 50) };

        public PendingNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }


        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }

    public class SFXMedStation : ActorNode
    {
        private static Color outlinePenColor = Color.FromArgb(255, 0, 0);
        private static Brush backgroundBrush = new SolidBrush(Color.FromArgb(128, 0, 0));
        private static PointF[] outlineShape = new PointF[] { new PointF(17, 0), new PointF(33, 0), //top side
            new PointF(33, 17),new PointF(50, 17),new PointF(50, 33), //right side
            
            new PointF(33, 33),new PointF(33, 50),new PointF(17, 50), //bottom side
            new PointF(17, 33),new PointF(0, 33),new PointF(0, 17), new PointF(17,17) //left side
            };

        public SFXMedStation(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape(false);
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PointF[] GetDefaultShapePoints()
        {
            return outlineShape;
        }
    }
}