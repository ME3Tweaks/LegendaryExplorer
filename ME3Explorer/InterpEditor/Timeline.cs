using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Util;
using ME3Explorer.Packages;

namespace ME3Explorer.InterpEditor
{
    /// <summary>
    /// Creates a timeline control, with tracks, track groups, and key frames
    /// </summary>
    public class Timeline : PCanvas
    {
        private IContainer components;
        private static int DEFAULT_WIDTH = 1;
        private static int DEFAULT_HEIGHT = 1;

        /// <summary>
        /// Empty Constructor is necessary so that this control can be used as an applet.
        /// </summary>
        public Timeline() : this(DEFAULT_WIDTH, DEFAULT_HEIGHT) { }

        public static float InfoHeight = 50;
        public static float TrackHeight = 24;
        public static float ListWidth = 200;

        private VScrollBar scrollbar;
        public VScrollBar Scrollbar
        {
            set
            {
                scrollbar = value;
                GroupList.Scrollbar = value;
            }
        }
        public InterpData GroupList;
        public PNode TimeLineInfo;
        public PLayer TimeLineView;
        protected bool setupDone = false;

        public Timeline(int width, int height)
        {
            InitializeComponent();
            DefaultRenderQuality = RenderQuality.LowQuality;
            this.Size = new Size(width, height);

            TimeLineView = this.Layer;
            Camera.AddLayer(TimeLineView);
            TimeLineView.MoveToBack();
            TimeLineView.Pickable = false;
            TimeLineView.Brush = new SolidBrush(Color.FromArgb(92, 92, 92));
            //BackColor = Color.FromArgb(60, 60, 60);

            GroupList = new InterpData();
            GroupList.Bounds = new RectangleF(0, 0, ListWidth, Camera.Bounds.Bottom - InfoHeight);
            GroupList.Brush = new SolidBrush(Color.FromArgb(60, 60, 60));
            Root.AddChild(GroupList);
            this.Camera.AddChild(GroupList);

            TimeLineInfo = new PNode();
            TimeLineInfo.Bounds = new RectangleF(0, Camera.Bounds.Bottom - InfoHeight, ListWidth, InfoHeight);
            TimeLineInfo.Brush = Brushes.Black;
            Root.AddChild(TimeLineInfo);
            this.Camera.AddChild(TimeLineInfo);

            RemoveInputEventListener(PanEventHandler);
            RemoveInputEventListener(ZoomEventHandler);
            setupDone = true;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (setupDone)
            {
                TimeLineView.SetBounds(ListWidth, 0, Camera.Width - GroupList.Width, Camera.Height);
                TimeLineInfo.Y = (int)Camera.Bounds.Bottom - InfoHeight;
                GroupList.Height = (int)Camera.Height - InfoHeight;
                GroupList.OnCameraChanged(Camera);
                if (scrollbar != null)
                {

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
                if (components != null)
                    components.Dispose();
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
            this.SuspendLayout();
            this.ResumeLayout(false);

        }
        #endregion

        // Draw a border for when this control is used as an applet.
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    public class InterpData : PNode
    {
        //private PPath seperationLine;
        private PPath endmark;
        private PNode TimelineView;
        private PPath TimeScale;
        private VScrollBar scrollbar;
        public VScrollBar Scrollbar
        {
            set
            {
                scrollbar = value;
                scrollbar.Scroll += scrollbar_Scroll;
            }
        }
        private HScrollBar scrollbarH;
        public HScrollBar ScrollbarH
        {
            set
            {
                scrollbarH = value;
                scrollbarH.Scroll += scrollbarH_Scroll;
                scrollbarH.Maximum = (int)TimelineView.Width;
            }
        }
        public TreeView tree1;
        public TreeView tree2;

        public ME3Package pcc;
        public int index;

        public List<InterpGroup> InterpGroups;
        public float InterpLength;
        public float EdSectionStart;
        public float EdSectionEnd;
        public int m_nBioCutSceneVersion;
        public int m_pSFXSceneData;
        public int ObjInstanceVersion;
        public int ParentSequence;

        public InterpData()
            : base()
        {
            InterpGroups = new List<InterpGroup>();
            TimelineView = new PNode();
            AddChild(TimelineView);
            TimelineView.MoveToBack();
            TimelineView.Pickable = false;
            //TimelineView.Brush = new SolidBrush(Color.FromArgb(60, 60, 60));
            TimelineView.SetBounds(0, 0, 3600, Height);
            TimelineView.TranslateBy(Timeline.ListWidth, 0);
            TimeScale = PPath.CreateRectangle(0, 0, 3600, Timeline.InfoHeight);
            TimeScale.TranslateBy(Timeline.ListWidth, 0);
            TimeScale.Pickable = false;
            TimeScale.Brush = new SolidBrush(Color.FromArgb(80, 80, 80));
            AddChild(TimeScale);
            TimeScale.MoveToFront();
            //seperationLine = PPath.CreateLine(Timeline.ListWidth, 0, Timeline.ListWidth, 10);
            //seperationLine.Pickable = false;
            //AddChild(seperationLine);
        }

        public void LoadInterpData(int idx, ME3Package pccobject)
        {
            TimeScale.RemoveAllChildren();
            TimeScale.Width = 3600;
            TimelineView.RemoveAllChildren();
            TimelineView.Width = 3600;
            scrollbarH.Maximum = 3600;
            PPath line;
            SText text;
            for (int i = 0; i < TimeScale.Width; i += 60)
            {
                line = PPath.CreateLine(i, 1, i, Timeline.InfoHeight);
                line.Pickable = false;
                line.Pen = new Pen(Color.FromArgb(110, 110, 110));
                TimeScale.AddChild(line);
                text = new SText(i / 60 - 1 + ".00", Color.FromArgb(175, 175, 175), false);
                text.Pickable = false;
                text.TranslateBy(i + 2, Timeline.InfoHeight - text.Height);
                TimeScale.AddChild(text);
            }

            pcc = pccobject;
            index = idx;
            foreach (InterpGroup g in InterpGroups)
                RemoveChild(g.listEntry);
            InterpGroups.Clear();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc.Exports[index]);
            List<int> groups = new List<int>();
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "InterpLength")
                    InterpLength = BitConverter.ToSingle(p.raw, 24);
                if (pcc.getNameEntry(p.Name) == "EdSectionStart")
                    EdSectionStart = BitConverter.ToSingle(p.raw, 24);
                if (pcc.getNameEntry(p.Name) == "EdSectionEnd")
                    EdSectionEnd = BitConverter.ToSingle(p.raw, 24);
                if (pcc.getNameEntry(p.Name) == "m_nBioCutSceneVersion")
                    m_nBioCutSceneVersion = p.Value.IntValue;
                if (pcc.getNameEntry(p.Name) == "m_pSFXSceneData")
                    m_pSFXSceneData = p.Value.IntValue;
                if (pcc.getNameEntry(p.Name) == "ObjInstanceVersion")
                    ObjInstanceVersion = p.Value.IntValue;
                if (pcc.getNameEntry(p.Name) == "ParentSequence")
                    ParentSequence = p.Value.IntValue;
                if (pcc.getNameEntry(p.Name) == "InterpGroups")
                {
                    for (int i = 0; i < p.Value.Array.Count; i += 4)
                        groups.Add(BitConverter.ToInt32(new byte[] { (byte)p.Value.Array[i].IntValue, (byte)p.Value.Array[i + 1].IntValue, (byte)p.Value.Array[i + 2].IntValue, (byte)p.Value.Array[i + 3].IntValue }, 0) - 1);
                }
            }
            foreach(int i in groups)
            {
                if (pcc.Exports[i].ClassName.StartsWith("InterpGroup"))
                    addGroup(new InterpGroup(i, pcc));
                else
                    addGroup(new SFXSceneGroup(i, pcc));
            }
            TimeScale.MoveToFront();
            PPath startmark = PPath.CreatePolygon(53,1, 61,1, 61,9);
            startmark.Pen = null;
            startmark.Brush = new SolidBrush(Color.FromArgb(255,80,80));
            startmark.Pickable = false;
            TimeScale.AddChild(startmark);
            endmark = PPath.CreatePolygon(InterpLength * 60 + 61, 1, InterpLength * 60 + 69, 1, InterpLength * 60 + 61, 9);
            endmark.Pen = null;
            endmark.Brush = startmark.Brush;
            TimeScale.AddChild(endmark);
            foreach (InterpGroup g in InterpGroups)
            {
                foreach (InterpTrack t in g.InterpTracks)
                {
                    t.GetKeyFrames();
                    t.DrawKeyFrames();
                    TimelineView.AddChild(t.timelineEntry);
                }
            }
        }

