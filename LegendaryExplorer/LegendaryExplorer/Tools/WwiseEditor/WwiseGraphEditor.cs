using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.WwiseEditor;
using Piccolo;
using Piccolo.Event;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Creates a simple graph control with some random nodes and connected edges.
    /// An event handler allows users to drag nodes around, keeping the edges connected.
    /// </summary>
    public sealed class WwiseGraphEditor : PCanvas
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private Container components;

        private readonly ZoomController zoomController;

        private const int DEFAULT_WIDTH = 1;
        private const int DEFAULT_HEIGHT = 1;

        public bool updating;

        /// <summary>
        /// Empty Constructor is necessary so that this control can be used as an applet.
        /// </summary>
        public WwiseGraphEditor() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) { }
        public PLayer nodeLayer;
        public PLayer edgeLayer;
        public PLayer backLayer;
        public WwiseGraphEditor(int width, int height)
        {
            InitializeComponent();
            this.Size = new Size(width, height);
            nodeLayer = this.Layer;
            edgeLayer = new PLayer();
            Root.AddChild(edgeLayer);
            this.Camera.AddLayer(0, edgeLayer);
            backLayer = new PLayer();
            Root.AddChild(backLayer);
            backLayer.MoveToBack();
            this.Camera.AddLayer(1, backLayer);
            dragHandler = new NodeDragHandler();
            nodeLayer.AddInputEventListener(dragHandler);
            zoomController = new ZoomController(this);
        }

        public void AllowDragging()
        {
            nodeLayer.RemoveInputEventListener(dragHandler);
            nodeLayer.AddInputEventListener(dragHandler); 
        }

        public void DisableDragging()
        {
            nodeLayer.RemoveInputEventListener(dragHandler);
        }

        public void AddBack(PNode p)
        {
            backLayer.AddChild(p);
        }

        public void AddEdge(WwiseEdEdge p)
        {
            edgeLayer.AddChild(p);
            UpdateEdge(p);
        }

        public void AddNode(PNode p)
        {
            nodeLayer.AddChild(p);
        }

        public static void UpdateEdge(WwiseEdEdge edge)
        {
            // Note that the node's "FullBounds" must be used (instead of just the "Bound") 
            // because the nodes have non-identity transforms which must be included when
            // determining their position.

            PNode node1 = edge.start;
            PNode node2 = edge.end;
            PointF start = node1.GlobalBounds.Location;
            PointF end = node2.GlobalBounds.Location;
            float h1x, h1y, h2x, h2y;
            if (edge is VarEdge)
            {
                start.X += node1.GlobalBounds.Width * 0.5f;
                start.Y += node1.GlobalBounds.Height;
                h1x = h2x = 0;
                h1y = end.Y > start.Y ? 200 * MathF.Log10((end.Y - start.Y) / 200 + 1) : 200 * MathF.Log10((start.Y - end.Y) / 100 + 1);
                if (h1y < 15)
                {
                    h1y = 15;
                }

                end.X += node2.GlobalBounds.Width / 2;

                h2y = 0;
                end.Y += node2.GlobalBounds.Height / 2;
            }
            else
            {
                start.X += node1.GlobalBounds.Width;
                start.Y += node1.GlobalBounds.Height * 0.5f;
                end.Y += node2.GlobalBounds.Height * 0.5f;
                h1x = h2x = end.X > start.X ? 200 * MathF.Log10((end.X - start.X) / 200 + 1) : 200 * MathF.Log10((start.X - end.X) / 100 + 1);
                if (h1x < 15)
                {
                    h1x = h2x = 15;
                }
                h1y = h2y = 0;
            }

            edge.Reset();
            edge.AddBezier(start.X, start.Y, start.X + h1x, start.Y + h1y, end.X - h2x, end.Y - h2y, end.X, end.Y);
        }

        //private PNode boxSelectOriginNode;
        //private PNode boxSelectExtentNode;
        //private readonly PDragEventHandler boxSelectDragEventHandler = new BoxSelectDragHandler();
        //public void StartBoxSelection(PInputEventArgs e)
        //{
        //    var (x, y) = e.Position;
        //    boxSelectOriginNode = new PNode
        //    {
        //        X = x,
        //        Y = y
        //    };
        //    boxSelectExtentNode = new PNode
        //    {
        //        X = x,
        //        Y = y
        //    };
        //    nodeLayer.AddChild(boxSelectOriginNode);
        //    nodeLayer.AddChild(boxSelectExtentNode);
        //    boxSelectExtentNode.AddInputEventListener(boxSelectDragEventHandler);
        //    e.PickedNode = boxSelectExtentNode;
        //    boxSelectExtentNode.OnMouseDown(e);
        //}

        //public List<PNode> EndBoxSelection()
        //{
        //    if (boxSelectExtentNode != null)
        //    {
        //        var (x1, y1) = boxSelectOriginNode.GlobalFullBounds;
        //        var (x2, y2) = boxSelectExtentNode.GlobalFullBounds;
        //        boxSelectExtentNode.RemoveInputEventListener(boxSelectDragEventHandler);
        //        nodeLayer.RemoveChild(boxSelectOriginNode);
        //        nodeLayer.RemoveChild(boxSelectExtentNode);
        //        boxSelectExtentNode = boxSelectOriginNode = null;

        //        var size = new SizeF(x2.Difference(x1), y2.Difference(y1));
        //        var origin = new PointF(Math.Min(x1, x2), Math.Min(y1, y2));
        //        return nodeLayer.FindIntersectingNodes(new RectangleF(origin, size));
        //    }

        //    return new List<PNode>();
        //}
        //public class BoxSelectDragHandler : PDragEventHandler
        //{
        //    public override bool DoesAcceptEvent(PInputEventArgs e)
        //    {
        //        return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave);
        //    }

        //    protected override void OnStartDrag(object sender, PInputEventArgs e)
        //    {

        //        base.OnStartDrag(sender, e);
        //        e.Handled = true;
        //    }

        //    protected override void OnDrag(object sender, PInputEventArgs e)
        //    {
        //        Debug.WriteLine($"dragging: {e.Position}");
        //        base.OnDrag(sender, e);
        //        if (false &&!e.Handled)
        //        {
        //        }
        //    }

        //    protected override void OnEndDrag(object sender, PInputEventArgs e)
        //    {
        //        ((PNode)sender).SetOffset(e.Position);
        //    }
        //}

        private readonly NodeDragHandler dragHandler;
        /// <summary>
        /// Simple event handler which applies the following actions to every node it is called on:
        ///   * Drag the node, and associated edges on mousedrag
        /// with a list of associated nodes.
        /// </summary>
        public class NodeDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave);
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                base.OnStartDrag(sender, e);
                e.Handled = true;
                e.PickedNode.MoveToFront();
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                if (!e.Handled)
                {
                    var edgesToUpdate = new HashSet<WwiseEdEdge>();
                    base.OnDrag(sender, e);
                    if (e.PickedNode is WwiseHircObjNode sObj)
                    {
                        foreach (WwiseEdEdge edge in sObj.Edges)
                        {
                            edgesToUpdate.Add(edge);
                        }
                    }

                    if (e.Canvas is WwiseGraphEditor g)
                    {
                        foreach (PNode node in g.nodeLayer)
                        {
                            if (node is WwiseHircObjNode { IsSelected: true } obj && obj != e.PickedNode)
                            {
                                SizeF s = e.GetDeltaRelativeTo(obj);
                                s = obj.LocalToParent(s);
                                obj.OffsetBy(s.Width, s.Height);
                                foreach (WwiseEdEdge edge in obj.Edges)
                                {
                                    edgesToUpdate.Add(edge);
                                }
                            }
                        }
                    }

                    foreach (WwiseEdEdge edge in edgesToUpdate)
                    {
                        UpdateEdge(edge);
                    }
                }
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                nodeLayer.RemoveAllChildren();
                edgeLayer.RemoveAllChildren();
                backLayer.RemoveAllChildren();
                zoomController.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        public void InitializeComponent()
        {
            components = new Container();
        }
        #endregion
        
        private int updatingCount = 0;
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!updating)
            {
                base.OnPaint(e);
            }
            else
            {
                const string msg = "Updating, please wait............";
                e.Graphics.DrawString(msg.Substring(0, updatingCount + 21), SystemFonts.DefaultFont, Brushes.Black, Width - Width / 2, Height - Height / 2);
                updatingCount++;
                if (updatingCount + 21 > msg.Length)
                {
                    updatingCount = 0;
                }
            }
        }
    }
}