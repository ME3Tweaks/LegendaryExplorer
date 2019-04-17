using ME1Explorer;
using ME3Explorer.SequenceObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UMD.HCIL.GraphEditor;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;

namespace ME3Explorer.HuffmanTreeViewer
{
    public class HuffmanNodeGraphObject : PNode
    {
        public PPath shape;
        static Color outlineColor = Color.AntiqueWhite;
        static Color commentColor = Color.FromArgb(74, 63, 190);
        public static Brush pathfindingNodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        protected Pen outlinePen = new Pen(Color.FromArgb(255, 255, 100));

        //public int Index { get { return index; } }
        public SText comment;
        private HuffmanGraph grapheditor;
        public ME1Explorer.Unreal.Classes.TalkFile.HuffmanNode node;

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

        public HuffmanNodeGraphObject(ME1Explorer.Unreal.Classes.TalkFile.HuffmanNode node, HuffmanGraph grapheditor)
        {
            this.grapheditor = grapheditor;
            this.node = node;
            float w = 50;
            float h = 50;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(outlineColor);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText("");
            if (node.LeftNodeID == node.RightNodeID)
            {
                val.Text = node.data == '\0' ? "NULL TERM" : node.data.ToString();
            }
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
        }

        public void CreateConnections(ref List<HuffmanNodeGraphObject> Objects)
        {
            if (node.LeftNodeID == node.RightNodeID)
            {
                return; //Leaf node
            }

            if (node.LeftNodeID >= 0)
            {
                HuffmanNodeGraphObject othernode = Objects[node.LeftNodeID];
                PPath edge = new PPath();
                //((ArrayList)Tag).Add(edge);
                //((ArrayList)othernode.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(this);
                ((ArrayList)edge.Tag).Add(othernode);
                grapheditor.edgeLayer.AddChild(edge);
            }
            if (node.RightNodeID >= 0)
            {
                HuffmanNodeGraphObject othernode = Objects[node.RightNodeID];
                PPath edge = new PPath();
                //((ArrayList)Tag).Add(edge);
                //((ArrayList)othernode.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(this);
                ((ArrayList)edge.Tag).Add(othernode);
                grapheditor.edgeLayer.AddChild(edge);
            }
        }

    }
}