        public void addGroup(InterpGroup t)
        {
            t.PropView = tree1;
            t.KeyPropView = tree2;
            InterpGroups.Add(t);
            AddChild(t.listEntry);
        }

        public void OnCameraChanged(PCamera c)
        {

            TimelineView.Height = c.Height;
            TimeScale.OffsetY = c.Height - Timeline.InfoHeight;
            foreach (InterpGroup g in InterpGroups)
                g.OnCameraChanged(c);
        }

        public override void LayoutChildren()
        {
            int yOffset = 0;

            foreach (InterpGroup g in InterpGroups)
            {
                g.listEntry.OffsetY = yOffset;
                foreach (InterpTrack t in g.InterpTracks)
                    t.timelineEntry.OffsetY = yOffset + t.listEntry.OffsetY;
                yOffset += (int)Math.Round(g.EffectiveHeight);
            }
            //seperationLine.Y = yOffset;
            //seperationLine.Height = Bounds.Bottom - yOffset + Timeline.InfoHeight;
            if (scrollbar != null)
            {
                scrollbar.Maximum = yOffset;
            }
        }

        void scrollbarH_Scroll(object sender, ScrollEventArgs e)
        {
            TimelineView.OffsetX = -e.NewValue + Timeline.ListWidth;
            TimeScale.OffsetX = -e.NewValue + Timeline.ListWidth;
            if (e.NewValue > TimelineView.Width/2 && TimelineView.Width < InterpLength * 60 + 600)
            {
                TimelineView.Width += 600;
                TimeScale.Width += 600;
                scrollbarH.Maximum = (int)TimelineView.Width;
                SText text;
                PPath p;
                for (int i = (int)TimeScale.Width - 600; i < TimeScale.Width; i += 60)
                {
                    p = PPath.CreateLine(i, 0, i, Timeline.InfoHeight);
                    p.Pickable = false;
                    p.Pen = new Pen(Color.FromArgb(110, 110, 110));
                    TimeScale.AddChild(p);
                    text = new SText(i / 60 - 1 + ".00", Color.FromArgb(175, 175, 175), false);
                    text.Pickable = false;
                    text.TranslateBy(i, Timeline.InfoHeight - text.Height);
                    TimeScale.AddChild(text);
                }
            }

        }

