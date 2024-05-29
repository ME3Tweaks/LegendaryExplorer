using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Helpers;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using SText = LegendaryExplorer.Tools.SequenceObjects.SText;
using WwiseStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    public abstract class WwiseEdEdge : PPath
    {
        public PNode start;
        public PNode end;
        public WwiseHircObjNode originator;
    }
    public class VarEdge : WwiseEdEdge
    {
    }

    public class ActionEdge : WwiseEdEdge
    {
    }
    public abstract class WwiseHircObjNode : PNode, IDisposable
    {
        public WwiseGraphEditor g;
        protected static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        protected static readonly Color intColor = Color.FromArgb(34, 218, 218);//cyan
        protected static readonly Color floatColor = Color.FromArgb(23, 23, 213);//blue
        protected static readonly Color boolColor = Color.FromArgb(215, 37, 33); //red
        protected static readonly Color objectColor = Color.FromArgb(219, 39, 217);//purple
        protected static readonly Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        protected static readonly Color stringColor = Color.FromArgb(24, 219, 12);//lime green
        protected static readonly Color vectorColor = Color.FromArgb(127, 123, 32);//dark gold
        protected static readonly Color EventColor = Color.FromArgb(214, 30, 28);
        protected static readonly Color titleColor = Color.FromArgb(255, 255, 128);
        protected static readonly Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));
        protected static readonly Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static readonly Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static readonly Pen selectedPen = new (Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink;
        public static bool draggingVarlink;
        public static bool draggingEventlink;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public RectangleF posAtDragStart;

        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }
        public virtual bool IsSelected { get; set; }

        protected WwiseBank.HIRCObject hircObject;
        protected Pen outlinePen;
        protected SText comment;

        public virtual uint ID => hircObject?.ID ?? 0;

        public string Comment => comment.Text;

        protected WwiseHircObjNode(WwiseBank.HIRCObject hircObj, WwiseGraphEditor grapheditor)
        {
            hircObject = hircObj;
            g = grapheditor;
            comment = new SText(GetComment(), commentColor, false)
            {
                Pickable = false,
                X = 0
            };
            comment.Y = 0 - comment.Height;
            AddChild(comment);
            Pickable = true;
        }

        public virtual void Layout()
        {
        }
        public virtual void Layout(float x, float y) => SetOffset(x, y);
        protected string GetComment() => GetIDString(ID);

        public static string GetIDString(uint id)
        {
            if (Misc.AppSettings.Settings.Soundplorer_ReverseIDDisplayEndianness)
            {
                id = id.Swap();
            }

            return $"{id:X8}";
        }

        protected Color GetColor(HIRCType t)
        {
            return t switch
            {
                HIRCType.SoundSXFSoundVoice => intColor,
                HIRCType.EventAction => interpDataColor,
                HIRCType.Event => boolColor,
                HIRCType.ActorMixer => stringColor,
                //HIRCType.Settings => expr,
                //HIRCType.RandomOrSequenceContainer => expr,
                //HIRCType.SwitchContainer => expr,
                //HIRCType.AudioBus => expr,
                //HIRCType.BlendContainer => expr,
                //HIRCType.MusicSegment => expr,
                //HIRCType.MusicTrack => expr,
                //HIRCType.MusicSwitchContainer => expr,
                //HIRCType.MusicPlaylistContainer => expr,
                //HIRCType.Attenuation => expr,
                //HIRCType.DialogueEvent => expr,
                //HIRCType.MotionBus => expr,
                //HIRCType.MotionFX => expr,
                //HIRCType.Effect => expr,
                //HIRCType.AuxiliaryBus => expr,
                _ => Color.Black
            };
        }

        public virtual void Dispose()
        {
            g = null;
        }

        public virtual IEnumerable<WwiseEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Cast<WwiseEdEdge>()
                                                                .Union(Varlinks.SelectMany(l => l.Edges)).Union(InputEdges);

        public List<InputLink> InLinks = new();
        public List<WwiseEdEdge> InputEdges = new();

        protected static Brush outputBrush = new SolidBrush(Color.Black);

        public struct OutputLink
        {
            public PPath node;
            public List<uint> Links;
            public string Desc;
            public List<ActionEdge> Edges;
        }

        public struct VarLink
        {
            public PPath node;
            public List<uint> Links;
            public string Desc;
            public List<VarEdge> Edges;
        }

        public struct InputLink
        {
            public PPath node;
            public string Desc;
            public int index;
            public bool hasName;
            public List<ActionEdge> Edges;
        }

        protected PPath titleBox;
        protected PPath varLinkBox;
        protected PPath outLinkBox;
        public readonly List<OutputLink> Outlinks = new();
        public readonly List<VarLink> Varlinks = new();
        private static readonly PointF[] downwardTrianglePoly = { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) };
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);
        protected PPath CreateVarLinkBox() => PPath.CreateRectangle(-4, 0, 8, 10);

        public virtual void CreateConnections(IList<WwiseHircObjNode> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                foreach (uint idLink in outLink.Links)
                {
                    foreach (var destObj in objects.Except(objects.OfType<WExport>()))
                    {
                        if (destObj.ID == idLink)
                        {
                            PPath p1 = outLink.node;
                            var edge = new ActionEdge();
                            p1.Tag ??= new List<ActionEdge>();
                            ((List<ActionEdge>)p1.Tag).Add(edge);
                            destObj.InputEdges.Add(edge);
                            edge.start = p1;
                            edge.end = destObj;
                            edge.originator = this;
                            g.AddEdge(edge);
                            outLink.Edges.Add(edge);
                        }
                    }
                }
            }
            foreach (VarLink varLink in Varlinks)
            {
                foreach (uint link in varLink.Links)
                {
                    foreach (var destVar in objects)
                    {
                        if (destVar is WExport wExp && wExp.Export.UIndex == link || destVar is WGeneric wGen && wGen.ID == link)
                        {
                            PPath p1 = varLink.node;
                            var edge = new VarEdge();
                            if (destVar.ChildrenCount > 1)
                                edge.Pen = ((PPath)destVar[1]).Pen;
                            p1.Tag ??= new List<VarEdge>();
                            ((List<VarEdge>)p1.Tag).Add(edge);
                            destVar.InputEdges.Add(edge);
                            edge.start = p1;
                            edge.end = destVar;
                            edge.originator = this;
                            g.AddEdge(edge);
                            varLink.Edges.Add(edge);
                        }
                    }
                }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            var title = new SText(s, titleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            if (title.Width + 20 > w)
            {
                w = title.Width + 20;
            }
            title.Width = w;
            titleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
            return w;
        }

        protected virtual void GetLinks(){}
    }
    public sealed class WExport : WwiseHircObjNode
    {
        public const float RADIUS = 30;

        public override uint ID { get; }

        public readonly ExportEntry Export;

        readonly SText val;
        private readonly PPath shape;
        public string Value
        {
            get => val.Text;
            set => val.Text = value;
        }

        public WExport(ExportEntry entry, float x, float y, WwiseGraphEditor grapheditor)
            : base(null, grapheditor)
        {
            Export = entry;
            ID = (Export.GetProperty<IntProperty>("Id")?.Value ?? 0).ReinterpretAsUint();
            const float w = RADIUS * 2;
            const float h = RADIUS * 2;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(Export.ClassName switch
            {
                "WwiseEvent" => boolColor,
                "WwiseStream" => intColor,
                _ => Color.Black
            });
            shape.Pen = outlinePen;
            shape.Brush = nodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue())
            {
                Pickable = false,
                TextAlignment = StringAlignment.Center
            };
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            SetOffset(x, y);
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
            comment.Text = "";
        }

        public string GetValue() => $"#{Export.UIndex} {Export.ObjectName.Instanced}";

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    shape.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    shape.Pen = outlinePen;
                }
            }
        }

        public override bool Intersects(RectangleF bounds)
        {
            var ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((WExport)sender)[1]).Pen = selectedPen;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((WExport)sender)[1]).Pen = outlinePen;
                dragTarget = null;
            }
        }

        public override void Dispose()
        {
            g = null;
        }
    }

    public sealed class WEvent : WwiseHircObjNode
    {
        public WwiseBank.Event Event => (WwiseBank.Event)hircObject;

        public WEvent(WwiseBank.Event hircEvent, float x, float y, WwiseGraphEditor grapheditor)
            : base(hircEvent, grapheditor)
        {
            outlinePen = new Pen(EventColor);
            const string s = "Event";
            float starty = 0;
            float w = 15;
            float midW = 0;
            varLinkBox = new PPath();
            GetLinks();
            foreach (var varLink in Varlinks)
            {
                string d = string.Join(",", varLink.Links.Select(l => $"#{l}"));
                var t2 = new SText(d + "\n" + varLink.Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                varLink.node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(varLink.node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                varLinkBox.AddRectangle(0, 0, w, varLinkBox[0].Height);
            varLinkBox.Pickable = false;
            varLinkBox.Pen = outlinePen;
            varLinkBox.Brush = nodeBrush;
            outLinkBox = new PPath();
            for (int i = 0; i < Outlinks.Count; i++)
            {
                string linkDesc = Outlinks[i].Desc;
                if (OutputNumbers && Outlinks[i].Links.Any())
                {
                    linkDesc += $": {string.Join(",", Outlinks[i].Links.Select(l => $"#{l}"))}";
                }
                var t2 = new SText(linkDesc);
                if (t2.Width + 10 > midW) midW = t2.Width + 10;
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
            outLinkBox.AddPolygon(new[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f * midW, starty + 30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW / -2, -30) });
            outLinkBox.Pickable = false;
            outLinkBox.Pen = outlinePen;
            outLinkBox.Brush = nodeBrush;
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
            outLinkBox.TranslateBy(titleBox.Width / 2 + midW / 2, h + 30);
            h += outLinkBox.Height + 1;
            varLinkBox.TranslateBy(0, h);
            h += varLinkBox.Height;
            bounds = new RectangleF(0, 0, w, h);
            AddChild(titleBox);
            AddChild(varLinkBox);
            AddChild(outLinkBox);
            SetOffset(x, y);
        }

        private bool _isSelected;

        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    outLinkBox.Pen = selectedPen;
                    varLinkBox.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    outLinkBox.Pen = outlinePen;
                    varLinkBox.Pen = outlinePen;
                }
            }
        }

        protected override void GetLinks()
        {
            if (Event.EventActions.Any())
            {
                var l = new OutputLink
                {
                    Desc = "Event Actions",
                    Links = Event.EventActions.Clone(),
                    Edges = new List<ActionEdge>(),
                    node = CreateActionLinkBox()
                };
                l.node.Brush = outputBrush;
                l.node.Pickable = false;
                // PPath dragger = CreateActionLinkBox();
                // dragger.Brush = mostlyTransparentBrush;
                // dragger.X = l.node.X;
                // dragger.Y = l.node.Y;
                // dragger.AddInputEventListener(outputDragHandler);
                //l.node.AddChild(dragger);
                Outlinks.Add(l);
            }

            var varLink = new VarLink
            {
                Desc = "Linked Export(s)",
                Edges = new List<VarEdge>(),
                Links = new List<uint>(),
                node = CreateVarLinkBox()
            };
            varLink.node.Brush = outputBrush;
            varLink.node.Pickable = false;
            Varlinks.Add(varLink);
        }
    }

    public class WGeneric : WwiseHircObjNode
    {
        protected PNode inputLinkBox;
        protected PPath box;
        protected float originalX;
        protected float originalY;

        protected InputDragHandler inputDragHandler = new ();

        public WGeneric(WwiseBank.HIRCObject hircO, float x, float y, WwiseGraphEditor grapheditor)
            : base(hircO, grapheditor)
        {
            originalX = x;
            originalY = y;
        }

        private bool _isSelected;
        public override bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                if (value)
                {
                    titleBox.Pen = selectedPen;
                    ((PPath)this[1]).Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    ((PPath)this[1]).Pen = outlinePen;
                }
            }
        }

        public override void Layout()
        {
            Layout(originalX, originalY);
        }

        public override void Layout(float x, float y)
        {
            outlinePen = new Pen(Color.Black);
            string s = GetTitle();
            float starty = 8;
            float w = 20;
            varLinkBox = new PPath();
            for (int i = 0; i < Varlinks.Count; i++)
            {
                string d = string.Join(",", Varlinks[i].Links.Select(l => $"#{l}"));
                var t2 = new SText($"{d}\n{Varlinks[i].Desc}")
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                Varlinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Any())
                varLinkBox.Height = varLinkBox[0].Height;
            varLinkBox.Width = w;
            varLinkBox.Pickable = false;
            outLinkBox = new PPath();
            float outW = 0;
            for (int i = 0; i < Outlinks.Count; i++)
            {
                string linkDesc = Outlinks[i].Desc;
                if (OutputNumbers && Outlinks[i].Links.Any())
                {
                    linkDesc += $": {string.Join(",", Outlinks[i].Links.Select(l => $"#{l}"))}";
                }
                var t2 = new SText(linkDesc);
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
            float inW = 0;
            float inY = 8;
            for (int i = 0; i < InLinks.Count; i++)
            {
                var t2 = new SText(InLinks[i].Desc);
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
            SetOffset(x, y);
        }

        protected virtual string GetTitle()
        {
            return WwiseStreamHelper.GetHircObjTypeString(hircObject.Type);
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
            if (draggingOutlink)
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

        public override void Dispose()
        {
            base.Dispose();
            if (inputDragHandler != null)
            {
                InLinks.ForEach(x => x.node.RemoveInputEventListener(inputDragHandler));
            }
        }
    }

    public sealed class WEventAction : WGeneric
    {
        public WwiseBank.EventAction  EventAction => (WwiseBank.EventAction)hircObject;

        public WEventAction(WwiseBank.EventAction evtAct, float x, float y, WwiseGraphEditor grapheditor) : base(evtAct, x, y, grapheditor)
        {
            GetLinks();
        }

        protected override string GetTitle()
        {
            return $"{base.GetTitle()}: {WwiseStreamHelper.GetEventActionTypeString(EventAction.ActionType)}";
        }

        protected override void GetLinks()
        {
            if (EventAction.ReferencedObjectID != 0)
            {
                var l = new OutputLink
                {
                    Desc = "Referenced object",
                    Links = new List<uint>{EventAction.ReferencedObjectID},
                    Edges = new List<ActionEdge>(),
                    node = CreateActionLinkBox()
                };
                l.node.Brush = outputBrush;
                l.node.Pickable = false;
                // PPath dragger = CreateActionLinkBox();
                // dragger.Brush = mostlyTransparentBrush;
                // dragger.X = l.node.X;
                // dragger.Y = l.node.Y;
                // dragger.AddInputEventListener(outputDragHandler);
                //l.node.AddChild(dragger);
                Outlinks.Add(l);
            }
        }
    }

    public sealed class WSoundSFXVoice : WGeneric
    {
        public WwiseBank.SoundSFXVoice SoundSFXVoice => (WwiseBank.SoundSFXVoice)hircObject;

        public WSoundSFXVoice(WwiseBank.SoundSFXVoice ssfxv, float x, float y, WwiseGraphEditor grapheditor) : base(ssfxv, x, y, grapheditor)
        {
            GetLinks();
        }

        protected override string GetTitle()
        {
            return $"{base.GetTitle()}: {SoundSFXVoice.State}, {SoundSFXVoice.SoundType}";
        }

        protected override void GetLinks()
        {
            if (SoundSFXVoice.AudioID != 0)
            {
                var l = new VarLink
                {
                    Desc = "Referenced object",
                    Links = new List<uint> { SoundSFXVoice.AudioID },
                    Edges = new List<VarEdge>(),
                    node = CreateVarLinkBox()
                };
                l.node.Brush = outputBrush;
                l.node.Pickable = false;
                // PPath dragger = CreateActionLinkBox();
                // dragger.Brush = mostlyTransparentBrush;
                // dragger.X = l.node.X;
                // dragger.Y = l.node.Y;
                // dragger.AddInputEventListener(outputDragHandler);
                //l.node.AddChild(dragger);
                Varlinks.Add(l);
            }
        }
    }
}