using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UMD.HCIL.PathingGraphEditor;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using static ME1Explorer.Unreal.Classes.TalkFile;

namespace ME3Explorer.HuffmanTreeViewer
{
    public partial class HuffmanTreeViewerForm : Form
    {
        private HuffmanZoomController zoomController;
        private List<HuffmanNodeGraphObject> Objects;

        public HuffmanTreeViewerForm(IExportEntry tlkEntry)
        {
            var tlkFile = new ME1Explorer.Unreal.Classes.TalkFile(tlkEntry); // Setup object as TalkFile


            InitializeComponent();
            zoomController = new HuffmanZoomController(graphEditor1);

            Objects = new List<HuffmanNodeGraphObject>();
            HuffmanNodeGraphObject root = new HuffmanNodeGraphObject(tlkFile.nodes, 0, graphEditor1, "");
            //LayoutTree();
            //CreateConnections();
            graphEditor1.addNode(root);
            foreach (PPath edge in graphEditor1.edgeLayer)
            {
                HuffmanGraph.UpdateEdgeStraight(edge);
            }

        }

        private void LayoutTree()
        {
            int numleafs = Objects.Count(x => x.node.LeftNodeID == x.node.RightNodeID);
            LayoutSubnode(Objects[0].node.LeftNodeID, -55, 0);
            LayoutSubnode(Objects[0].node.RightNodeID, 55, 0);
        }

        private void LayoutSubnode(int nodeID, int xOffset, int y)
        {
            var nodegraphobj = Objects[nodeID];
            //nodegraphobj.X = xOffset;
            //nodegraphobj.Y = y;
            nodegraphobj.TranslateBy(xOffset, y);
            var parentalWidthSplit = Math.Abs(xOffset);
            if (nodegraphobj.node.LeftNodeID >= 0)
            {
                LayoutSubnode(nodegraphobj.node.LeftNodeID, xOffset + ((parentalWidthSplit / 2) * -1), y + 70);
            }
            if (nodegraphobj.node.RightNodeID >= 0)
            {
                LayoutSubnode(nodegraphobj.node.RightNodeID, xOffset + (parentalWidthSplit / 2), y + 70);
            }
        }


        public class HuffmanZoomController : IDisposable
        {
            public static float MIN_SCALE = .005f;
            public static float MAX_SCALE = 15;
            HuffmanGraph graphEditor;
            PCamera camera;

            public HuffmanZoomController(HuffmanGraph graphEditor)
            {
                this.graphEditor = graphEditor;
                this.camera = graphEditor.Camera;
                camera.ViewScale = 0.5f;
                camera.MouseWheel += OnMouseWheel;
                graphEditor.KeyDown += OnKeyDown;
            }

            public void Dispose()
            {
                //Remove event handlers for memory cleanup
                camera.Canvas.ZoomEventHandler = null;
                camera.MouseWheel -= OnMouseWheel;
                graphEditor.KeyDown -= OnKeyDown;
                camera = null;
                graphEditor = null;
            }

            public void OnKeyDown(object o, KeyEventArgs e)
            {
                if (e.Control)
                {
                    if (e.KeyCode == Keys.OemMinus)
                    {
                        scaleView(0.8f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                    }
                    else if (e.KeyCode == Keys.Oemplus)
                    {
                        scaleView(1.2f, new PointF(camera.ViewBounds.X + (camera.ViewBounds.Height / 2), camera.ViewBounds.Y + (camera.ViewBounds.Width / 2)));
                    }
                }
            }

            public void OnMouseWheel(object o, PInputEventArgs ea)
            {
                scaleView(1.0f + (0.001f * ea.WheelDelta), ea.Position);
            }

            public void scaleView(float scaleDelta, PointF p)
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
}