        void scrollbar_Scroll(object sender, ScrollEventArgs e)
        {
            OffsetY = -e.NewValue;
            TimeScale.OffsetBy(0, e.NewValue - e.OldValue);
            Height += e.NewValue - e.OldValue;
        }
    }

    public class InterpGroup
    {
        private static Brush GroupBrush = new SolidBrush(Color.FromArgb(129,129,129));

        public PPath listEntry;
        protected SText title;
        protected Color groupColor;
        protected PNode colorAccent;
        protected bool collapsed;
        protected TreeView propView;
        public TreeView PropView
        {
            set
            {
                propView = value;
                foreach (InterpTrack t in InterpTracks)
                    t.propView = value;
            }
        }
        public TreeView keyPropView;
        public TreeView KeyPropView
        {
            set
            {
                keyPropView = value;
                foreach (InterpTrack t in InterpTracks)
                    t.keyPropView = value;
            }
        }
        
        public ME3Package pcc;
        public int index;

        public List<InterpTrack> InterpTracks;
        public List<int> GroupAnimSets;
        public int m_nmSFXFindActor;
        public string GroupName 
        {
            get
            {
                return title.Text;
            }
            set
            {
                if (value.Contains("\0"))
                    title.Text = value.Substring(0, value.Length - 1);
                else
                    title.Text = value;
            }
        }
        public int GroupColor
        {
            get
            { 
                return groupColor.ToArgb();
            }
            set
            {
                groupColor = Color.FromArgb(value);
                colorAccent.Brush = new SolidBrush(groupColor);
            }
        }
        public int BioForcedLodModel;
        public bool bCollapsed
        {
            get
            {
                return collapsed;
            }
            set
            {
                collapsed = value;
                foreach (InterpTrack t in InterpTracks)
                {
                    t.Visible = !value;

                }
            }
        }
        
