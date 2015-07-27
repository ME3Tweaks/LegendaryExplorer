using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.GraphEditor {
	/// <summary>
	/// Creates a simple graph control with some random nodes and connected edges.
	/// An event handler allows users to drag nodes around, keeping the edges connected.
	/// </summary>
	public class GraphEditor : PCanvas {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private static int DEFAULT_WIDTH = 1;
		private static int DEFAULT_HEIGHT = 1;

		/// <summary>
		/// Empty Constructor is necessary so that this control can be used as an applet.
		/// </summary>
		public GraphEditor() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) {}
        public PLayer nodeLayer;
        public PLayer edgeLayer;
        public PLayer backLayer;
		public GraphEditor(int width, int height) {
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
			nodeLayer.AddInputEventListener(new NodeDragHandler());
		}

        public void addBack(PNode p)
        {
            backLayer.AddChild(p);
        }

        public void addEdge(PPath p)
        {
            edgeLayer.AddChild(p);
            UpdateEdge(p);
        }

        public void addNode(PNode p)
        {
            nodeLayer.AddChild(p);
        }

		public static void UpdateEdge(PPath edge) {
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
                h1y = end.Y > start.Y ? 200 * (float)Math.Log10((end.Y - start.Y) / 200 + 1) : 200 * (float)Math.Log10((start.Y - end.Y) / 100 + 1);
                end.X += node2.GlobalBounds.Width / 2;
                end.Y += node2.GlobalBounds.Height / 2;
            }
            else
            {
                start.X += node1.GlobalBounds.Width;
                start.Y += node1.GlobalBounds.Height * 0.5f;
                end.Y += node2.GlobalBounds.Height * 0.5f;
                h1x = h2x = end.X > start.X ? 200 * (float)Math.Log10((end.X - start.X)/200 + 1) : 200 * (float)Math.Log10((start.X - end.X) / 100 + 1);
                h1y = 0;
            }

            edge.Reset();
			//edge.AddLine(start.X, start.Y, end.X, end.Y);
            edge.AddBezier(start.X, start.Y, start.X + h1x, start.Y + h1y, end.X - h2x, end.Y, end.X, end.Y);
		}

        public void ScaleViewTo(float scale)
        {
            this.Camera.ViewScale = scale;
        }

		/// <summary>
		/// Simple event handler which applies the following actions to every node it is called on:
		///   * Drag the node, and associated edges on mousedrag
		/// It assumes that the node's Tag references an ArrayList with a list of associated
		/// edges where each edge is a PPath which each have a Tag that references an ArrayList
		/// with a list of associated nodes.
		/// </summary>
	    public class NodeDragHandler : PDragEventHandler {
			public override bool DoesAcceptEvent(PInputEventArgs e) {
				return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave);
			}

			protected override void OnStartDrag(object sender, PInputEventArgs e) {
				base.OnStartDrag(sender, e);
				e.Handled = true;
				e.PickedNode.MoveToFront();
			}

			protected override void OnDrag(object sender, PInputEventArgs e) {
                if (!e.Handled)
                {
                    base.OnDrag(sender, e);
                    foreach (PNode node in e.PickedNode.AllNodes)
                    {
                        ArrayList edges = (ArrayList)node.Tag;
                        if (edges != null)
                            foreach (PPath edge in edges)
                            {
                                GraphEditor.UpdateEdge(edge);
                            }
                    }
                }
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing ) {
			if( disposing ) {
				if( components != null )
					components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		public void InitializeComponent() {
			components = new System.ComponentModel.Container();
		}
		#endregion

		// Draw a border for when this control is used as an applet.
		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint (e);
		}
	}
}