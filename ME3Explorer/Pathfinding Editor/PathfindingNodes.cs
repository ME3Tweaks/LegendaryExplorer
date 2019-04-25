using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using UMD.HCIL.Piccolo.Nodes;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SequenceObjects;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ME3Explorer.PathfindingNodes
{
    public abstract class PathfindingNode : PathfindingNodeMaster
    {
        //protected PPath shape;
        //public IMEPackage pcc;
        //public PathingGraphEditor g;
        //public static ME1Explorer.TalkFiles talkfiles { get; set; }
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        static Pen blackPen = Pens.Black;
        static Pen halfReachSpecPen = new Pen(Color.FromArgb(90, 90, 90));
        static Pen slotToSlotPen = Pens.DarkOrange;
        static Pen coverSlipPen = Pens.OrangeRed;
        static Pen sfxLadderPen = Pens.Purple;
        static Pen sfxBoostPen = Pens.Blue;
        static Pen sfxJumpDownPen = Pens.Maroon;
        static Pen sfxLargeBoostPen = Pens.DeepPink;
        internal SText val;

        private Pen edgePen = blackPen;

        public List<Volume> Volumes = new List<Volume>();
        public List<IExportEntry> ReachSpecs = new List<IExportEntry>();

        public abstract Color GetDefaultShapeColor();
        public abstract PPath GetDefaultShape();
        //protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        //protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        //public static bool draggingOutlink = false;
        //public static bool draggingVarlink = false;
        //public static PNode dragTarget;
        //public static bool OutputNumbers;

        //public int Index { get { return index; } }
        ////public float Width { get { return shape.Width; } }
        ////public float Height { get { return shape.Height; } }

        //protected int index;
        //public IExportEntry export;
        //protected Pen outlinePen;
        //protected SText comment;
        protected PathfindingNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
            try
            {
                pcc = p;
                g = grapheditor;
                index = idx;
                export = pcc.getUExport(index);
                comment = new SText(GetComment(), commentColor, false);
                comment.X = 0;
                comment.Y = 65 - comment.Height;
                comment.Pickable = false;

                var volsArray = export.GetProperty<ArrayProperty<StructProperty>>("Volumes");
                if (volsArray != null)
                {
                    foreach (var volumestruct in volsArray)
                    {
                        Volumes.Add(new Volume(volumestruct));
                    }
                }

                this.AddChild(comment);
                this.Pickable = true;
            }
            catch (Exception e)
            {
                Debugger.Break();
            }
        }

        /// <summary>
        /// Refreshes reachspecs after one has been removed from this node.
        /// </summary>
        public void RefreshConnectionsAfterReachspecRemoval(List<PathfindingNodeMaster> graphNodes)
        {
            List<PathfindingEditorEdge> edgesToRemove = new List<PathfindingEditorEdge>();
            foreach (PathfindingEditorEdge edge in Edges)
            {
                //Remove remote connections
                if (edge.EndPoints[0] != this && edge.EndPoints[0] is PathfindingNode pn0)
                {
                    var removed = pn0.Edges.Remove(edge);
                    if (removed)
                    {
                        Debug.WriteLine("Removed edge during refresh on endpoint 0");
                    }
                }
                if (edge.EndPoints[1] != this && edge.EndPoints[1] is PathfindingNode pn1)
                {
                    pn1.Edges.Remove(edge);
                    Debug.WriteLine("Removed edge during refresh on endpoint 1");
                }

                if (edge.IsOneWayOnly())
                {
                    edge.Pen.DashStyle = DashStyle.Dash;
                }
                else if (!edge.HasAnyOutboundConnections())
                {
                    edgesToRemove.Add(edge);
                }
            }
            Debug.WriteLine("Remaining edges: " + Edges.Count);
            //don't know why i have to cast since my edge class is subclass of pnode already
            g.edgeLayer.RemoveChildrenList(edgesToRemove.Cast<UMD.HCIL.Piccolo.PNode>().ToList());
            //Edges.Clear();

            //CreateConnections(graphNodes);
            Edges.ForEach(x => PathingGraphEditor.UpdateEdgeStraight(x));
        }

        /// <summary>
        /// Creates the reachspec connections from this pathfinding node to others.
        /// </summary>
        public override void CreateConnections(List<PathfindingNodeMaster> graphNodes)
        {
            ReachSpecs = (SharedPathfinding.GetReachspecExports(export));
            foreach (IExportEntry spec in ReachSpecs)
            {
                Pen penToUse = blackPen;
                switch (spec.ObjectName)
                {
                    case "SlotToSlotReachSpec":
                        penToUse = slotToSlotPen;
                        break;
                    case "CoverSlipReachSpec":
                        penToUse = coverSlipPen;
                        break;
                    case "SFXLadderReachSpec":
                        penToUse = sfxLadderPen;
                        break;
                    case "SFXLargeBoostReachSpec":
                        penToUse = sfxLargeBoostPen;
                        break;
                    case "SFXBoostReachSpec":
                        penToUse = sfxBoostPen;
                        break;
                    case "SFXJumpDownReachSpec":
                        penToUse = sfxJumpDownPen;
                        break;
                }
                //Get ending
                PathfindingNodeMaster othernode = null;
                PropertyCollection props = spec.GetProperties();
                IExportEntry otherEndExport = SharedPathfinding.GetReachSpecEndExport(spec, props);

                /*
                if (props.GetProp<StructProperty>("End") is StructProperty endProperty &&
                    endProperty.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(spec)) is ObjectProperty otherNodeValue)
                {
                    othernodeidx = otherNodeValue.Value;
                }*/

                if (otherEndExport != null)
                {
                    bool isTwoWay = false;
                    othernode = graphNodes.FirstOrDefault(x => x.export == otherEndExport);
                    if (othernode != null)
                    {
                        //Check for returning reachspec for pen drawing. This is going to incur a significant performance penalty...
                        var othernodeSpecs = SharedPathfinding.GetReachspecExports(otherEndExport);
                        foreach (var path in othernodeSpecs)
                        {
                            if (SharedPathfinding.GetReachSpecEndExport(path) == export)
                            {
                                isTwoWay = true;
                                break;
                            }
                        }

                        //var 
                        //    PropertyCollection otherSpecProperties = possibleIncomingSpec.GetProperties();

                        //    if (otherSpecProperties.GetProp<StructProperty>("End") is StructProperty endStruct)
                        //    {
                        //        if (endStruct.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(possibleIncomingSpec)) is ObjectProperty incomingTargetIdx)
                        //        {
                        //            if (incomingTargetIdx.Value == export.UIndex)
                        //            {
                        //                isTwoWay = true;
                        //                break;
                        //            }
                        //        }
                        //    }
                        //}

                        //if (othernode != null)
                        //{
                        IntProperty radius = props.GetProp<IntProperty>("CollisionRadius");
                        IntProperty height = props.GetProp<IntProperty>("CollisionHeight");

                        bool penCloned = false;
                        if (radius != null && height != null && (radius >= ReachSpecSize.MINIBOSS_RADIUS || height >= ReachSpecSize.MINIBOSS_HEIGHT))
                        {
                            penCloned = true;
                            penToUse = (Pen)penToUse.Clone();

                            if (radius >= ReachSpecSize.BOSS_RADIUS && height >= ReachSpecSize.BOSS_HEIGHT)
                            {
                                penToUse.Width = 3;
                            }
                            else
                            {
                                penToUse.Width = 2;
                            }
                        }
                        if (!isTwoWay)
                        {
                            if (!penCloned)
                            {
                                penToUse = (Pen)penToUse.Clone();
                                penCloned = true;
                            }
                            penToUse.DashStyle = DashStyle.Dash;
                        }

                        PathfindingEditorEdge edge = new PathfindingEditorEdge();
                        edge.Pen = penToUse;
                        edge.EndPoints[0] = this;
                        edge.EndPoints[1] = othernode;
                        edge.OutboundConnections[0] = true;
                        edge.OutboundConnections[1] = isTwoWay;
                        if (!Edges.Any(x => x.DoesEdgeConnectSameNodes(edge)) && !othernode.Edges.Any(x => x.DoesEdgeConnectSameNodes(edge)))
                        {
                            //Only add edge if neither node contains this edge
                            Edges.Add(edge);
                            othernode.Edges.Add(edge);
                            g.edgeLayer.AddChild(edge);
                        }
                    }

                    //if ()
                    //((ArrayList)Tag).Add(edge); //add edge to my tracked items
                    //((ArrayList)othernode.Tag).Add(edge); //add edge to other node's tracked items
                    //edge.Tag = new ArrayList();
                    //((ArrayList)edge.Tag).Add(this); //Add edge's tracked item for me
                    //((ArrayList)edge.Tag).Add(othernode); //Add edge's tracked item for the other

                    //g.edgeLayer.AddChild(edge);
                }
            }
        }

        public void SetShape()
        {
            if (shape != null)
            {
                RemoveChild(shape);
            }
            bool addVal = val == null;
            if (val == null)
            {
                val = new SText(index.ToString());

                if (Properties.Settings.Default.PathfindingEditorShowNodeSizes)
                {
                    StructProperty maxPathSize = export.GetProperty<StructProperty>("MaxPathSize");
                    if (maxPathSize != null)
                    {
                        float height = maxPathSize.GetProp<FloatProperty>("Height");
                        float radius = maxPathSize.GetProp<FloatProperty>("Radius");

                        if (radius >= 135)
                        {
                            val.Text += "\nBS";
                        }
                        else if (radius >= 90)
                        {
                            val.Text += "\nMB";
                        }
                        else
                        {
                            val.Text += "\nM";
                        }
                    }
                }
                val.Pickable = false;
                val.TextAlignment = StringAlignment.Center;
            }
            outlinePen = new Pen(GetDefaultShapeColor()); //Pen's can't be static and used in more than one place at a time.
            shape = GetDefaultShape();
            shape.Pen = Selected ? selectedPen : outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            val.X = 50 / 2 - val.Width / 2;
            val.Y = 50 / 2 - val.Height / 2; AddChild(0, shape);
            if (addVal)
            {
                AddChild(val);
            }
        }


        /// <summary>
        /// Class for storing Volumes information. THIS IS NOT A NODE TYPE.
        /// </summary>
        public class Volume
        {
            public int ActorUIndex;
            public UnrealGUID ActorReference;
            public Volume(StructProperty volumestruct)
            {
                ActorReference = new UnrealGUID(volumestruct.GetProp<StructProperty>("Guid"));
                ActorUIndex = volumestruct.GetProp<ObjectProperty>("Actor").Value;
            }
        }
    }
    [DebuggerDisplay("PathNode - {UIndex}")]
    public class PathNode : PathfindingNode
    {
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color outlinePenColor = Color.FromArgb(34, 218, 218);

        public PathNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreateEllipse(0, 0, 50, 50);
        }
    }

    public class BioPathPoint : PathNode
    {
        public BioPathPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }

    public class PathNode_Dynamic : PathNode
    {
        public PathNode_Dynamic(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathnodefindingNodeBrush;
        }
    }

    public class CoverLink : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(63, 102, 207);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(0, 35), new PointF(15, 35), new PointF(15, 0), new PointF(35, 0), new PointF(35, 35), new PointF(50, 35), new PointF(50, 50) };

        public CoverLink(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXDynamicCoverLink : CoverLink
    {
        public SFXDynamicCoverLink(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }



    public class SFXNav_BoostNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(17, 189, 146);
        private static PointF[] boostdownshape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 50), new PointF(40, 40), new PointF(30, 50), new PointF(20, 40), new PointF(10, 50), new PointF(0, 40) };
        private static PointF[] boostbottomshape = new PointF[] { new PointF(0, 50), new PointF(50, 50), new PointF(50, 0), new PointF(40, 10), new PointF(30, 0), new PointF(20, 10), new PointF(10, 0), new PointF(0, 10) };

        public SFXNav_BoostNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = boostdownshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = boostbottomshape;
            }
            return PPath.CreatePolygon(shapetouse);
        }
    }

    public class SFXNav_JumpDownNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(191, 82, 93);
        private static PointF[] jumptopshape = new PointF[] { new PointF(0, 0), new PointF(40, 0), new PointF(40, 35), new PointF(50, 35), new PointF(35, 50), new PointF(20, 35), new PointF(30, 35), new PointF(30, 10), new PointF(0, 10) };
        private static PointF[] jumplandingshape = new PointF[] { new PointF(15, 0), new PointF(35, 0), new PointF(35, 20), new PointF(50, 20), new PointF(25, 40), new PointF(50, 40), new PointF(50, 50), new PointF(0, 50), new PointF(0, 40), new PointF(25, 40), new PointF(0, 20), new PointF(15, 20) };

        public SFXNav_JumpDownNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = jumptopshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = jumplandingshape;
            }
            return PPath.CreatePolygon(shapetouse);
        }
    }

    public class SFXNav_LadderNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(127, 76, 186);
        private static PointF[] laddertopshape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 10), new PointF(30, 10), new PointF(30, 20), new PointF(50, 20), new PointF(50, 30), new PointF(30, 30), new PointF(30, 40),
            new PointF(35, 40), new PointF(25, 50), new PointF(15, 40),
            new PointF(20, 40), new PointF(20, 30), new PointF(0, 30),new PointF(0, 20), new PointF(20, 20),new PointF(20, 10), new PointF(0, 10) };

        private static PointF[] ladderbottomshape = new PointF[] { new PointF(15, 10), new PointF(25, 0), new PointF(35, 10), new PointF(30, 10), new PointF(30, 10), new PointF(30, 20), new PointF(50, 20), new PointF(50, 30), new PointF(30, 30), new PointF(30, 40), new PointF(50, 40), new PointF(50, 50),
            new PointF(0, 50), new PointF(0, 40), new PointF(20, 40), new PointF(20, 30), new PointF(0, 30),new PointF(0, 20), new PointF(20, 20),new PointF(20, 10) };

        public SFXNav_LadderNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            BoolProperty bTopNode = export.GetProperty<BoolProperty>("bTopNode");
            PointF[] shapetouse = laddertopshape;
            if (bTopNode == null || bTopNode.Value == false)
            {
                shapetouse = ladderbottomshape;
            }
            return PPath.CreatePolygon(shapetouse);
        }
    }

    public class SFXNav_LargeBoostNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(219, 112, 147);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 10), new PointF(10, 0), new PointF(20, 10), new PointF(30, 0), new PointF(40, 10), new PointF(50, 0), new PointF(50, 50), new PointF(40, 40), new PointF(30, 50), new PointF(20, 40), new PointF(10, 50), new PointF(0, 40) };

        public SFXNav_LargeBoostNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }
    public class SFXDoorMarker : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 100, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 25), new PointF(10, 0), new PointF(10, 13), new PointF(40, 13), new PointF(40, 0), new PointF(50, 25), new PointF(40, 50), new PointF(40, 37), new PointF(10, 37), new PointF(10, 50) };

        public SFXDoorMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXNav_LeapNodeHumanoid : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 100, 0);
        private static PointF[] outlineShape = new PointF[] {
            new PointF(20, 0), new PointF(30, 0), new PointF(30, 5), new PointF(27, 5), new PointF(27, 10),  //inner elbow of right arm
            new PointF(45, 10), new PointF(45, 3), new PointF(50, 3), new PointF(50, 14), new PointF(27, 14), //upper thigh of right leg
            new PointF(27, 25), new PointF(50, 25), new PointF(40, 45), new PointF(35, 45), new PointF(44, 30), //behind right leg kneecap 
            new PointF(26, 30), new PointF(7, 50), new PointF(0, 50), new PointF(23, 30), new PointF(23, 14), //left armpit
            new PointF(0, 22), new PointF(0, 18), new PointF(23, 10), new PointF(23, 5), new PointF(20, 5) //bottom left of head
        };

        public SFXNav_LeapNodeHumanoid(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class PendingNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(0, 0, 255);

        public PendingNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return shape = PPath.CreateRectangle(0, 0, 50, 50);
        }
    }

    public class CoverSlotMarker : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(153, 153, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 15), new PointF(25, 0), new PointF(50, 15), new PointF(25, 50) };

        public CoverSlotMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXDynamicCoverSlotMarker : CoverSlotMarker
    {
        public SFXDynamicCoverSlotMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            shape.Brush = dynamicPathfindingNodeBrush;
        }
    }

    public class MantleMarker : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(85, 59, 255);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        public MantleMarker(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXNav_HarvesterMoveNode : PathfindingNode
    {
        //Not sure if this is actually implemented or not.
        private static Color outlinePenColor = Color.FromArgb(85, 59, 255);
        //Shape may be same as mantle marker
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        public SFXNav_HarvesterMoveNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXNav_LargeMantleNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(185, 59, 55);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 50), new PointF(0, 10), new PointF(35, 10), new PointF(35, 0), new PointF(50, 20), new PointF(35, 35), new PointF(35, 25), new PointF(20, 25), new PointF(20, 50), new PointF(0, 50) };

        public SFXNav_LargeMantleNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXEnemySpawnPoint : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(255, 0, 0);
        private static PointF[] outlineShape = new PointF[] { new PointF(0, 0), new PointF(25, 12), new PointF(50, 0), new PointF(37, 25), new PointF(50, 50), new PointF(25, 37), new PointF(0, 50), new PointF(12, 25) };

        public SFXEnemySpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXNav_JumpNode : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(148, 0, 211);
        private static PointF[] outlineShape = new PointF[] { new PointF(25, 0), new PointF(50, 50), new PointF(25, 37), new PointF(0, 50) };

        public SFXNav_JumpNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }

        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }

    public class SFXNav_TurretPoint : PathfindingNode
    {
        private static Color outlinePenColor = Color.FromArgb(139, 69, 19);
        private static PointF[] outlineShape = new PointF[] { new PointF(25, 0), new PointF(50, 25), new PointF(25, 50), new PointF(0, 25) };

        public SFXNav_TurretPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            Bounds = new RectangleF(0, 0, 50, 50);
            SetShape();
            TranslateBy(x, y);
        }
        public override Color GetDefaultShapeColor()
        {
            return outlinePenColor;
        }

        public override PPath GetDefaultShape()
        {
            return PPath.CreatePolygon(outlineShape);
        }
    }
}