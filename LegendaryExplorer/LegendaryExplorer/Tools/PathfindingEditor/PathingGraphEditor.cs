using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;
using Piccolo;
using Piccolo.Event;

namespace LegendaryExplorer.Tools.PathfindingEditor
{
    /// <summary>
    /// Creates a simple graph control with some random nodes and connected edges.
    /// An event handler allows users to drag nodes around, keeping the edges connected.
    /// </summary>
    public class PathingGraphEditor : PCanvas
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private Container components;

        private const int DEFAULT_WIDTH = 1;
        private const int DEFAULT_HEIGHT = 1;
        public bool showVolumeBrushes = true;

        public bool updating = false;

        /// <summary>
        /// Empty Constructor is necessary so that this control can be used as an applet.
        /// </summary>
        public PathingGraphEditor() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) { }
        public PLayer nodeLayer;
        public PLayer edgeLayer;
        public PLayer backLayer;
        public PathingGraphEditor(int width, int height)
        {
            InitializeComponent();
            Size = new Size(width, height);
            nodeLayer = this.Layer;
            edgeLayer = new PLayer();
            Root.AddChild(edgeLayer);
            Camera.AddLayer(0, edgeLayer);
            backLayer = new PLayer();
            Root.AddChild(backLayer);
            backLayer.MoveToBack();
            Camera.AddLayer(1, backLayer);
            dragHandler = new NodeDragHandler();
            nodeLayer.AddInputEventListener(dragHandler);
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

        public void addEdge(PathfindingEditorEdge p)
        {
            edgeLayer.AddChild(p);
            UpdateEdgeStraight(p);
        }

        public void addEdgeBezier(PathfindingEditorEdge p)
        {
            edgeLayer.AddChild(p);
            UpdateEdgeBezier(p);
        }

        public void addNode(PNode p)
        {
            nodeLayer.AddChild(p);
        }

        public static void UpdateEdgeBezier(PathfindingEditorEdge edge)
        {
            // Note that the node's "FullBounds" must be used (instead of just the "Bound") 
            // because the nodes have non-identity transforms which must be included when
            // determining their position.
            ArrayList nodes = (ArrayList)edge.Tag;
            PNode node1 = (PNode)nodes[0];
            PNode node2 = (PNode)nodes[1];
            PointF start = node1.GlobalBounds.Location;
            PointF end = node2.GlobalBounds.Location;
            float h1x, h1y, h2x;
            if (nodes.Count > 2 && (int)nodes[2] == -1) //var link
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
                h1y = 0;
            }

            edge.Reset();
            edge.AddBezier(start.X, start.Y, start.X + h1x, start.Y + h1y, end.X - h2x, end.Y, end.X, end.Y);
        }

        /// <summary>
        /// Creates straight edged lines, from the center of the node.
        /// </summary>
        /// <param name="edge"></param>
        public static void UpdateEdgeStraight(PathfindingEditorEdge edge)
        {
            // Note that the node's "FullBounds" must be used (instead of just the "Bound") 
            // because the nodes have non-identity transforms which must be included when
            // determining their position.

            PNode node1 = edge.EndPoints[0];
            PNode node2 = edge.EndPoints[1];

            PointF start = node1.GlobalBounds.Location;

            edge.Reset();
            if (node1 is SplinePointControlNode)
            {
                edge.AddLine(start.X + node1.GlobalBounds.Width * 0.5f, start.Y + node1.GlobalBounds.Height * 0.5f, node2.OffsetX, node2.OffsetY);
            }
            else
            {
                PointF end = node2.GlobalBounds.Location;
                edge.AddLine(start.X + node1.GlobalBounds.Width * 0.5f, start.Y + node1.GlobalBounds.Height * 0.5f, end.X + node2.GlobalBounds.Width * 0.5f, end.Y + node2.GlobalBounds.Height * 0.5f);
            }
        }

        public void ScaleViewTo(float scale)
        {
            Camera.ViewScale = scale;
        }

        /// <summary>
        /// Simple event handler which applies the following actions to every node it is called on:
        ///   * Drag the node, and associated edges on mousedrag
        /// It assumes that the node's Tag references an ArrayList with a list of associated
        /// edges where each edge is a PathfindingEditorEdge which each have a Tag that references an ArrayList
        /// with a list of associated nodes.
        /// </summary>
        public class NodeDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button == MouseButtons.Left || e.IsMouseEnterOrMouseLeave);
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                base.OnStartDrag(sender, e);
                e.Handled = true;
                e.PickedNode.MoveToFront();
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                if (!e.Handled && e.Button == MouseButtons.Left)
                {
                    base.OnDrag(sender, e);
                    foreach (PNode node in e.PickedNode.AllNodes)
                    {
                        if (node is PathfindingNode pn)
                        {
                            foreach (PathfindingEditorEdge edge in pn.Edges)
                            {
                                UpdateEdgeStraight(edge);
                            }
                        }
                        else if (node.Tag is ArrayList edges)
                        {
                            foreach (PathfindingEditorEdge edge in edges)
                            {
                                UpdateEdgeStraight(edge);
                            }
                        }
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
            }
            nodeLayer.RemoveAllChildren();
            edgeLayer.RemoveAllChildren();
            backLayer.RemoveAllChildren();
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
        public bool showVolume_BioTriggerVolume = false;
        public bool showVolume_BioTriggerStream = false;
        public bool showVolume_DynamicBlockingVolume = false;
        public bool showVolume_WwiseAudioVolume = false;
        public bool showVolume_BlockingVolume = false;
        public bool showVolume_SFXCombatZones = false;
        public bool showVolume_SFXBlockingVolume_Ledge = false;
        private readonly NodeDragHandler dragHandler;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!updating)
            {
                base.OnPaint(e);
            }
            else
            {
                const string msg = "Updating, please wait............"; //without multithread this does nothing.
                e.Graphics.DrawString(msg.Substring(0, updatingCount + 21), SystemFonts.DefaultFont, Brushes.Black, Width - Width / 2, Height - Height / 2);
                updatingCount++;
                if (updatingCount + 21 > msg.Length)
                {
                    updatingCount = 0;
                }
            }
        }

        public void DebugEventHandlers()
        {
            EventHandlerList events = (EventHandlerList)typeof(Component)
                           .GetField("events", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField)
                           .GetValue(this);

            object current = events.GetType()
                   .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField)[0]
                   .GetValue(events);

            List<Delegate> delegates = new List<Delegate>();
            while (current != null)
            {
                delegates.Add((Delegate)GetField(current, "handler"));
                current = GetField(current, "next");
            }
        }

        public static object GetField(object listItem, string fieldName)
        {
            return listItem.GetType()
               .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField)
               .GetValue(listItem);
        }
    }
}