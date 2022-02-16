using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;
using System.Runtime.InteropServices;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Resources;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.SequenceObjects
{
    public enum VarTypes { Int, Bool, Object, Float, StrRef, MatineeData, Extern, String, Vector, Rotator };
    public abstract class SeqEdEdge : PPath
    {
        public PNode start;
        public PNode end;
        public SBox originator;
    }
    public class VarEdge : SeqEdEdge
    {
    }

    public class EventEdge : VarEdge
    {
    }

    [DebuggerDisplay("ActionEdge | {originator} to {inputIndex}")]
    public class ActionEdge : SeqEdEdge
    {
        public int inputIndex;
    }

    [DebuggerDisplay("SObj | #{UIndex}: {export.ObjectName.Instanced}")]
    public abstract class SObj : PNode, IDisposable
    {
        static readonly Color commentColor = Color.FromArgb(74, 63, 190);
        static readonly Color intColor = Color.FromArgb(34, 218, 218);//cyan
        static readonly Color floatColor = Color.FromArgb(23, 23, 213);//blue
        static readonly Color boolColor = Color.FromArgb(215, 37, 33); //red
        static readonly Color objectColor = Color.FromArgb(219, 39, 217);//purple
        static readonly Color interpDataColor = Color.FromArgb(222, 123, 26);//orange
        static readonly Color stringColor = Color.FromArgb(24, 219, 12);//lime green
        static readonly Color vectorColor = Color.FromArgb(127, 123, 32);//dark gold
        static readonly Color rotatorColor = Color.FromArgb(176, 97, 63);//burnt sienna
        protected static readonly Color EventColor = Color.FromArgb(214, 30, 28);
        protected static readonly Color titleColor = Color.FromArgb(255, 255, 128);
        protected static readonly Brush titleBoxBrush = new SolidBrush(Color.FromArgb(112, 112, 112));
        protected static readonly Brush mostlyTransparentBrush = new SolidBrush(Color.FromArgb(1, 255, 255, 255));
        protected static readonly Brush nodeBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        protected static readonly Pen selectedPen = new Pen(Color.FromArgb(255, 255, 0));
        public static bool draggingOutlink;
        public static bool draggingVarlink;
        public static bool draggingEventlink;
        public static PNode dragTarget;
        public static bool OutputNumbers;

        public IMEPackage pcc;
        public GraphEditor g;
        public RectangleF posAtDragStart;

        public int UIndex => export.UIndex;
        //public float Width { get { return shape.Width; } }
        //public float Height { get { return shape.Height; } }
        public ExportEntry Export => export;
        public virtual bool IsSelected { get; set; }

        protected ExportEntry export;
        protected Pen outlinePen;
        protected SText comment;

        public string Comment => comment.Text;

        protected SObj(ExportEntry entry, GraphEditor grapheditor)
        {
            pcc = entry.FileRef;
            export = entry;
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

        public virtual void CreateConnections(IList<SObj> objects) { }
        public virtual void Layout(float x, float y) => SetOffset(x, y);

        public virtual IEnumerable<SeqEdEdge> Edges => Enumerable.Empty<SeqEdEdge>();
        protected string GetComment()
        {
            string res = "";
            var comments = export.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
            if (comments != null)
            {
                foreach (var s in comments)
                {
                    res += s + "\n";
                }
            }

            if (Settings.SequenceEditor_ShowParsedInfo)
            {
                //Parse and show relevant info so user doesn't have to dig info file to find

                //I am sure there is a more efficient way to do this we will want to move to if we expand this feature
                var properties = export.GetProperties();
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
                                var defTipId = defaultTip?.Value;
                                if (defTipId != null)
                                {
                                    res +=
                                        $"\nDefaultTip: {TLKManagerWPF.GlobalFindStrRefbyID(defTipId.Value, export.FileRef).WordWrap(40)}";
                                }
                            }
                        }

                        break;
                    case "SeqEvent_Death":
                        var originator = properties.GetProp<ObjectProperty>("Originator");
                        if (originator != null && originator.Value != 0)
                        {
                            res += $"Originator: {export.FileRef.GetEntry(originator.Value).InstancedFullPath}";
                        }
                        break;
                    case "SFXSeqAct_AIFactory2":
                        var sets = properties.GetProp<ArrayProperty<StructProperty>>("SpawnSets");
                        if (sets != null)
                        {
                            foreach (var set in sets)
                            {
                                var types = set.GetProp<ArrayProperty<ObjectProperty>>("Types");
                                if (types != null)
                                {
                                    res += "SpawnSet 0:\n";
                                    foreach (var v in types)
                                    {
                                        res += $"  {v.ResolveToEntry(export.FileRef).FullPath}";
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
                            foreach (var c in commands)
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

        protected Color getColor(VarTypes t)
        {
            switch (t)
            {
                case VarTypes.Int:
                    return intColor;
                case VarTypes.Float:
                    return floatColor;
                case VarTypes.Bool:
                    return boolColor;
                case VarTypes.Object:
                    return objectColor;
                case VarTypes.MatineeData:
                    return interpDataColor;
                case VarTypes.String:
                    return stringColor;
                case VarTypes.Vector:
                    return vectorColor;
                case VarTypes.Rotator:
                    return rotatorColor;
                default:
                    return Color.Black;
            }
        }

        protected VarTypes getType(string s)
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
            pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("SVar | #{UIndex}: {export.ObjectName.Instanced}")]
    public class SVar : SObj
    {
        public const float RADIUS = 30;

        public List<VarEdge> connections = new();
        public override IEnumerable<SeqEdEdge> Edges => connections;
        public VarTypes type { get; set; }
        readonly SText val;
        protected PPath shape;
        public string Value
        {
            get => val.Text;
            set => val.Text = value;
        }

        public SVar(ExportEntry entry, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            string s = export.ObjectName;
            s = s.Replace("BioSeqVar_", "");
            s = s.Replace("SFXSeqVar_", "");
            s = s.Replace("SeqVar_", "");
            type = getType(s);
            const float w = RADIUS * 2;
            const float h = RADIUS * 2;
            shape = PPath.CreateEllipse(0, 0, w, h);
            outlinePen = new Pen(getColor(type));
            shape.Pen = outlinePen;
            shape.Brush = nodeBrush;
            shape.Pickable = false;
            AddChild(shape);
            Bounds = new RectangleF(0, 0, w, h);
            val = new SText(GetValue().Truncate(Settings.SequenceEditor_MaxVarStringLength, true));
            val.Pickable = false;
            val.TextAlignment = StringAlignment.Center;
            val.X = w / 2 - val.Width / 2;
            val.Y = h / 2 - val.Height / 2;
            AddChild(val);
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if ((prop.Name == "VarName" || prop.Name == "varName")
                    && prop is NameProperty nameProp)
                {
                    SText VarName = new SText(nameProp.Value.Instanced, Color.Red, false)
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
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        public string GetValue()
        {
            const string unknownValue = "???";
            try
            {
                var props = export.GetProperties();
                switch (type)
                {
                    case VarTypes.Int:
                        if (export.ClassName == "BioSeqVar_StoryManagerInt")
                        {
                            if (props.GetProp<StrProperty>("m_sRefName") is StrProperty m_sRefName)
                            {
                                appendToComment(m_sRefName);
                            }
                            if (props.GetProp<IntProperty>("m_nIndex") is IntProperty m_nIndex)
                            {
                                return "Plot Int\n#" + m_nIndex.Value;
                            }
                        }

                        if (export.ClassName == "SeqVar_RandomInt")
                        {
                            var minV = props.GetProp<IntProperty>("Min") ?? 0;
                            var maxV = props.GetProp<IntProperty>("Max") ?? 0;
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
                                appendToComment(m_sRefName);
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
                                appendToComment(m_sRefName);
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
                        foreach (var prop in props)
                        {
                            switch (prop)
                            {
                                case NameProperty nameProp when nameProp.Name == "m_sObjectTagToFind":
                                    return nameProp.Value.Instanced;
                                case StrProperty strProp when strProp.Name == "m_sObjectTagToFind":
                                    return strProp.Value;
                                case ObjectProperty objProp when objProp.Name == "ObjValue":
                                    {
                                        IEntry entry = pcc.GetEntry(objProp.Value);
                                        if (entry == null) return unknownValue;
                                        if (entry is ExportEntry objValueExport && objValueExport.GetProperty<NameProperty>("Tag") is NameProperty tagProp && tagProp.Value != objValueExport.ObjectName)
                                        {
                                            return $"#{entry.UIndex}\n{entry.ObjectName.Instanced}\n{ tagProp.Value}";
                                        }
                                        else
                                        {
                                            return $"#{entry.UIndex}\n{entry.ObjectName.Instanced}";
                                        }
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
                        foreach (var prop in props)
                        {
                            if ((prop.Name == "m_srValue" || prop.Name == "m_srStringID")
                                && prop is StringRefProperty strRefProp)
                            {
                                return TlkManagerNS.TLKManagerWPF.GlobalFindStrRefbyID(strRefProp.Value, export.FileRef.Game, export.FileRef);
                            }
                        }
                        return unknownValue;
                    case VarTypes.String:
                        var strValue = props.GetProp<StrProperty>("StrValue");
                        if (strValue != null)
                        {
                            return strValue.Value;
                        }
                        return unknownValue;
                    case VarTypes.Extern:
                        foreach (var prop in props)
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

        void appendToComment(string s)
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

        public override void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }
    }

    [DebuggerDisplay("SFrame | #{UIndex}: {export.ObjectName.Instanced}")]
    public class SFrame : SObj
    {
        protected PPath shape;
        protected PPath titleBox;
        public SFrame(ExportEntry entry, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            float w = 0;
            float h = 0;
            var props = export.GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "SizeX")
                {
                    w = (prop as IntProperty);
                }
                if (prop.Name == "SizeY")
                {
                    h = (prop as IntProperty);
                }
            }
            MakeTitleBox(export.ObjectName.Instanced);
            shape = PPath.CreateRectangle(0, -titleBox.Height, w, h + titleBox.Height);
            outlinePen = new Pen(Color.Black);
            shape.Pen = outlinePen;
            shape.Brush = new SolidBrush(Color.Transparent);
            shape.Pickable = false;
            this.AddChild(shape);
            titleBox.TranslateBy(0, -titleBox.Height);
            this.AddChild(titleBox);
            comment.Y -= titleBox.Height;
            this.Bounds = new RectangleF(0, -titleBox.Height, titleBox.Width, titleBox.Height);
        }

        public override void Dispose()
        {
            g = null;
            pcc = null;
            export = null;
        }

        protected void MakeTitleBox(string s)
        {
            s = $"#{UIndex} : {s}";
            var title = new SText(s, titleColor)
            {
                TextAlignment = StringAlignment.Center,
                ConstrainWidthToTextWidth = false,
                X = 0,
                Y = 3,
                Pickable = false
            };
            title.Width += 20;
            titleBox = PPath.CreateRectangle(0, 0, title.Width, title.Height + 5);
            titleBox.Pen = outlinePen;
            titleBox.Brush = titleBoxBrush;
            titleBox.AddChild(title);
            titleBox.Pickable = false;
        }
    }

    [DebuggerDisplay("SBox | #{UIndex}: {export.ObjectName.Instanced}")]
    public abstract class SBox : SObj
    {
        public override IEnumerable<SeqEdEdge> Edges => Outlinks.SelectMany(l => l.Edges).Cast<SeqEdEdge>()
                                                                .Union(Varlinks.SelectMany(l => l.Edges))
                                                                .Union(EventLinks.SelectMany(l => l.Edges));

        protected static Brush outputBrush = new SolidBrush(Color.Black);

        public struct OutputLink
        {
            public PPath node;
            public List<int> Links;
            public List<int> InputIndices;
            public string Desc;
            public List<ActionEdge> Edges;
        }

        public struct VarLink
        {
            public PPath node;
            public List<int> Links;
            public string Desc;
            public VarTypes type;
            public bool writeable;
            public List<VarEdge> Edges;
        }

        public struct EventLink
        {
            public PPath node;
            public List<int> Links;
            public string Desc;
            public List<EventEdge> Edges;
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
        public readonly List<OutputLink> Outlinks = new List<OutputLink>();
        public readonly List<VarLink> Varlinks = new List<VarLink>();
        public readonly List<EventLink> EventLinks = new List<EventLink>();
        protected readonly VarDragHandler varDragHandler;
        protected readonly OutputDragHandler outputDragHandler;
        protected readonly EventDragHandler eventDragHandler;
        private static readonly PointF[] downwardTrianglePoly = { new PointF(-4, 0), new PointF(4, 0), new PointF(0, 10) };
        protected PPath CreateActionLinkBox() => PPath.CreateRectangle(0, -4, 10, 8);
        protected PPath CreateVarLinkBox() => PPath.CreateRectangle(-4, 0, 8, 10);

        protected SBox(ExportEntry entry, GraphEditor grapheditor)
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
                            PPath p1 = outLink.node;
                            var edge = new ActionEdge();
                            if (p1.Tag == null)
                                p1.Tag = new List<ActionEdge>();
                            ((List<ActionEdge>)p1.Tag).Add(edge);
                            destAction.InputEdges.Add(edge);
                            edge.start = p1;
                            edge.end = destAction;
                            edge.originator = this;
                            edge.inputIndex = outLink.InputIndices[j];
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
                            PPath p1 = varLink.node;
                            var edge = new VarEdge();
                            if (destVar.ChildrenCount > 1)
                                edge.Pen = ((PPath)destVar[1]).Pen;
                            if (p1.Tag == null)
                                p1.Tag = new List<VarEdge>();
                            ((List<VarEdge>)p1.Tag).Add(edge);
                            destVar.connections.Add(edge);
                            edge.start = p1;
                            edge.end = destVar;
                            edge.originator = this;
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
                            PPath p1 = eventLink.node;
                            var edge = new EventEdge
                            {
                                Pen = new Pen(EventColor),
                                start = p1,
                                end = destEvent,
                                originator = this
                            };
                            if (p1.Tag == null)
                                p1.Tag = new List<EventEdge>();
                            ((List<EventEdge>)p1.Tag).Add(edge);
                            destEvent.connections.Add(edge);
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
            SText title = new SText(s, titleColor)
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

        protected void GetVarLinks()
        {
            var varLinksProp = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linkedVars = props.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                    if (linkedVars != null)
                    {
                        VarLink l = new VarLink
                        {
                            Links = new List<int>(),
                            Edges = new List<VarEdge>(),
                            Desc = props.GetProp<StrProperty>("LinkDesc"),
                            type = getType(pcc.getObjectName(props.GetProp<ObjectProperty>("ExpectedType").Value))
                        };

                        // CROSSGEN - TRYING TO FIGURE OUT WHATS UP
                        //if (props.GetProp<NameProperty>("PropertyName").Value.Instanced is string str && str != l.Desc)
                        //{
                        //    l.Desc += $" (PN: {str})";
                        //}

                        // ENDCROSSGEN

                        foreach (var objProp in linkedVars)
                        {
                            l.Links.Add(objProp.Value);
                        }
                        PPath dragger;
                        if (props.GetProp<BoolProperty>("bWriteable").Value)
                        {
                            l.node = PPath.CreatePolygon(downwardTrianglePoly);
                            dragger = PPath.CreatePolygon(downwardTrianglePoly);
                        }
                        else
                        {
                            l.node = CreateVarLinkBox();
                            dragger = CreateVarLinkBox();
                        }
                        l.node.Brush = new SolidBrush(getColor(l.type));
                        l.node.Pen = new Pen(getColor(l.type));
                        l.node.Pickable = false;
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.Pen = l.node.Pen;
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(varDragHandler);
                        l.node.AddChild(dragger);
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
                foreach (var prop in eventLinksProp)
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
                            node = CreateVarLinkBox()
                        };
                        l.node.Brush = new SolidBrush(EventColor);
                        l.node.Pen = new Pen(EventColor);
                        l.node.Pickable = false;
                        foreach (var objProp in linkedEvents)
                        {
                            l.Links.Add(objProp.Value);
                        }
                        PPath dragger = CreateVarLinkBox();
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.Pen = l.node.Pen;
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(eventDragHandler);
                        l.node.AddChild(dragger);
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
                foreach (var prop in outLinksProp)
                {
                    PropertyCollection props = prop.Properties;
                    var linksProp = props.GetProp<ArrayProperty<StructProperty>>("Links");
                    if (linksProp != null)
                    {
                        OutputLink l = new OutputLink
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
                        l.node = CreateActionLinkBox();
                        l.node.Brush = outputBrush;
                        l.node.Pickable = false;
                        PPath dragger = CreateActionLinkBox();
                        dragger.Brush = mostlyTransparentBrush;
                        dragger.X = l.node.X;
                        dragger.Y = l.node.Y;
                        dragger.AddInputEventListener(outputDragHandler);
                        l.node.AddChild(dragger);
                        Outlinks.Add(l);
                    }
                }
            }
        }

        protected class OutputDragHandler : PDragEventHandler
        {
            private readonly GraphEditor graphEditor;
            private readonly SBox sObj;
            public OutputDragHandler(GraphEditor graph, SBox obj)
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
                if (p1.Tag == null)
                    p1.Tag = new List<ActionEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<ActionEdge>();
                ((List<ActionEdge>)p1.Tag).Add(edge);
                ((List<ActionEdge>)p2.Tag).Add(edge);
                edge.start = p1;
                edge.end = p2;
                edge.originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingOutlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge(((List<ActionEdge>)((PNode)sender).Tag)[0]);
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
                if (dragTarget != null)
                {
                    sObj.CreateOutlink(((PPath)sender).Parent, dragTarget);
                    dragTarget = null;
                }
            }
        }

        protected class VarDragHandler : PDragEventHandler
        {
            private readonly GraphEditor graphEditor;
            private readonly SBox sObj;
            public VarDragHandler(GraphEditor graph, SBox obj)
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
                if (p1.Tag == null)
                    p1.Tag = new List<VarEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<VarEdge>();
                ((List<VarEdge>)p1.Tag).Add(edge);
                ((List<VarEdge>)p2.Tag).Add(edge);
                edge.start = p1;
                edge.end = p2;
                edge.originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingVarlink = true;

            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge(((List<VarEdge>)((PNode)sender).Tag)[0]);
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
                if (dragTarget != null)
                {
                    sObj.CreateVarlink(((PPath)sender).Parent, (SVar)dragTarget);
                    dragTarget = null;
                }
            }
        }

        protected class EventDragHandler : PDragEventHandler
        {
            private readonly GraphEditor graphEditor;
            private readonly SBox sObj;
            public EventDragHandler(GraphEditor graph, SBox obj)
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
                if (p1.Tag == null)
                    p1.Tag = new List<EventEdge>();
                if (p2.Tag == null)
                    p2.Tag = new List<EventEdge>();
                ((List<EventEdge>)p1.Tag).Add(edge);
                ((List<EventEdge>)p2.Tag).Add(edge);
                edge.start = p1;
                edge.end = p2;
                edge.originator = sObj;
                graphEditor.addEdge(edge);
                base.OnStartDrag(sender, e);
                draggingEventlink = true;
            }

            protected override void OnDrag(object sender, PInputEventArgs e)
            {
                base.OnDrag(sender, e);
                e.Handled = true;
                GraphEditor.UpdateEdge(((List<EventEdge>)((PNode)sender).Tag)[0]);
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
                if (dragTarget != null)
                {
                    sObj.CreateEventlink(((PPath)sender).Parent, (SEvent)dragTarget);
                    dragTarget = null;
                }
            }
        }

        public void CreateOutlink(PNode n1, PNode n2)
        {
            SBox start = (SBox)n1.Parent.Parent.Parent;
            SAction end = (SAction)n2.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (OutputLink l in start.Outlinks)
            {
                if (l.node == n1)
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
                if (l.node == n2)
                {
                    inputIndex = l.index;
                }
            }
            if (inputIndex == -1)
                return;
            KismetHelper.CreateOutputLink(start.export, linkDesc, end.export, inputIndex);
        }


        public void CreateVarlink(PNode p1, SVar end)
        {
            SBox start = (SBox)p1.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (VarLink l in start.Varlinks)
            {
                if (l.node == p1)
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

        public void CreateEventlink(PNode p1, SEvent end)
        {
            SBox start = (SBox)p1.Parent.Parent.Parent;
            string linkDesc = null;
            foreach (EventLink l in start.EventLinks)
            {
                if (l.node == p1)
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
                foreach (var prop in outLinksProp)
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
                foreach (var prop in eventLinksProp)
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
                foreach (var x in Outlinks) x.node[0].RemoveInputEventListener(outputDragHandler);
            }
            if (varDragHandler != null)
            {
                foreach (var x in Varlinks) x.node[0].RemoveInputEventListener(varDragHandler);
            }

            if (eventDragHandler != null)
            {
                foreach (var x in EventLinks) x.node[0].RemoveInputEventListener(eventDragHandler);
            }
        }
    }

    [DebuggerDisplay("SEvent | #{UIndex}: {export.ObjectName.Instanced}")]
    public class SEvent : SBox
    {
        public List<EventEdge> connections = new();
        public override IEnumerable<SeqEdEdge> Edges => connections.Union(base.Edges);

        public SEvent(ExportEntry entry, GraphEditor grapheditor)
            : base(entry, grapheditor)
        {
            outlinePen = new Pen(EventColor);
            string s = export.ObjectName.Instanced;
            s = s.Replace("BioSeqEvt_", "");
            s = s.Replace("SFXSeqEvt_", "");
            s = s.Replace("SeqEvt_", "");
            s = s.Replace("SeqEvent_", "");
            float starty = 0;
            float w = 15;
            float midW = 0;
            varLinkBox = new PPath();
            GetVarLinks();
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
            GetOutputLinks();
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
            var props = export.GetProperties();
            foreach (var prop in props)
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
                    titleBox.Pen = selectedPen;
                    varLinkBox.Pen = selectedPen;
                    outLinkBox.Pen = selectedPen;
                    MoveToFront();
                }
                else
                {
                    titleBox.Pen = outlinePen;
                    varLinkBox.Pen = outlinePen;
                    outLinkBox.Pen = outlinePen;
                }
            }
        }

        public void OnMouseEnter(object sender, PInputEventArgs e)
        {
            if (draggingEventlink)
            {
                ((SEvent)sender).IsSelected = true;
                dragTarget = (PNode)sender;
            }
        }

        public void OnMouseLeave(object sender, PInputEventArgs e)
        {
            if (draggingEventlink)
            {
                ((SEvent)sender).IsSelected = false;
                dragTarget = null;
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
    public class SAction : SBox
    {
        public override IEnumerable<SeqEdEdge> Edges => InLinks.SelectMany(l => l.Edges).Union(base.Edges);
        public List<ActionEdge> InputEdges = new();
        public List<InputLink> InLinks;
        protected PNode inputLinkBox;
        protected PPath box;

        protected InputDragHandler inputDragHandler = new();

        public SAction(ExportEntry entry, GraphEditor grapheditor)
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

        public override void Layout(float x, float y)
        {
            outlinePen = new Pen(Color.Black);
            string s = export.ObjectName.Instanced;
            s = s.Replace("BioSeqAct_", "").Replace("SFXSeqAct_", "")
                 .Replace("SeqAct_", "").Replace("SeqCond_", "");
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
            for (int i = 0; i < EventLinks.Count; i++)
            {
                string d = string.Join(",", EventLinks[i].Links.Select(l => $"#{l}"));
                SText t2 = new SText($"{d}\n{EventLinks[i].Desc}")
                {
                    X = w,
                    Y = 0,
                    Pickable = false
                };
                w += t2.Width + 20;
                EventLinks[i].node.TranslateBy(t2.X + t2.Width / 2, t2.Y + t2.Height);
                t2.AddChild(EventLinks[i].node);
                varLinkBox.AddChild(t2);
            }
            if (Varlinks.Any() || EventLinks.Any())
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
                SText t2 = new SText(linkDesc);
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
            for (int i = 0; i < InLinks.Count; i++)
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
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case ObjectProperty objProp when objProp.Name == "oSequenceReference":
                        {
                            string seqName = pcc.GetEntry(objProp.Value)?.ObjectName ?? "";
                            if (pcc.IsUExport(objProp.Value)
                                && seqName == "Sequence"
                                && pcc.GetUExport(objProp.Value).GetProperty<StrProperty>("ObjName") is StrProperty objNameProp)
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
                        s += $"\n\"{pcc.GetEntry(objProp.Value)?.ObjectName.Instanced ?? ""}\"";
                        break;
                }
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
                    if (pcc.TryGetUExport(referencedIndex, out var exportRef))
                    {
                        inputLinksProp = exportRef.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
                    }
                    else
                    {
                        var referencedFullPath = pcc.GetEntry(referencedIndex).InstancedFullPath;
                        Debug.WriteLine($"Can't get input links of {referencedFullPath} because it is an import.");
                    }
                }
            }

            void CreateInputLink(string desc, int idx, bool hasName = true)
            {
                var l = new InputLink
                {
                    Desc = desc,
                    hasName = hasName,
                    index = idx,
                    node = CreateActionLinkBox(),
                    Edges = new List<ActionEdge>()
                };
                l.node.Brush = outputBrush;
                l.node.MouseEnter += OnMouseEnter;
                l.node.MouseLeave += OnMouseLeave;
                l.node.AddInputEventListener(inputDragHandler);
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
                    int inputNum = edge.inputIndex;
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
                        edge.end = InLinks[inputNum].node;
                        InLinks[inputNum].Edges.Add(edge);
                    }
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

    public class SText : PText
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private readonly Brush black = new SolidBrush(Color.Black);
        public bool shadowRendering { get; set; }
        //Making fontcollection a local variable causes font to be unloaded
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private static readonly PrivateFontCollection fontcollection;
        private static readonly Font kismetFont;

        public SText(string s, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            base.Font = kismetFont;
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        public SText(string s, Color c, bool shadows = true, float scale = 1)
            : base(s)
        {
            base.TextBrush = new SolidBrush(c);
            base.Font = kismetFont;
            base.GlobalScale = scale;
            shadowRendering = shadows;
        }

        //Static constructor
        static SText()
        {
            fontcollection = new PrivateFontCollection();
            byte[] fontData = EmbeddedResources.KismetFont;
            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            fontcollection.AddMemoryFont(fontPtr, fontData.Length);
            uint tmp = 0;
            AddFontMemResourceEx(fontPtr, (uint)fontData.Length, IntPtr.Zero, ref tmp);
            Marshal.FreeCoTaskMem(fontPtr);
            kismetFont = new Font(fontcollection.Families[0], 6, GraphicsUnit.Pixel);
        }

        protected override void Paint(PPaintContext paintContext)
        {
            paintContext.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            //paints dropshadow
            if (shadowRendering && paintContext.Scale >= 1 && base.Text != null && base.TextBrush != null && base.Font != null)
            {
                Graphics g = paintContext.Graphics;
                float renderedFontSize = base.Font.SizeInPoints * paintContext.Scale;
                if (renderedFontSize >= PUtil.GreekThreshold && renderedFontSize < PUtil.MaxFontSize)
                {
                    RectangleF shadowbounds = Bounds;
                    shadowbounds.Offset(1, 1);
                    StringFormat stringformat = new StringFormat { Alignment = base.TextAlignment };
                    g.DrawString(base.Text, base.Font, black, shadowbounds, stringformat);
                }
            }
            base.Paint(paintContext);
        }
    }
}