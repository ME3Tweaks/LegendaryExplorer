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
        HuffmanNodeGraphObject left, right;

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

        public HuffmanNodeGraphObject(List<ME1Explorer.Unreal.Classes.TalkFile.HuffmanNode> tree, int huffmanIndex, HuffmanGraph grapheditor, string currentCode)
        {
            Tag = new ArrayList(); //outbound reachspec edges.

            this.grapheditor = grapheditor;
            this.node = tree[huffmanIndex];
            float w = 50;
            float h = 50;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(outlineColor);
            shape.Pen = outlinePen;
            shape.Brush = pathfindingNodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            SText val = new SText(huffmanIndex.ToString());
            if (node.LeftNodeID == node.RightNodeID)
            {
                string text = node.data == '\0' ? "NULL TERM" : node.data.ToString(); ;
                text += "\n" + currentCode;
                val.Text += "\n" + text;
            }
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            //Add children
            if (node.LeftNodeID >= 0)
            {
                left = new HuffmanNodeGraphObject(tree, node.LeftNodeID, grapheditor, currentCode + 0);
                left.TranslateBy(left.GetHeight() * -70, 110);
                AddChild(left);
            }
            if (node.RightNodeID >= 0)
            {
                right = new HuffmanNodeGraphObject(tree, node.RightNodeID, grapheditor, currentCode + 1);
                right.TranslateBy(right.GetHeight() * 70, 110);
                AddChild(right);
            }

            if (left != null)
            {
                PPath edge = new PPath();
                edge.Pen = new Pen(Color.Black);
                ((ArrayList)Tag).Add(edge);
                ((ArrayList)left.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(this);
                ((ArrayList)edge.Tag).Add(left);
                grapheditor.edgeLayer.AddChild(edge);
            }

            if (right != null)
            {
                PPath edge = new PPath();
                edge.Pen = new Pen(Color.Black);
                ((ArrayList)Tag).Add(edge);
                ((ArrayList)right.Tag).Add(edge);
                edge.Tag = new ArrayList();
                ((ArrayList)edge.Tag).Add(this);
                ((ArrayList)edge.Tag).Add(right);
                grapheditor.edgeLayer.AddChild(edge);
            }
        }

        public int GetHeight()
        {
            if (left == null && right == null)
            {
                return 1;
            }
            return 1 + Math.Max(left.GetHeight(), right.GetHeight());
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