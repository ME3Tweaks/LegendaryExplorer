using ME3Explorer.SequenceObjects;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Event;

namespace ME3Explorer.InterpEditor
{
    public struct byteprop
    {
        public byteprop(byte[] raw, string n, string[] list)
        {
            this.type = BitConverter.ToInt32(raw, 24);
            this.val = BitConverter.ToInt32(raw, 32);
            this.enumName = n;
            this.values = list;
        }
        public byteprop(string n, string[] list)
        {
            this.type = -1;
            this.val = -1;
            this.enumName = n;
            this.values = list;
        }
        public int type, val;
        public string enumName;
        public string[] values;
        public void set(byte[] raw)
        {
            type = BitConverter.ToInt32(raw, 24);
            val = BitConverter.ToInt32(raw, 32);
        }
        public string ToString(PCCObject p)
        {
            if (val == -1)
                return enumName + ", " + values[0];
            else
                return p.getNameEntry(type) + ", " + p.getNameEntry(val);
        }
    }

    public struct Vector
    {
        public float x;
        public float y;
        public float z;

        public TreeNode ToTree(string name)
        {
            TreeNode t = new TreeNode(name);
            t.Nodes.Add("X : " + x);
            t.Nodes.Add("Y : " + y);
            t.Nodes.Add("Z : " + z);
            return t;
        }
    }

    public struct Rotator
    {
        public int pitch;
        public int yaw;
        public int roll;

        public TreeNode ToTree(string name)
        {
            TreeNode t = new TreeNode(name);
            t.Nodes.Add("pitch : " + pitch);
            t.Nodes.Add("yaw : " + yaw);
            t.Nodes.Add("roll : " + roll);
            return t;
        }
    }

    public struct InterpCurvePointVector
    {
        public float InVal;
        public Vector OutVal;
        public Vector ArriveTangent;
        public Vector LeaveTangent;
        public byteprop InterpMode;

        public TreeNode ToTree(int index, PCCObject pcc)
        {
            TreeNode root = new TreeNode(index + " : " + InVal);
            root.Nodes.Add("InVal : " + InVal);
            root.Nodes.Add(OutVal.ToTree("OutVal"));
            root.Nodes.Add(ArriveTangent.ToTree("ArriveTangent"));
            root.Nodes.Add(LeaveTangent.ToTree("LeaveTangent"));
            root.Nodes.Add("InterpMode : " + InterpMode.ToString(pcc));
            return root;
        }
    }

    public struct InterpCurvePointFloat
    {
        public float InVal;
        public float OutVal;
        public float ArriveTangent;
        public float LeaveTangent;
        public byteprop InterpMode;

        public TreeNode ToTree(int index, PCCObject pcc)
        {
            TreeNode root = new TreeNode(index + " : " + InVal);
            root.Nodes.Add("InVal : " + InVal);
            root.Nodes.Add("OutVal : " + OutVal);
            root.Nodes.Add("ArriveTangent : " + ArriveTangent);
            root.Nodes.Add("LeaveTangent : " + LeaveTangent);
            root.Nodes.Add("InterpMode : " + InterpMode.ToString(pcc));
            return root;
        }
    }

    public struct InterpCurveVector //DistributionVectorConstantCurve
    {
        public List<InterpCurvePointVector> Points;

        public TreeNode ToTree(string name, PCCObject pcc)
        {
            TreeNode root = new TreeNode(name);
            TreeNode t = new TreeNode("Points");
            if (Points != null)
            {
                for (int i = 0; i < Points.Count; i++)
                {
                    t.Nodes.Add(Points[i].ToTree(i, pcc));
                } 
            }
            root.Nodes.Add(t);
            return root;
        }
    }

    public struct InterpCurveFloat //DistributionFloatConstantCurve
    {
        public List<InterpCurvePointFloat> Points;

        public TreeNode ToTree(string name, PCCObject pcc)
        {
            TreeNode root = new TreeNode(name);
            TreeNode t = new TreeNode("Points");
            if(Points != null)
            {
                for (int i = 0; i < Points.Count; i++)
                    t.Nodes.Add(Points[i].ToTree(i, pcc));
            }
            root.Nodes.Add(t);
            return root;
        }
    }

    public abstract class InterpTrack
    {
        private static Brush TrackListBrush = Brushes.DarkGray;
        private static Pen ChildLinePen = new Pen(Color.FromArgb(49, 49, 49));



        public TreeView propView;
        public TreeView keyPropView;
        public TalkFile talkfile;
        public PCCObject pcc;
        public int index;

        private SText title;
        public PPath listEntry;
        public PNode timelineEntry;
        public List<PPath> keys;

        public bool Visible
        {
            get
            {
                return listEntry.Visible;
            }
            set
            {
                listEntry.Visible = value;
                timelineEntry.Visible = value;
                listEntry.Pickable = value;
                timelineEntry.Pickable = value;
            }
        }

        public byteprop m_eFindActorMode = new byteprop("ESFXFindByTagTypes", new string[] { "UseGroupActor", "FindActorByNode", "FindActorByTag" });

        public byteprop ActiveCondition = new byteprop("ETrackActiveCondition", new string[] { "ETAC_Always", "ETAC_BioSingleHandWeapon", "ETAC_BioDualHandWeapon", "ETAC_BioMalePlayer", "ETAC_BioFemalePlayer", "ETAC_GoreEnabled" });

        public string TrackTitle
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

        public bool bImportedTrack;
        public bool bDisableTrack;

        public InterpTrack(int idx, PCCObject pccobj)
        {
            index = idx;
            pcc = pccobj;

            title = new SText("");
            listEntry = PPath.CreateRectangle(0, 0, Timeline.ListWidth, Timeline.TrackHeight);
            listEntry.Brush = TrackListBrush;
            listEntry.Pen = null;
            listEntry.MouseDown += listEntry_MouseDown;
            PPath p = PPath.CreateLine(9, 2, 9, 12);
            p.AddLine(9, 12, 31, 12);
            p.Brush = null;
            listEntry.AddChild(p);
            listEntry.AddChild(PPath.CreateLine(0, listEntry.Bounds.Bottom, Timeline.ListWidth, listEntry.Bounds.Bottom));
            title.TranslateBy(30, 3);
            listEntry.AddChild(title);
            timelineEntry = new PNode();
            //timelineEntry.Brush = Brushes.Green;
            LoadGenericData();
        }

