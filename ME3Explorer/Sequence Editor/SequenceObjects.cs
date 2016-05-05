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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;

namespace ME3Explorer.SequenceObjects
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String };

    public abstract class SObj : PNode
    {
        public PCCObject pcc;
        public GraphEditor g;
        protected static Color commentColor = Color.FromArgb(74, 63, 190);
        protected static Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink = false;
        public static bool draggingVarlink = false;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public int Index { get { return index;} }
        public delegate void refreshDelegate();
        public refreshDelegate refreshView;
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }

        protected int index;
        protected Pen outlinePen;
        protected SText comment;

        public SObj(int idx, float x, float y, PCCObject p, GraphEditor grapheditor)
            : base()
        {
            pcc = p;
            g = grapheditor;
            index = idx;
            comment = new SText(GetComment(index), commentColor, false);
            comment.X = 0;
            comment.Y = 0 - comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
        }

        public SObj(int idx, float x, float y, PCCObject p)
            : base()
        {
            pcc = p;
            index = idx;
            comment = new SText(GetComment(index), commentColor, false);
            comment.X = 0;
            comment.Y = 0 - comment.Height;
            comment.Pickable = false;
            this.AddChild(comment);
            this.Pickable = true;
        }

        public virtual void CreateConnections(ref List<SObj> objects) { }
        public virtual void Layout(float x, float y) { }
        public virtual void Select() { }
        public virtual void Deselect() { }

        protected string GetComment(int index)
        {
            string res = "";
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
                if (pcc.getNameEntry(p[i].Name) == "m_aObjComment")
                {
                    f = i;
                    break;
                }
            if (f != -1)
            {
                byte[] buff2 = p[f].raw;
                BitConverter.IsLittleEndian = true;
                int count = BitConverter.ToInt32(buff2, 24);
                int pos = 28;
                for (int i = 0; i < count; i++)
                {
                    int len = BitConverter.ToInt32(buff2, pos);
                    pos += 4;
                    while (pos < buff2.Length && buff2[pos] != 0)
                    {
                        res += (char)buff2[pos];
                        pos += 2;
                    }
                    res += "\n";
                    pos += 2;
                }
            }
            return res;
        }

        protected Color getColor(VarTypes t)
        {
            switch (t)
            {
                case VarTypes.Int:
                    return Color.FromArgb(34, 218, 218);//cyan
                case VarTypes.Float:
                    return Color.FromArgb(23, 23, 213);//blue
                case VarTypes.Bool:
                    return Color.FromArgb(215, 37, 33); //red
                case VarTypes.Object:
                    return Color.FromArgb(219, 39, 217);//purple
                case VarTypes.MatineeData:
                    return Color.FromArgb(222, 123, 26);//orange
                default:
                    return Color.Black;
            }
        }

        protected VarTypes getType(string s)
        {
            if (s.Contains("InterpData"))
                return VarTypes.MatineeData;
            else if (s.Contains("Int"))
                return VarTypes.Int;
            else if (s.Contains("Bool"))
                return VarTypes.Bool;
            else if (s.Contains("Object") || s.Contains("Player"))
                return VarTypes.Object;
            else if (s.Contains("Float"))
                return VarTypes.Float;
            else if (s.Contains("StrRef"))
                return VarTypes.StrRef;
            else if (s.Contains("String"))
                return VarTypes.String;
            else
                return VarTypes.Extern;
        }

        
    }
    
    public class SVar : SObj
    {
        public VarTypes type {get; set;}
        private SText val;
        protected PPath shape;
        public string Value { get { return val.Text; } set { val.Text = value; } }

        public SVar(int idx, float x, float y, PCCObject p, GraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            string s = pcc.Exports[index].ObjectName;
            s = s.Replace("BioSeqVar_", "");
            s = s.Replace("SFXSeqVar_", "");
            s = s.Replace("SeqVar_", "");
            type = getType(s);
            float w = 60;
            float h = 60;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(getColor(type));
            shape.Pen = outlinePen;
            shape.Brush = nodeBrush;
            shape.Pickable = false;
            this.AddChild(shape);
            this.Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue());
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            this.AddChild(val);
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            foreach (PropertyReader.Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name) == "VarName" || pcc.getNameEntry(prop.Name) == "varName")
                {
                    SText VarName = new SText(pcc.getNameEntry(prop.Value.IntValue), Color.Red, false);
                    VarName.Pickable = false;
                    VarName.TextAlignment = StringAlignment.Center;
                    VarName.X = w / 2 - VarName.Width / 2;
                    VarName.Y = h;
                    this.AddChild(VarName);
                    break;
                }
            }
            this.TranslateBy(x, y);
            this.MouseEnter += new PInputEventHandler(OnMouseEnter);
            this.MouseLeave += new PInputEventHandler(OnMouseLeave);
        }

        public string GetValue()
        {
            try
            {
                List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
                PropertyReader.Property property;
                switch (type)
                {
                    case VarTypes.Int:
                        if (pcc.Exports[index].ObjectName == "BioSeqVar_StoryManagerInt")
                        {
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_sRefName"));
                            if (property != null)
                            {
                                if (comment.Text.Length > 0)
                                {
                                    comment.TranslateBy(0, -1 * comment.Height);
                                    comment.Text += property.Value.StringValue + "\n";
                                }
                                else
                                {
                                    comment.Text += property.Value.StringValue + "\n";
                                    comment.TranslateBy(0, -1 * comment.Height);
                                }
                            }
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_nIndex"));
                            if (property != null)
                                return "Plot Int\n#" + property.Value.IntValue;
                        }
                        property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("IntValue"));
                        if (property != null)
                            return property.Value.IntValue.ToString();
                        else
                            return "0";
                    case VarTypes.Float:
                        if (pcc.Exports[index].ObjectName == "BioSeqVar_StoryManagerFloat")
                        {
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_sRefName"));
                            if (property != null)
                            {
                                if (comment.Text.Length > 0)
                                {
                                    comment.TranslateBy(0, -1 * comment.Height);
                                    comment.Text += "\n" + property.Value.StringValue;
                                }
                                else
                                {
                                    comment.Text += property.Value.StringValue;
                                    comment.TranslateBy(0, -1 * comment.Height);
                                }
                            }
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_nIndex"));
                            if (property != null)
                                return "Plot Float\n#" + property.Value.IntValue;
                        }
                        foreach (PropertyReader.Property prop in p)
                        {
                            if (pcc.getNameEntry(prop.Name) == "FloatValue")
                            {
                                return BitConverter.ToSingle(prop.raw, 24).ToString();
                            }
                        }
                        return "0.00";
                    case VarTypes.Bool:
                        if (pcc.Exports[index].ObjectName == "BioSeqVar_StoryManagerBool")
                        {
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_sRefName"));
                            if (property != null)
                            {
                                if (comment.Text.Length > 0)
                                {
                                    comment.TranslateBy(0, -1 * comment.Height);
                                    comment.Text += "\n" + property.Value.StringValue;
                                }
                                else
                                {
                                    comment.Text += property.Value.StringValue;
                                    comment.TranslateBy(0, -1 * comment.Height);
                                }
                            }
                            property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("m_nIndex"));
                            if (property != null)
                                return "Plot Bool\n#" + property.Value.IntValue;
                        }
                        property = p.FirstOrDefault(i => pcc.getNameEntry(i.Name).Equals("bValue"));
                        if (property != null)
                            return property.Value.IntValue > 0 ? "True" : "False";
                        else
                            return "False";
                    case VarTypes.Object:
                        if (pcc.Exports[index].ObjectName == "SeqVar_Player")
                            return "Player";
                        foreach (PropertyReader.Property prop in p)
                        {
                            if (pcc.getNameEntry(prop.Name) == "m_sObjectTagToFind")
                            {
                                return pcc.getNameEntry(prop.Value.IntValue);
                            }
                            else if (pcc.getNameEntry(prop.Name) == "ObjValue")
                            {
                                if (prop.Value.IntValue > 0)
                                {
                                    return pcc.Exports[prop.Value.IntValue - 1].ObjectName;
                                }
                                else if (prop.Value.IntValue < 0)
                                {
                                    return pcc.Imports[-1 * prop.Value.IntValue - 1].ObjectName;
                                }
                            }
                        }
                        return "???";
                    case VarTypes.StrRef:
                        foreach (PropertyReader.Property prop in p)
                        {
                            if (pcc.getNameEntry(prop.Name) == "m_srValue" || pcc.getNameEntry(prop.Name) == "m_srStringID")
                            {
                                return TalkFiles.findDataById(prop.Value.IntValue);
                            }
                        }
                        return "???";
                    case VarTypes.String:
                        foreach (PropertyReader.Property prop in p)
                        {
                            if (pcc.getNameEntry(prop.Name) == "StrValue")
                            {
                                return prop.Value.StringValue;
                            }
                        }
                        return "???";
                    case VarTypes.Extern:
                        foreach (PropertyReader.Property prop in p)
                        {
                            if (pcc.getNameEntry(prop.Name) == "FindVarName")//Named Variable
                            {
                                return "< " + pcc.getNameEntry(prop.Value.IntValue) + " >";
                            }
                            else if (pcc.getNameEntry(prop.Name) == "NameValue")//SeqVar_Name
                            {
                                return pcc.getNameEntry(prop.Value.IntValue);
                            }
                            else if (pcc.getNameEntry(prop.Name) == "VariableLabel")//External
                            {
                                return "Extern:\n" + prop.Value.StringValue;
                            }
                        }
                        return "???";
                    case VarTypes.MatineeData:
                        return "#" + index + "\n" + "InterpData";
                    default:
                        return "???";
                }
            }
            catch (Exception)
            {
                return "???";
            }
        }

        public override void Select()
        {
            shape.Pen = selectedPen;
        }

        public override void Deselect()
        {
            shape.Pen = outlinePen;
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
                ((PPath)((SVar)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }
    }

    public abstract class SBox : SObj
    {
        protected static Color titleBrush = Color.FromArgb(255, 255, 128);
        protected static Brush outputBrush = new SolidBrush(Color.Black);
        protected static Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));

        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
            public List<int> offsets;
        }

        public struct VarLink
        {
            public PPath node;
            public List<int> Links;
            public string Desc;
            public VarTypes type;
            public bool writeable;
            public List<int> offsets;
        }

        public struct InputLink
        {
            public PPath node;
            public string Desc;
            public int index;
            public bool hasName;
        }

        protected PPath titleBox;
        protected PPath varLinkBox;
        protected PPath outLinkBox;
        public List<OutputLink> Outlinks;
        public List<VarLink> Varlinks;

        public SBox(int idx, float x, float y, PCCObject p, GraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            
        }

        public SBox(int idx, float x, float y, PCCObject p)
            : base(idx, x, y, p)
        {
            
        }
        public override void CreateConnections(ref List<SObj> objects) 
        {
            for (int i = 0; i < Outlinks.Count; i++)
            {
                for (int j = 0; j < objects.Count; j++)
                    for (int k = 0; k < Outlinks[i].Links.Count; k++ )
                        if (objects[j].Index == Outlinks[i].Links[k])
                        {
                            PPath p1 = Outlinks[i].node;
                            SObj p2 = (SObj)g.nodeLayer[j];
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
                            ((ArrayList)edge.Tag).Add(Outlinks[i].InputIndices[k]);
                            g.addEdge(edge);
                        }
            }
            for (int i = 0; i < Varlinks.Count; i++)
            {
                for (int j = 0; j < objects.Count(); j++)
                    for (int k = 0; k < Varlinks[i].Links.Count; k++ )
                        if (objects[j].Index == Varlinks[i].Links[k])
                        {
                            PPath p1 = Varlinks[i].node;
                            PNode p2 = g.nodeLayer[j];
                            PPath edge = new PPath();
                            if (p1.Tag == null)
                                p1.Tag = new ArrayList();
                            if (p2.Tag == null)
                                p2.Tag = new ArrayList();
                            ((ArrayList)p1.Tag).Add(edge);
                            ((ArrayList)p2.Tag).Add(edge);
                            edge.Tag = new ArrayList();
                            if(p2.ChildrenCount > 1)
                                edge.Pen = ((PPath)p2[1]).Pen;
                            ((ArrayList)edge.Tag).Add(p1);
                            ((ArrayList)edge.Tag).Add(p2);
                            ((ArrayList)edge.Tag).Add(-1);//is a var link
                            g.addEdge(edge);
                         }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            s = "#" + index.ToString() + " : " + s;
            SText title = new SText(s,titleBrush);
            title.TextAlignment = StringAlignment.Center;
            title.ConstrainWidthToTextWidth = false;
            if (title.Width + 20 > w)
            {
                w = title.Width + 20;
            }
            title.Width = w;
            title.X = 0;
            title.Y = 3;
            title.Pickable = false;
            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
            return w;
        }

        protected void GetVarLinks()
        {
            Varlinks = new List<VarLink>();
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
                if (pcc.getNameEntry(p[i].Name) == "VariableLinks")
                {
                    f = i;
                    break;
                }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                BitConverter.IsLittleEndian = true;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        pos += p2[i].raw.Length;
                        if (pcc.getNameEntry(p2[i].Name) == "LinkedVariables")
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            VarLink l = new VarLink();
                            l.Links = new List<int>();
                            l.offsets = new List<int>();
                            l.Desc = p2[i + 1].Value.StringValue;
                            l.Desc = l.Desc.Substring(0, l.Desc.Length - 1);
                            if (count2 != 0)
                            {
                                for (int k = 0; k < count2; k += 1)
                                {
                                    l.Links.Add(BitConverter.ToInt32(p2[i].raw, 28 + k*4) - 1);
                                    l.offsets.Add(pos + 28 + k*4 - p2[i].raw.Length);
                                }
                            }
                            else
                            {
                                l.Links.Add(-1);
                                l.offsets.Add(-1);
                            }
                            l.type = getType(pcc.getClassName(p2[i + 2].Value.IntValue));
                            l.writeable = p2[i + 7].Value.IntValue == 1;
                            if (l.writeable)
                            {//downward pointing triangle
                                l.node = PPath.CreatePolygon(new PointF[] { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) });
                                l.node.AddChild(PPath.CreatePolygon(new PointF[] { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) }));
                            }
                            else
                            {
                                l.node = PPath.CreateRectangle(-4, 0, 8, 10);
                                l.node.AddChild(PPath.CreateRectangle(-4, 0, 8, 10));
                            }
                            l.node.Brush = new SolidBrush(getColor(l.type));
                            l.node.Pen = new Pen(getColor(l.type));
                            l.node.Pickable = false;
                            l.node[0].Brush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
                            ((PPath)l.node[0]).Pen = l.node.Pen;
                            l.node[0].X = l.node.X;
                            l.node[0].Y = l.node.Y;
                            l.node[0].AddInputEventListener(new VarDragHandler());
                            Varlinks.Add(l);
                        }
                    }
                }
            }
        }

        protected void GetOutputLinks()
        {
            Outlinks = new List<OutputLink>();
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
                if (pcc.getNameEntry(p[i].Name) == "OutputLinks")
                {
                    f = i;
                    break;
                }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                BitConverter.IsLittleEndian = true;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        pos += p2[i].raw.Length;
                        string nm = pcc.getNameEntry(p2[i].Name);
                        if (nm == "Links")
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            if (count2 != 0)
                            {
                                OutputLink l = new OutputLink();
                                l.Links = new List<int>();
                                l.InputIndices = new List<int>();
                                l.offsets = new List<int>();
                                l.Desc = p2[i + 1].Value.StringValue;
                                if(l.Desc.Length > 0)
                                    l.Desc = l.Desc.Substring(0, l.Desc.Length - 1);
                                for (int k = 0; k < count2; k += 1)
                                {
                                    List<PropertyReader.Property> p3 = PropertyReader.ReadProp(pcc, p2[i].raw, 28 + k * 64);
                                    l.Links.Add(p3[0].Value.IntValue - 1);
                                    l.InputIndices.Add(p3[1].Value.IntValue);
                                    l.offsets.Add(pos + p3[0].offsetval - p2[i].raw.Length);
                                    if(OutputNumbers)
                                        l.Desc = l.Desc + (k > 0 ? "," : ": ") + "#" + (p3[0].Value.IntValue - 1);
                                }
                                l.node = PPath.CreateRectangle(0, -4, 10, 8);
                                l.node.Brush = outputBrush;
                                l.node.Pickable = false;
                                l.node.AddChild(PPath.CreateRectangle(0, -4, 10, 8));
                                l.node[0].Brush = new SolidBrush(Color.FromArgb(1,255,255,255));
                                l.node[0].X = l.node.X;
                                l.node[0].Y = l.node.Y;
                                l.node[0].AddInputEventListener(new OutputDragHandler());
                                Outlinks.Add(l);
                            }
                            else
                            {
                                OutputLink l = new OutputLink();
                                l.Links = new List<int>();
                                l.InputIndices = new List<int>();
                                l.offsets = new List<int>();
                                l.Desc = p2[i + 1].Value.StringValue;
                                l.Links.Add(-1);
                                l.InputIndices.Add(0);
                                l.Desc = l.Desc.Substring(0, l.Desc.Length - 1);
                                if (OutputNumbers)
                                    l.Desc = l.Desc + ": #-1";
                                l.offsets.Add(-1);
                                l.node = PPath.CreateRectangle(0, -4, 10, 8);
                                l.node.Brush = outputBrush;
                                l.node.Pickable = false;
                                l.node.AddChild(PPath.CreateRectangle(0, -4, 10, 8));
                                l.node[0].Brush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
                                l.node[0].X = l.node.X;
                                l.node[0].Y = l.node.Y;
                                l.node[0].AddInputEventListener(new OutputDragHandler());
                                Outlinks.Add(l);
                            }

                        }
                    }
                }
            }
        }

        protected class OutputDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.PickedNode.Parent.Parent.Parent.Parent.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
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
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge((PPath)((ArrayList)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                PPath edge = (PPath)((ArrayList)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0,0);
                ((ArrayList)((PNode)sender).Parent.Tag).Remove(edge);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.edgeLayer.RemoveChild(edge);
                ((ArrayList)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingOutlink = false;
                if (dragTarget != null)
                {
                    ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).CreateOutlink(((PPath)sender).Parent, dragTarget);
                }
            }
        }
        protected class VarDragHandler : PDragEventHandler
        {

            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.PickedNode.Parent.Parent.Parent.Parent.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
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
                ((ArrayList)edge.Tag).Add(-1);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingVarlink = true;

            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge((PPath)((ArrayList)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                PPath edge = (PPath)((ArrayList)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((ArrayList)((PNode)sender).Parent.Tag).Remove(edge);
                ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).g.edgeLayer.RemoveChild(edge);
                ((ArrayList)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingVarlink = false;
                if (dragTarget != null)
                {
                    ((SBox)e.PickedNode.Parent.Parent.Parent.Parent).CreateVarlink(((PPath)sender).Parent, (SVar)dragTarget);
                }
            }
        }

        public void CreateOutlink(PNode n1, PNode n2)
        {
            SBox start = (SBox)n1.Parent.Parent.Parent;
            SAction end = (SAction)n2.Parent.Parent.Parent;
            byte[] buff = this.pcc.Exports[start.Index].Data;
            List<byte> ListBuff = new List<byte>(buff);
            OutputLink link = new OutputLink();
            bool firstLink = false;
            foreach (OutputLink l in start.Outlinks)
            {
                if (l.node == n1)
                {
                    if (l.Links.Contains(end.Index))
                        return;
                    if (l.Links[0] == -1)
                    {
                        firstLink = true;
                        l.Links.RemoveAt(0);
                        l.offsets.RemoveAt(0);
                        l.InputIndices.RemoveAt(0);
                    }
                    else
                    {
                        l.offsets.Add(l.offsets[l.offsets.Count - 1] + 64);
                    }
                    l.Links.Add(end.Index);
                    link = l;
                    break;
                }
            }
            if (link.Links == null)
                return;
            int inputIndex = -1;
            foreach (InputLink l in end.InLinks)
            {
                if (l.node == n2)
                {
                    inputIndex = l.index;
                    link.InputIndices.Add(inputIndex);
                }
            }
            if (inputIndex == -1)
                return;
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, this.pcc.Exports[start.Index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
            {
                if (pcc.getNameEntry(p[i].Name) == "OutputLinks")
                {

                    byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p[i].raw, 16) + 64);
                    for (int j = 0; j < 4; j++)
                    {
                        ListBuff[p[i].offsetval - 8 + j] = sizebuff[j];
                    }
                    f = i;
                    break;
                }
            }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        if (pcc.getNameEntry(p2[i].Name) == "Links" && p2[i + 1].Value.StringValue + (p2[i + 1].Value.StringValue.Length == 0 ? "\0" : "" ) == (OutputNumbers ? link.Desc.Substring(0, link.Desc.LastIndexOf(":")) : link.Desc) + '\0')
                        {
                            if (firstLink)
                                link.offsets.Add(pos + 52);
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            byte[] countbuff = BitConverter.GetBytes(count2 + 1);
                            byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p2[i].raw, 16) + 64);
                            for (int k = 0; k < 4; k++)
                            {
                                ListBuff[p[f].offsetval + pos + k] = countbuff[k];
                                ListBuff[p[f].offsetval + pos - 8 + k] = sizebuff[k];
                            }
                            MemoryStream m = new MemoryStream();
                            m.Write(BitConverter.GetBytes(pcc.findName("LinkedOp")), 0, 4); //name: LinkedOp
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes(pcc.findName("ObjectProperty")), 0, 4); //type: ObjectProperty
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes((int)4), 0, 4); //size
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes(end.Index + 1), 0, 4);//value
                            m.Write(BitConverter.GetBytes(pcc.findName("InputLinkIdx")), 0, 4); //name: InputLinkIdx
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes(pcc.findName("IntProperty")), 0, 4); //type: IntProperty
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes((int)4), 0, 4); //size
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            m.Write(BitConverter.GetBytes(inputIndex), 0, 4);//value
                            m.Write(BitConverter.GetBytes(pcc.findName("None")), 0, 4); //name: None
                            m.Write(BitConverter.GetBytes((int)0), 0, 4);
                            ListBuff.InsertRange(p[f].offsetval + pos + 4 + count2 * 64, m.ToArray());
                            pcc.Exports[start.Index].Data = ListBuff.ToArray();
                            j = count; //break outer loop
                            break;
                        }
                        pos += p2[i].raw.Length;
                    }
                }
            }
            refreshView();
        }

        public void CreateVarlink(PNode p1, SVar end)
        {
            SBox start = (SBox)p1.Parent.Parent.Parent;
            byte[] buff = pcc.Exports[start.Index].Data;
            List<byte> ListBuff = new List<byte>(buff);
            VarLink link = new VarLink();
            bool firstLink = false;
            foreach (VarLink l in start.Varlinks)
            {
                if (l.node == p1)
                {
                    if (l.Links.Contains(end.Index))
                        return;
                    if (l.Links[0] == -1)
                    {
                        firstLink = true;
                        l.Links.RemoveAt(0);
                        l.offsets.RemoveAt(0);
                    }
                    else
                    {
                        l.offsets.Add(l.offsets[l.offsets.Count -1] + 4);
                    }
                    l.Links.Add(end.Index);
                    link = l;
                    break;
                }
            }
            if(link.Links == null)
                return;
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[start.Index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
            {
                if (pcc.getNameEntry(p[i].Name) == "VariableLinks")
                {
                    
                    byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p[i].raw, 16) + 4);
                    for (int j = 0; j < 4; j++)
                    {
                        ListBuff[p[i].offsetval - 8 + j] = sizebuff[j];
                    }
                    f = i;
                    break;
                }
            }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                int count = BitConverter.ToInt32(global, 24);
                for(int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        if (pcc.getNameEntry(p2[i].Name) == "LinkedVariables" && p2[i + 1].Value.StringValue == link.Desc + '\0')
                        {
                            if (firstLink)
                                link.offsets.Add(pos + 28);
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            byte[] countbuff = BitConverter.GetBytes(count2 + 1);
                            byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p2[i].raw, 16) + 4);
                            for(int k = 0; k < 4; k++)
                            {
                                ListBuff[p[f].offsetval + pos + k] = countbuff[k];
                                ListBuff[p[f].offsetval + pos - 8 + k] = sizebuff[k];
                            }
                            ListBuff.InsertRange(p[f].offsetval + pos + 4 + count2 * 4, BitConverter.GetBytes(end.Index + 1));
                            pcc.Exports[start.Index].Data = ListBuff.ToArray();
                            j = count; //break outer loop
                            break;
                        }
                        pos += p2[i].raw.Length;
                    }
                }
            }
            refreshView();
        }

        public void RemoveOutlink(int linkconnection, int linkIndex, bool refresh = true)
        {

            byte[] buff = pcc.Exports[index].Data;
            List<byte> ListBuff = new List<byte>(buff);
            BitConverter.IsLittleEndian = true;
            OutputLink link = Outlinks[linkconnection];
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
            {
                if (pcc.getNameEntry(p[i].Name) == "OutputLinks")
                {

                    byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p[i].raw, 16) - 64);
                    for (int j = 0; j < 4; j++)
                    {
                        ListBuff[p[i].offsetval - 8 + j] = sizebuff[j];
                    }
                    f = i;
                    break;
                }
            }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        if (pcc.getNameEntry(p2[i].Name) == "Links" && p2[i + 1].Value.StringValue == (OutputNumbers ? link.Desc.Substring(0, link.Desc.LastIndexOf(":")) : link.Desc) + '\0')
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            byte[] countbuff = BitConverter.GetBytes(count2 - 1);
                            byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p2[i].raw, 16) - 64);
                            for (int k = 0; k < 4; k++)
                            {
                                ListBuff[p[f].offsetval + pos + k] = countbuff[k];
                                ListBuff[p[f].offsetval + pos - 8 + k] = sizebuff[k];
                            }
                            ListBuff.RemoveRange(p[f].offsetval + pos + 4 + linkIndex * 64, 64);
                            pcc.Exports[index].Data = ListBuff.ToArray();
                            j = count; //break outer loop
                            break;
                        }
                        pos += p2[i].raw.Length;
                    }
                }
            }
            if (refresh)
            {
                refreshView();
            }
        }

        public void RemoveVarlink(int linkconnection, int linkIndex, bool refresh = true)
        {
            byte[] buff = pcc.Exports[index].Data;
            List<byte> ListBuff = new List<byte>(buff);
            BitConverter.IsLittleEndian = true;
            VarLink link = Varlinks[linkconnection];
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
            {
                if (pcc.getNameEntry(p[i].Name) == "VariableLinks")
                {

                    byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p[i].raw, 16) - 4);
                    for (int j = 0; j < 4; j++)
                    {
                        ListBuff[p[i].offsetval - 8 + j] = sizebuff[j];
                    }
                    f = i;
                    break;
                }
            }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        if (pcc.getNameEntry(p2[i].Name) == "LinkedVariables" && p2[i + 1].Value.StringValue == link.Desc + '\0')
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            byte[] countbuff = BitConverter.GetBytes(count2 - 1);
                            byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p2[i].raw, 16) - 4);
                            for (int k = 0; k < 4; k++)
                            {
                                ListBuff[p[f].offsetval + pos + k] = countbuff[k];
                                ListBuff[p[f].offsetval + pos - 8 + k] = sizebuff[k];
                            }
                            ListBuff.RemoveRange(p[f].offsetval + pos + 4 + linkIndex * 4, 4);
                            pcc.Exports[index].Data = ListBuff.ToArray();
                            j = count; //break outer loop
                            break;
                        }
                        pos += p2[i].raw.Length;
                    }
                }
            }
            if (refresh)
            {
                refreshView();
            }
        }

    }

    public class SEvent : SBox
    {
        public SEvent(int idx, float x, float y, PCCObject p, GraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            outlinePen = new Pen(Color.FromArgb(214, 30, 28));
            string s = pcc.Exports[index].ObjectName;
            s = s.Replace("BioSeqEvt_", "");
            s = s.Replace("SFXSeqEvt_", "");
            s = s.Replace("SeqEvt_", "");
            s = s.Replace("SeqEvent_", "");
            float starty = 0;
            float w = 15;
            float midW = 0;
            varLinkBox = new PPath();
            GetVarLinks();
            for (int i = 0; i < Varlinks.Count; i++)
            {
                string d = "";
                foreach (int l in Varlinks[i].Links)
                    d = d + "#" + l + ",";
                d = d.Remove(d.Length - 1);
                SText t2 = new SText( d + "\n" + Varlinks[i].Desc);
                t2.X = w;
                t2.Y = 0;
                w += t2.Width + 20;
                t2.Pickable = false;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if(Varlinks.Count != 0)
                varLinkBox.AddRectangle(0, 0, w, varLinkBox[0].Height);
            varLinkBox.Pickable = false;
            varLinkBox.Pen = outlinePen;
            varLinkBox.Brush = nodeBrush;
            GetOutputLinks();
            outLinkBox = new PPath();
            for (int i = 0; i < Outlinks.Count(); i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
                if(t2.Width + 10 > midW) midW = t2.Width + 10;
                //t2.TextAlignment = StringAlignment.Far;
                //t2.ConstrainWidthToTextWidth = false;
                t2.X = 0 - t2.Width;
                t2.Y = starty + 3;
                starty += t2.Height + 6;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.AddPolygon(new PointF[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f*midW, starty+30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW/-2, -30) });
            outLinkBox.Pickable = false;
            outLinkBox.Pen = outlinePen;
            outLinkBox.Brush = nodeBrush;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            foreach (PropertyReader.Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name).Contains("EventName") || pcc.getNameEntry(prop.Name) == "sScriptName")
                    s += "\n\"" + pcc.getNameEntry(prop.Value.IntValue) + "\"";
                else if (pcc.getNameEntry(prop.Name) == "InputLabel" || pcc.getNameEntry(prop.Name) == "sEvent")
                    s += "\n\"" + prop.Value.StringValue + "\"";
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                if (midW > tW)
                {
                    w = midW;
                    titleBox.Width = w;
                }
                else
                {
                    w = tW;
                }
                varLinkBox.Width = w;
            }
            float h = titleBox.Height + 1;
            outLinkBox.TranslateBy(titleBox.Width/2 + midW/2, h + 30);
            h += outLinkBox.Height + 1;
            varLinkBox.TranslateBy(0, h);
            h += varLinkBox.Height;
            this.bounds = new RectangleF(0, 0, w, h);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.TranslateBy(x, y);
        }

        public override void Select()
        {
            titleBox.Pen = selectedPen;
            varLinkBox.Pen = selectedPen;
            outLinkBox.Pen = selectedPen;
        }

        public override void Deselect()
        {
            titleBox.Pen = outlinePen;
            varLinkBox.Pen = outlinePen;
            outLinkBox.Pen = outlinePen;
        }

        //public override bool Intersects(RectangleF bounds)
        //{
        //    Region hitregion = new Region(titleBox.PathReference);
        //    hitregion.Complement(outLinkBox.PathReference);
        //    hitregion.Complement(varLinkBox.PathReference);
        //    return hitregion.IsVisible(bounds);
        //}
    }

    public class SAction : SBox
    {
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;

        public SAction(int idx, float x, float y, PCCObject p, GraphEditor grapheditor)
            : base(idx, x, y, p, grapheditor)
        {
            GetVarLinks();
            GetOutputLinks();
            originalX = x;
            originalY = y;
        }

        public SAction(int idx, float x, float y, PCCObject p)
            : base(idx, x, y, p)
        {
            GetVarLinks();
            GetOutputLinks();
            originalX = x;
            originalY = y;
        }
        public override void Select()
        {
            titleBox.Pen = selectedPen;
            ((PPath)this[1]).Pen = selectedPen;
        }

        public override void Deselect()
        {
            titleBox.Pen = outlinePen;
            ((PPath)this[1]).Pen = outlinePen;
        }

        public override void Layout(float x, float y)
        {
            if (originalX != -1)
                x = originalX;
            if (originalY != -1)
                y = originalY;
            outlinePen = new Pen(Color.Black);
            string s = pcc.Exports[index].ObjectName;
            s = s.Replace("BioSeqAct_", "");
            s = s.Replace("SFXSeqAct_", "");
            s = s.Replace("SeqAct_", "");
            s = s.Replace("SeqCond_", "");
            float starty = 8;
            float w = 20;
            varLinkBox = new PPath();
            for (int i = 0; i < Varlinks.Count(); i++)
            {
                string d = "";
                foreach (int l in Varlinks[i].Links)
                    d = d + "#" + l + ",";
                d = d.Remove(d.Length - 1);
                SText t2 = new SText(d + "\n" + Varlinks[i].Desc);
                t2.X = w;
                t2.Y = 0;
                w += t2.Width + 20;
                t2.Pickable = false;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.Height = varLinkBox[0].Height;
            varLinkBox.Width = w;
            varLinkBox.Pickable = false;
            outLinkBox = new PPath();
            float outW = 0;
            for (int i = 0; i < Outlinks.Count(); i++)
            {
                SText t2 = new SText(Outlinks[i].Desc);
                if (t2.Width + 10 > outW) outW = t2.Width + 10;
                t2.X = 0 - t2.Width;
                t2.Y = starty;
                starty += t2.Height;
                t2.Pickable = false;
                Outlinks[i].node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].node);
                outLinkBox.AddChild(t2);
            }
            outLinkBox.Pickable = false;
            inputLinkBox = new PNode();
            GetInputLinks();
            float inW = 0;
            float inY = 8;
            for (int i = 0; i < InLinks.Count(); i++)
            {
                SText t2 = new SText(InLinks[i].Desc);
                if (t2.Width > inW) inW = t2.Width;
                t2.X = 3;
                t2.Y = inY;
                inY += t2.Height;
                t2.Pickable = false;
                InLinks[i].node.X = -10;
                InLinks[i].node.Y = t2.Y + t2.Height / 2 - 5;
                t2.AddChild(InLinks[i].node);
                inputLinkBox.AddChild(t2);
            }
            inputLinkBox.Pickable = false;
            if (inY > starty) starty = inY;
            if (inW + outW + 10 > w) w = inW + outW + 10;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            foreach (PropertyReader.Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name) == "oSequenceReference")
                    s += "\n\"" + pcc.Exports[prop.Value.IntValue - 1].ObjectName + "\"";
                else if (pcc.getNameEntry(prop.Name) == "EventName" || pcc.getNameEntry(prop.Name) == "StateName")
                    s += "\n\"" + pcc.getNameEntry(prop.Value.IntValue) + "\"";
                else if (pcc.getNameEntry(prop.Name) == "OutputLabel" || pcc.getNameEntry(prop.Name) == "m_sMovieName")
                    s += "\n\"" + prop.Value.StringValue + "\"";
                else if (pcc.getNameEntry(prop.Name) == "m_pEffect")
                    if(prop.Value.IntValue > 0)
                        s += "\n\"" + pcc.Exports[prop.Value.IntValue - 1].ObjectName + "\"";
                    else
                        s += "\n\"" + pcc.Imports[-prop.Value.IntValue - 1].ObjectName + "\"";
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                w = tW;
                titleBox.Width = w;
            }
            titleBox.X = 0;
            titleBox.Y = 0;
            float h = titleBox.Height + 2;
            inputLinkBox.TranslateBy(0, h);
            outLinkBox.TranslateBy(w, h);
            h += starty + 8;
            varLinkBox.TranslateBy(varLinkBox.Width < w ? (w - varLinkBox.Width) / 2 : 0, h);
            h += varLinkBox.Height;
            box = PPath.CreateRectangle(0, titleBox.Height + 2, w, h - (titleBox.Height + 2));
            box.Brush = nodeBrush;
            box.Pen = outlinePen;
            box.Pickable = false;
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(titleBox);
            this.AddChild(varLinkBox);
            this.AddChild(outLinkBox);
            this.AddChild(inputLinkBox);
            this.TranslateBy(x, y);
        }

        private void GetInputLinks()
        {
            InLinks = new List<InputLink>();
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            if (pcc.Exports[index].ClassName == "SequenceReference")
            {
                PropertyReader.Property prop = PropertyReader.getPropOrNull(pcc, pcc.Exports[index], "oSequenceReference");
                if (prop != null)
                {
                    p = PropertyReader.getPropList(pcc, pcc.Exports[prop.Value.IntValue - 1]);
                }
            }
            int f = -1;
            for (int i = 0; i < p.Count(); i++)
                if (pcc.getNameEntry(p[i].Name) == "InputLinks")
                {
                    f = i;
                    break;
                }
            if (f != -1)
            {
                int pos = 28;
                byte[] global = p[f].raw;
                BitConverter.IsLittleEndian = true;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);
                    InputLink l = new InputLink();
                    l.Desc = p2[0].Value.StringValue;
                    l.hasName = true;
                    l.index = j;
                    l.node = PPath.CreateRectangle(0, -4, 10, 8);
                    l.node.Brush = outputBrush;
                    l.node.MouseEnter += new PInputEventHandler(OnMouseEnter);
                    l.node.MouseLeave += new PInputEventHandler(OnMouseLeave);
                    l.node.AddInputEventListener(new InputDragHandler());
                    InLinks.Add(l);
                    for (int i = 0; i < p2.Count(); i++)
                        pos += p2[i].raw.Length;
                }
            }
            else
            {
                try
                {
                    List<string> inputLinks = UnrealObjectInfo.getSequenceObjectInfo(pcc.Exports[index].ClassName)?.inputLinks;
                    if (inputLinks != null)
                    {
                        for (int i = 0; i < inputLinks.Count; i++)
                        {
                            InputLink l = new InputLink();
                            l.Desc = inputLinks[i];
                            l.hasName = true;
                            l.index = i;
                            l.node = PPath.CreateRectangle(0, -4, 10, 8);
                            l.node.Brush = outputBrush;
                            l.node.MouseEnter += new PInputEventHandler(OnMouseEnter);
                            l.node.MouseLeave += new PInputEventHandler(OnMouseLeave);
                            l.node.AddInputEventListener(new InputDragHandler());
                            InLinks.Add(l);
                        }
                    }
                }
                catch (Exception)
                {
                    InLinks.Clear();
                }
            }
            if(this.Tag != null)
            {
                int numInputs = InLinks.Count;
                int inputNum;
                foreach(PPath edge in ((ArrayList)this.Tag))
                {
                    inputNum = (int)((ArrayList)edge.Tag)[2];
                    if (inputNum + 1 > numInputs)
                    {
                        for (int i = numInputs; i <= inputNum; i++)
                        {
                            InputLink l = new InputLink();
                            l.node = PPath.CreateRectangle(0, -4, 10, 8);
                            l.node.Brush = outputBrush;
                            l.node.MouseEnter += new PInputEventHandler(OnMouseEnter);
                            l.node.MouseLeave += new PInputEventHandler(OnMouseLeave);
                            l.node.AddInputEventListener(new InputDragHandler());
                            l.Desc = ":" + i;
                            l.hasName = false;
                            l.index = i;
                            InLinks.Add(l);
                        }
                        numInputs = inputNum + 1;
                    }
                    if(inputNum >= 0)
                        ((ArrayList)edge.Tag)[1] = InLinks[inputNum].node;
                }
            }
        }

        public class InputDragHandler : PDragEventHandler
        {
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                e.Handled = true;
            }
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if(draggingOutlink)
            {
                ((PPath)sender).Pen = selectedPen;
                dragTarget = (PPath)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            ((PPath)sender).Pen = outlinePen;
            dragTarget = null;
        }

    }

    public class SText : PText
    {
        private Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }
        private static PrivateFontCollection fontcollection;
        private static Font kismetFont;

        public SText(string s, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;

            shadowRendering = shadows;
        }

        public SText(string s, Color c, bool shadows = true)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
            shadowRendering = shadows;
        }

        public static void LoadFont()
        {
            if(fontcollection == null || fontcollection.Families.Length < 1)
            {
                fontcollection = new PrivateFontCollection();
                fontcollection.AddFontFile(@"exec\KismetFont.ttf");
                kismetFont = new Font(fontcollection.Families[0], 6);
            }
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            if (shadowRendering && base.Text != null && base.TextBrush != null && base.Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = base.Font.SizeInPoints * paintContext.Scale;
                if(paintContext.Scale >= 1)
                    if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                    {
                        RectangleF shadowbounds = new RectangleF();
                        shadowbounds = Bounds;
                        shadowbounds.Offset(1, 1);
                        StringFormat stringformat = new StringFormat();
                        stringformat.Alignment = base.TextAlignment;
                        g.DrawString(base.Text, base.Font, black, shadowbounds, stringformat);
                    }
            }
            base.Paint(paintContext);
        }
    }
}