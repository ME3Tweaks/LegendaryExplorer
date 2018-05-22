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

namespace ME3Explorer.Pathfinding_Editor
{
    public abstract class PathfindingNodeMaster : PNode
    {
        public PPath shape;
        public IMEPackage pcc;
        public PathingGraphEditor g;
        public static ME1Explorer.TalkFiles talkfiles { get; set; }
        static Color commentColor = Color.FromArgb(74, 63, 190);
        static Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static Color boolColor = Color.FromArgb(215, 37, 33); //red
        static Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        public static Brush sfxCombatZoneBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
        public static Brush highlightedCoverSlotBrush = new SolidBrush(Color.FromArgb(219, 137,6));

        protected static Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static Brush actorNodeBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
        protected static Brush splineNodeBrush = new SolidBrush(Color.FromArgb(255, 60, 200));
        public static Brush pathfindingNodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Brush dynamicPathfindingNodeBrush = new SolidBrush(Color.FromArgb(46, 184, 25));
        protected static Brush dynamicPathnodefindingNodeBrush = new SolidBrush(Color.FromArgb(80, 184, 25));


        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink = false;
        public static bool draggingVarlink = false;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public int Index { get { return index; } }
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }

        protected int index;
        public IExportEntry export;
        protected Pen outlinePen;
        public SText comment;
        public List<IExportEntry> ReachSpecs = new List<IExportEntry>();

        public void Select()
        {
            shape.Pen = selectedPen;
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

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((PathfindingNodeMaster)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((PathfindingNodeMaster)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }

        //Empty implementation
        public virtual void CreateConnections(ref List<PathfindingNodeMaster> Objects)
        {

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
                    return retval;
                }
            }
            return "";
        }

    }
}