        public bool bIsParented;
        public byteprop m_eSFXFindActorMode = new byteprop("ESFXFindByTagTypes", new string[] { "UseGroupActor", "FindActorByNode", "FindActorByTag" });

        public float EffectiveHeight
        {
            get
            {
                if (collapsed)
                    return listEntry.Bounds.Height + 1;
                else
                    return listEntry.FullBounds.Height;
            }
        }

        public InterpGroup(int idx, ME3Package pccobj)
            : base()
        {
            index = idx;
            pcc = pccobj;

            title = new SText("");
            if (pcc.Exports[index].ClassName == "InterpGroupDirector")
                GroupName = "DirGroup";
            listEntry = PPath.CreateRectangle(0, 0, Timeline.ListWidth, Timeline.TrackHeight);
            listEntry.Brush = GroupBrush;
            listEntry.Pen = null;
            PPath p = PPath.CreatePolygon(7,5, 12,10, 7,15);
            p.Brush = Brushes.Black;
            listEntry.AddChild(p);
            listEntry.AddChild(PPath.CreateLine(0, listEntry.Bounds.Bottom, Timeline.ListWidth, listEntry.Bounds.Bottom));
            colorAccent = new PNode();
            colorAccent.Brush = null;
            colorAccent.Bounds = new RectangleF(Timeline.ListWidth - 10, 0, 10, listEntry.Bounds.Bottom);
            listEntry.AddChild(colorAccent);
            title.TranslateBy(20, 3);
            listEntry.AddChild(title);
            listEntry.MouseDown += listEntry_MouseDown;
            collapsed = true;
            InterpTracks = new List<InterpTrack>();

            LoadData();
            if (bIsParented)
            {
                listEntry.TranslateBy(10, 0);
            }
        }

