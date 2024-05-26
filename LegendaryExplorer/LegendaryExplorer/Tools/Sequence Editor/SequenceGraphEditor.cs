using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LegendaryExplorer.Tools.SequenceObjects;
using Piccolo;
using Piccolo.Event;

namespace LegendaryExplorer.Tools.Sequence_Editor
{
    /// <summary>
    /// Creates a simple graph control with some random nodes and connected edges.
    /// An event handler allows users to drag nodes around, keeping the edges connected.
    /// </summary>
    public sealed class SequenceGraphEditor : PCanvas
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components;

        private readonly ZoomController zoomController;

        private const int DEFAULT_WIDTH = 1;
        private const int DEFAULT_HEIGHT = 1;

        /// <summary>
        /// Empty Constructor is necessary so that this control can be used as an applet.
        /// </summary>
        public SequenceGraphEditor() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) { }
        public readonly PLayer nodeLayer;
        public readonly PLayer edgeLayer;
        public readonly PLayer backLayer;
        public SequenceGraphEditor(int width, int height)
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

        public void addBack(PNode p)
        {
            backLayer.AddChild(p);
        }

        public void addEdge(SeqEdEdge p)
        {
            edgeLayer.AddChild(p);
            UpdateEdge(p);
        }

        public void addNode(PNode p)
        {
            nodeLayer.AddChild(p);
        }

        public static void UpdateEdge(SeqEdEdge edge)
        {
            // Note that the node's "FullBounds" must be used (instead of just the "Bound") 
            // because the nodes have non-identity transforms which must be included when
            // determining their position.

            PNode node1 = edge.Start;
            PNode node2 = edge.End;
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
                if (edge is EventEdge)
                {
                    h2y = h1y;
                }
                else
                {
                    h2y = 0;
                    end.Y += node2.GlobalBounds.Height / 2;
                }
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
        private class NodeDragHandler : PDragEventHandler
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
                    var edgesToUpdate = new HashSet<SeqEdEdge>();
                    base.OnDrag(sender, e);
                    if (e.PickedNode is SObj sObj)
                    {
                        foreach (SeqEdEdge edge in sObj.Edges)
                        {
                            edgesToUpdate.Add(edge);
                        }
                    }

                    if (e.Canvas is SequenceGraphEditor g)
                    {
                        foreach (PNode node in g.nodeLayer)
                        {
                            if (node is SObj obj && obj.IsSelected && obj != e.PickedNode)
                            {
                                SizeF s = e.GetDeltaRelativeTo(obj);
                                s = obj.LocalToParent(s);
                                obj.OffsetBy(s.Width, s.Height);
                                foreach (SeqEdEdge edge in obj.Edges)
                                {
                                    edgesToUpdate.Add(edge);
                                }
                            }
                        }
                    }

                    foreach (SeqEdEdge edge in edgesToUpdate)
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
            components = new System.ComponentModel.Container();
        }
        #endregion
    }

    public class ZoomController : IDisposable
    {
        private PCamera camera;
        private PCanvas graphEditor;

        public ZoomController(PCanvas graphEditor)
        {
            this.graphEditor = graphEditor;
            this.camera = graphEditor.Camera;
            camera.Canvas.ZoomEventHandler = null;
            camera.MouseWheel += OnMouseWheel;
            graphEditor.KeyDown += OnKeyDown;
        }

        public void Dispose()
        {
            //Remove event handlers for memory cleanup
            if (graphEditor != null)
            {
                graphEditor.KeyDown -= OnKeyDown;
                graphEditor.Camera.MouseWheel -= OnMouseWheel;
            }
            graphEditor = null;
            camera = null;
        }

        public void OnKeyDown(object o, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.OemMinus:
                        ScaleView(0.8f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                        break;
                    case Keys.Oemplus:
                        ScaleView(1.2f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                        break;
                }
            }
        }

        public void OnMouseWheel(object o, PInputEventArgs ea)
        {
            ScaleView(1.0f + (0.001f * ea.WheelDelta), ea.Position);
        }

        private void ScaleView(float scaleDelta, PointF p)
        {
            const float MIN_SCALE = .005f;
            const float MAX_SCALE = 15;
            float currentScale = camera.ViewScale;
            float newScale = currentScale * scaleDelta;
            if (newScale < MIN_SCALE)
            {
                camera.ViewScale = MIN_SCALE;
                return;
            }
            if ((MAX_SCALE > 0) && (newScale > MAX_SCALE))
            {
                camera.ViewScale = MAX_SCALE;
                return;
            }
            camera.ScaleViewBy(scaleDelta, p.X, p.Y);
        }
    }
}