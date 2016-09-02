using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.SequenceObjects;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;

namespace ME3Explorer.DialogEditor
{
    public abstract class DlgObj : PNode
    {
        protected static Brush outputBrush = new SolidBrush(Color.Black);
        protected static Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Color titleBrush = Color.FromArgb(255, 255, 255);

        public struct OutputLink
        {
            public PPath node;
            public Tuple<char, int> Link;
            public string Desc;
        }

        public abstract Tuple<char, int> Id { get; }
        public ME3BioConversation conv;
        public DialogVis dv;
        public List<OutputLink> Outlinks;

        protected PPath outLinkBox;
        protected int index;
        protected Pen outlinePen;

#pragma warning disable RECS0154 // Parameter is never used
        protected DlgObj(int idx, float x, float y, ME3BioConversation bc, DialogVis dialogvis)
#pragma warning restore RECS0154 // Parameter is never used
            : base()
        {
            conv = bc;
            dv = dialogvis;
            index = idx;
            this.Pickable = true;
        }

        public void CreateConnections(ref List<DlgObj> objects)
        {
            for (int i = 0; i < Outlinks.Count; i++)
            {
                for (int j = 0; j < objects.Count; j++)
                        if (objects[j].Id == Outlinks[i].Link)
                        {
                            PPath p1 = Outlinks[i].node;
                            DlgObj p2 = (DlgObj)dv.nodeLayer[j];
                            PPath edge = new PPath();
                            if (p1.Tag == null)
                                p1.Tag = new ArrayList();
                            if (p2.Tag == null)
                                p2.Tag = new ArrayList();
                            ((ArrayList)p1.Tag).Add(edge);
                            ((ArrayList)p2.Tag).Add(edge);
                            edge.Tag = new ArrayList();
                            ((ArrayList)edge.Tag).Add(p1);
                            ((ArrayList)edge.Tag).Add(p2);
                            dv.addEdge(edge);
                        }
            }
        }
    }

    public class DlgStart : DlgObj
    {
        public override Tuple<char, int> Id { get { return new Tuple<char, int>('s', index); } }

        protected PPath box;

        public DlgStart(int idx, float x, float y, ME3BioConversation bc, DialogVis dialogvis)
            : base(idx, x, y, bc, dialogvis)
        {
            outlinePen = new Pen(Color.Black);
            GetOutputLinks();
            SText title = new SText("Start: " + index, titleBrush);
            title.TextAlignment = StringAlignment.Center;
            title.X = 5;
            title.Y = 3;
            title.Pickable = false;
            box = PPath.CreateRectangle(0, 0, title.Width + 10, title.Height + 5);
            box.Pen = outlinePen;
            box.Brush = nodeBrush;
            box.AddChild(title);
            this.AddChild(box);
            this.TranslateBy(x, y);
        }

        protected void GetOutputLinks()
        {
            Outlinks = new List<OutputLink>();

            OutputLink l = new OutputLink();
            l.Link = new Tuple<char, int>('s', conv.StartingList[index]);
            l.Desc = Convert.ToString(l.Link);
            l.node = PPath.CreateRectangle(-4, 0, 8, 10);
            //l.node.Brush = outputBrush;
            l.node.Pickable = false;
            Outlinks.Add(l);
        }
    }

    public abstract class DlgLine : DlgObj
    {
        public DlgLine(int idx, float x, float y, ME3BioConversation bc, DialogVis dialogvis)
            : base(idx, x, y, bc, dialogvis)
        {

        }
    }

    public class DlgEntry : DlgObj
    {
        public override Tuple<char, int> Id { get { return new Tuple<char, int>('e', index); } }

        public DlgEntry(int idx, float x, float y, ME3BioConversation bc, DialogVis dialogvis)
            : base(idx, x, y, bc, dialogvis)
        {
            
        }
    }

    public class DlgReply : DlgObj
    {
        public override Tuple<char, int> Id { get { return new Tuple<char, int>('r', index); } }

        public DlgReply(int idx, float x, float y, ME3BioConversation bc, DialogVis dialogvis)
            : base(idx, x, y, bc, dialogvis)
        {

        }
    }
}
