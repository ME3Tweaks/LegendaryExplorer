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
using ME3Explorer.Pathfinding_Editor;

namespace ME3Explorer.ActorNodes
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };

    public class ActorNode : PathfindingNodeMaster
    {
        public PathingGraphEditor g;
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange



        protected ActorNode(int idx, IMEPackage p, PathingGraphEditor grapheditor)
        {
            Tag = new ArrayList(); //outbound reachspec edges.
            pcc = p;
            g = grapheditor;
            index = idx;
            export = pcc.getExport(index);
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
            export = pcc.getExport(index);
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

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((BlockingVolumeNode)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((BlockingVolumeNode)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }

        /// <summary>
        /// Creates the reachspec connections from this pathfinding node to others.
        /// </summary>
        public virtual void CreateConnections(ref List<ActorNode> Objects)
        {
            /*var outLinksProp = export.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    int reachspecexport = prop.Value;
                    ReachSpecs.Add(pcc.Exports[reachspecexport - 1]);
                }

                foreach (IExportEntry spec in ReachSpecs)
                {
                    //Get ending
                    PNode othernode = null;
                    int othernodeidx = 0;
                    PropertyCollection props = spec.GetProperties();
                    foreach (var prop in props)
                    {
                        if (prop.Name == "End")
                        {
                            PropertyCollection reachspecprops = (prop as StructProperty).Properties;
                            foreach (var rprop in reachspecprops)
                            {
                                if (rprop.Name == "Actor")
                                {
                                    othernodeidx = (rprop as ObjectProperty).Value;
                                    break;
                                }
                            }
                        }
                        if (othernodeidx != 0)
                        {
                            break;
                        }
                    }

                    if (othernodeidx != 0)
                    {
                        foreach (ActorNode node in Objects)
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
                        ((ArrayList)Tag).Add(edge);
                        ((ArrayList)othernode.Tag).Add(edge);
                        edge.Tag = new ArrayList();
                        ((ArrayList)edge.Tag).Add(this);
                        ((ArrayList)edge.Tag).Add(othernode);
                        g.edgeLayer.AddChild(edge);
                    }
                }
            }*/
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

    public class BlockingVolumeNode : ActorNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);

        public BlockingVolumeNode(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
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
            this.TranslateBy(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXBlockingVolume_Ledge : ActorNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 30, 30);
        PointF[] edgeShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(30, 50), new PointF(0, 50) };
        public SFXBlockingVolume_Ledge(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(edgeShape);
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class BioStartLocation : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXStuntActor : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SkeletalMeshActor : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class PendingActorNode : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class WwiseAmbientSound : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }


    }

    public class SFXAmmoContainer : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXGrenadeContainer : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXCombatZone : ActorNode
    {
        public VarTypes type { get; set; }
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
            shape = PPath.CreatePolygon(cShape);
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXPlaceable : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXDoorMarker : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class InterpActorNode : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    //This is technically not a BlockingVolumeNode...
    public class SMAC_ActorNode : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
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

    //This is technically not a BlockingVolumeNode...
    public class BioTriggerVolume : ActorNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(0, 0, 255);
        PointF[] TShape = new PointF[] { new PointF(0, 0), new PointF(50, 0), new PointF(50, 15), new PointF(35, 15), new PointF(35, 50), new PointF(15, 50), new PointF(15, 15), new PointF(0, 15) };

        public BioTriggerVolume(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(TShape);
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
            if (comment != null)
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
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXEnemySpawnPoint : ActorNode
    {
        public VarTypes type { get; set; }
        private SText val;
        public string Value { get { return val.Text; } set { val.Text = value; } }
        private static Color color = Color.FromArgb(255, 0, 0);
        PointF[] starshape = new PointF[] { new PointF(0, 0), new PointF(25, 12), new PointF(50, 0), new PointF(37, 25), new PointF(50, 50), new PointF(25, 37), new PointF(0, 50), new PointF(12, 25) };

        public SFXEnemySpawnPoint(int idx, float x, float y, IMEPackage p, PathingGraphEditor grapheditor)
            : base(idx, p, grapheditor)
        {
            string s = export.ObjectName;

            // = getType(s);
            float w = 50;
            float h = 50;
            shape = PPath.CreatePolygon(starshape);
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_JumpNode : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }

    public class SFXNav_TurretPoint : ActorNode
    {
        public VarTypes type { get; set; }
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }
    }
}