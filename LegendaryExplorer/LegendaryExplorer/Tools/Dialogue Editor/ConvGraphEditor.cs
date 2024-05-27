using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Piccolo;
using Piccolo.Event;

namespace LegendaryExplorer.DialogueEditor
{
    /// <summary>
    /// Creates a simple graph control with some random nodes and connected edges.
    /// An event handler allows users to drag nodes around, keeping the edges connected.
    /// </summary>
    public class ConvGraphEditor : PCanvas
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components;

        private readonly ZoomController zoomController;

        private const int DEFAULT_WIDTH = 1;
        private const int DEFAULT_HEIGHT = 1;

        public bool updating;

        /// <summary>
        /// Empty Constructor is necessary so that this control can be used as an applet.
        /// </summary>
        public ConvGraphEditor() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) { }
        public PLayer nodeLayer;
        public PLayer edgeLayer;
        public PLayer backLayer;
        public ConvGraphEditor(int width, int height)
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

        public void addEdge(DiagEdEdge p)
        {
            edgeLayer.AddChild(p);
            UpdateEdge(p);
        }

        public void addNode(PNode p)
        {
            nodeLayer.AddChild(p);
        }

        public static void UpdateEdge(DiagEdEdge edge)
        {
            // Note that the node's "FullBounds" must be used (instead of just the "Bound") 
            // because the nodes have non-identity transforms which must be included when
            // determining their position.

            PNode node1 = edge.start;
            PNode node2 = edge.end;
            PointF start = node1.GlobalBounds.Location;
            PointF end = node2.GlobalBounds.Location;
            float h1x, h1y, h2x, h2y;

            start.X += node1.GlobalBounds.Width;
            start.Y += node1.GlobalBounds.Height * 0.5f;
            end.Y += node2.GlobalBounds.Height * 0.5f;
            h1x = h2x = end.X > start.X ? 200 * MathF.Log10((end.X - start.X) / 200 + 1) : 200 * MathF.Log10((start.X - end.X) / 100 + 1);
            if (h1x < 15)
            {
                h1x = h2x = 15;
            }
            h1y = h2y = 0;

            edge.Reset();
            edge.AddBezier(start.X, start.Y, start.X + h1x, start.Y + h1y, end.X - h2x, end.Y - h2y, end.X, end.Y);
        }
       

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
                    var edgesToUpdate = new HashSet<DiagEdEdge>();
                    base.OnDrag(sender, e);
                    if (e.PickedNode is DObj DObj)
                    {
                        foreach (DiagEdEdge edge in DObj.Edges)
                        {
                            edgesToUpdate.Add(edge);
                        }
                    }

                    if (e.Canvas is ConvGraphEditor g)
                    {
                        foreach (PNode node in g.nodeLayer)
                        {
                            if (node is DObj { IsSelected: true } obj && obj != e.PickedNode)
                            {
                                SizeF s = e.GetDeltaRelativeTo(obj);
                                s = obj.LocalToParent(s);
                                obj.OffsetBy(s.Width, s.Height);
                                foreach (DiagEdEdge edge in obj.Edges)
                                {
                                    edgesToUpdate.Add(edge);
                                }
                            }
                        }
                    }

                    foreach (DiagEdEdge edge in edgesToUpdate)
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
                zoomController?.Dispose();
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

    public class ZoomController : IDisposable
    {
        public const float MIN_SCALE = .005f;
        public const float MAX_SCALE = 15;
        private PCamera camera;
        private ConvGraphEditor ConvGraphEditor;

        public ZoomController(ConvGraphEditor convGraphEditor)
        {
            ConvGraphEditor = convGraphEditor;
            camera = convGraphEditor.Camera;
            camera.Canvas.ZoomEventHandler = null;
            camera.MouseWheel += OnMouseWheel;
            convGraphEditor.KeyDown += OnKeyDown;
        }

        public void Dispose()
        {
            //Remove event handlers for memory cleanup
            if (ConvGraphEditor != null)
            {
                ConvGraphEditor.KeyDown -= OnKeyDown;
                ConvGraphEditor.Camera.MouseWheel -= OnMouseWheel;
            }
            ConvGraphEditor = null;
            camera = null;
        }

        public void OnKeyDown(object o, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.OemMinus:
                        scaleView(0.8f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                        break;
                    case Keys.Oemplus:
                        scaleView(1.2f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                        break;
                }
            }
        }

        public void OnMouseWheel(object o, PInputEventArgs ea)
        {
            scaleView(1.0f + (0.001f * ea.WheelDelta), ea.Position);
        }

        private void scaleView(float scaleDelta, PointF p)
        {
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