        protected void LoadData()
        {
            m_nmSFXFindActor = -1;

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc.Exports[index]);
            List<int> tracks = new List<int>();
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_nmSFXFindActor")
                    m_nmSFXFindActor = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "GroupName")
                    GroupName = pcc.getNameEntry(p.Value.IntValue);
                else if (pcc.getNameEntry(p.Name) == "GroupColor")
                    GroupColor = BitConverter.ToInt32(p.raw, 32);
                else if (pcc.getNameEntry(p.Name) == "BioForcedLodModel")
                    BioForcedLodModel = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "bCollapsed")
                    bCollapsed = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bIsParented")
                    bIsParented = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "m_eSFXFindActorMode")
                {
                    m_eSFXFindActorMode.set(p.raw);
                }
                else if (pcc.getNameEntry(p.Name) == "InterpTracks")
                {
                    for (int i = 0; i < p.Value.Array.Count; i += 4)
                        tracks.Add(BitConverter.ToInt32(new byte[] { (byte)p.Value.Array[i].IntValue, (byte)p.Value.Array[i + 1].IntValue, (byte)p.Value.Array[i + 2].IntValue, (byte)p.Value.Array[i + 3].IntValue }, 0) - 1);
                }
                else if (pcc.getNameEntry(p.Name) == "GroupAnimSets")
                {
                    GroupAnimSets = new List<int>();
                    for (int i = 0; i < p.Value.Array.Count; i += 4)
                        GroupAnimSets.Add(BitConverter.ToInt32(new byte[] { (byte)p.Value.Array[i].IntValue, (byte)p.Value.Array[i + 1].IntValue, (byte)p.Value.Array[i + 2].IntValue, (byte)p.Value.Array[i + 3].IntValue }, 0) - 1);
                }
            }
            foreach (int i in tracks)
            {
                switch (pcc.Exports[i].ClassName)
                {
                    case "BioInterpTrackMove":
                        addTrack(new BioInterpTrackMove(i, pcc));
                        break;
                    case "BioScalarParameterTrack":
                        addTrack(new BioScalarParameterTrack(i, pcc));
                        break;
                    case "BioEvtSysTrackInterrupt":
                        addTrack(new BioEvtSysTrackInterrupt(i, pcc));
                        break;
                    case "BioEvtSysTrackSubtitles":
                        addTrack(new BioEvtSysTrackSubtitles(i, pcc));
                        break;
                    case "BioEvtSysTrackSwitchCamera":
                        addTrack(new BioEvtSysTrackSwitchCamera(i, pcc));
                        break;
                    case "BioEvtSysTrackVOElements":
                        addTrack(new BioEvtSysTrackVOElements(i, pcc));
                        break;
                    case "BioInterpTrackRotationMode":
                        addTrack(new BioInterpTrackRotationMode(i, pcc));
                        break;
                    case "BioEvtSysTrackGesture":
                        addTrack(new BioEvtSysTrackGesture(i, pcc));
                        break;
                    case "BioEvtSysTrackLighting":
                        addTrack(new BioEvtSysTrackLighting(i, pcc));
                        break;
                    case "BioEvtSysTrackLookAt":
                        addTrack(new BioEvtSysTrackLookAt(i, pcc));
                        break;
                    case "BioEvtSysTrackProp":
                        addTrack(new BioEvtSysTrackProp(i, pcc));
                        break;
                    case "BioEvtSysTrackSetFacing":
                        addTrack(new BioEvtSysTrackSetFacing(i, pcc));
                        break;
                    case "SFXGameInterpTrackProcFoley":
                        addTrack(new SFXGameInterpTrackProcFoley(i, pcc));
                        break;
                    case "SFXInterpTrackPlayFaceOnlyVO":
                        addTrack(new SFXInterpTrackPlayFaceOnlyVO(i, pcc));
                        break;
                    case "SFXInterpTrackAttachCrustEffect":
                        addTrack(new SFXInterpTrackAttachCrustEffect(i, pcc));
                        break;
                    case "SFXInterpTrackAttachToActor":
                        addTrack(new SFXInterpTrackAttachToActor(i, pcc));
                        break;
                    case "SFXInterpTrackAttachVFXToObject":
                        addTrack(new SFXInterpTrackAttachVFXToObject(i, pcc));
                        break;
                    case "SFXInterpTrackBlackScreen":
                        addTrack(new SFXInterpTrackBlackScreen(i, pcc));
                        break;
                    case "SFXInterpTrackDestroy":
                        addTrack(new SFXInterpTrackDestroy(i, pcc));
                        break;
                    case "SFXInterpTrackForceLightEnvUpdate":
                        addTrack(new SFXInterpTrackForceLightEnvUpdate(i, pcc));
                        break;
                    case "SFXInterpTrackLightEnvQuality":
                        addTrack(new SFXInterpTrackLightEnvQuality(i, pcc));
                        break;
                    case "SFXInterpTrackMovieBink":
                        addTrack(new SFXInterpTrackMovieBink(i, pcc));
                        break;
                    case "SFXInterpTrackMovieTexture":
                        addTrack(new SFXInterpTrackMovieTexture(i, pcc));
                        break;
                    case "SFXInterpTrackSetPlayerNearClipPlane":
                        addTrack(new SFXInterpTrackSetPlayerNearClipPlane(i, pcc));
                        break;
                    case "SFXInterpTrackSetWeaponInstant":
                        addTrack(new SFXInterpTrackSetWeaponInstant(i, pcc));
                        break;
                    case "SFXInterpTrackToggleAffectedByHitEffects":
                        addTrack(new SFXInterpTrackToggleAffectedByHitEffects(i, pcc));
                        break;
                    case "SFXInterpTrackToggleHidden":
                        addTrack(new SFXInterpTrackToggleHidden(i, pcc));
                        break;
                    case "SFXInterpTrackToggleLightEnvironment":
                        addTrack(new SFXInterpTrackToggleLightEnvironment(i, pcc));
                        break;
                    case "SFXGameInterpTrackWwiseMicLock":
                        addTrack(new SFXGameInterpTrackWwiseMicLock(i, pcc));
                        break;
                    case "InterpTrackEvent":
                        addTrack(new InterpTrackEvent(i, pcc));
                        break;
                    case "InterpTrackFaceFX":
                        addTrack(new InterpTrackFaceFX(i, pcc));
                        break;
                    case "InterpTrackAnimControl":
                        addTrack(new InterpTrackAnimControl(i, pcc));
                        break;
                    case "InterpTrackMove":
                        addTrack(new InterpTrackMove(i, pcc));
                        break;
                    case "InterpTrackVisibility":
                        addTrack(new InterpTrackVisibility(i, pcc));
                        break;
                    case "InterpTrackToggle":
                        addTrack(new InterpTrackToggle(i, pcc));
                        break;
                    case "InterpTrackWwiseEvent":
                        addTrack(new InterpTrackWwiseEvent(i, pcc));
                        break;
                    case "InterpTrackWwiseSoundEffect":
                        addTrack(new InterpTrackWwiseSoundEffect(i, pcc));
                        break;
                    case "InterpTrackWwiseRTPC":
                        addTrack(new InterpTrackWwiseRTPC(i, pcc));
                        break;
                    case "InterpTrackVectorProp":
                        addTrack(new InterpTrackVectorProp(i, pcc));
                        break;
                    case "InterpTrackVectorMaterialParam":
                        addTrack(new InterpTrackVectorMaterialParam(i, pcc));
                        break;
                    case "InterpTrackColorProp":
                        addTrack(new InterpTrackColorProp(i, pcc));
                        break;
                    case "InterpTrackFloatProp":
                        addTrack(new InterpTrackFloatProp(i, pcc));
                        break;
                    case "InterpTrackFloatMaterialParam":
                        addTrack(new InterpTrackFloatMaterialParam(i, pcc));
                        break;
                    case "InterpTrackFloatParticleParam":
                        addTrack(new InterpTrackFloatParticleParam(i, pcc));
                        break;
                    case "SFXInterpTrackClientEffect":
                        addTrack(new SFXInterpTrackClientEffect(i, pcc));
                        break;
                    case "InterpTrackSound":
                        addTrack(new InterpTrackSound(i, pcc));
                        break;
                        //Director only ?
                    case "BioEvtSysTrackDOF":
                        addTrack(new BioEvtSysTrackDOF(i, pcc));
                        break;
                    case "InterpTrackDirector":
                        addTrack(new InterpTrackDirector(i, pcc));
                        break;
                    case "InterpTrackFade":
                        addTrack(new InterpTrackFade(i, pcc));
                        break;
                    case "InterpTrackColorScale":
                        addTrack(new InterpTrackColorScale(i, pcc));
                        break;
                    default:
                        MessageBox.Show(pcc.Exports[i].ClassName + " is not recognized.\nPlease make a bug report here: https://github.com/ME3Explorer/ME3Explorer/issues \nwith this information: #" + i + " " + pcc.fileName.Substring(pcc.fileName.LastIndexOf(@"\")));
                        break;
                }
            }
        }

        void listEntry_MouseDown(object sender, PInputEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                ToolStripMenuItem openInPCCEd = new ToolStripMenuItem("Open in Package Editor");
                openInPCCEd.Click += openInPCCEd_Click;
                menu.Items.AddRange(new ToolStripItem[] { openInPCCEd });
                menu.Show(Cursor.Position);
            }
            else
            {
                if (collapsed)
                {
                    listEntry[0].RotateInPlace(90);
                    listEntry[0].TranslateBy(5, 5);
                }
                else
                {
                    listEntry[0].TranslateBy(-5, -5);
                    listEntry[0].RotateInPlace(-90);
                }
                bCollapsed = !collapsed;
            }
            ToTree();
        }

        protected void openInPCCEd_Click(object sender, EventArgs e)
        {

            PackageEditor p = new PackageEditor();
            //p.MdiParent = Form.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.LoadFile(pcc.fileName);
            p.goToNumber(index);
        }

        public void addTrack(InterpTrack t)
        {
            InterpTracks.Add(t);
            t.listEntry.TranslateBy(0, (int)InterpTracks.Count * (Timeline.TrackHeight + 1));
            //t.listEntry.Pickable = false;
            t.listEntry.ChildrenPickable = false;
            if (collapsed)
            {
                t.Visible = false;
            }
            listEntry.AddChild(t.listEntry);
        }

        public void OnCameraChanged(PCamera c)
        {
            listEntry.Width = c.Width;
            listEntry[1].Width = c.Width;
            foreach (InterpTrack t in InterpTracks)
                t.listEntry[1].Width = c.Width;
        }

        public virtual void ToTree()
        {
            propView.Nodes.Clear();
            TreeNode t = new TreeNode("Group Name : \"" + GroupName + "\" (#" + index + " " + pcc.getClassName(index + 1) + ")");
            t.Name = "GroupName";
            propView.Nodes.Add(t);
            t = new TreeNode("GroupColor");
            t.Nodes.Add("Alpha: " + groupColor.A);
            t.Nodes.Add("Red: " + groupColor.R);
            t.Nodes.Add("Green: " + groupColor.G);
            t.Nodes.Add("Blue: " + groupColor.B);
            propView.Nodes.Add(t);
            propView.Nodes.Add("Find Actor Mode: " + m_eSFXFindActorMode.ToString(pcc));
            if (m_nmSFXFindActor != -1)
                propView.Nodes.Add("m_nmSFXFindActor : " + m_nmSFXFindActor);
        }
    }

    public class SFXSceneGroup : InterpGroup
    {
        public struct BioResourcePreloadItem
        {
	        public int pObject; //object
	        public int nKeyIndex;
            public float fTime;
            public bool bPreloadFired;

            public TreeNode ToTree(int index, ME3Package pcc)
            {
                TreeNode root = new TreeNode(index + ": " + fTime);
                root.Nodes.Add("pObject : " + pObject);
                root.Nodes.Add("nKeyIndex : " + nKeyIndex);
                root.Nodes.Add("fTime : " + fTime);
                root.Nodes.Add("bPreloadFired : " + bPreloadFired);
                return root;
            }
        }


        public List<BioResourcePreloadItem> m_aBioPreloadData = new List<BioResourcePreloadItem>();
        public float m_fSceneLength;
        public float m_fPlayRate = 1;

        public SFXSceneGroup(int idx, ME3Package pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            listEntry[0].RotateInPlace(90);
            listEntry[0].TranslateBy(5, 5);
            collapsed = false;
            //hack to undo the triangle rotation
            listEntry.MouseDown += listEntry_MouseDown;
        }

        protected void LoadData()
        {

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc.Exports[index]);
            List<int> tracks = new List<int>();
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_fSceneLength")
                    m_fSceneLength = BitConverter.ToSingle(p.raw, 24);
                else if (pcc.getNameEntry(p.Name) == "m_fPlayRate")
                    m_fPlayRate = BitConverter.ToSingle(p.raw, 24);
                else if (pcc.getNameEntry(p.Name) == "m_aBioPreloadData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        BioResourcePreloadItem key = new BioResourcePreloadItem();
                        for (int i = 0; i < p2.Count; i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "pObject")
                                key.pObject = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "nKeyIndex")
                                key.nKeyIndex = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "fTime")
                                key.fTime = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "bPreloadFired")
                                key.bPreloadFired = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aBioPreloadData.Add(key);
                    }
                }
            }
        }

        void listEntry_MouseDown(object sender, PInputEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                if (!collapsed)
                {
                    listEntry[0].RotateInPlace(90);
                    listEntry[0].TranslateBy(5, 5);
                }
                else
                {
                    listEntry[0].TranslateBy(-5, -5);
                    listEntry[0].RotateInPlace(-90);
                }
            }
        }

        public override void ToTree()
        {
            propView.Nodes.Clear();
            TreeNode t = new TreeNode("SFXSceneGroup: \"" + GroupName + "\" (#" + index + " " + pcc.getClassName(index + 1) + ")");
            t.Name = "GroupName";
            propView.Nodes.Add(t);
            t = new TreeNode("m_aBioPreloadData");
            for (int i = 0; i < m_aBioPreloadData.Count; i++)
                t.Nodes.Add(m_aBioPreloadData[i].ToTree(i, pcc));
            propView.Nodes.Add(t);
            propView.Nodes.Add("m_fSceneLength: " + m_fSceneLength);
            propView.Nodes.Add("m_fPlayRate : " + m_fPlayRate);
        }
    }
}