        public void LoadGenericData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                
                if (pcc.getNameEntry(p.Name) == "TrackTitle")
                    TrackTitle = p.Value.StringValue;
                if (pcc.getNameEntry(p.Name) == "bImportedTrack")
                    bImportedTrack = p.Value.IntValue != 0;
                if (pcc.getNameEntry(p.Name) == "bDisableTrack")
                    bDisableTrack = p.Value.IntValue != 0;
                if (pcc.getNameEntry(p.Name) == "m_eFindActorMode")
                    m_eFindActorMode.set(p.raw);
                if (pcc.getNameEntry(p.Name) == "ActiveCondition")
                    ActiveCondition.set(p.raw);
            }
        }

        public virtual void GetKeyFrames()
        {
        }

        protected PPath GenerateKeyFrame(float time)
        {
            PPath p = PPath.CreatePolygon(-7, 7, 0, 0, 7, 7);
            //p.Pickable = false;
            p.Pen = null;
            p.Brush = new SolidBrush(Color.FromArgb(100, 0, 0));
            p.Tag = time;
            //p.MouseDown += p_MouseDown;
            return p;
        }

        public virtual void DrawKeyFrames()
        {
            foreach (PPath k in keys)
            {
                timelineEntry.AddChild(k);
                k.TranslateBy((float)k.Tag * 60 + 60, 13);
            }
            timelineEntry.Height = Timeline.TrackHeight;
            if (timelineEntry.ChildrenCount != 0)
                timelineEntry.Width = (float)timelineEntry[timelineEntry.ChildrenCount - 1].OffsetX + 10;
        }

        //key click handler
        //protected virtual void p_MouseDown(object sender, PInputEventArgs e)
        //{
        //    if (e.Button == System.Windows.Forms.MouseButtons.Right)
        //    {
        //        ContextMenuStrip menu = new ContextMenuStrip();
        //        ToolStripMenuItem setTime = new ToolStripMenuItem("SetTime");
        //        setTime.Click += setTime_Click;
        //        ToolStripMenuItem deleteKey = new ToolStripMenuItem("DeleteKey");
        //        deleteKey.Click += deleteKey_Click;
        //        menu.Items.AddRange(new ToolStripItem[] { setTime, deleteKey });
        //        menu.Show(Cursor.Position);
        //    }
        //}

        private void deleteKey_Click(object sender, EventArgs e)
        {
        }

        private void setTime_Click(object sender, EventArgs e)
        {
        }

        private void listEntry_MouseDown(object sender, PInputEventArgs e)
        {
            e.Handled = true;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                ToolStripMenuItem openInPCCEd = new ToolStripMenuItem("Open in PCCEditor2");
                openInPCCEd.Click += openInPCCEd_Click;
                menu.Items.AddRange(new ToolStripItem[] { openInPCCEd });
                menu.Show(Cursor.Position);
            }
            //Interpreter2.Interpreter2 ip = new Interpreter2.Interpreter2();
            //ip.pcc = pcc;
            //ip.Index = index;
            //ip.InitInterpreter(talkfile);
            //ip.Show();
            //propView.Nodes.Add(ip.Scan());
            //ip.Dispose();
            ToTree();
            AdditionalToTree();
        }

        private void openInPCCEd_Click(object sender, EventArgs e)
        {
            PCCEditor2 p = new PCCEditor2();
            //p.MdiParent = Form.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.pcc = new PCCObject(pcc.pccFileName);
            p.SetView(2);
            p.RefreshView();
            p.InitStuff();
            p.listBox1.SelectedIndex = index;
        }

        public virtual void ToTree()
        {
            propView.Nodes.Clear();
            AddToTree("Track Title : \"" + TrackTitle + "\" (#" + index + " " + pcc.getClassName(index + 1) + ")");
        }

        public void AdditionalToTree()
        {
            AddToTree("ActiveCondition:" + ActiveCondition.ToString(pcc));
            AddToTree("bDisableTrack: " + bDisableTrack);
            AddToTree("bImportedTrack: " + bImportedTrack);
        }

        #region helper methods

        public void AddToTree(string s)
        {
            propView.Nodes.Add(s);
        }

        public void AddToTree(TreeNode t)
        {
            propView.Nodes.Add(t);
        }

        public static InterpCurveVector GetCurveVector(PropertyReader.Property p, PCCObject pcc)
        {
            InterpCurveVector vec = new InterpCurveVector();
            vec.Points = new List<InterpCurvePointVector>();
            int pos = 60;
            int count = BitConverter.ToInt32(p.raw, 56);
            for (int j = 0; j < count; j++)
            {
                List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                InterpCurvePointVector point = new InterpCurvePointVector();
                for (int i = 0; i < p2.Count(); i++)
                {
                    if (pcc.getNameEntry(p2[i].Name) == "InVal")
                        point.InVal = BitConverter.ToSingle(p2[i].raw, 24);
                    else if (pcc.getNameEntry(p2[i].Name) == "OutVal")
                    {
                        point.OutVal.x = BitConverter.ToSingle(p2[i].raw, 32);
                        point.OutVal.y = BitConverter.ToSingle(p2[i].raw, 36);
                        point.OutVal.z = BitConverter.ToSingle(p2[i].raw, 40);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "ArriveTangent")
                    {
                        point.ArriveTangent.x = BitConverter.ToSingle(p2[i].raw, 32);
                        point.ArriveTangent.y = BitConverter.ToSingle(p2[i].raw, 36);
                        point.ArriveTangent.z = BitConverter.ToSingle(p2[i].raw, 40);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "LeaveTangent")
                    {
                        point.LeaveTangent.x = BitConverter.ToSingle(p2[i].raw, 32);
                        point.LeaveTangent.y = BitConverter.ToSingle(p2[i].raw, 36);
                        point.LeaveTangent.z = BitConverter.ToSingle(p2[i].raw, 40);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "InterpMode")
                        point.InterpMode = new byteprop(p2[i].raw, "EInterpCurveMode", new string[] { "CIM_Linear", "CIM_CurveAuto", "CIM_Constant", "CIM_CurveUser", "CIM_CurveBreak", "CIM_CurveAutoClamped"});
                    pos += p2[i].raw.Length;
                }
                vec.Points.Add(point);
            }
            return vec;
        }

        public static InterpCurveFloat GetCurveFloat(PropertyReader.Property p, PCCObject pcc)
        {
            InterpCurveFloat CurveFloat = new InterpCurveFloat();
            CurveFloat.Points = new List<InterpCurvePointFloat>();
            int pos = 60;
            int count = BitConverter.ToInt32(p.raw, 56);
            for (int j = 0; j < count; j++)
            {
                List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                InterpCurvePointFloat point = new InterpCurvePointFloat();
                for (int i = 0; i < p2.Count(); i++)
                {
                    if (pcc.getNameEntry(p2[i].Name) == "InVal")
                        point.InVal = BitConverter.ToSingle(p2[i].raw, 24);
                    else if (pcc.getNameEntry(p2[i].Name) == "OutVal")
                    {
                        point.OutVal = BitConverter.ToSingle(p2[i].raw, 24);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "ArriveTangent")
                    {
                        point.ArriveTangent = BitConverter.ToSingle(p2[i].raw, 24);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "LeaveTangent")
                    {
                        point.LeaveTangent = BitConverter.ToSingle(p2[i].raw, 24);
                    }
                    else if (pcc.getNameEntry(p2[i].Name) == "InterpMode")
                        point.InterpMode = new byteprop(p2[i].raw, "EInterpCurveMode", new string[] { "CIM_Linear", "CIM_CurveAuto", "CIM_Constant", "CIM_CurveUser", "CIM_CurveBreak", "CIM_CurveAutoClamped" });
                    pos += p2[i].raw.Length;
                }
                CurveFloat.Points.Add(point);
            }
            return CurveFloat;
        }

        #endregion helper methods
    }

    public abstract class BioInterpTrack : InterpTrack
    {
        public struct TrackKey
        {
            public NameReference KeyName;
            public float fTime;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + " : " + fTime);
                root.Nodes.Add("KeyName : " + KeyName.Name);
                root.Nodes.Add("fTime : " + fTime);
                return root;
            }
        }

        public List<TrackKey> m_aTrackKeys;

        public BioInterpTrack(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            GetKeyFrames();
        }

        public void LoadData()
        {   //default values
            m_aTrackKeys = new List<TrackKey>();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aTrackKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        TrackKey key = new TrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "KeyName")
                                key.KeyName = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "fTime")
                                key.fTime = BitConverter.ToSingle(p2[i].raw, 24);
                            pos += p2[i].raw.Length;
                        }
                        m_aTrackKeys.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (m_aTrackKeys != null)
                foreach (TrackKey k in m_aTrackKeys)
                    keys.Add(GenerateKeyFrame(k.fTime));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aTrackKeys");
            for (int i = 0; i < m_aTrackKeys.Count; i++)
                t.Nodes.Add(m_aTrackKeys[i].ToTree(i, pcc));
            AddToTree(t);
        }
    }

    public abstract class SFXGameActorInterpTrack : BioInterpTrack
    {
        public NameReference m_nmFindActor;

        public SFXGameActorInterpTrack(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
        }

        public void LoadData()
        {   //default values
            m_nmFindActor = new NameReference();
            m_nmFindActor.index = -1;

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_nmFindActor")
                    m_nmFindActor = p.Value.NameValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (m_nmFindActor.index != -1)
                AddToTree("m_nmFindActor : " + m_nmFindActor.Name);
            AddToTree("m_eFindActorMode : " + m_eFindActorMode.ToString(pcc));
        }
    }

    public abstract class SFXInterpTrackMovieBase : BioInterpTrack
    {
        public struct MovieKey
        {
            public int PlaceHolder;
            public byteprop m_eState;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + " : " + time);
                root.Nodes.Add("PlaceHolder : " + PlaceHolder);
                root.Nodes.Add("m_eState : " + m_eState.ToString(pcc));
                return root;
            }
        }

        public List<MovieKey> m_aMovieKeyData;

        public SFXInterpTrackMovieBase(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
        }

        public void LoadData()
        {   //default values
            m_aMovieKeyData = new List<MovieKey>();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aMovieKeyData")
                {
                    m_aMovieKeyData = new List<MovieKey>();
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        MovieKey key = new MovieKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "PlaceHolder")
                                key.PlaceHolder = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "m_eState")
                                key.m_eState = new byteprop(p2[i].raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aMovieKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aMovieKeyData");
            for (int i = 0; i < m_aMovieKeyData.Count; i++)
                t.Nodes.Add(m_aMovieKeyData[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public abstract class SFXInterpTrackToggleBase : BioInterpTrack
    {
        public struct ToggleKey
        {
            public bool m_bToggle;
            public bool m_bEnable;

            public TreeNode ToTree(int index, float time)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("m_bToggle : " + m_bToggle);
                root.Nodes.Add("m_bEnable : " + m_bEnable);
                return root;
            }
        }

        public List<ToggleKey> m_aToggleKeyData;
        public List<int> m_aTarget;
        public int m_TargetActor;  

        public SFXInterpTrackToggleBase(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
        }

        public void LoadData()
        {   //default values
            m_aToggleKeyData = new List<ToggleKey>();
            m_aTarget = new List<int>();         

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aTarget")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aTarget.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
                else if (pcc.getNameEntry(p.Name) == "m_TargetActor")
                    m_TargetActor = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aToggleKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        ToggleKey key = new ToggleKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "m_bToggle")
                                key.m_bToggle = p2[i].Value.IntValue != 0;
                            if (pcc.getNameEntry(p2[i].Name) == "m_bEnable")
                                key.m_bEnable = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aToggleKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aToggleKeyData");
            for (int i = 0; i < m_aToggleKeyData.Count; i++)
                t.Nodes.Add(m_aToggleKeyData[i].ToTree(i, m_aTrackKeys[i].fTime));
            AddToTree(t);
            t = new TreeNode("m_aTarget");
            if (m_aTarget != null)
            {
                for (int i = 0; i < m_aTarget.Count; i++)
                    t.Nodes.Add(m_aTarget[i].ToString()); 
            }
            AddToTree(t);
            AddToTree("m_TargetActor : " + pcc.getObjectName(m_TargetActor) + " (" + m_TargetActor + ")");
        }
    }

    public abstract class InterpTrackFloatBase : InterpTrack
    {
        public InterpCurveFloat FloatTrack;
        public float CurveTension;

        public InterpTrackFloatBase(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            GetKeyFrames();
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "FloatTrack")
                    FloatTrack = GetCurveFloat(p, pcc);
                if(pcc.getNameEntry(p.Name) == "CurveTension")
                    CurveTension = BitConverter.ToSingle(p.raw, 24);
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (FloatTrack.Points != null)
                foreach (InterpCurvePointFloat e in FloatTrack.Points)
                    keys.Add(GenerateKeyFrame(e.InVal));
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree(FloatTrack.ToTree("Float Track", pcc));
            AddToTree("CurveTension: " + CurveTension);
        }
    }

    public abstract class InterpTrackVectorBase : InterpTrack
    {
        public InterpCurveVector VectorTrack;

        public InterpTrackVectorBase(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            GetKeyFrames();
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "VectorTrack")
                    VectorTrack = GetCurveVector(p, pcc);
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (VectorTrack.Points != null)
                foreach (InterpCurvePointVector e in VectorTrack.Points)
                    keys.Add(GenerateKeyFrame(e.InVal));
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree(VectorTrack.ToTree("Vector Track", pcc));
        }
    }

    public class BioInterpTrackMove : InterpTrackMove
    {
        public NameReference FacingController;

        public BioInterpTrackMove(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Bio Movement";
        }

        public void LoadData()
        {   //default values
            FacingController = new NameReference();
            FacingController.index = -1;
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "FacingController")
                    FacingController = p.Value.NameValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (FacingController.index != -1)
                AddToTree("FacingController : " + FacingController.Name);
        }
    }

    public class BioScalarParameterTrack : InterpTrackFloatBase
    {
        public float InterpValue; //unused?
        public int PropertyName;
        public int m_pParentEffect; //unused?

        public BioScalarParameterTrack(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Scalar Param";
        }

        public void LoadData()
        {   //default values
            m_pParentEffect = 0;
            InterpValue = 0;
            PropertyName = -1;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "InterpValue")
                    InterpValue = BitConverter.ToSingle(p.raw, 24);
                else if (pcc.getNameEntry(p.Name) == "PropertyName")
                    PropertyName = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_pParentEffect")
                    m_pParentEffect = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("PropertyName : " + PropertyName);
            //propView.Nodes.Add(new TreeNode("InterpValue : " + InterpValue));
            //propView.Nodes.Add(new TreeNode("m_pParentEffect : " + m_pParentEffect));
        }
    }

    public class BioEvtSysTrackInterrupt : BioInterpTrack
    {
        public struct InterruptKey
        {
            public bool bShowInterrupt;

            public TreeNode ToTree(int index, float time)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(new TreeNode("bShowInterrupt : " + bShowInterrupt));
                return root;
            }
        }

        public List<InterruptKey> m_aInterruptData;

        public BioEvtSysTrackInterrupt(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Interrupt";
        }

        public void LoadData()
        {   //default values
            m_aInterruptData = new List<InterruptKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aInterruptData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        InterruptKey key = new InterruptKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "bShowInterrupt")
                                key.bShowInterrupt = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aInterruptData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aInterruptData");
            for (int i = 0; i < m_aInterruptData.Count; i++)
                t.Nodes.Add(m_aInterruptData[i].ToTree(i, m_aTrackKeys[i].fTime));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackSubtitles : BioInterpTrack
    {
        public struct SubtitleKey
        {
            public int nStrRefID;
            public float fLength;
            public bool bShowAtTop;
            public bool bUseOnlyAsReplyWheelHint;

            public TreeNode ToTree(int index, float time, PCCObject pcc, TalkFile tlk)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("nStrRefID : " + tlk.findDataById(nStrRefID) + " (" + nStrRefID + ")");
                root.Nodes.Add("fLength : " + fLength);
                root.Nodes.Add("bShowAtTop : " + bShowAtTop);
                root.Nodes.Add("bUseOnlyAsReplyWheelHint : " + bUseOnlyAsReplyWheelHint);
                return root;
            }
        }

        public List<SubtitleKey> m_aSubtitleData;

        public BioEvtSysTrackSubtitles(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Subtitles";
            GetKeyFrames();
        }

        public void LoadData()
        {   //default values
            m_aSubtitleData = new List<SubtitleKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aSubtitleData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        SubtitleKey key = new SubtitleKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "nStrRefID")
                                key.nStrRefID = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "fLength")
                                key.fLength = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "bShowAtTop")
                                key.bShowAtTop = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bUSeOnlyAsReplyWheelHint")
                                key.bUseOnlyAsReplyWheelHint = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aSubtitleData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aSubtitleData");
            for (int i = 0; i < m_aSubtitleData.Count; i++)
                t.Nodes.Add(m_aSubtitleData[i].ToTree(i, m_aTrackKeys[i].fTime, pcc, talkfile));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackSwitchCamera : BioInterpTrack
    {
        public struct CameraSwitchKey
        {
            public NameReference nmStageSpecificCam;
            public bool bForceCrossingLineOfAction;
            public bool bUseForNextCamera;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(new TreeNode("nmStageSpecificCam : " + nmStageSpecificCam.Name));
                root.Nodes.Add(new TreeNode("bForceCrossingLineOfAction : " + bForceCrossingLineOfAction));
                root.Nodes.Add(new TreeNode("bUseForNextCamera : " + bUseForNextCamera));
                return root;
            }
        }

        public List<CameraSwitchKey> m_aCameras;

        public BioEvtSysTrackSwitchCamera(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Switch Camera";
        }

        public void LoadData()
        {   //default values
            m_aCameras = new List<CameraSwitchKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aCameras")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        CameraSwitchKey key = new CameraSwitchKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "nmStageSpecificCam")
                                key.nmStageSpecificCam = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "bForceCrossingLineOfAction")
                                key.bForceCrossingLineOfAction = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bUseForNextCamera")
                                key.bUseForNextCamera = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aCameras.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aCameras");
            for (int i = 0; i < m_aCameras.Count; i++)
                t.Nodes.Add(m_aCameras[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackVOElements : BioInterpTrack
    {
        public int m_nStrRefID;
        public float m_fJCutOffset;

        public BioEvtSysTrackVOElements(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "VO Elements";
        }

        public void LoadData()
        {
            m_nStrRefID = 0;
            m_fJCutOffset = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_nStrRefID")
                    m_nStrRefID = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_fJCutOffset")
                    m_fJCutOffset = BitConverter.ToSingle(p.raw, 24);
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("m_nStrRefID : " + talkfile.findDataById(m_nStrRefID) + " (" + m_nStrRefID + ")");
            AddToTree("m_fJCutOffset : " + m_fJCutOffset);
        }
    }

    public class BioInterpTrackRotationMode : BioInterpTrack
    {
        public struct RotationModeKey
        {
            public NameReference FindActorTag; //name
            public float InterpTime;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(new TreeNode("nmStageSpecificCam : " + FindActorTag.Name));
                root.Nodes.Add(new TreeNode("bForceCrossingLineOfAction : " + InterpTime));
                return root;
            }
        }

        public List<RotationModeKey> EventTrack;

        public BioInterpTrackRotationMode(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Rotation Mode";
        }

        public void LoadData()
        {   //default values
            EventTrack = new List<RotationModeKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "EventTrack")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        RotationModeKey key = new RotationModeKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "FindActorTag")
                                key.FindActorTag = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "InterpTime")
                                key.InterpTime = BitConverter.ToSingle(p2[i].raw, 24);
                            pos += p2[i].raw.Length;
                        }
                        EventTrack.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("EventTrack");
            for (int i = 0; i < EventTrack.Count; i++)
                t.Nodes.Add(EventTrack[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackGesture : SFXGameActorInterpTrack
    {
        public struct Gesture
        {
            public List<int> aChainedGestures;
            public NameReference nmPoseSet;
            public NameReference nmPoseAnim;
            public NameReference nmGestureSet;
            public NameReference nmGestureAnim;
            public NameReference nmTransitionSet;
            public NameReference nmTransitionAnim;
            public float fPlayRate;
            public float fStartOffset;
            public float fEndOffset;
            public float fStartBlendDuration;
            public float fEndBlendDuration;
            public float fWeight;
            public float fTransBlendTime;
            public bool bInvalidData;
            public bool bOneShotAnim;
            public bool bChainToPrevious;
            public bool bPlayUntilNext;
            public bool bTerminateAllGestures;
            public bool bUseDynAnimSets;
            public bool bSnapToPose;
            public byteprop ePoseFilter;
            public byteprop ePose;
            public byteprop eGestureFilter;
            public byteprop eGesture;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                TreeNode t = new TreeNode("aChainedGestures");
                if (aChainedGestures != null)
                {
                    for (int i = 0; i < aChainedGestures.Count; i++)
                        t.Nodes.Add(aChainedGestures[i].ToString()); 
                }
                root.Nodes.Add(t);
                root.Nodes.Add("nmPoseSet : " + nmPoseSet.Name);
                root.Nodes.Add("nmPoseAnim : " + nmPoseAnim.Name);
                root.Nodes.Add("nmGestureSet : " + nmGestureSet.Name);
                root.Nodes.Add("nmGestureAnim : " + nmGestureAnim.Name);
                root.Nodes.Add("nmTransitionSet : " + nmTransitionSet.Name);
                root.Nodes.Add("nmTransitionAnim : " + nmTransitionAnim.Name);
                root.Nodes.Add("fPlayRate : " + fPlayRate);
                root.Nodes.Add("fStartOffset : " + fStartOffset);
                root.Nodes.Add("fEndOffset : " + fEndOffset);
                root.Nodes.Add("fStartBlendDuration : " + fStartBlendDuration);
                root.Nodes.Add("fEndBlendDuration : " + fEndBlendDuration);
                root.Nodes.Add("fWeight : " + fWeight);
                root.Nodes.Add("fTransBlendTime : " + fTransBlendTime);
                root.Nodes.Add("bInvalidData : " + bInvalidData);
                root.Nodes.Add("bOneShotAnim : " + bOneShotAnim);
                root.Nodes.Add("bChainToPrevious : " + bChainToPrevious);
                root.Nodes.Add("bPlayUntilNext : " + bPlayUntilNext);
                root.Nodes.Add("bTerminateAllGestures : " + bTerminateAllGestures);
                root.Nodes.Add("bUseDynAnimSets : " + bUseDynAnimSets);
                root.Nodes.Add("bSnapToPose : " + bSnapToPose);
                root.Nodes.Add("ePoseFilter : " + ePoseFilter.ToString(pcc));
                root.Nodes.Add("ePose : " + ePose.ToString(pcc));
                root.Nodes.Add("eGestureFilter : " + eGestureFilter.ToString(pcc));
                root.Nodes.Add("eGesture : " + eGesture.ToString(pcc));
                return root;
            }
        }

        public List<Gesture> m_aGestures;
        public int nmStartingPoseSet = -1;
        public int nmStartingPoseAnim = -1;
        public float m_fStartPoseOffset;
        public bool m_bUseDynamicAnimsets;
        public bool m_bAutoGenFemaleTrack;
        public byteprop ePoseFilter = new byteprop("EBioTrackAllPoseGroups", new string[] { "None" });
        public byteprop eStartingPose = new byteprop("EBioGestureAllPoses", new string[] { "None" });

        public BioEvtSysTrackGesture(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Gesture";
        }

        public void LoadData()
        {   //default values
            m_aGestures = new List<Gesture>();
            nmStartingPoseSet = -1;
            nmStartingPoseAnim = -1;
            m_fStartPoseOffset = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                name = pcc.getNameEntry(p.Name);
                if (name == "nmStartingPoseSet")
                    nmStartingPoseSet = p.Value.IntValue;
                else if (name == "nmStartingPoseAnim")
                    nmStartingPoseAnim = p.Value.IntValue;
                else if (name == "m_fStartPoseOffset")
                    m_fStartPoseOffset = BitConverter.ToSingle(p.raw, 24);
                else if (name == "m_bAutoGenFemaleTrack")
                    m_bAutoGenFemaleTrack = p.Value.IntValue != 0;
                else if (name == "m_bUseDynamicAnimsets")
                    m_bUseDynamicAnimsets = p.Value.IntValue != 0;
                else if (name == "ePoseFilter")
                    ePoseFilter.set(p.raw);
                else if (name == "eStartingPose")
                    eStartingPose.set(p.raw);
                else if (name == "m_aGestures")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        Gesture key = new Gesture();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            name = pcc.getNameEntry(p2[i].Name);
                            if (name == "aChainedGestures")
                            {
                                int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                                key.aChainedGestures = new List<int>();
                                for (int k = 0; k < count2; k++)
                                {
                                    key.aChainedGestures.Add(BitConverter.ToInt32(p2[i].raw, 28 + k * 4));
                                }
                            }
                            else if (name == "nmPoseSet")
                                key.nmPoseSet = p2[i].Value.NameValue;
                            else if (name == "nmPoseAnim")
                                key.nmPoseAnim = p2[i].Value.NameValue;
                            else if (name == "nmGestureSet")
                                key.nmGestureSet = p2[i].Value.NameValue;
                            else if (name == "nmGestureAnim")
                                key.nmGestureAnim = p2[i].Value.NameValue;
                            else if (name == "nmTransitionSet")
                                key.nmTransitionSet = p2[i].Value.NameValue;
                            else if (name == "nmTransitionAnim")
                                key.nmTransitionAnim = p2[i].Value.NameValue;
                            else if (name == "fPlayRate")
                                key.fPlayRate = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fStartOffset")
                                key.fStartOffset = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fEndOffset")
                                key.fEndOffset = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fStartBlendDuration")
                                key.fStartBlendDuration = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fEndBlendDuration")
                                key.fEndBlendDuration = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fWeight")
                                key.fWeight = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fTransBlendTime")
                                key.fTransBlendTime = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "bInvalidData")
                                key.bInvalidData = p2[i].Value.IntValue != 0;
                            else if (name == "bOneShotAnim")
                                key.bOneShotAnim = p2[i].Value.IntValue != 0;
                            else if (name == "bChainToPrevious")
                                key.bChainToPrevious = p2[i].Value.IntValue != 0;
                            else if (name == "bPlayUntilNext")
                                key.bPlayUntilNext = p2[i].Value.IntValue != 0;
                            else if (name == "bTerminateAllGestures")
                                key.bTerminateAllGestures = p2[i].Value.IntValue != 0;
                            else if (name == "bUseDynAnimSets")
                                key.bUseDynAnimSets = p2[i].Value.IntValue != 0;
                            else if (name == "bSnapToPose")
                                key.bSnapToPose = p2[i].Value.IntValue != 0;
                            else if (name == "ePoseFilter")
                                key.ePoseFilter = new byteprop(p2[i].raw, "EBioValidPoseGroups", new string[] { "None", "ValidPoseGroups_Unset" });
                            else if (name == "ePose")
                                key.ePose = new byteprop(p2[i].raw, "EBioGestureValidPoses", new string[] { "None", "GestValidPoses_Unset" });
                            else if (name == "eGestureFilter")
                                key.eGestureFilter = new byteprop(p2[i].raw, "EBioGestureGroups", new string[] { "None", "GestGroups_Unset" });
                            else if (name == "eGesture")
                                key.eGesture = new byteprop(p2[i].raw, "EBioGestureValidGestures", new string[] { "None", "GestValidGest_Unset" });
                            pos += p2[i].raw.Length;
                        }
                        m_aGestures.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aGestures");
            for (int i = 0; i < m_aGestures.Count; i++)
                t.Nodes.Add(m_aGestures[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
            if (nmStartingPoseSet != -1)
                AddToTree("nmStartingPoseSet : " + pcc.getNameEntry(nmStartingPoseSet));
            if (nmStartingPoseAnim != -1)
                AddToTree("nmStartingPoseAnim : " + pcc.getNameEntry(nmStartingPoseAnim));
            AddToTree("m_fStartPoseOffset : " + m_fStartPoseOffset);
            AddToTree("m_bAutoGenFemaleTrack : " + m_bAutoGenFemaleTrack);
            AddToTree("m_bUseDynamicAnimsets : " + m_bUseDynamicAnimsets);
            if (ePoseFilter.val != -1)
                AddToTree("ePoseFilter : " + ePoseFilter.ToString(pcc));
            if (eStartingPose.val != -1)
                AddToTree("eStartingPose : " + eStartingPose.ToString(pcc));
        }
    }

    public class BioEvtSysTrackLighting : SFXGameActorInterpTrack
    {
        public struct LightingKey
        {
            public NameReference TargetBoneName;  //name
            public float KeyLight_Scale_Red;
            public float KeyLight_Scale_Green;
            public float KeyLight_Scale_Blue;
            public float FillLight_Scale_Red;
            public float FillLight_Scale_Green;
            public float FillLight_Scale_Blue;
            public Color RimLightColor;
            public float RimLightScale;
            public float RimLightYaw;
            public float RimLightPitch;
            public float BouncedLightingIntensity;
            public int LightRig; //object
            public float LightRigOrientation;
            public bool bLockEnvironment;
            public bool bTriggerFullUpdate;
            public bool bUseForNextCamera;
            public bool bCastShadows;
            public byteprop RimLightControl;
            public byteprop LightingType;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("TargetBoneName : " + TargetBoneName.Name);
                root.Nodes.Add("KeyLight_Scale_Red : " + KeyLight_Scale_Red);
                root.Nodes.Add("KeyLight_Scale_Green : " + KeyLight_Scale_Green);
                root.Nodes.Add("KeyLight_Scale_Blue : " + KeyLight_Scale_Blue);
                root.Nodes.Add("FillLight_Scale_Red : " + FillLight_Scale_Red);
                root.Nodes.Add("FillLight_Scale_Green : " + FillLight_Scale_Green);
                root.Nodes.Add("FillLight_Scale_Blue : " + FillLight_Scale_Blue);
                TreeNode n = new TreeNode("RimLightColor");
                n.Nodes.Add("Alpha: " + RimLightColor.A);
                n.Nodes.Add("Red: " + RimLightColor.R);
                n.Nodes.Add("Green: " + RimLightColor.G);
                n.Nodes.Add("Blue: " + RimLightColor.B);
                root.Nodes.Add(n);
                root.Nodes.Add("RimLightScale : " + RimLightScale);
                root.Nodes.Add("RimLightYaw : " + RimLightYaw);
                root.Nodes.Add("RimLightPitch : " + RimLightPitch);
                root.Nodes.Add("BouncedLightingIntensity : " + BouncedLightingIntensity);
                root.Nodes.Add("LightRig : " + LightRig);
                root.Nodes.Add("LightRigOrientation : " + LightRigOrientation);
                root.Nodes.Add("bLockEnvironment : " + bLockEnvironment);
                root.Nodes.Add("bTriggerFullUpdate : " + bTriggerFullUpdate);
                root.Nodes.Add("bUseForNextCamera : " + bUseForNextCamera);
                root.Nodes.Add("bCastShadows : " + bCastShadows);
                root.Nodes.Add("RimLightControl : " + RimLightControl.ToString(pcc));
                root.Nodes.Add("LightingType : " + LightingType.ToString(pcc));
                return root;
            }
        }

        public List<LightingKey> m_aLightingKeys;

        public BioEvtSysTrackLighting(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Lighting";
        }

        public void LoadData()
        {   //default values
            m_aLightingKeys = new List<LightingKey>();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aLightingKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        LightingKey key = new LightingKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            name = pcc.getNameEntry(p2[i].Name);
                            if (name == "TargetBoneName")
                                key.TargetBoneName = p2[i].Value.NameValue;
                            else if (name == "KeyLight_Scale_Red")
                                key.KeyLight_Scale_Red = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "KeyLight_Scale_Green")
                                key.KeyLight_Scale_Green = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "KeyLight_Scale_Blue")
                                key.KeyLight_Scale_Blue = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "FillLight_Scale_Red")
                                key.FillLight_Scale_Red = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "FillLight_Scale_Green")
                                key.FillLight_Scale_Green = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "FillLight_Scale_Blue")
                                key.FillLight_Scale_Blue = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "RimLightColor")
                                key.RimLightColor = Color.FromArgb(BitConverter.ToInt32(p2[i].raw, 32));
                            else if (name == "RimLightScale")
                                key.RimLightScale = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "RimLightYaw")
                                key.RimLightYaw = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "RimLightPitch")
                                key.RimLightPitch = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "BouncedLightingIntensity")
                                key.BouncedLightingIntensity = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "LightRig")
                                key.LightRig = p2[i].Value.IntValue;
                            else if (name == "LightRigOrientation")
                                key.LightRigOrientation = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "bLockEnvironment")
                                key.bLockEnvironment = p2[i].Value.IntValue != 0;
                            else if (name == "bTriggerFullUpdate")
                                key.bTriggerFullUpdate = p2[i].Value.IntValue != 0;
                            else if (name == "bUseForNextCamera")
                                key.bUseForNextCamera = p2[i].Value.IntValue != 0;
                            else if (name == "bCastShadows")
                                key.bCastShadows = p2[i].Value.IntValue != 0;
                            else if (name == "RimLightControl")
                                key.RimLightControl = new byteprop(p2[i].raw, "ERimLightControlType", new string[] { "RLCT_Key", "RLCT_Camera" });
                            else if (name == "LightingType")
                                key.LightingType = new byteprop(p2[i].raw, "EConvLightingType", new string[] { "ConvLighting_Cinematic", "ConvLighting_Dynamic", "ConvLighting_Exploration"});
                            pos += p2[i].raw.Length;
                        }
                        m_aLightingKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aLightingKeys");
            for (int i = 0; i < m_aLightingKeys.Count; i++)
                t.Nodes.Add(m_aLightingKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackLookAt : SFXGameActorInterpTrack
    {
        public struct LookAtKey
        {
            public NameReference nmFindActor;
            public bool bEnabled;
            public bool bInstantTransition;
            public bool bLockedToTarget;
            public byteprop eFindActorMode;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("nmFindActor : " + nmFindActor.Name);
                root.Nodes.Add("bEnabled : " + bEnabled);
                root.Nodes.Add("bInstantTransition : " + bInstantTransition);
                root.Nodes.Add("bLockedToTarget : " + bLockedToTarget);
                root.Nodes.Add("eFindActorMode : " + eFindActorMode.ToString(pcc));
                return root;
            }
        }

        public List<LookAtKey> m_aLookAtKeys;

        public BioEvtSysTrackLookAt(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "LookAt";
        }

        public void LoadData()
        {   //default values
            m_aLookAtKeys = new List<LookAtKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aLookAtKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        LookAtKey key = new LookAtKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            name = pcc.getNameEntry(p2[i].Name);
                            if (name == "nmFindActor")
                                key.nmFindActor = p2[i].Value.NameValue;
                            else if (name == "bEnabled")
                                key.bEnabled = p2[i].Value.IntValue != 0;
                            else if (name == "bInstantTransition")
                                key.bInstantTransition = p2[i].Value.IntValue != 0;
                            else if (name == "bLockedToTarget")
                                key.bLockedToTarget = p2[i].Value.IntValue != 0;
                            else if (name == "eFindActorMode")
                                key.eFindActorMode = new byteprop(p2[i].raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aLookAtKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aLookAtKeys");
            for (int i = 0; i < m_aLookAtKeys.Count; i++)
                t.Nodes.Add(m_aLookAtKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackProp : SFXGameActorInterpTrack
    {
        public struct PropKey
        {
            public int pWeaponClass; //object
            public NameReference nmProp; //name
            public NameReference nmAction; //name
            public int pPropMesh; //object
            public int pActionPartSys; //object
            public int pActionClientEffect; //object
            public bool bEquip;
            public bool bForceGenericWeapon;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("pWeaponClass : " + pWeaponClass);
                root.Nodes.Add("nmProp : " + nmProp.Name);
                root.Nodes.Add("nmAction : " + nmAction.Name);
                root.Nodes.Add("pPropMesh : " + pPropMesh);
                root.Nodes.Add("pActionPartSys : " + pActionPartSys);
                root.Nodes.Add("pActionClientEffect : " + pActionClientEffect);
                root.Nodes.Add("bEquip : " + bEquip);
                root.Nodes.Add("bForceGenericWeapon : " + bForceGenericWeapon);
                return root;
            }
        }

        public List<PropKey> m_aPropKeys;

        public BioEvtSysTrackProp(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Prop";
        }

        public void LoadData()
        {   //defultt values
            m_aPropKeys = new List<PropKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aPropKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        PropKey key = new PropKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            name = pcc.getNameEntry(p2[i].Name);
                            if (name == "pWeaponClass")
                                key.pWeaponClass = p2[i].Value.IntValue;
                            else if (name == "nmProp")
                                key.nmProp = p2[i].Value.NameValue;
                            else if (name == "nmAction")
                                key.nmAction = p2[i].Value.NameValue;
                            else if (name == "pPropMesh")
                                key.pPropMesh = p2[i].Value.IntValue;
                            else if (name == "pActionPartSys")
                                key.pActionPartSys = p2[i].Value.IntValue;
                            else if (name == "pActionClientEffect")
                                key.pActionClientEffect = p2[i].Value.IntValue;
                            else if (name == "bEquip")
                                key.bEquip = p2[i].Value.IntValue != 0;
                            else if (name == "bForceGenericWeapon")
                                key.bForceGenericWeapon = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aPropKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aPropKeys");
            for (int i = 0; i < m_aPropKeys.Count; i++)
                t.Nodes.Add(m_aPropKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class BioEvtSysTrackSetFacing : SFXGameActorInterpTrack
    {
        public struct FacingKey
        {
            public NameReference nmStageNode;
            public float fOrientation;
            public bool bApplyOrientation;
            public byteprop eCurrentStageNode;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("nmStageNode : " + nmStageNode.Name);
                root.Nodes.Add("fOrientation : " + fOrientation);
                root.Nodes.Add("bApplyOrientation : " + bApplyOrientation);
                root.Nodes.Add("eCurrentStageNode : " + eCurrentStageNode.ToString(pcc));
                return root;
            }
        }

        public List<FacingKey> m_aFacingKeys;

        public BioEvtSysTrackSetFacing(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "SetFacing";
        }

        public void LoadData()
        {   //default values
            m_aFacingKeys = new List<FacingKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aFacingKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        FacingKey key = new FacingKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "nmStageNode")
                                key.nmStageNode = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "fOrientation")
                                key.fOrientation = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "bApplyOrientation")
                                key.bApplyOrientation = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "eCurrentStageNode")
                                key.eCurrentStageNode = new byteprop(p2[i].raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aFacingKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aFacingKeys");
            for (int i = 0; i < m_aFacingKeys.Count; i++)
                t.Nodes.Add(m_aFacingKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class SFXGameInterpTrackProcFoley : SFXGameActorInterpTrack
    {
        public struct ProcFoleyStartStopKey
        {
            public float m_fMaxThreshold;
            public float m_fSmoothingFactor;
            public bool m_bStart;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("m_fMaxThreshold : " + m_fMaxThreshold);
                root.Nodes.Add("m_fSmoothingFactor : " + m_fSmoothingFactor);
                root.Nodes.Add("m_bStart : " + m_bStart);
                return root;
            }
        }

        public List<ProcFoleyStartStopKey> m_aProcFoleyStartStopKeys;
        public int m_TrackFoleySound; //unused?

        public SFXGameInterpTrackProcFoley(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "ProcFoley";
        }

        public void LoadData()
        {   //default
            m_aProcFoleyStartStopKeys = new List<ProcFoleyStartStopKey>();
            m_TrackFoleySound = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_TrackFoleySound")
                    m_TrackFoleySound = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aProcFoleyStartStopKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        ProcFoleyStartStopKey key = new ProcFoleyStartStopKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "m_fMaxThreshold")
                                key.m_fMaxThreshold = BitConverter.ToSingle(p2[i].raw, 24);
                            if (pcc.getNameEntry(p2[i].Name) == "m_fSmoothingFactor")
                                key.m_fSmoothingFactor = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "m_bStart")
                                key.m_bStart = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aProcFoleyStartStopKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aProcFoleyStartStopKeys");
            for (int i = 0; i < m_aProcFoleyStartStopKeys.Count; i++)
                t.Nodes.Add(m_aProcFoleyStartStopKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
            //AddToTree("m_TrackFoleySound : " + m_TrackFoleySound);
        }
    }

    public class SFXInterpTrackPlayFaceOnlyVO : SFXGameActorInterpTrack
    {
        public struct FOVOKey
        {
            public int pConversation; //object
            public int nLineStrRef;
            public int srActorNameOverride;
            public bool bForceHideSubtitles;
            public bool bPlaySoundOnly;
            public bool bDisableDelayUntilPreload;
            public bool bAllowInConversation;
            public bool bSubtitleHasPriority;

            public TreeNode ToTree(int index, float time, TalkFile tlk)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("pConversation : " + pConversation);
                root.Nodes.Add("nLineStrRef : " + tlk.findDataById(nLineStrRef) + " (" + nLineStrRef + ")");
                root.Nodes.Add("srActorNameOverride : " + srActorNameOverride);
                root.Nodes.Add("bForceHideSubtitles : " + bForceHideSubtitles);
                root.Nodes.Add("bPlaySoundOnly : " + bPlaySoundOnly);
                root.Nodes.Add("bDisableDelayUntilPreload : " + bDisableDelayUntilPreload);
                root.Nodes.Add("bAllowInConversation : " + bAllowInConversation);
                root.Nodes.Add("bSubtitleHasPriority : " + bSubtitleHasPriority);
                return root;
            }
        }

        public List<FOVOKey> m_aFOVOKeys;

        public SFXInterpTrackPlayFaceOnlyVO(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "PlayFaceOnlyVO";
        }

        public void LoadData()
        {   //default values
            m_aFOVOKeys = new List<FOVOKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aFOVOKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        FOVOKey key = new FOVOKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "pConversation")
                                key.pConversation = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "nLineStrRef")
                                key.nLineStrRef = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "srActorNameOverride")
                                key.srActorNameOverride = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "bForceHideSubtitles")
                                key.bForceHideSubtitles = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bPlaySoundOnly")
                                key.bPlaySoundOnly = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bDisableDelayUntilPreload")
                                key.bDisableDelayUntilPreload = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bAllowInConversation")
                                key.bAllowInConversation = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bSubtitleHasPriority")
                                key.bSubtitleHasPriority = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aFOVOKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aFOVOKeys");
            for (int i = 0; i < m_aFOVOKeys.Count; i++)
                t.Nodes.Add(m_aFOVOKeys[i].ToTree(i, m_aTrackKeys[i].fTime, talkfile));
            AddToTree(t);
        }
    }

    public class SFXInterpTrackAttachCrustEffect : BioInterpTrack
    {
        public struct CrustEffectKey
        {
            public float m_fLifeTime;
            public bool m_bAttach;

            public TreeNode ToTree(int index, float time)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(new TreeNode("m_fLifeTime : " + m_fLifeTime));
                root.Nodes.Add(new TreeNode("m_bAttach : " + m_bAttach));
                return root;
            }
        }

        public List<CrustEffectKey> m_aCrustEffectKeyData;
        public List<int> m_aTarget;
        public int oEffect;

        public SFXInterpTrackAttachCrustEffect(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Attach Crust Effect";
        }

        public void LoadData()
        {   //default values
            m_aCrustEffectKeyData = new List<CrustEffectKey>();
            m_aTarget = new List<int>();
            oEffect = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "oEffect")
                    oEffect = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aCrustEffectKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        CrustEffectKey key = new CrustEffectKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "m_fLifeTime")
                                key.m_fLifeTime = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "m_bAttach")
                                key.m_bAttach = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aCrustEffectKeyData.Add(key);
                    }
                }
                else if (pcc.getNameEntry(p.Name) == "m_aTarget")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aTarget.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aCrustEffectKeyData");
            for (int i = 0; i < m_aCrustEffectKeyData.Count; i++)
                t.Nodes.Add(m_aCrustEffectKeyData[i].ToTree(i, m_aTrackKeys[i].fTime));
            propView.Nodes.Add(t);
            t = new TreeNode("m_aTarget");
            if (m_aTarget != null)
            {
                for (int i = 0; i < m_aTarget.Count; i++)
                    t.Nodes.Add(m_aTarget[i].ToString()); 
            }
            propView.Nodes.Add(t);
            propView.Nodes.Add(new TreeNode("oEffect : " + oEffect));
        }
    }

    public class SFXInterpTrackAttachToActor : BioInterpTrack
    {
        public List<int> m_aTarget;
        public Vector RelativeOffset;
        public Rotator RelativeRotation;
        public int BoneName;
        public bool bDetach;
        public bool bHardAttach;
        public bool bUseRelativeOffset;
        public bool bUseRelativeRotation;

        public SFXInterpTrackAttachToActor(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Attach To Actor";
        }

        public void LoadData()
        {   //default values
            m_aTarget = new List<int>();
            BoneName = -1;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aTarget")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aTarget.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
                else if (pcc.getNameEntry(p.Name) == "RelativeOffset")
                {
                    RelativeOffset.x = BitConverter.ToSingle(p.raw, 32);
                    RelativeOffset.y = BitConverter.ToSingle(p.raw, 36);
                    RelativeOffset.z = BitConverter.ToSingle(p.raw, 40);
                }
                else if (pcc.getNameEntry(p.Name) == "RelativeRotation")
                {
                    RelativeRotation.pitch = BitConverter.ToInt32(p.raw, 32);
                    RelativeRotation.yaw = BitConverter.ToInt32(p.raw, 36);
                    RelativeRotation.roll = BitConverter.ToInt32(p.raw, 40);
                }
                else if (pcc.getNameEntry(p.Name) == "BoneName")
                    BoneName = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "bDetach")
                    bDetach = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bHardAttach")
                    bHardAttach = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bUseRelativeOffset")
                    bUseRelativeOffset = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bDetach")
                    bUseRelativeRotation = p.Value.IntValue != 0;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aTarget");
            if (m_aTarget != null)
            {
                for (int i = 0; i < m_aTarget.Count; i++)
                    t.Nodes.Add(m_aTarget[i].ToString()); 
            }
            AddToTree(t);
            AddToTree(RelativeOffset.ToTree("RelativeOffset"));
            AddToTree(RelativeRotation.ToTree("RelativeRotation"));
            AddToTree("BoneName : " + BoneName);
            AddToTree("bDetach : " + bDetach);
            AddToTree("bHardAttach : " + bHardAttach);
            AddToTree("bUseRelativeOffset : " + bUseRelativeOffset);
            AddToTree("bUseRelativeRotation : " + bUseRelativeRotation);
        }
    }

    public class SFXInterpTrackAttachVFXToObject : BioInterpTrack
    {
        public List<int> m_aAttachToTarget;
        public float[] m_vOffset;
        public int m_nmSocketOrBone;
        public int m_oEffect;

        public SFXInterpTrackAttachVFXToObject(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Attach VFX To Object";
        }

        public void LoadData()
        {   //default values
            m_aAttachToTarget = new List<int>();
            m_vOffset = new float[3];
            m_nmSocketOrBone = -1;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aAttachToTarget")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aAttachToTarget.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
                else if (pcc.getNameEntry(p.Name) == "m_vOffset")
                {
                    m_vOffset[0] = BitConverter.ToSingle(p.raw, 32);
                    m_vOffset[1] = BitConverter.ToSingle(p.raw, 36);
                    m_vOffset[2] = BitConverter.ToSingle(p.raw, 40);
                }
                else if (pcc.getNameEntry(p.Name) == "m_nmSocketOrBone")
                    m_nmSocketOrBone = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_oEffect")
                    m_oEffect = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aAttachToTarget");
            if (m_aAttachToTarget != null)
            {
                for (int i = 0; i < m_aAttachToTarget.Count; i++)
                    t.Nodes.Add(m_aAttachToTarget[i].ToString()); 
            }
            propView.Nodes.Add(t);
            t = new TreeNode("m_vOffset");
            t.Nodes.Add("" + m_vOffset[0]);
            t.Nodes.Add("" + m_vOffset[1]);
            t.Nodes.Add("" + m_vOffset[2]);
            propView.Nodes.Add(t);
            propView.Nodes.Add(new TreeNode("m_nmSocketOrBone : " + m_nmSocketOrBone));
            propView.Nodes.Add(new TreeNode("m_oEffect : " + m_oEffect));
        }
    }

    public class SFXInterpTrackBlackScreen : BioInterpTrack
    {
        public struct BlackScreenKey
        {
            public int PlaceHolder;
            public byteprop BlackScreenState;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("PlaceHolder : " + PlaceHolder);
                root.Nodes.Add("BlackScreenState : " + BlackScreenState.ToString(pcc));
                return root;
            }
        }

        public List<BlackScreenKey> m_aBlackScreenKeyData;
        public int m_BlackScreenSeq;

        public SFXInterpTrackBlackScreen(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Black Screen";
        }

        public void LoadData()
        {   //default values
            m_aBlackScreenKeyData = new List<BlackScreenKey>();
            m_BlackScreenSeq = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_BlackScreenSeq")
                    m_BlackScreenSeq = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aBlackScreenKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        BlackScreenKey key = new BlackScreenKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "PlaceHolder")
                                key.PlaceHolder = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "BlackScreenState")
                                key.BlackScreenState = new byteprop(p2[i].raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aBlackScreenKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aBlackScreenKeyData");
            for (int i = 0; i < m_aBlackScreenKeyData.Count; i++)
                t.Nodes.Add(m_aBlackScreenKeyData[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
            AddToTree("m_BlackScreenSeq : " + m_BlackScreenSeq);
        }
    }

    public class SFXInterpTrackDestroy : BioInterpTrack
    {
        public List<int> m_aTarget;

        public SFXInterpTrackDestroy(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Destroy";
        }

        public void LoadData()
        {   //default values
            m_aTarget = new List<int>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aTarget")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aTarget.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aTarget");
            if (m_aTarget != null)
            {
                for (int i = 0; i < m_aTarget.Count; i++)
                    t.Nodes.Add(m_aTarget[i].ToString()); 
            }
            propView.Nodes.Add(t);
        }
    }

    public class SFXInterpTrackForceLightEnvUpdate : BioInterpTrack
    {
        public int m_SeqForceUpdateLight;

        public SFXInterpTrackForceLightEnvUpdate(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "ForceLightEnvUpdate";
        }

        public void LoadData()
        {   //default values
            m_SeqForceUpdateLight = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_SeqForceUpdateLight")
                    m_SeqForceUpdateLight = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            propView.Nodes.Add(new TreeNode("m_SeqForceUpdateLight : " + m_SeqForceUpdateLight));
        }
    }

    public class SFXInterpTrackLightEnvQuality : BioInterpTrack
    {
        public struct LightEnvKey
        {
            public int PlaceHolder;
            public byteprop Quality;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("PlaceHolder : " + PlaceHolder);
                root.Nodes.Add("Quality : " + Quality.ToString(pcc));
                return root;
            }
        }

        public List<LightEnvKey> m_aLightEnvKeyData;
        public int m_LightEnvSeq;

        public SFXInterpTrackLightEnvQuality(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Light Env Quality";
        }

        public void LoadData()
        {   //default values
            m_aLightEnvKeyData = new List<LightEnvKey>();
            m_LightEnvSeq = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_LightEnvSeq")
                    m_LightEnvSeq = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aLightEnvKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        LightEnvKey key = new LightEnvKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "PlaceHolder")
                                key.PlaceHolder = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "Quality")
                                key.Quality = new byteprop(p.raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aLightEnvKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aLightEnvKeyData");
            for (int i = 0; i < m_aLightEnvKeyData.Count; i++)
                t.Nodes.Add(m_aLightEnvKeyData[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
            AddToTree("m_LightEnvSeq : " + m_LightEnvSeq);
        }
    }

    public class SFXInterpTrackMovieBink : SFXInterpTrackMovieBase
    {
        public string m_sMovieName;

        public float m_fAutoResizeBuffer;//unused?
        public int m_SoundEvent; //object

        public bool m_bIgnoreShrinking;
        public bool m_bIgnoreGrowing;

        public SFXInterpTrackMovieBink(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Movie Bink";
        }

        public void LoadData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                name = pcc.getNameEntry(p.Name);
                if (name == "m_sMovieName")
                    m_sMovieName = p.Value.StringValue;
                else if (name == "m_SoundEvent")
                    m_SoundEvent = p.Value.IntValue;
                else if (name == "m_bIgnoreShrinking")
                    m_bIgnoreShrinking = p.Value.IntValue != 0;
                else if (name == "m_bIgnoreGrowing")
                    m_bIgnoreGrowing = p.Value.IntValue != 0;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("m_sMovieName : \"" + m_sMovieName + "\"");
            AddToTree("m_SoundEvent : " + m_SoundEvent);
            AddToTree("m_bIgnoreShrinking : " + m_bIgnoreShrinking);
            AddToTree("m_bIgnoreGrowing : " + m_bIgnoreGrowing);
        }
    }

    public class SFXInterpTrackMovieTexture : SFXInterpTrackMovieBase
    {
        public int m_oTextureMovie;

        public SFXInterpTrackMovieTexture(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Movie Texture";
        }

        public void LoadData()
        {   //default values
            m_oTextureMovie = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_oTextureMovie")
                    m_oTextureMovie = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("m_oTextureMovie : " + m_oTextureMovie);
        }
    }

    public class SFXInterpTrackSetPlayerNearClipPlane : BioInterpTrack
    {
        public struct NearClipKey
        {
            public float m_fValue;
            public bool m_bUseDefaultValue;

            public TreeNode ToTree(int index, float time)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("m_fValue : " + m_fValue);
                root.Nodes.Add("m_bUseDefaultValue : " + m_bUseDefaultValue);
                return root;
            }
        }

        public List<NearClipKey> m_aNearClipKeyData;

        public SFXInterpTrackSetPlayerNearClipPlane(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "SetPlayerNearClipPlane";
        }

        public void LoadData()
        {   //default values
            m_aNearClipKeyData = new List<NearClipKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aNearClipKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        NearClipKey key = new NearClipKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "m_fValue")
                                key.m_fValue = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "m_bUseDefaultValue")
                                key.m_bUseDefaultValue = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aNearClipKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aNearClipKeyData");
            for (int i = 0; i < m_aNearClipKeyData.Count; i++)
                t.Nodes.Add(m_aNearClipKeyData[i].ToTree(i, m_aTrackKeys[i].fTime));
            AddToTree(t);
        }
    }

    public class SFXInterpTrackSetWeaponInstant : BioInterpTrack
    {
        public struct WeaponClassKey
        {
            public int cWeapon;

            public TreeNode ToTree(int index, float time)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(new TreeNode("cWeapon : " + cWeapon));
                return root;
            }
        }

        public List<WeaponClassKey> m_aWeaponClassKeyData;
        public int m_PawnRefTag;
        public int m_Pawn;

        public SFXInterpTrackSetWeaponInstant(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "SetWeaponInstant";
        }

        public void LoadData()
        {   //default values
            m_aWeaponClassKeyData = new List<WeaponClassKey>();
            m_PawnRefTag = 0;
            m_Pawn = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_PawnRefTag")
                    m_PawnRefTag = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_Pawn")
                    m_Pawn = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "m_aWeaponClassKeyData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        WeaponClassKey key = new WeaponClassKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "cWeapon")
                                key.cWeapon = p2[i].Value.IntValue;
                            pos += p2[i].raw.Length;
                        }
                        m_aWeaponClassKeyData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aWeaponClassKeyData");
            for (int i = 0; i < m_aWeaponClassKeyData.Count; i++)
                t.Nodes.Add(m_aWeaponClassKeyData[i].ToTree(i, m_aTrackKeys[i].fTime));
            propView.Nodes.Add(t);
            propView.Nodes.Add(new TreeNode("m_PawnRefTag : " + m_PawnRefTag));
            propView.Nodes.Add(new TreeNode("m_Pawn : " + m_Pawn));
        }
    }

    public class SFXInterpTrackToggleAffectedByHitEffects : SFXInterpTrackToggleBase
    {
        public SFXInterpTrackToggleAffectedByHitEffects(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            if (TrackTitle == "")
                TrackTitle = "ToggleAffectedByHitEffects";
        }
    }

    public class SFXInterpTrackToggleHidden : SFXInterpTrackToggleBase
    {
        public SFXInterpTrackToggleHidden(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            if (TrackTitle == "")
                TrackTitle = "Toggle Hidden";
        }
    }

    public class SFXInterpTrackToggleLightEnvironment : SFXInterpTrackToggleBase
    {
        public int m_LightEnvSeq;

        public SFXInterpTrackToggleLightEnvironment(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "ToggleLightEnvironment";
        }

        public void LoadData()
        {
            m_LightEnvSeq = 0;

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_LightEnvSeq")
                    m_LightEnvSeq = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            propView.Nodes.Add(new TreeNode("m_LightEnvSeq : " + m_LightEnvSeq));
        }
    }

    public class SFXGameInterpTrackWwiseMicLock : BioInterpTrack
    {
        public struct MicLockKey
        {
            public NameReference m_nmFindActor;
            public bool m_bLock;
            public byteprop m_eFindActorMode;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("m_nmFindActor : " + m_nmFindActor.Name);
                root.Nodes.Add("m_bLock : " + m_bLock);
                root.Nodes.Add("m_eFindActorMode : " + m_eFindActorMode.ToString(pcc));
                return root;
            }
        }

        public List<MicLockKey> m_aMicLockKeys;
        public bool m_bUnlockAtEnd;

        public SFXGameInterpTrackWwiseMicLock(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
        }

        public void LoadData()
        {   //default values
            m_aMicLockKeys = new List<MicLockKey>();

            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_bUnlockAtEnd")
                    m_bUnlockAtEnd = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "m_aMicLockKeys")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        MicLockKey key = new MicLockKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "m_nmFindActor")
                                key.m_nmFindActor = p2[i].Value.NameValue;
                            if (pcc.getNameEntry(p2[i].Name) == "m_bLock")
                                key.m_bLock = p2[i].Value.IntValue != 0;
                            if (pcc.getNameEntry(p2[i].Name) == "m_eFindActorMode")
                                key.m_eFindActorMode = new byteprop(p2[i].raw, "", new string[] { "" });
                            pos += p2[i].raw.Length;
                        }
                        m_aMicLockKeys.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aMicLockKeys");
            for (int i = 0; i < m_aMicLockKeys.Count; i++)
                t.Nodes.Add(m_aMicLockKeys[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
            AddToTree("m_bUnlockAtEnd : " + m_bUnlockAtEnd);
        }
    }

    public class InterpTrackEvent : InterpTrack
    {
        public struct EventTrackKey
        {
            public NameReference EventName; //name
            public float Time;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("EventName : " + EventName.Name);
                root.Nodes.Add("Time : " + Time);
                return root;
            }
        }

        public List<EventTrackKey> EventTrack;
        public bool bFireEventsWhenForwards = true;
        public bool bFireEventsWhenBackwards = true;
        public bool bFireEventsWhenJumpingForwards;

        public InterpTrackEvent(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Event";
            GetKeyFrames();
        }

        public void LoadData()
        {
            EventTrack = new List<EventTrackKey>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "bFireEventsWhenForwards")
                    bFireEventsWhenForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenBackwards")
                    bFireEventsWhenBackwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenJumpingForwards")
                    bFireEventsWhenJumpingForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "EventTrack")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        EventTrackKey key = new EventTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "EventName")
                                key.EventName = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            pos += p2[i].raw.Length;
                        }
                        EventTrack.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (EventTrack != null)
                foreach (EventTrackKey e in EventTrack)
                    keys.Add(GenerateKeyFrame(e.Time));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("EventTrack");
            for (int i = 0; i < EventTrack.Count; i++)
                t.Nodes.Add(EventTrack[i].ToTree(i, pcc));
            AddToTree(t);
            AddToTree("bFireEventsWhenForwards : " + bFireEventsWhenForwards);
            AddToTree("bFireEventsWhenBackwards : " + bFireEventsWhenBackwards);
            AddToTree("bFireEventsWhenJumpingForwards : " + bFireEventsWhenJumpingForwards);
        }
    }

    public class InterpTrackFaceFX : InterpTrack
    {
        public struct FaceFXTrackKey
        {
            public string FaceFXGroupName;
            public string FaceFXSeqName;
            public float StartTime;

            public TreeNode ToTree(int index)
            {
                TreeNode root = new TreeNode(index + ": " + StartTime);
                root.Nodes.Add("FaceFXGroupName : \"" + FaceFXGroupName + "\"");
                root.Nodes.Add("FaceFXSeqName : \"" + FaceFXSeqName + "\"");
                root.Nodes.Add("StartTime : " + StartTime);
                return root;
            }
        }

        public struct FaceFXSoundCueKey
        {
            public int FaceFXSoundCue; //object

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add("FaceFXSoundCue : " + FaceFXSoundCue);
                return root;
            }
        }

        public struct Override_Asset
        {
            public int fxAsset; //object
            public byteprop eAnimSeq; //unused?
            public byteprop eAnimGroup; //unused?

            public TreeNode ToTree()
            {
                TreeNode root = new TreeNode("OverrideAsset");
                root.Nodes.Add(new TreeNode("fxAsset : " + fxAsset));
                return root;
            }
        }

        public struct Override_AnimSet
        {
            public List<int> aBioMaleSets;
            public List<int> aBioFemaleSets;
            public int fxaAnimSet;//unused?
            public byteprop eAnimSequence;

            public TreeNode ToTree(PCCObject pcc)
            {
                TreeNode root = new TreeNode("OverrideAnimSet");
                TreeNode t = new TreeNode("aBioMaleSets");
                if (aBioMaleSets != null)
                {
                    for (int i = 0; i < aBioMaleSets.Count; i++)
                        t.Nodes.Add(aBioMaleSets[i].ToString()); 
                }
                root.Nodes.Add(t);
                t = new TreeNode("aBioFemaleSets");
                if (aBioFemaleSets != null)
                {
                    for (int i = 0; i < aBioFemaleSets.Count; i++)
                        t.Nodes.Add(aBioFemaleSets[i].ToString()); 
                }
                root.Nodes.Add(t);
                root.Nodes.Add("eAnimSequence: " + eAnimSequence.ToString(pcc));
                return root;
            }
        }

        public List<int> m_aBioMaleAnimSets;
        public List<int> m_aBioFemaleAnimSets;
        public List<FaceFXTrackKey> FaceFXSeqs;
        public List<FaceFXSoundCueKey> FaceFXSoundCueKeys;
        public Override_Asset OverrideAsset;
        public Override_AnimSet OverrideAnimSet;
        public NameReference m_nmSFXFindActor;
        public byteprop m_eSFXFindActorMode;
        public bool m_bSFXEnableClipToClipBlending;

        public InterpTrackFaceFX(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "FaceFX";
            GetKeyFrames();
        }

        public void LoadData()
        {
            FaceFXSeqs = new List<FaceFXTrackKey>();
            FaceFXSoundCueKeys = new List<FaceFXSoundCueKey>();
            OverrideAsset = new Override_Asset();
            m_aBioMaleAnimSets = new List<int>();
            m_aBioFemaleAnimSets = new List<int>();

            byte[] buff = pcc.Exports[index].Data;
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                name = pcc.getNameEntry(p.Name);
                if (name == "m_nmSFXFindActor")
                {
                    m_nmSFXFindActor = p.Value.NameValue;
                }
                else if (name == "m_bSFXEnableClipToClipBlending")
                {
                    m_bSFXEnableClipToClipBlending = p.Value.IntValue != 0;
                }
                else if (name == "m_eSFXFindActorMode")
                {
                    m_eSFXFindActorMode = new byteprop(p.raw, "", new string[] { "" });
                }
                else if (name == "m_aBioMaleAnimSets")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aBioMaleAnimSets.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
                else if (name == "m_aBioFemaleAnimSets")
                {
                    int count2 = BitConverter.ToInt32(p.raw, 24);
                    for (int k = 0; k < count2; k++)
                    {
                        m_aBioFemaleAnimSets.Add(BitConverter.ToInt32(p.raw, 28 + k * 4));
                    }
                }
                else if (name == "FaceFXSeqs")
                {
                    FaceFXSeqs = new List<FaceFXTrackKey>();
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        FaceFXTrackKey key = new FaceFXTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "FaceFXGroupName")
                                key.FaceFXGroupName = p2[i].Value.StringValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "FaceFXSeqName")
                                key.FaceFXSeqName = p2[i].Value.StringValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "StartTime")
                                key.StartTime = BitConverter.ToSingle(p2[i].raw, 24);
                            pos += p2[i].raw.Length;
                        }
                        FaceFXSeqs.Add(key);
                    }
                }
                else if (name == "FaceFXSoundCueKeys")
                {
                    FaceFXSoundCueKeys = new List<FaceFXSoundCueKey>();
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        FaceFXSoundCueKey key = new FaceFXSoundCueKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "FaceFXSoundCue")
                                key.FaceFXSoundCue = p2[i].Value.IntValue;
                            pos += p2[i].raw.Length;
                        }
                        FaceFXSoundCueKeys.Add(key);
                    }
                }
                else if (name == "OverrideAsset")
                {
                    OverrideAsset = new Override_Asset();
                    OverrideAsset.fxAsset = BitConverter.ToInt32(p.raw, 56);
                }
                else if (name == "OverrideAnimSet")
                {
                    OverrideAnimSet = new Override_AnimSet();
                    OverrideAnimSet.aBioFemaleSets = new List<int>();
                    OverrideAnimSet.aBioMaleSets = new List<int>();
                    int pos = 32;
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                    for (int i = 0; i < p2.Count(); i++)
                    {
                        name = pcc.getNameEntry(p2[i].Name);
                        if (name == "aBioMaleSets")
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            OverrideAnimSet.aBioMaleSets = new List<int>();
                            for (int k = 0; k < count2; k++)
                            {
                                OverrideAnimSet.aBioMaleSets.Add(BitConverter.ToInt32(p2[i].raw, 28 + k * 4));
                            }
                        }
                        else if (name == "aBioFemaleSets")
                        {
                            int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                            OverrideAnimSet.aBioFemaleSets = new List<int>();
                            for (int k = 0; k < count2; k++)
                            {
                                OverrideAnimSet.aBioFemaleSets.Add(BitConverter.ToInt32(p2[i].raw, 28 + k * 4));
                            }
                        }
                        else if (name == "eAnimSequence")
                            OverrideAnimSet.eAnimSequence = new byteprop(p2[i].raw, "", new string[] { "" });
                        pos += p2[i].raw.Length;
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (FaceFXSeqs != null)
                foreach (FaceFXTrackKey s in FaceFXSeqs)
                    keys.Add(GenerateKeyFrame(s.StartTime));
        }

        public override void ToTree()
        {
            base.ToTree();
            if (OverrideAnimSet.aBioMaleSets != null)
                AddToTree(OverrideAnimSet.ToTree(pcc));
            TreeNode t = new TreeNode("m_aBioMaleAnimSets");
            for (int i = 0; i < m_aBioMaleAnimSets.Count; i++)
                t.Nodes.Add(m_aBioMaleAnimSets[i].ToString());
            AddToTree(t);
            t = new TreeNode("m_aBioFemaleAnimSets");
            for (int i = 0; i < m_aBioFemaleAnimSets.Count; i++)
                t.Nodes.Add(m_aBioFemaleAnimSets[i].ToString());
            AddToTree(t);
            t = new TreeNode("FaceFXSeqs");
            for (int i = 0; i < FaceFXSeqs.Count; i++)
                t.Nodes.Add(FaceFXSeqs[i].ToTree(i));
            AddToTree(t);
            t = new TreeNode("FaceFXSoundCueKeys");
            for (int i = 0; i < FaceFXSoundCueKeys.Count; i++)
                t.Nodes.Add(FaceFXSoundCueKeys[i].ToTree(i, FaceFXSeqs[i].StartTime, pcc));
            AddToTree(t);
            AddToTree(OverrideAsset.ToTree());
            AddToTree("m_nmSFXFindActor: " + m_nmSFXFindActor.Name);
            AddToTree("m_eSFXFindActorMode: " + m_eSFXFindActorMode.ToString(pcc));
            AddToTree("m_bSFXEnableClipToClipBlending: " + m_bSFXEnableClipToClipBlending);
        }
    }

    public class InterpTrackAnimControl : InterpTrack
    {
        public struct AnimControlTrackKey
        {
            public NameReference AnimSeqName; //name
            public float StartTime;
            public float AnimStartOffset;
            public float AnimEndOffset;
            public float AnimPlayRate;
            public bool bLooping;
            public bool bReverse;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + StartTime + " : AnimControlTrackKey");
                root.Nodes.Add("AnimSeqName : " + AnimSeqName.Name);
                root.Nodes.Add("StartTime : " + StartTime);
                root.Nodes.Add("AnimStartOffset : " + AnimStartOffset);
                root.Nodes.Add("AnimEndOffset : " + AnimEndOffset);
                root.Nodes.Add("AnimPlayRate : " + AnimPlayRate);
                root.Nodes.Add("bLooping : " + bLooping);
                root.Nodes.Add("bReverse : " + bReverse);
                return root;
            }
        }

        public List<AnimControlTrackKey> AnimSeqs;

        public InterpTrackAnimControl(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "AnimControl";
            GetKeyFrames();
        }

        public void LoadData()
        {
            AnimSeqs = new List<AnimControlTrackKey>();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "AnimSeqs")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        AnimControlTrackKey key = new AnimControlTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "AnimSeqName")
                                key.AnimSeqName = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "StartTime")
                                key.StartTime = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "AnimStartOffset")
                                key.AnimStartOffset = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "AnimEndOffset")
                                key.AnimEndOffset = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "AnimPlayRate")
                                key.AnimPlayRate = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "bLooping")
                                key.bLooping = p2[i].Value.IntValue != 0;
                            else if (pcc.getNameEntry(p2[i].Name) == "bReverse")
                                key.bReverse = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        AnimSeqs.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (AnimSeqs != null)
                foreach (AnimControlTrackKey a in AnimSeqs)
                    keys.Add(GenerateKeyFrame(a.StartTime));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("AnimSeqs");
            for (int i = 0; i < AnimSeqs.Count; i++)
                t.Nodes.Add(AnimSeqs[i].ToTree(i, pcc));
            AddToTree(t);
        }
    }

    public class InterpTrackMove : InterpTrack
    {
        public struct InterpLookupTrack
        {
            public struct Point
            {
                public NameReference GroupName; //name
                public float Time;

                public TreeNode ToTree(int index, PCCObject pcc)
                {
                    TreeNode root = new TreeNode(index + ": " + Time);
                    root.Nodes.Add("GroupName : " + GroupName.Name);
                    root.Nodes.Add("Time : " + Time);
                    return root;
                }
            }

            public List<Point> Points;

            public TreeNode ToTree(PCCObject pcc)
            {
                TreeNode root = new TreeNode("LookupTrack");
                TreeNode t = new TreeNode("Points");
                for (int i = 0; i < Points.Count; i++)
                    t.Nodes.Add(Points[i].ToTree(i, pcc));
                root.Nodes.Add(t);
                return root;
            }
        }

        public InterpCurveVector PosTrack;
        public InterpCurveVector EulerTrack;
        public InterpLookupTrack LookupTrack;
        public bool SFXCreatedBeforeStuntActorLocationChange;
        public bool bUseQuatInterpolation;
        public byteprop MoveFrame = new byteprop("EInterpTrackMoveFrame", new string[] {"IMF_World","IMF_RelativeToInitial"});
        public byteprop RotMode = new byteprop("EInterpTrackMoveRotMode", new string[] { "IMR_Keyframed", "IMR_LookAtGroup"});  
        public int LookAtGroupName = -1;
        public float AngCurveTension = 0f;

        public InterpTrackMove(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Movement";
            GetKeyFrames();
        }

        public void LoadData()
        {
            LookupTrack = new InterpLookupTrack();
            LookupTrack.Points = new List<InterpLookupTrack.Point>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                name = pcc.getNameEntry(p.Name);
                if (name == "bUseQuatInterpolation")
                    bUseQuatInterpolation = p.Value.IntValue != 0;
                if (name == "SFXCreatedBeforeStuntActorLocationChange")
                    SFXCreatedBeforeStuntActorLocationChange = p.Value.IntValue != 0;
                if (name == "MoveFrame")
                    MoveFrame.set(p.raw);
                if (name == "RotMode")
                    RotMode.set(p.raw);
                if (name == "LookAtGroupName")
                    LookAtGroupName = p.Value.IntValue;
                if (name == "AngCurveTension")
                    AngCurveTension = BitConverter.ToSingle(p.raw, 24);
                if (name == "EulerTrack")
                    EulerTrack = GetCurveVector(p, pcc);
                if (name == "PosTrack")
                    PosTrack = GetCurveVector(p, pcc);
                if (name == "LookupTrack")
                {
                    int pos = 60;
                    int count = BitConverter.ToInt32(p.raw, 56);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        InterpLookupTrack.Point point = new InterpLookupTrack.Point();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "Time")
                                point.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "GroupName")
                                point.GroupName = p2[i].Value.NameValue;
                            pos += p2[i].raw.Length;
                        }
                        LookupTrack.Points.Add(point);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (LookupTrack.Points != null)
                foreach (InterpLookupTrack.Point p in LookupTrack.Points)
                    keys.Add(GenerateKeyFrame(p.Time));
            else if (PosTrack.Points != null)
                foreach (InterpCurvePointVector p in PosTrack.Points)
                    keys.Add(GenerateKeyFrame(p.InVal));
            else if (EulerTrack.Points != null)
                foreach (InterpCurvePointVector p in EulerTrack.Points)
                    keys.Add(GenerateKeyFrame(p.InVal));
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree(PosTrack.ToTree("PosTrack", pcc));
            AddToTree(EulerTrack.ToTree("EulerTrack", pcc));
            AddToTree(LookupTrack.ToTree(pcc));
            AddToTree("bUseQuatInterpolation: " + bUseQuatInterpolation);
            AddToTree("SFXCreatedBeforeStuntActorLocationChange : " + SFXCreatedBeforeStuntActorLocationChange);
            AddToTree("MoveFrame: " + MoveFrame.ToString(pcc));
            AddToTree("RotMode: " + RotMode.ToString(pcc));
            if (LookAtGroupName != -1)
                AddToTree("LookAtGroupName: " + LookAtGroupName);
            AddToTree("AngCurveTension: " + AngCurveTension);
        }
    }

    public class InterpTrackVisibility : InterpTrack
    {
        public struct VisibilityTrackKey
        {
            public int Time;
            public byteprop Action;
            public byteprop ActiveCondition;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("Time : " + Time);
                root.Nodes.Add("Action : " + Action.ToString(pcc));
                root.Nodes.Add("ActiveCondition : " + ActiveCondition.ToString(pcc));
                return root;
            }
        }

        public List<VisibilityTrackKey> VisibilityTrack;
        public bool bFireEventsWhenForwards = true;//unused?
        public bool bFireEventsWhenBackwards = true;//unused?
        public bool bFireEventsWhenJumpingForwards = true;//unused?

        public InterpTrackVisibility(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Visibility";
            GetKeyFrames();
        }

        public void LoadData()
        {
            VisibilityTrack = new List<VisibilityTrackKey>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "bFireEventsWhenForwards")
                    bFireEventsWhenForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenBackwards")
                    bFireEventsWhenBackwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenJumpingForwards")
                    bFireEventsWhenJumpingForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "VisibilityTrack")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        VisibilityTrackKey key = new VisibilityTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "Action")
                                key.Action = new byteprop(p2[i].raw, "EVisibilityTrackAction", new string[] { "EVTA_Hide", "EVTA_Show", "EVTA_Toggle" });
                            else if (pcc.getNameEntry(p2[i].Name) == "ActiveCondition")
                                key.ActiveCondition = new byteprop(p2[i].raw, "EVisibilityTrackCondition", new string[] { "EVTC_Always", "EVTC_GoreEnabled", "EVTC_GoreDisabled"});
                            pos += p2[i].raw.Length;
                        }
                        VisibilityTrack.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (VisibilityTrack != null)
                foreach (VisibilityTrackKey k in VisibilityTrack)
                    keys.Add(GenerateKeyFrame(k.Time));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("VisibilityTrack");
            for (int i = 0; i < VisibilityTrack.Count; i++)
                t.Nodes.Add(VisibilityTrack[i].ToTree(i, pcc));
            AddToTree(t);
            //AddToTree("bFireEventsWhenForwards : " + bFireEventsWhenForwards);
            //AddToTree("bFireEventsWhenBackwards : " + bFireEventsWhenBackwards);
            //AddToTree("bFireEventsWhenJumpingForwards : " + bFireEventsWhenJumpingForwards);
        }
    }

    public class InterpTrackToggle : InterpTrack
    {
        public struct ToggleTrackKey
        {
            public float Time;
            public byteprop ToggleAction;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("Time : " + Time);
                root.Nodes.Add("ToggleAction : " + ToggleAction.ToString(pcc));
                return root;
            }
        }

        public List<ToggleTrackKey> ToggleTrack;
        public bool bFireEventsWhenForwards = true;
        public bool bFireEventsWhenBackwards = true;
        public bool bFireEventsWhenJumpingForwards = true;
        public bool bActivateSystemEachUpdate;

        public InterpTrackToggle(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Toggle";
            GetKeyFrames();
        }

        public void LoadData()
        {
            ToggleTrack = new List<ToggleTrackKey>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "bFireEventsWhenForwards")
                    bFireEventsWhenForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenBackwards")
                    bFireEventsWhenBackwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bFireEventsWhenJumpingForwards")
                    bFireEventsWhenJumpingForwards = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bActivateSystemEachUpdate")
                    bActivateSystemEachUpdate = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "ToggleTrack")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        ToggleTrackKey key = new ToggleTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "ToggleAction")
                                key.ToggleAction = new byteprop(p2[i].raw, "ETrackToggleAction", new string[] { "ETTA_Off", "ETTA_On", "ETTA_Toggle", "ETTA_Trigger" });
                            pos += p2[i].raw.Length;
                        }
                        ToggleTrack.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (ToggleTrack != null)
                foreach (ToggleTrackKey t in ToggleTrack)
                    keys.Add(GenerateKeyFrame(t.Time));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("ToggleTrack");
            for (int i = 0; i < ToggleTrack.Count; i++)
                t.Nodes.Add(ToggleTrack[i].ToTree(i, pcc));
            AddToTree(t);
            AddToTree("bFireEventsWhenForwards : " + bFireEventsWhenForwards);
            AddToTree("bFireEventsWhenBackwards : " + bFireEventsWhenBackwards);
            AddToTree("bFireEventsWhenJumpingForwards : " + bFireEventsWhenJumpingForwards);
            AddToTree("bActivateSystemEachUpdate: " + bActivateSystemEachUpdate);
        }
    }

    public class InterpTrackWwiseEvent : InterpTrack
    {
        public struct WwiseEvent
        {
            public float Time;
            public int Event; //object

            public TreeNode ToTree(int index)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("Time : " + Time);
                root.Nodes.Add("Event : " + Event);
                return root;
            }
        }

        public List<WwiseEvent> WwiseEvents = new List<WwiseEvent>();

        public InterpTrackWwiseEvent(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "WwiseEvent";
            GetKeyFrames();
        }

        public void LoadData()
        {
            WwiseEvent key = new WwiseEvent();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "WwiseEvents")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "Event")
                                key.Event = p2[i].Value.IntValue;
                            pos += p2[i].raw.Length;
                        }
                        WwiseEvents.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (WwiseEvents != null)
                foreach (WwiseEvent e in WwiseEvents)
                    keys.Add(GenerateKeyFrame(e.Time));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("WwiseEvents");
            for (int i = 0; i < WwiseEvents.Count; i++)
                t.Nodes.Add(WwiseEvents[i].ToTree(i));
            AddToTree(t);
        }
    }

    public class InterpTrackWwiseSoundEffect : InterpTrackWwiseEvent
    {
        public InterpTrackWwiseSoundEffect(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            if (TrackTitle == "" || TrackTitle == "WwiseEvent")
                TrackTitle = "WwiseSoundEffect";
            GetKeyFrames();
        }
    }

    public class InterpTrackWwiseRTPC : InterpTrackFloatBase
    {
        public string Param;

        public InterpTrackWwiseRTPC(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Float Track")
                TrackTitle = "Wwise RTPC";
        }

        public void LoadData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "Param")
                    Param = p.Value.StringValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("Param : " + Param);
        }
    }

    public class InterpTrackVectorProp : InterpTrackVectorBase
    {
        public int PropertyName; //name

        public InterpTrackVectorProp(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Vector Track")
                TrackTitle = "Vector Property";
        }

        public void LoadData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "PropertyName")
                    PropertyName = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("PropertyName : " + PropertyName);
        }
    }

    public class InterpTrackVectorMaterialParam : InterpTrackVectorBase
    {
        public struct MeshMaterialRef
        {
            public int MeshComp; //object
            public int MaterialIndex;

            public TreeNode ToTree(int index)
            {
                TreeNode root = new TreeNode(index.ToString());
                root.Nodes.Add("MeshComp : " + MeshComp);
                root.Nodes.Add("MaterialIndex : " + MaterialIndex);
                return root;
            }
        }

        public List<MeshMaterialRef> AffectedMaterialRefs = new List<MeshMaterialRef>(); //unused?
        public int ParamName = -1; //name

        public InterpTrackVectorMaterialParam(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Vector Track")
                TrackTitle = "VectorMaterialParam";
        }

        public void LoadData()
        {
            AffectedMaterialRefs = new List<MeshMaterialRef>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "ParamName")
                    ParamName = p.Value.IntValue;
                else if (pcc.getNameEntry(p.Name) == "AffectedMaterialRefs")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        MeshMaterialRef key = new MeshMaterialRef();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "MeshComp")
                                key.MeshComp = p2[i].Value.IntValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "MaterialIndex")
                                key.MaterialIndex = p2[i].Value.IntValue;
                            pos += p2[i].raw.Length;
                        }
                        AffectedMaterialRefs.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (ParamName != -1)
                AddToTree("ParamName : " + ParamName);
            if (AffectedMaterialRefs.Count > 0)
            {
                TreeNode t = new TreeNode("AffectedMaterialRefs");
                for (int i = 0; i < AffectedMaterialRefs.Count; i++)
                    t.Nodes.Add(AffectedMaterialRefs[i].ToTree(i));
                AddToTree(t);
            }
            
        }
    }

    public class InterpTrackColorProp : InterpTrackVectorBase
    {
        public int PropertyName = -1; //name

        public InterpTrackColorProp(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Vector Track")
                TrackTitle = "ColorProperty";
        }

        public void LoadData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "PropertyName")
                    PropertyName = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (PropertyName != -1)
                AddToTree("PropertyName : " + PropertyName);
        }
    }

    public class InterpTrackFloatProp : InterpTrackFloatBase
    {
        public int PropertyName; //name

        public InterpTrackFloatProp(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Float Track")
                TrackTitle = "Float Property";
            GetKeyFrames();
        }

        public void LoadData()
        {
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "PropertyName")
                    PropertyName = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (PropertyName != -1)
                AddToTree("PropertyName : " + PropertyName);
        }
    }

    public class InterpTrackFloatMaterialParam : InterpTrackFloatBase
    {
        public int ParamName; //name

        public InterpTrackFloatMaterialParam(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Float Track")
                TrackTitle = "Float Material Param";
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "ParamName")
                    ParamName = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (ParamName != -1)
                AddToTree("ParamName : " + ParamName);
        }
    }

    public class InterpTrackFloatParticleParam : InterpTrackFloatBase
    {
        public int ParamName; //name

        public InterpTrackFloatParticleParam(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            if (TrackTitle == "" || TrackTitle == "Generic Float Track")
                TrackTitle = "Float Particle Param";
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "ParamName")
                    ParamName = p.Value.IntValue;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            if (ParamName != -1)
                AddToTree("ParamName : " + ParamName);
        }
    }

    public class SFXInterpTrackClientEffect : InterpTrackToggle
    {
        public int m_pEffect;
        public Vector m_vSpawnParameters;
        public bool m_bStopAllMatchingEffects = true;
        public bool m_bAllowCooldown = true;

        public SFXInterpTrackClientEffect(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Client Effect";
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                name = pcc.getNameEntry(p.Name);
                if (name == "m_pEffect")
                    m_pEffect = p.Value.IntValue;
                if(name == "m_vSpawnParameters")
                {
                    m_vSpawnParameters.x = BitConverter.ToSingle(p.raw, 36);
                    m_vSpawnParameters.y = BitConverter.ToSingle(p.raw, 40);
                    m_vSpawnParameters.z = BitConverter.ToSingle(p.raw, 44);
                }
                if (name == "m_bStopAllMatchingEffects")
                    m_bStopAllMatchingEffects = p.Value.IntValue != 0;
                if (name == "m_bAllowCooldown")
                    m_bAllowCooldown = p.Value.IntValue != 0;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("m_pEffect : " + m_pEffect);
            AddToTree(m_vSpawnParameters.ToTree("m_vSpawnParameters"));
            AddToTree("m_bStopAllMatchingEffects: " + m_bStopAllMatchingEffects);
            AddToTree("m_bAllowCooldown: " + m_bAllowCooldown);
        }
    }

    public class InterpTrackSound : InterpTrackVectorBase
    {
        public struct SoundTrackKey
        {
            public float Time;
            public float Volume;
            public float Pitch;
            public int Sound; //object

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("Time : " + Time);
                root.Nodes.Add("Volume : " + Volume);
                root.Nodes.Add("Pitch : " + Pitch);
                root.Nodes.Add("Sound : " + Sound);
                return root;
            }
        }


        public List<SoundTrackKey> Sounds = new List<SoundTrackKey>();
        public bool bContinueSoundOnMatineeEnd;
        public bool bSuppressSubtitles;

        public InterpTrackSound(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Vector Track")
                TrackTitle = "Sound";
        }

        public void LoadData()
        {

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "bContinueSoundOnMatineeEnd")
                    bContinueSoundOnMatineeEnd = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "bSuppressSubtitles")
                    bSuppressSubtitles = p.Value.IntValue != 0;
                else if (pcc.getNameEntry(p.Name) == "Sounds")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        SoundTrackKey key = new SoundTrackKey();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "Volume")
                                key.Volume = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "Pitch")
                                key.Pitch = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "Sound")
                                key.Sound = p2[i].Value.IntValue;
                            pos += p2[i].raw.Length;
                        }
                        Sounds.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("bContinueSoundOnMatineeEnd : " + bContinueSoundOnMatineeEnd);
            AddToTree("bSuppressSubtitles : " + bSuppressSubtitles);
            TreeNode t = new TreeNode("Sounds");
            for (int i = 0; i < Sounds.Count; i++)
                t.Nodes.Add(Sounds[i].ToTree(i, pcc));
        }
    }

    //Director
    public class BioEvtSysTrackDOF : BioInterpTrack
    {
        public struct BioDOFTrackData
        {
            public Vector vFocusPosition;
            public float fFalloffExponent;
            public float fBlurKernelSize;
            public float fMaxNearBlurAmount;
            public float fMaxFarBlurAmount;
            public Color cModulateBlurColor;
            public float fFocusInnerRadius;
            public float fFocusDistance;
            public float fInterpolateSeconds;
            public bool bEnableDOF;

            public TreeNode ToTree(int index, float time, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + time);
                root.Nodes.Add(vFocusPosition.ToTree("vFocusPosition"));
                root.Nodes.Add("fFalloffExponent : " + fFalloffExponent);
                root.Nodes.Add("fBlurKernelSize : " + fBlurKernelSize);
                root.Nodes.Add("fMaxNearBlurAmount : " + fMaxNearBlurAmount);
                root.Nodes.Add("fMaxFarBlurAmount : " + fMaxFarBlurAmount);
                TreeNode n = new TreeNode("cModulateBlurColor");
                n.Nodes.Add("Alpha: " + cModulateBlurColor.A);
                n.Nodes.Add("Red: " + cModulateBlurColor.R);
                n.Nodes.Add("Green: " + cModulateBlurColor.G);
                n.Nodes.Add("Blue: " + cModulateBlurColor.B);
                root.Nodes.Add(n);
                root.Nodes.Add("fFocusInnerRadius : " + fFocusInnerRadius);
                root.Nodes.Add("fFocusDistance : " + fFocusDistance);
                root.Nodes.Add("fInterpolateSeconds : " + fInterpolateSeconds);
                root.Nodes.Add("bEnableDOF : " + bEnableDOF);
                return root;
            }
        }

        public List<BioDOFTrackData> m_aDOFData;

        public BioEvtSysTrackDOF(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "DOF";
        }

        public void LoadData()
        {
            m_aDOFData = new List<BioDOFTrackData>();
            
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            string name;
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "m_aDOFData")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        BioDOFTrackData key = new BioDOFTrackData();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            name = pcc.getNameEntry(p2[i].Name);
                            if (name == "vFocusPosition")
                            {
                                key.vFocusPosition.x = BitConverter.ToSingle(p2[i].raw, 32);
                                key.vFocusPosition.y = BitConverter.ToSingle(p2[i].raw, 36);
                                key.vFocusPosition.z = BitConverter.ToSingle(p2[i].raw, 40);
                            }
                            else if (name == "fFalloffExponent")
                                key.fFalloffExponent = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fBlurKernelSize")
                                key.fBlurKernelSize = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fMaxNearBlurAmount")
                                key.fMaxNearBlurAmount = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fMaxFarBlurAmount")
                                key.fMaxFarBlurAmount = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "cModulateBlurColor")
                                key.cModulateBlurColor = Color.FromArgb(BitConverter.ToInt32(p2[i].raw, 32));
                            else if (name == "fFocusInnerRadius")
                                key.fFocusInnerRadius = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fFocusDistance")
                                key.fFocusDistance = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "fInterpolateSeconds")
                                key.fInterpolateSeconds = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (name == "bEnableDOF")
                                key.bEnableDOF = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        m_aDOFData.Add(key);
                    }
                }
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("m_aDOFData");
            for (int i = 0; i < m_aDOFData.Count; i++)
                t.Nodes.Add(m_aDOFData[i].ToTree(i, m_aTrackKeys[i].fTime, pcc));
            AddToTree(t);
        }
    }

    public class InterpTrackDirector : InterpTrack
    {
        public struct DirectorTrackCut
        {
            public NameReference TargetCamGroup; //name
            public float Time;
            public float TransitionTime;
            public bool bSkipCameraReset;

            public TreeNode ToTree(int index, PCCObject pcc)
            {
                TreeNode root = new TreeNode(index + ": " + Time);
                root.Nodes.Add("TargetCamGroup : " + TargetCamGroup.Name);
                root.Nodes.Add("Time : " + Time);
                root.Nodes.Add("TransitionTime : " + TransitionTime);
                root.Nodes.Add("bSkipCameraReset : " + bSkipCameraReset);
                return root;
            }
        }

        public List<DirectorTrackCut> CutTrack;

        public InterpTrackDirector(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "")
                TrackTitle = "Director";
            GetKeyFrames();
        }

        public void LoadData()
        {
            CutTrack = new List<DirectorTrackCut>();

            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "CutTrack")
                {
                    int pos = 28;
                    int count = BitConverter.ToInt32(p.raw, 24);
                    for (int j = 0; j < count; j++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        DirectorTrackCut key = new DirectorTrackCut();
                        for (int i = 0; i < p2.Count(); i++)
                        {
                            if (pcc.getNameEntry(p2[i].Name) == "TargetCamGroup")
                                key.TargetCamGroup = p2[i].Value.NameValue;
                            else if (pcc.getNameEntry(p2[i].Name) == "Time")
                                key.Time = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "TransitionTime")
                                key.TransitionTime = BitConverter.ToSingle(p2[i].raw, 24);
                            else if (pcc.getNameEntry(p2[i].Name) == "bSkipCameraReset")
                                key.bSkipCameraReset = p2[i].Value.IntValue != 0;
                            pos += p2[i].raw.Length;
                        }
                        CutTrack.Add(key);
                    }
                }
            }
        }

        public override void GetKeyFrames()
        {
            keys = new List<PPath>();
            if (CutTrack != null)
                foreach (DirectorTrackCut c in CutTrack)
                    keys.Add(GenerateKeyFrame(c.Time));
        }

        public override void ToTree()
        {
            base.ToTree();
            TreeNode t = new TreeNode("CutTrack");
            for (int i = 0; i < CutTrack.Count; i++)
                t.Nodes.Add(CutTrack[i].ToTree(i, pcc));
            AddToTree(t);
        }
    }

    public class InterpTrackFade : InterpTrackFloatBase
    {
        public bool bPersistFade;

        public InterpTrackFade(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            LoadData();
            if (TrackTitle == "" || TrackTitle == "Generic Float Track")
                TrackTitle = "Fade";
        }

        public void LoadData()
        {
            BitConverter.IsLittleEndian = true;
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index].Data);
            foreach (PropertyReader.Property p in props)
            {
                if (pcc.getNameEntry(p.Name) == "bPersistFade")
                    bPersistFade = p.Value.IntValue != 0;
            }
        }

        public override void ToTree()
        {
            base.ToTree();
            AddToTree("bPersistFade : " + bPersistFade);
        }
    }

    public class InterpTrackColorScale : InterpTrackVectorBase
    {

        public InterpTrackColorScale(int idx, PCCObject pccobj)
            : base(idx, pccobj)
        {
            if (TrackTitle == "" || TrackTitle == "Generic Vector Track")
                TrackTitle = "ColorScale";
        }

    }
}