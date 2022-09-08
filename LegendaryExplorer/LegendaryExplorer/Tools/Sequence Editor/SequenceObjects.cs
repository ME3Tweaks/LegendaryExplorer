using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Resources;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Piccolo;
using Piccolo.Event;
using Piccolo.Nodes;
using Piccolo.Util;

namespace LegendaryExplorer.Tools.SequenceObjects
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String, Vector, Rotator };
    public abstract class SeqEdEdge : PPath
    {
        public PNode Start;
        public PNode End;
        public SBox Originator;
    }
    public class VarEdge : SeqEdEdge
    {
    }

    public sealed class EventEdge : VarEdge
    {
    }

    [DebuggerDisplay("ActionEdge | {Originator} to {InputIndex}")]
    public sealed class ActionEdge : SeqEdEdge
    {
        public int InputIndex;
    }

    [DebuggerDisplay("SObj | #{UIndex}: {export.ObjectName.Instanced}")]
    public abstract class SObj : PNode, IDisposable
    {
        private static readonly Color CommentColor = Color.FromArgb(74, 63, 190);
        private static readonly Color IntColor = Color.FromArgb(34, 218, 218);//cyan
        private static readonly Color FloatColor = Color.FromArgb(23, 23, 213);//blue
        private static readonly Color BoolColor = Color.FromArgb(215, 37, 33); //red
        private static readonly Color ObjectColor = Color.FromArgb(219, 39, 217);//purple
        private static readonly Color InterpDataColor = Color.FromArgb(222, 123, 26);//orange
        private static readonly Color StringColor = Color.FromArgb(24, 219, 12);//lime green
        private static readonly Color VectorColor = Color.FromArgb(127, 123, 32);//dark gold
        private static readonly Color RotatorColor = Color.FromArgb(176, 97, 63);//burnt sienna
        protected static readonly Color EventColor = Color.FromArgb(214, 30, 28);
        protected static readonly Color TitleColor = Color.FromArgb(255, 255, 128);
        protected static readonly Brush TitleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));
        protected static readonly Brush MostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static readonly Brush NodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static readonly Pen SelectedPen = new(Color.FromArgb(255, 255, 0));
        protected static bool draggingOutlink;
        protected static bool draggingVarlink;
        protected static bool draggingEventlink;
        protected static PNode DragTarget;
        public static bool OutputNumbers;

        protected IMEPackage Pcc;
        protected SequenceGraphEditor g;
        public RectangleF PosAtDragStart;


        protected ExportEntry export;
        public ExportEntry Export => export;
        public int UIndex => export.UIndex;
        public virtual bool IsSelected { get; set; }

        protected Pen OutlinePen;
        protected readonly SText comment;

        public string Comment => comment.Text;

        protected SObj(ExportEntry entry, SequenceGraphEditor grapheditor)
        {
            Pcc = entry.FileRef;
            export = entry;
            g = grapheditor;
            comment = new SText(GetComment(), CommentColor, false)
            {
                Pickable = false,
                X = 0
            };
            comment.Y = 0 - comment.Height;
            AddChild(comment);
            Pickable = true;
        }

        public virtual void CreateConnections(IList<SObj> objects) { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);

        public virtual IEnumerable<SeqEdEdge> Edges => Enumerable.Empty<SeqEdEdge>();

        private string GetComment()
        {
            string res = "";
            var comments = export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
            if (comments != null)
            {
                foreach (StrProperty s in comments)
                {
                    res += s + "\n";
                }
            }

            if (Settings.SequenceEditor_ShowParsedInfo)
            {
                //Parse and show relevant info so user doesn't have to dig info file to find

                //I am sure there is a more efficient way to do this we will want to move to if we expand this feature
                PropertyCollection properties = export.GetProperties();
                switch (export.ClassName)
                {
                    case "BioSeqAct_ScalarMathUnit":
                        var op = properties.GetProp<EnumProperty>("Operation");
                        if (op == null)
                        {
                            res += "Operation: OP_Add"; // is this right? The class doesn't define anything as default
                        }
                        else
                        {
                            res += $"Operation: {op.Value}";
                        }
                        break;
                    case "BioSeqAct_AreaTransition":
                        var newMap = properties.GetProp<NameProperty>("AreaName");
                        var startPoint = properties.GetProp<NameProperty>("startPoint");
                        string newText = "";
                        if (newMap != null)
                        {
                            newText += $"Map: :{newMap.Value.Instanced}\n";
                        }
                        if (startPoint != null)
                        {
                            newText += $"Startpoint: {startPoint.Value.Instanced}";
                        }
                        res += newText;
                        break;
                    case "SeqAct_Delay":
                        var delayValue = properties.GetProp<FloatProperty>("Duration");
                        res += $"Delay: {delayValue?.Value ?? 1}s";
                        break;
                    case "SFXSeqAct_InitLoadingMovies":
                        if (properties.GetProp<ObjectProperty>(@"Movie")?.ResolveToEntry(export.FileRef) is ExportEntry movieExp)
                        {
                            var movieName = movieExp.GetProperty<StrProperty>("MovieName");
                            res += $"Movie: {movieName?.Value}";
                            if (properties.GetProp<ObjectProperty>(@"ScreenTip")?.ResolveToEntry(export.FileRef) is
                                ExportEntry tipExp)
                            {
                                var defaultTip = tipExp.GetProperty<StringRefProperty>(@"Default_Body");
                                int? defTipId = defaultTip?.Value;
                                if (defTipId != null)
                                {
                                    res += $"\nDefaultTip: {TLKManagerWPF.GlobalFindStrRefbyID(defTipId.Value, export.FileRef).WordWrap(40)}";
                                }
                            }
                        }

                        break;
                    case "SeqEvent_Death":
                        var originator = properties.GetProp<ObjectProperty>("Originator");
                        if (originator != null && originator.Value != 0)
                        {
                            res += $"Originator: {export.FileRef.GetEntry(originator.Value)?.InstancedFullPath}";
                        }
                        break;
                    case "SFXSeqAct_AIFactory2":
                        var sets = properties.GetProp<ArrayProperty<StructProperty>>("SpawnSets");
                        if (sets != null)
                        {
                            for (int i = 0; i < sets.Count; i++)
                            {
                                var types = sets[i].GetProp<ArrayProperty<ObjectProperty>>("Types");
                                if (types != null)
                                {
                                    res += $"SpawnSet {i}:\n";
                                    foreach (ObjectProperty v in types)
                                    {
                                        res += $"  {v.ResolveToEntry(export.FileRef)?.FullPath}\n";
                                    }
                                }
                            }
                        }
                        break;
                    case "SeqAct_ConsoleCommand":
                        var commands = properties.GetProp<ArrayProperty<StrProperty>>("Commands");
                        if (commands != null)
                        {
                            res += "Commands:\n";
                            foreach (StrProperty c in commands)
                            {
                                res += $"   {c.Value}\n";
                            }
                        }
                        break;
                    case "SeqAct_PlaySound":
                        var soundObjRef = properties.GetProp<ObjectProperty>("PlaySound");
                        if (soundObjRef != null)
                        {
                            res += export.FileRef.GetEntry(soundObjRef.Value)?.InstancedFullPath ?? "";
                        }
                        break;
                    case "BioSeqAct_SetWeapon":
                        //This might depend on game

                        //ME1:
                        var weaponEnum = properties.GetProp<EnumProperty>("eWeapon");
                        if (weaponEnum != null)
                        {
                            res += weaponEnum.Value;
                        }
                        break;
                    case "BioSeqAct_BlackScreen":
                        if (properties.GetProp<EnumProperty>("m_eBlackScreenAction") is { } blackScreenProp)
                        {
                            res += blackScreenProp.Value.Name.Split('_').Last();
                        }
                        break;
                    case "BioSeqVar_StoryManagerInt":
                        var intId = properties.GetProp<IntProperty>("m_nIndex");
                        if (intId != null)
                        {
                            res += PlotDatabases.FindPlotIntByID(intId, export.Game)?.Path ?? "";
                            res += "\n";
                        }
                        break;
                    case "BioSeqVar_StoryManagerFloat":
                        var floatId = properties.GetProp<IntProperty>("m_nIndex");
                        if (floatId != null)
                        {
                            res += PlotDatabases.FindPlotFloatByID(floatId, export.Game)?.Path ?? "";
                            res += "\n";
                        }
                        break;
                    case "BioSeqVar_StoryManagerStateId":
                    case "BioSeqVar_StoryManagerBool":
                        var boolId = properties.GetProp<IntProperty>("m_nIndex");
                        if (boolId != null)
                        {
                            res += PlotDatabases.FindPlotBoolByID(boolId, export.Game)?.Path ?? "";
                            res += "\n";
                        }
                        break;
                    case "SFXSeqAct_AwardGAWAsset":
                    case "SFXSeqAct_AwardGAWAsset_Silent":
                        var asset = properties.GetProp<StrProperty>("AssetName");
                        if (asset != null)
                        {
                            res += asset.ToString();
                            res += "\n";
                        }
                        break;
                }
            }
            return res;
        }

        protected static Color GetColor(VarTypes t)
        {
            return t switch
            {
                VarTypes.Int => IntColor,
                VarTypes.Float => FloatColor,
                VarTypes.Bool => BoolColor,
                VarTypes.Object => ObjectColor,
                VarTypes.MatineeData => InterpDataColor,
                VarTypes.String => StringColor,
                VarTypes.Vector => VectorColor,
                VarTypes.Rotator => RotatorColor,
                _ => Color.Black
            };
        }

        protected static VarTypes GetVarType(string s)
        {
            if (s.Contains("InterpData"))
                return VarTypes.MatineeData;
            if (s.Contains("Int"))
                return VarTypes.Int;
            if (s.Contains("Bool"))
                return VarTypes.Bool;
            if (s.Contains("Object") || s.Contains("Player"))
                return VarTypes.Object;
            if (s.Contains("Float"))
                return VarTypes.Float;
            if (s.Contains("StrRef"))
                return VarTypes.StrRef;
            if (s.Contains("String"))
                return VarTypes.String;
            if (s.Contains("Vector"))
                return VarTypes.Vector;
            if (s.Contains("Rotator"))
                return VarTypes.Rotator;
            return VarTypes.Extern;
        }

        public virtual void Dispose()
        {
            g = null;
            Pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("SVar | #{UIndex}: {export.ObjectName.Instanced}")]
    public sealed class SVar : SObj
    {
        public const float RADIUS = 30;

        public readonly List<VarEdge> Connections = new();
        public override IEnumerable<SeqEdEdge> Edges => Connections;
        private VarTypes Type { get; }
        private readonly SText val;
        private readonly PPath shape;
        public string Value
        {
            get => val.Text;
            set => val.Text = value;
        }

        public SVar(ExportEntry entry, SequenceGraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            string s = export.ObjectName;
            s = s.Replace("BioSeqVar_", "");
            s = s.Replace("SFXSeqVar_", "");
            s = s.Replace("SeqVar_", "");
            Type = GetVarType(s);
            const float w = RADIUS * 2;
            const float h = RADIUS * 2;
            shape = PPath.CreateEllipse(0, 0, w, h);
            OutlinePen = new Pen(GetColor(Type));
            shape.Pen = OutlinePen;
            shape.Brush = NodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue().Truncate(Settings.SequenceEditor_MaxVarStringLength, true))
            {
                Pickable = false,
                TextAlignment = StringAlignment.Center
            };
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            PropertyCollection props = export.GetProperties();
            foreach (Property prop in props)
            {
                if ((prop.Name == "VarName" || prop.Name == "varName")
                    && prop is NameProperty nameProp)
                {
                    var VarName = new SText(nameProp.Value.Instanced, Color.Red, false)
                    {
                        Pickable = false,
                        TextAlignment = StringAlignment.Center,
                        Y = h
                    };
                    VarName.X = w / 2 - VarName.Width / 2;
                    AddChild(VarName);
                    break;
                }
            }
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
        }

        private string GetValue()
        {
            const string unknownValue = "???";
            try
            {
                PropertyCollection props = export.GetProperties();
                switch (Type)
                {
                    case VarTypes.Int:
                        if (export.ClassName == "BioSeqVar_StoryManagerInt")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                AppendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Int\n#" + m_nIndex.Value;
                            }
                        }

                        if (export.ClassName == "SeqVar_RandomInt")
                        {
                            IntProperty minV = props.GetProp<IntProperty>("Min") ?? 0;
                            IntProperty maxV = props.GetProp<IntProperty>("Max") ?? 0;
                            return $"Rand({minV.Value},{maxV.Value})";
                        }
                        if (props.GetProp<IntProperty>("IntValue") is IntProperty intValue)
                        {
                            return intValue.Value.ToString();
                        }
                        return "0";
                    case VarTypes.Float:
                        if (export.ClassName == "BioSeqVar_StoryManagerFloat")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                AppendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Float\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<FloatProperty>("FloatValue") is FloatProperty floatValue)
                        {
                            return floatValue.Value.ToString();
                        }

                        if (export.ClassName == "SeqVar_RandomFloat" && props.GetProp<FloatProperty>("Min") is FloatProperty minVal &&
                            props.GetProp<FloatProperty>("Max") is FloatProperty maxVal)
                        {
                            return $"{minVal.Value}\nto\n{maxVal.Value}";
                        }
                        return "0.00";
                    case VarTypes.Bool:
                        if (export.ClassName == "BioSeqVar_StoryManagerBool")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                AppendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Bool\n#" + m_nIndex.Value;
                            }
                        }
                        if (props.GetProp<IntProperty>("bValue") is IntProperty bValue)
                        {
                            return (bValue.Value == 1).ToString();
                        }
                        return "False";
                    case VarTypes.Object:
                        if (export.ClassName == "SeqVar_Player")
                            return "Player";
                        foreach (Property prop in props)
                        {
                            switch (prop)
                            {
                                case NameProperty nameProp when nameProp.Name == "m_sObjectTagToFind":
                                    return nameProp.Value.Instanced;
                                case StrProperty strProp when strProp.Name == "m_sObjectTagToFind":
                                    return strProp.Value;
                                case ObjectProperty objProp when objProp.Name == "ObjValue":
                                    {
                                        IEntry entry = Pcc.GetEntry(objProp.Value);
                                        if (entry == null) return unknownValue;
                                        if (entry is ExportEntry objValueExport && objValueExport.GetProperty<NameProperty>("Tag") is NameProperty tagProp && tagProp.Value != objValueExport.ObjectName)
                                        {
                                            return $"#{entry.UIndex}\n{entry.ObjectName.Instanced}\n{ tagProp.Value}";
                                        }
                                        return $"#{entry.UIndex}\n{entry.ObjectName.Instanced}";
                                    }
                                case ArrayProperty<ObjectProperty> objList when objList.Name == "ObjList":
                                    {
                                        string text = $"ObjList: {objList.Count} item (s)";
                                        if (objList.Count > 0)
                                            text += $"\n0: {objList[0].ResolveToEntry(export.FileRef)?.ObjectName.Instanced}";
                                        if (objList.Count > 1)
                                            text += $"\n1: {objList[1].ResolveToEntry(export.FileRef)?.ObjectName.Instanced}";
                                        if (objList.Count > 2)
                                            text += "\n...";

                                        return text;
                                    }
                            }
                        }
                        return unknownValue;
                    case VarTypes.StrRef:
                        foreach (Property prop in props)
                        {
                            if (prop.Name == "m_srValue" || prop.Name == "m_srStringID")
                            {
                                if (prop is StringRefProperty strRefProp)
                                {
                                    return TLKManagerWPF.GlobalFindStrRefbyID(strRefProp.Value, export.FileRef.Game, export.FileRef);
                                }
                                else if (prop is IntProperty intProp)
                                {
                                    return TLKManagerWPF.GlobalFindStrRefbyID(intProp.Value, export.FileRef.Game, export.FileRef);
                                }
                            }
                        }
                        return unknownValue;
                    case VarTypes.String:
                        return props.GetProp<StrProperty>("StrValue")?.Value ?? unknownValue;
                    case VarTypes.Extern:
                        foreach (Property prop in props)
                        {
                            switch (prop)
                            {
                                //Named Variable
                                case NameProperty nameProp when nameProp.Name == "FindVarName":
                                    return $"< {nameProp.Value.Instanced} >";
                                //SeqVar_Name
                                case NameProperty nameProp when nameProp.Name == "NameValue":
                                    return nameProp.Value.Instanced;
                                //External
                                case StrProperty strProp when strProp.Name == "VariableLabel":
                                    return $"Extern:\n{strProp.Value}";
                            }
                        }
                        return unknownValue;
                    case VarTypes.MatineeData:
                        return $"#{UIndex}\n{Export.ObjectName.Instanced}";
                    case VarTypes.Vector:
                        if (props.GetProp<StructProperty>("VectValue") is { } vecStruct)
                        {
                            return CommonStructs.GetVector3(vecStruct).ToString();
                        }
                        return unknownValue;
                    case VarTypes.Rotator:
                        if (props.GetProp<StructProperty>("m_Rotator") is { } rotStruct)
                        {
                            return CommonStructs.GetRotator(rotStruct).ToString();
                        }
                        return unknownValue;

                    default:
                        return unknownValue;
                }
            }
            catch (Exception)
            {
                return unknownValue;
            }
        }

        private void AppendToComment(string s)
        {
            if (comment.Text.Length > 0)
            {
                comment.TranslateBy(0, -1 * comment.Height);
                comment.Text += s + "\n";
            }
            else
            {
                comment.Text += s + "\n";
                comment.TranslateBy(0, -1 * comment.Height);
            }
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
                    shape.Pen = SelectedPen;
                    MoveToFront();
                }
                else
                {
                    shape.Pen = OutlinePen;
                }
            }
        }

        public override bool Intersects(RectangleF bounds)
        {
            var ellipseRegion = new Region(shape.PathReference);
            return ellipseRegion.IsVisible(bounds);
        }

        private static void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = SelectedPen;
                DragTarget = (PNode)sender;
            }
        }

        private void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingVarlink)
            {
                ((PPath)((SVar)sender)[1]).Pen = OutlinePen;
                DragTarget = null;
            }
        }

        public override void Dispose()
        {
            g = null;
            Pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("SFrame | #{UIndex}: {export.ObjectName.Instanced}")]
    public sealed class SFrame : SObj
    {
        public SFrame(ExportEntry entry, SequenceGraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            float w = 0;
            float h = 0;
            PropertyCollection props = export.GetProperties();
            foreach (Property prop in props)
            {
                if (prop.Name == "SizeX")
                {
                    w = prop as IntProperty;
                }
                if (prop.Name == "SizeY")
                {
                    h = prop as IntProperty;
                }
            }
            PPath titleBox = MakeTitleBox(export.ObjectName.Instanced);
            var shape = PPath.CreateRectangle(0, -titleBox.Height, w, h + titleBox.Height);
            OutlinePen = new Pen(Color.Black);
            shape.Pen = OutlinePen;
            shape.Brush = new SolidBrush(Color.Transparent);
            shape.Pickable = false;
            AddChild(shape);
            titleBox.TranslateBy(0, -titleBox.Height);
            AddChild(titleBox);
            comment.Y -= titleBox.Height;
            Bounds = new RectangleF(0, -titleBox.Height, titleBox.Width, titleBox.Height);
        }

        public override void Dispose()
        {
            g = null;
            Pcc = null;
            export = null;
        }

        private PPath MakeTitleBox(string s)
        {
            s = $"#{UIndex} : {s}";
            var title = new SText(s, TitleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            title.Width += 20;
            var titleBox = PPath.CreateRectangle(0, 0, title.Width, title.Height + 5);
            titleBox.Pen = OutlinePen;
            titleBox.Brush = TitleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
            return titleBox;
        }
    }

    [DebuggerDisplay("SBox | #{UIndex}: {export.ObjectName.Instanced}")]
    public abstract class SBox : SObj
    {
        public override IEnumerable<SeqEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Cast<SeqEdEdge>()
                                                                .Union(Varlinks.SelectMany(l => l.Edges))
                                                                .Union(EventLinks.SelectMany(l => l.Edges));

        protected static readonly Brush OutputBrush = new SolidBrush(Color.Black);

        public struct OutputLink
        {
            public PPath Node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
            public List<ActionEdge> Edges;
        }

        public struct VarLink
        {
            public PPath Node;
            public List<int> Links;
            public string Desc;
            public List<VarEdge> Edges;
            public VarTypes Type;
        }

        public struct EventLink
        {
            public PPath Node;
            public List<int> Links;
            public string Desc;
            public List<EventEdge> Edges;
        }

        public struct InputLink
        {
            public PPath Node;
            public string Desc;
            public List<ActionEdge> Edges;
            public int Index;
            public bool HasName;
        }

        protected PPath TitleBox;
        protected PPath VarLinkBox;
        protected PPath OutLinkBox;
        public readonly List<OutputLink> Outlinks = new();
        public readonly List<VarLink> Varlinks = new();
        public readonly List<EventLink> EventLinks = new();
        private readonly VarDragHandler varDragHandler;
        private readonly OutputDragHandler outputDragHandler;
        private readonly EventDragHandler eventDragHandler;
        private static readonly PointF[] DownwardTrianglePoly = { new(-4, 0), new(4, 0), new(0, 10) };
        protected static PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);
        private static PPath CreateVarLinkBox() => PPath.CreateRectangle(-4, 0, 8, 10);

        protected SBox(ExportEntry entry, SequenceGraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            varDragHandler = new VarDragHandler(grapheditor, this);
            outputDragHandler = new OutputDragHandler(grapheditor, this);
            eventDragHandler = new EventDragHandler(grapheditor, this);
        }

        public override void CreateConnections(IList<SObj> objects)
        {
            foreach (OutputLink outLink in Outlinks)
            {
                for (int j = 0; j < outLink.Links.Count; j++)
                {
                    foreach (SAction destAction in objects.OfType<SAction>())
                    {
                        if (destAction.UIndex == outLink.Links[j])
                        {
                            PPath p1 = outLink.Node;
                            var edge = new ActionEdge();
                            p1.Tag ??= new List<ActionEdge>();
                            ((List<ActionEdge>)p1.Tag).Add(edge);
                            destAction.InputEdges.Add(edge);
                            edge.Start = p1;
                            edge.End = destAction;
                            edge.Originator = this;
                            edge.InputIndex = outLink.InputIndices[j];
                            g.addEdge(edge);
                            outLink.Edges.Add(edge);
                        }
                    }
                }
            }
            foreach (VarLink varLink in Varlinks)
            {
                foreach (int link in varLink.Links)
                {
                    foreach (SVar destVar in objects.OfType<SVar>())
                    {
                        if (destVar.UIndex == link)
                        {
                            PPath p1 = varLink.Node;
                            var edge = new VarEdge();
                            if (destVar.ChildrenCount > 1)
                                edge.Pen = ((PPath)destVar[1]).Pen;
                            p1.Tag ??= new List<VarEdge>();
                            ((List<VarEdge>)p1.Tag).Add(edge);
                            destVar.Connections.Add(edge);
                            edge.Start = p1;
                            edge.End = destVar;
                            edge.Originator = this;
                            g.addEdge(edge);
                            varLink.Edges.Add(edge);
                        }
                    }
                }
            }
            foreach (EventLink eventLink in EventLinks)
            {
                foreach (int link in eventLink.Links)
                {
                    foreach (SEvent destEvent in objects.OfType<SEvent>())
                    {
                        if (destEvent.UIndex == link)
                        {
                            PPath p1 = eventLink.Node;
                            var edge = new EventEdge
                            {
                                Pen = new Pen(EventColor),
                                Start = p1,
                                End = destEvent,
                                Originator = this
                            };
                            p1.Tag ??= new List<EventEdge>();
                            ((List<EventEdge>)p1.Tag).Add(edge);
                            destEvent.Connections.Add(edge);
                            g.addEdge(edge);
                            eventLink.Edges.Add(edge);
                        }
                    }
                }
            }
        }

        protected float GetTitleBox(string s, float w)
        {
            s = $"#{UIndex} : {s}";
            var title = new SText(s, TitleColor)
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
            TitleBox = PPath.CreateRectangle(0, 0, w, title.Height + 5);
            TitleBox.Pen = OutlinePen;
            TitleBox.Brush = TitleBoxBrush;
            TitleBox.AddChild(title);
            TitleBox.Pickable = false;
            return w;
        }

        protected void GetVarLinks()
        {
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (StructProperty prop in varLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linkedVars = props.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                    if (linkedVars != null)
                    {
                        var l = new VarLink
                        {
                            Links = new List<int>(),
                            Edges = new List<VarEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc"),
                            Type = GetVarType(Pcc.getObjectName(props.GetProp<ObjectProperty>("ExpectedType").Value))
                        };

                        // CROSSGEN - TRYING TO FIGURE OUT WHATS UP
                        //if (props.GetProp<NameProperty>("PropertyName").Value.Instanced is string str && str != l.Desc)
                        //{
                        //    l.Desc += $" (PN: {str})";
                        //}

                        // ENDCROSSGEN

                        foreach (ObjectProperty objProp in linkedVars)
                        {
                            l.Links.Add(objProp.Value);
                        }
                        PPath dragger;
                        if (props.GetProp<BoolProperty>("bWriteable").Value)
                        {
                            l.Node = PPath.CreatePolygon(DownwardTrianglePoly);
                            dragger = PPath.CreatePolygon(DownwardTrianglePoly);
                        }
                        else
                        {
                            l.Node = CreateVarLinkBox();
                            dragger = CreateVarLinkBox();
                        }
                        l.Node.Brush = new SolidBrush(GetColor(l.Type));
                        l.Node.Pen = new Pen(GetColor(l.Type));
                        l.Node.Pickable = false;
                        dragger.Brush = MostlyTransparentBrush;
                        dragger.Pen = l.Node.Pen;
                        dragger.X = l.Node.X;
                        dragger.Y = l.Node.Y;
                        dragger.AddInputEventListener(varDragHandler);
                        l.Node.AddChild(dragger);
                        Varlinks.Add(l);
                    }
                }
            }
        }

        protected void GetEventLinks()
        {
            var eventLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("EventLinks");
            if (eventLinksProp != null)
            {
                foreach (StructProperty prop in eventLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linkedEvents = props.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents");
                    if (linkedEvents != null)
                    {
                        var l = new EventLink
                        {
                            Links = new List<int>(),
                            Edges = new List<EventEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc"),
                            Node = CreateVarLinkBox()
                        };
                        l.Node.Brush = new SolidBrush(EventColor);
                        l.Node.Pen = new Pen(EventColor);
                        l.Node.Pickable = false;
                        foreach (ObjectProperty objProp in linkedEvents)
                        {
                            l.Links.Add(objProp.Value);
                        }
                        PPath dragger = CreateVarLinkBox();
                        dragger.Brush = MostlyTransparentBrush;
                        dragger.Pen = l.Node.Pen;
                        dragger.X = l.Node.X;
                        dragger.Y = l.Node.Y;
                        dragger.AddInputEventListener(eventDragHandler);
                        l.Node.AddChild(dragger);
                        EventLinks.Add(l);
                    }
                }
            }
        }

        protected void GetOutputLinks()
        {
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (StructProperty prop in outLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linksProp = props.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (linksProp != null)
                    {
                        var l = new OutputLink
                        {
                            Links = new List<int>(),
                            InputIndices = new List<int>(),
                            Edges = new List<ActionEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc")
                        };
                        foreach (StructProperty outputInputLink in linksProp)
                        {
                            l.Links.Add(outputInputLink.GetProp<ObjectProperty>("LinkedOp").Value);
                            l.InputIndices.Add(outputInputLink.GetProp<IntProperty>("InputLinkIdx"));
                        }
                        l.Node = CreateActionLinkBox();
                        l.Node.Brush = OutputBrush;
                        l.Node.Pickable = false;
                        PPath dragger = CreateActionLinkBox();
                        dragger.Brush = MostlyTransparentBrush;
                        dragger.X = l.Node.X;
                        dragger.Y = l.Node.Y;
                        dragger.AddInputEventListener(outputDragHandler);
                        l.Node.AddChild(dragger);
                        Outlinks.Add(l);
                    }
                }
            }
        }

        private sealed class OutputDragHandler : PDragEventHandler
        {
            private readonly SequenceGraphEditor graphEditor;
            private readonly SBox sObj;
            public OutputDragHandler(SequenceGraphEditor graph, SBox obj)
            {
                graphEditor = graph;
                sObj = obj;
            }
            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                sObj.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                var edge = new ActionEdge();
                p1.Tag ??= new List<ActionEdge>();
                p2.Tag ??= new List<ActionEdge>();
                ((List<ActionEdge>)p1.Tag).Add(edge);
                ((List<ActionEdge>)p2.Tag).Add(edge);
                edge.Start = p1;
                edge.End = p2;
                edge.Originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                SequenceGraphEditor.UpdateEdge(((List<ActionEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                ActionEdge edge = ((List<ActionEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<ActionEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                graphEditor.edgeLayer.RemoveChild(edge);
                ((List<ActionEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingOutlink = false;
                if (DragTarget != null)
                {
                    CreateOutlink(((PPath)sender).Parent, DragTarget);
                    DragTarget = null;
                }
            }
        }

        private sealed class VarDragHandler : PDragEventHandler
        {
            private readonly SequenceGraphEditor graphEditor;
            private readonly SBox sObj;
            public VarDragHandler(SequenceGraphEditor graph, SBox obj)
            {
                graphEditor = graph;
                sObj = obj;
            }

            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                sObj.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                var edge = new VarEdge();
                p1.Tag ??= new List<VarEdge>();
                p2.Tag ??= new List<VarEdge>();
                ((List<VarEdge>)p1.Tag).Add(edge);
                ((List<VarEdge>)p2.Tag).Add(edge);
                edge.Start = p1;
                edge.End = p2;
                edge.Originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingVarlink = true;

            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                SequenceGraphEditor.UpdateEdge(((List<VarEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                VarEdge edge = ((List<VarEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<VarEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                graphEditor.edgeLayer.RemoveChild(edge);
                ((List<VarEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingVarlink = false;
                if (DragTarget != null)
                {
                    CreateVarlink(((PPath)sender).Parent, (SVar)DragTarget);
                    DragTarget = null;
                }
            }
        }

        private sealed class EventDragHandler : PDragEventHandler
        {
            private readonly SequenceGraphEditor graphEditor;
            private readonly SBox sObj;
            public EventDragHandler(SequenceGraphEditor graph, SBox obj)
            {
                graphEditor = graph;
                sObj = obj;
            }

            public override bool DoesAcceptEvent(PInputEventArgs e)
            {
                return e.IsMouseEvent && (e.Button != MouseButtons.None || e.IsMouseEnterOrMouseLeave) && !e.Handled;
            }

            protected override void OnStartDrag(object sender, PInputEventArgs e)
            {
                sObj.MoveToBack();
                e.Handled = true;
                PNode p1 = ((PNode)sender).Parent;
                PNode p2 = (PNode)sender;
                var edge = new EventEdge();
                p1.Tag ??= new List<EventEdge>();
                p2.Tag ??= new List<EventEdge>();
                ((List<EventEdge>)p1.Tag).Add(edge);
                ((List<EventEdge>)p2.Tag).Add(edge);
                edge.Start = p1;
                edge.End = p2;
                edge.Originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingEventlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                SequenceGraphEditor.UpdateEdge(((List<EventEdge>)((PNode)sender).Tag)[0]);
            }

            protected override void OnEndDrag(object sender, PInputEventArgs e)
            {
                EventEdge edge = ((List<EventEdge>)((PNode)sender).Tag)[0];
                ((PNode)sender).SetOffset(0, 0);
                ((List<EventEdge>)((PNode)sender).Parent.Tag).Remove(edge);
                graphEditor.edgeLayer.RemoveChild(edge);
                ((List<EventEdge>)((PNode)sender).Tag).RemoveAt(0);
                base.OnEndDrag(sender, e);
                draggingEventlink = false;
                if (DragTarget != null)
                {
                    CreateEventlink(((PPath)sender).Parent, (SEvent)DragTarget);
                    DragTarget = null;
                }
            }
        }

        private static void CreateOutlink(PNode n1, PNode n2)
        {
            var start = (SBox)n1.Parent.Parent.Parent;
            var end = (SAction)n2.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (OutputLink l in start.Outlinks)
            {
                if (l.Node == n1)
                {
                    if (l.Links.Contains(end.UIndex))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            int inputIndex = -1;
            foreach (InputLink l in end.InLinks)
            {
                if (l.Node == n2)
                {
                    inputIndex = l.Index;
                }
            }
            if (inputIndex == -1)
                return;
            KismetHelper.CreateOutputLink(start.export, linkDesc, end.export, inputIndex);
        }


        private static void CreateVarlink(PNode p1, SVar end)
        {
            var start = (SBox)p1.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (VarLink l in start.Varlinks)
            {
                if (l.Node == p1)
                {
                    if (l.Links.Contains(end.UIndex))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            KismetHelper.CreateVariableLink(start.export, linkDesc, end.Export);
        }

        private static void CreateEventlink(PNode p1, SEvent end)
        {
            var start = (SBox)p1.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (EventLink l in start.EventLinks)
            {
                if (l.Node == p1)
                {
                    if (l.Links.Contains(end.UIndex))
                        return;
                    linkDesc = l.Desc;
                    break;
                }
            }
            if (linkDesc == null)
                return;
            KismetHelper.CreateEventLink(start.export, linkDesc, end.export);
        }
        public void RemoveOutlink(ActionEdge edge)
        {
            for (int i = 0; i < Outlinks.Count; i++)
            {
                OutputLink outLink = Outlinks[i];
                for (int j = 0; j < outLink.Edges.Count; j++)
                {
                    ActionEdge actionEdge = outLink.Edges[j];
                    if (actionEdge == edge)
                    {
                        RemoveOutlink(i, j);
                        return;
                    }
                }
            }
        }

        public void RemoveOutlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Outlinks[linkconnection].Desc;
            var outLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (StructProperty prop in outLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<StructProperty>>("Links").RemoveAt(linkIndex);
                        export.WriteProperty(outLinksProp);
                        return;
                    }
                }
            }
        }

        public void RemoveVarlink(VarEdge edge)
        {
            for (int i = 0; i < Varlinks.Count; i++)
            {
                VarLink varlink = Varlinks[i];
                for (int j = 0; j < varlink.Edges.Count; j++)
                {
                    VarEdge varEdge = varlink.Edges[j];
                    if (varEdge == edge)
                    {
                        RemoveVarlink(i, j);
                        return;
                    }
                }
            }
        }

        public void RemoveVarlink(int linkconnection, int linkIndex)
        {
            string linkDesc = Varlinks[linkconnection].Desc;
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").RemoveAt(linkIndex);
                        export.WriteProperty(varLinksProp);
                        return;
                    }
                }
            }
        }

        public void RemoveEventlink(EventEdge edge)
        {
            for (int i = 0; i < EventLinks.Count; i++)
            {
                EventLink eventLink = EventLinks[i];
                for (int j = 0; j < eventLink.Edges.Count; j++)
                {
                    EventEdge eventEdge = eventLink.Edges[j];
                    if (eventEdge == edge)
                    {
                        RemoveEventlink(i, j);
                        return;
                    }
                }
            }
        }

        public void RemoveEventlink(int linkconnection, int linkIndex)
        {
            string linkDesc = EventLinks[linkconnection].Desc;
            var eventLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("EventLinks");
            if (eventLinksProp != null)
            {
                foreach (StructProperty prop in eventLinksProp)
                {
                    if (prop.GetProp<StrProperty>("LinkDesc") == linkDesc)
                    {
                        prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents").RemoveAt(linkIndex);
                        export.WriteProperty(eventLinksProp);
                        return;
                    }
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (outputDragHandler != null)
            {
                foreach (OutputLink x in Outlinks) x.Node[0].RemoveInputEventListener(outputDragHandler);
            }
            if (varDragHandler != null)
            {
                foreach (VarLink x in Varlinks) x.Node[0].RemoveInputEventListener(varDragHandler);
            }

            if (eventDragHandler != null)
            {
                foreach (EventLink x in EventLinks) x.Node[0].RemoveInputEventListener(eventDragHandler);
            }
        }
    }

    [DebuggerDisplay("SEvent | #{UIndex}: {export.ObjectName.Instanced}")]
    public sealed class SEvent : SBox
    {
        public readonly List<EventEdge> Connections = new();
        public override IEnumerable<SeqEdEdge> Edges => Connections.Union(base.Edges);

        public SEvent(ExportEntry entry, SequenceGraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            OutlinePen = new Pen(EventColor);
            string s = export.ObjectName.Instanced;
            s = s.Replace("BioSeqEvt_", "");
            s = s.Replace("SFXSeqEvt_", "");
            s = s.Replace("SeqEvt_", "");
            s = s.Replace("SeqEvent_", "");
            float starty = 0;
            float w = 15;
            float midW = 0;
            VarLinkBox = new PPath();
            GetVarLinks();
            foreach (VarLink varLink in Varlinks)
            {
                string d = string.Join(",", varLink.Links.Select(l => $"#{l}"));
                var t2 = new SText(d + "\n" + varLink.Desc)
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                varLink.Node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(varLink.Node);
                VarLinkBox.AddChild(t2);
            }
            if (Varlinks.Count != 0)
                VarLinkBox.AddRectangle(0, 0, w, VarLinkBox[0].Height);
            VarLinkBox.Pickable = false;
            VarLinkBox.Pen = OutlinePen;
            VarLinkBox.Brush = NodeBrush;
            GetOutputLinks();
            OutLinkBox = new PPath();
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
                Outlinks[i].Node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].Node);
                OutLinkBox.AddChild(t2);
            }
            OutLinkBox.AddPolygon(new[] { new PointF(0, 0), new PointF(0, starty), new PointF(-0.5f * midW, starty + 30), new PointF(0 - midW, starty), new PointF(0 - midW, 0), new PointF(midW / -2, -30) });
            OutLinkBox.Pickable = false;
            OutLinkBox.Pen = OutlinePen;
            OutLinkBox.Brush = NodeBrush;
            PropertyCollection props = export.GetProperties();
            foreach (Property prop in props)
            {
                if (prop.Name.Name.Contains("EventName") || prop.Name == "sScriptName")
                    s += "\n\"" + (prop as NameProperty)?.Value.Instanced + "\"";
                else if (prop.Name == "InputLabel" || prop.Name == "sEvent")
                    s += "\n\"" + (prop as StrProperty) + "\"";
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                if (midW > tW)
                {
                    w = midW;
                    TitleBox.Width = w;
                }
                else
                {
                    w = tW;
                }
                VarLinkBox.Width = w;
            }
            float h = TitleBox.Height + 1;
            OutLinkBox.TranslateBy(TitleBox.Width / 2 + midW / 2, h + 30);
            h += OutLinkBox.Height + 1;
            VarLinkBox.TranslateBy(0, h);
            h += VarLinkBox.Height;
            bounds = new RectangleF(0, 0, w, h);
            AddChild(TitleBox);
            AddChild(VarLinkBox);
            AddChild(OutLinkBox);
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
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
                    TitleBox.Pen = SelectedPen;
                    VarLinkBox.Pen = SelectedPen;
                    OutLinkBox.Pen = SelectedPen;
                    MoveToFront();
                }
                else
                {
                    TitleBox.Pen = OutlinePen;
                    VarLinkBox.Pen = OutlinePen;
                    OutLinkBox.Pen = OutlinePen;
                }
            }
        }

        private static void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingEventlink)
            {
                ((SEvent)sender).IsSelected = true;
                DragTarget = (PNode)sender;
            }
        }

        private static void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingEventlink)
            {
                ((SEvent)sender).IsSelected = false;
                DragTarget = null;
            }
        }

        //public override bool Intersects(RectangleF bounds)
        //{
        //    Region hitregion = new Region(outLinkBox.PathReference);
        //    //hitregion.Union(titleBox.PathReference);
        //    //hitregion.Union(varLinkBox.PathReference);
        //    return hitregion.IsVisible(bounds);
        //}
    }

    [DebuggerDisplay("SAction | #{UIndex}: {export.ObjectName.Instanced}")]
    public sealed class SAction : SBox
    {
        public override IEnumerable<SeqEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public readonly List<ActionEdge> InputEdges = new();
        public List<InputLink> InLinks;
        private PNode inputLinkBox;
        private PPath box;

        private readonly InputDragHandler inputDragHandler = new();

        public SAction(ExportEntry entry, SequenceGraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            GetVarLinks();
            GetEventLinks();
            GetOutputLinks();
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
                    TitleBox.Pen = SelectedPen;
                    ((PPath)this[1]).Pen = SelectedPen;
                    MoveToFront();
                }
                else
                {
                    TitleBox.Pen = OutlinePen;
                    ((PPath)this[1]).Pen = OutlinePen;
                }
            }
        }

        public override void Layout(float x, float y)
        {
            OutlinePen = new Pen(Color.Black);
            string s = export.ObjectName.Instanced;
            s = s.Replace("BioSeqAct_", "").Replace("SFXSeqAct_", "")
                 .Replace("SeqAct_", "").Replace("SeqCond_", "");
            float starty = 8;
            float w = 20;
            VarLinkBox = new PPath();
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
                Varlinks[i].Node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(Varlinks[i].Node);
                VarLinkBox.AddChild(t2);
            }
            for (int i = 0; i < EventLinks.Count; i++)
            {
                string d = string.Join(",", EventLinks[i].Links.Select(l => $"#{l}"));
                var t2 = new SText($"{d}\n{EventLinks[i].Desc}")
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                EventLinks[i].Node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(EventLinks[i].Node);
                VarLinkBox.AddChild(t2);
            }
            if (Varlinks.Any() || EventLinks.Any())
                VarLinkBox.Height = VarLinkBox[0].Height;
            VarLinkBox.Width = w;
            VarLinkBox.Pickable = false;
            OutLinkBox = new PPath();
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
                Outlinks[i].Node.TranslateBy(0, t2.Y + t2.Height / 2);
                t2.AddChild(Outlinks[i].Node);
                OutLinkBox.AddChild(t2);
            }
            OutLinkBox.Pickable = false;
            inputLinkBox = new PNode();
            GetInputLinks();
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
                InLinks[i].Node.X = -10;
                InLinks[i].Node.Y = t2.Y + t2.Height / 2 - 5;
                t2.AddChild(InLinks[i].Node);
                inputLinkBox.AddChild(t2);
            }
            inputLinkBox.Pickable = false;
            if (inY > starty) starty = inY;
            if (inW + outW + 10 > w) w = inW + outW + 10;
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case ObjectProperty objProp when objProp.Name == "oSequenceReference":
                        {
                            string seqName = Pcc.GetEntry(objProp.Value)?.ObjectName ?? "";
                            if (Pcc.IsUExport(objProp.Value)
                                && seqName == "Sequence"
                                && Pcc.GetUExport(objProp.Value).GetProperty<StrProperty>("ObjName") is StrProperty objNameProp)
                            {
                                seqName = objNameProp;
                            }
                            s += $"\n\"{seqName}\"";
                            break;
                        }
                    case NameProperty nameProp when nameProp.Name == "EventName" || nameProp.Name == "StateName":
                        s += $"\n\"{nameProp.Value.Instanced}\"";
                        break;
                    case StrProperty strProp when strProp.Name == "OutputLabel" || strProp.Name == "m_sMovieName":
                        s += $"\n\"{strProp}\"";
                        break;
                    case ObjectProperty objProp when objProp.Name == "m_pEffect":
                        s += $"\n\"{Pcc.GetEntry(objProp.Value)?.ObjectName.Instanced ?? ""}\"";
                        break;
                }
            }
            float tW = GetTitleBox(s, w);
            if (tW > w)
            {
                w = tW;
                TitleBox.Width = w;
            }
            TitleBox.X = 0;
            TitleBox.Y = 0;
            float h = TitleBox.Height + 2;
            inputLinkBox.TranslateBy(0, h);
            OutLinkBox.TranslateBy(w, h);
            h += starty + 8;
            VarLinkBox.TranslateBy(VarLinkBox.Width < w ? (w - VarLinkBox.Width) / 2 : 0, h);
            h += VarLinkBox.Height;
            box = PPath.CreateRectangle(0, TitleBox.Height + 2, w, h - (TitleBox.Height + 2));
            box.Brush = NodeBrush;
            box.Pen = OutlinePen;
            box.Pickable = false;
            this.Bounds = new RectangleF(0, 0, w, h);
            this.AddChild(box);
            this.AddChild(TitleBox);
            this.AddChild(VarLinkBox);
            this.AddChild(OutLinkBox);
            this.AddChild(inputLinkBox);
            SetOffset(x, y);
        }

        private void GetInputLinks()
        {
            InLinks = new List<InputLink>();
            var inputLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (export.ClassName == "SequenceReference")
            {
                var oSequenceReference = export.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSequenceReference != null)
                {
                    int referencedIndex = oSequenceReference.Value;
                    if (Pcc.TryGetUExport(referencedIndex, out ExportEntry exportRef))
                    {
                        inputLinksProp = exportRef.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
                    }
                    else
                    {
                        var referencedFullPath = Pcc.GetEntry(referencedIndex).InstancedFullPath;
                        Debug.WriteLine($"Can't get input links of {referencedFullPath} because it is an import.");
                    }
                }
            }

            void CreateInputLink(string desc, int idx, bool hasName = true)
            {
                var l = new InputLink
                {
                    Desc = desc,
                    HasName = hasName,
                    Index = idx,
                    Node = CreateActionLinkBox(),
                    Edges = new List<ActionEdge>()
                };
                l.Node.Brush = OutputBrush;
                l.Node.MouseEnter += OnMouseEnter;
                l.Node.MouseLeave += OnMouseLeave;
                l.Node.AddInputEventListener(inputDragHandler);
                InLinks.Add(l);
            }

            if (inputLinksProp != null)
            {
                for (int i = 0; i < inputLinksProp.Count; i++)
                {
                    CreateInputLink(inputLinksProp[i].GetProp<StrProperty>("LinkDesc"), i);
                }
            }
            else
            {
                try
                {
                    if (GlobalUnrealObjectInfo.GetSequenceObjectInfoInputLinks(export.Game, export.ClassName) is List<string> inputLinks)
                    {
                        for (int i = 0; i < inputLinks.Count; i++)
                        {
                            CreateInputLink(inputLinks[i], i);
                        }
                    }
                }
                catch (Exception)
                {
                    InLinks.Clear();
                }
            }
            if (InputEdges.Any())
            {
                int numInputs = InLinks.Count;
                foreach (ActionEdge edge in InputEdges)
                {
                    int inputNum = edge.InputIndex;
                    //if there are inputs with an index greater than is accounted for by
                    //the current number of inputs, create enough inputs to fill up to that index
                    //With current toolset advances this is unlikely to occur, but no harm in leaving it in
                    if (inputNum + 1 > numInputs)
                    {
                        for (int i = numInputs; i <= inputNum; i++)
                        {
                            CreateInputLink($":{i}", i, false);
                        }
                        numInputs = inputNum + 1;
                    }
                    //change the end of the edge to the input box, not the SAction
                    if (inputNum >= 0)
                    {
                        edge.End = InLinks[inputNum].Node;
                        InLinks[inputNum].Edges.Add(edge);
                    }
                }
            }
        }

        private sealed class InputDragHandler : PDragEventHandler
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

        private static void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingOutlink)
            {
                ((PPath)sender).Pen = SelectedPen;
                DragTarget = (PPath)sender;
            }
        }

        private void OnMouseLeave(object sender, PInputEventArgs e)
        {
            ((PPath)sender).Pen = OutlinePen;
            DragTarget = null;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (inputDragHandler != null)
            {
                InLinks.ForEach(x => x.Node.RemoveInputEventListener(inputDragHandler));
            }
        }
    }

    public sealed class SText : PText
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private readonly Brush black = new SolidBrush(Color.Black);

        private readonly bool ShadowRendering;
        //Making Fontcollection a local variable causes the font to be unloaded
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private static readonly PrivateFontCollection Fontcollection;
        private static readonly Font KismetFont;

        public SText(string s, bool shadows = true, float scale = 1)
            : base(s)
        {
            TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            Font = KismetFont;
            GlobalScale = scale;
            ShadowRendering = shadows;
        }

        public SText(string s, Color c, bool shadows = true, float scale = 1)
            : base(s)
        {
            TextBrush = new SolidBrush(c);
            Font = KismetFont;
            GlobalScale = scale;
            ShadowRendering = shadows;
        }
        
        static SText()
        {
            Fontcollection = new PrivateFontCollection();
            byte[] fontData = EmbeddedResources.KismetFont;
            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            Fontcollection.AddMemoryFont(fontPtr, fontData.Length);
            uint tmp = 0;
            AddFontMemResourceEx(fontPtr, (uint)fontData.Length, IntPtr.Zero, ref tmp);
            Marshal.FreeCoTaskMem(fontPtr);
            KismetFont = new Font(Fontcollection.Families[0], 6, GraphicsUnit.Pixel);
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            //paints dropshadow
            if (ShadowRendering && paintContext.Scale >= 1 && Text != null && TextBrush != null && Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = FontSizeInPoints * paintContext.Scale;
                if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                {
                    RectangleF shadowbounds = Bounds;
                    shadowbounds.Offset(1, 1);
                    var stringformat = new StringFormat { Alignment = TextAlignment };
                    g.DrawString(Text, Font, black, shadowbounds, stringformat);
                }
            }
            base.Paint(paintContext);
        }
    }
}