using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.SequenceObjects;
using ME3Explorer.TOCUpdater;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;

using Newtonsoft.Json;
using KFreonLib.MEDirectories;


namespace ME3Explorer
{
    public partial class SequenceEditor : Form
    {

        public SequenceEditor()
        {
            if (String.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This tool requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                this.Close();
                return;
            }
            InitializeComponent();

            graphEditor.BackColor = Color.FromArgb(167, 167, 167);

            var tlkPath = ME3Directory.cookedPath + "BIOGame_INT.tlk";
            talkFile = new TalkFile();
            talkFile.LoadTlkData(tlkPath);
            if(SText.fontcollection == null)
                SText.fontcollection = LoadFont("KismetFont.ttf", 8);
            SObj.talkfile = talkFile;
            if (System.IO.File.Exists(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"))
            {
                Dictionary<string, object> options = JsonConvert.DeserializeObject<Dictionary<string, object>>(System.IO.File.ReadAllText(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"));
                if (options.ContainsKey("AutoSave")) 
                    autoSaveViewToolStripMenuItem.Checked = (bool)options["AutoSave"];
                if (options.ContainsKey("OutputNumbers"))
                    showOutputNumbersToolStripMenuItem.Checked = (bool)options["OutputNumbers"];
                if (options.ContainsKey("GlobalSeqRefView"))
                    useGlobalSequenceRefSavesToolStripMenuItem.Checked = (bool)options["GlobalSeqRefView"];
                SObj.OutputNumbers = showOutputNumbersToolStripMenuItem.Checked;
            }
        }

        private struct SaveData
        {
            public bool absoluteIndex;
            public int index;
            public float X;
            public float Y;
        }

        private TalkFile talkFile;
        private bool selectedByNode;
        private int selectedIndex;
        public TreeNode SeqTree;
        public PropGrid pg;
        public PCCObject pcc;
        public List<int> CurrentObjects;
        public List<SObj> Objects;
        private List<SaveData> SavedPositions;
        public int SequenceIndex;
        public bool RefOrRefChild;

        public string CurrentFile;
        public string JSONpath;


        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (autoSaveViewToolStripMenuItem.Checked)
                saveView();
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "PCC Files(*.pcc)|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc = new PCCObject(d.FileName);
                CurrentFile = d.FileName;
                toolStripStatusLabel1.Text = CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1);
                LoadSequences();
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                if(CurrentObjects != null)
                    CurrentObjects.Clear();
            }
        }

        private void LoadSequences()
        {
            int f = -1;
            treeView1.Nodes.Clear();

            // Search for a main sequence.
            for (int i = 0; i < pcc.Exports.Count; i++)
                if ((pcc.Exports[i].ObjectName == "Main_Sequence") &&
                   pcc.Exports[i].ClassName == "Sequence")
                    f = i;

            if (f > -1)
            {
                // Found a main sequence.  Add it and its children.
                SeqTree = FindSequences(pcc, f);
                treeView1.Nodes.Add(SeqTree);
            }
            else
            {
                // No main sequence.  Try searching for others -- we may be in a dialogue-driving file (LOC_INT) which have no main sequence.
                for (int i = 0; i < pcc.Exports.Count; i++)
                    if (pcc.Exports[i].ClassName == "Sequence") // (pcc.Exports[i].ObjectName == "Node_Data_Sequence") &&
                    {
                        treeView1.Nodes.Add(FindSequences(pcc, i, true));
                        f = -2; // Dirty flag so we don't display error message.  Yes, I am a lazy coder.
                    }
            }
            if (f == -1)
            {
                MessageBox.Show("No Main Sequence found!");
                return;
            }

            treeView1.ExpandAll();
        }
        public TreeNode FindSequences(PCCObject pcc, int index, bool wantFullName = false)
        {
            TreeNode ret = new TreeNode("#" + index.ToString() + ": " + (wantFullName ? pcc.Exports[index].GetFullPath : pcc.Exports[index].ObjectName));
            ret.Name = index.ToString();
            Sequence seq = new Sequence(pcc, index);
            if (seq.SequenceObjects != null)
                for (int i = 0; i < seq.SequenceObjects.Count(); i++)
                    if (pcc.Exports[seq.SequenceObjects[i] - 1].ClassName == "Sequence" || pcc.Exports[seq.SequenceObjects[i] - 1].ClassName.StartsWith("PrefabSequence"))
                    {
                        TreeNode t = FindSequences(pcc, seq.SequenceObjects[i] - 1, wantFullName);
                        ret.Nodes.Add(t);
                    }
                    else if (pcc.Exports[seq.SequenceObjects[i] - 1].ClassName == "SequenceReference")
                    {
                        var props = ME3Explorer.Unreal.PropertyReader.getPropList(pcc, pcc.Exports[seq.SequenceObjects[i] - 1].Data);
                        var propSequenceReference = props.FirstOrDefault(p => pcc.getNameEntry(p.Name).Equals("oSequenceReference"));
                        if (propSequenceReference != null)
                        {
                            TreeNode t = FindSequences(pcc, propSequenceReference.Value.IntValue - 1, wantFullName);
                            ret.Nodes.Add(t);
                        }
                    }
            return ret;
        }
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(autoSaveViewToolStripMenuItem.Checked)
                saveView();
            LoadSequence(Convert.ToInt32(e.Node.Name));
        }

        public void RefreshView()
        {
            saveView(false);
            LoadSequence(SequenceIndex, false);
        }

        private void LoadSequence(int index, bool fromFile = true)
        {
            SequenceIndex = index;
            toolStripStatusLabel2.Text = "\t#" + SequenceIndex + pcc.Exports[index].ObjectName;
            GetProperties(SequenceIndex);
            GetObjects(SequenceIndex);
            SetupJSON(index);
            if(SavedPositions == null)
                SavedPositions = new List<SaveData>();
            if (fromFile && System.IO.File.Exists(JSONpath))
                SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(System.IO.File.ReadAllText(JSONpath));
            GenerateGraph(SequenceIndex);
            selectedIndex = -1;
        }

        private void SetupJSON(int index)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(pcc.Exports[index].ObjectName, @"[<>:""/\\|?*]", "");
            if (useGlobalSequenceRefSavesToolStripMenuItem.Checked && pcc.Exports[index].PackageFullName.Contains("SequenceReference"))
            {
                JSONpath = ME3Directory.cookedPath + @"\SequenceViews\" + pcc.Exports[index].PackageFullName.Substring(pcc.Exports[index].PackageFullName.LastIndexOf("SequenceReference")) + "." + objectName + ".JSON";
                RefOrRefChild = true;
            }
            else
            {

                JSONpath = ME3Directory.cookedPath + @"\SequenceViews\" + CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1) + ".#" + SequenceIndex + objectName + ".JSON";
                RefOrRefChild = false;
            }
        }

        public void GenerateGraph(int n)
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            Sequence seq = new Sequence(pcc, n);
            StartPosEvents = 0;
            StartPosActions = 0;
            StartPosVars = 0;
            Objects = new List<SObj>();
            if (CurrentObjects != null)
            {
                for (int i = 0; i < CurrentObjects.Count(); i++)
                {
                    LoadObjects(CurrentObjects[i]);
                }
                CreateConnections();
            }
            foreach (SObj o in Objects)
            {
                o.refreshView = RefreshView;
                o.MouseDown += new PInputEventHandler(node_MouseDown);
            }
            if (SavedPositions.Count == 0 && CurrentFile.Contains("_LOC_INT"))
            {
                LoadDialogueObjects();
            }
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;
        public void LoadObjects(int index)
        {
            string s = pcc.Exports[index].ObjectName;
            SaveData savedInfo;
            if (RefOrRefChild)
                savedInfo = SavedPositions.FirstOrDefault(p => CurrentObjects.IndexOf(index) == p.index);
            else
                savedInfo = SavedPositions.FirstOrDefault(p => index == p.index);

            if (s.StartsWith("BioSeqEvt_") || s.StartsWith("SeqEvt_") || s.StartsWith("SFXSeqEvt_") || s.StartsWith("SeqEvent_"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(index) : index))
                {
                    Objects.Add(new SEvent(index, savedInfo.X, savedInfo.Y, pcc, graphEditor));
                }
                else
                {
                    Objects.Add(new SEvent(index, StartPosEvents, 0, pcc, graphEditor));
                    StartPosEvents += Objects[Objects.Count - 1].Width + 20;
                }
            }
             else if (s.StartsWith("SeqVar_") || s.StartsWith("BioSeqVar_") || s.StartsWith("SFXSeqVar_") || s.StartsWith("InterpData"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(index) : index))
                    Objects.Add(new SVar(index, savedInfo.X, savedInfo.Y, pcc, graphEditor));
                else
                {
                    Objects.Add(new SVar(index, StartPosVars, 500, pcc, graphEditor));
                    StartPosVars += Objects[Objects.Count - 1].Width + 20;
                }
            }
            else //if (s.StartsWith("BioSeqAct_") || s.StartsWith("SeqAct_") || s.StartsWith("SFXSeqAct_") || s.StartsWith("SeqCond_") || pcc.Exports[index].ClassName == "Sequence" || pcc.Exports[index].ClassName == "SequenceReference")
            {
                Objects.Add(new SAction(index, -1, -1, pcc, graphEditor));
            }
        }

        public bool LoadDialogueObjects()
        {
            float StartPosDialog = 0;
            int InterpIndex;
            try
            {
                for (int i = 0; i < CurrentObjects.Count; i++)
                {
                    if (pcc.Exports[CurrentObjects[i]].ObjectName.StartsWith("BioSeqEvt_ConvNode"))
                    {
                        Objects[i].SetOffset(StartPosDialog, 600);//Startconv event
                        InterpIndex = CurrentObjects.IndexOf(((SEvent)Objects[i]).Outlinks[0].Links[0]);
                        Objects[InterpIndex].SetOffset(StartPosDialog + 150, 600);//Interp
                        Objects[CurrentObjects.IndexOf(((SAction)Objects[InterpIndex]).Varlinks[0].Links[0])].SetOffset(StartPosDialog + 165, 770);//Interpdata
                        StartPosDialog += Objects[InterpIndex].Width + 200;
                        Objects[CurrentObjects.IndexOf(((SAction)Objects[InterpIndex]).Outlinks[0].Links[0])].SetOffset(StartPosDialog, 600);//Endconv node
                        StartPosDialog += 270;
                    }
                }
                foreach (PPath edge in graphEditor.edgeLayer)
                    GraphEditor.UpdateEdge(edge);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        public void CreateConnections()
        {
            if (Objects != null && Objects.Count != 0)
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    graphEditor.addNode(Objects[i]);
                }
                foreach (SObj o in graphEditor.nodeLayer)
                {
                    o.CreateConnections(ref Objects);
                }
                foreach (SObj o in graphEditor.nodeLayer)
                {
                    if (o.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SAction"))
                    {
                        SaveData savedInfo = new SaveData();
                        if(RefOrRefChild)
                            savedInfo = SavedPositions.FirstOrDefault(p => CurrentObjects.IndexOf(o.Index) == p.index);
                        else
                            savedInfo = SavedPositions.FirstOrDefault(p => o.Index == p.index);
                        if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(o.Index) : o.Index))
                            o.Layout(savedInfo.X, savedInfo.Y);
                        else
                        {
                            o.Layout(StartPosActions, 250);
                            StartPosActions += o.Width + 20;
                        }
                    }
                }
                foreach (PPath edge in graphEditor.edgeLayer)
                {
                    GraphEditor.UpdateEdge(edge);
                }
            }
        }

        public void GetObjects(int n)
        {
            CurrentObjects = new List<int>();
            listBox1.Items.Clear();
            Sequence seq = new Sequence(pcc, n);
            if (seq.SequenceObjects != null)
                for (int i = 0; i < seq.SequenceObjects.Count(); i++)
                {
                    int m = seq.SequenceObjects[i] - 1;
                    CurrentObjects.Add(m);
                    listBox1.Items.Add("#" + m.ToString() + " :" + pcc.Exports[m].ObjectName + " class: " + pcc.Exports[m].ClassName);
                }
        }
        public void GetProperties(int n)
        {
            List<PropertyReader.Property> p;
            switch (pcc.Exports[n].ClassName)
            {
                default:
                    byte[] buff = pcc.Exports[n].Data;
                    p = PropertyReader.getPropList(pcc, buff);
                    break;
            }
            pg = new PropGrid();
            pg1.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.Exports[n].ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.Exports[n].ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.Exports[n].DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.Exports[n].DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));
            pg1.Refresh();
        }
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || n < 0 || n >= CurrentObjects.Count())
                return;
            GetProperties(CurrentObjects[n]);
            SObj s = Objects.FirstOrDefault(o => o.Index == CurrentObjects[n]);
            if (s != null)
            {
                s.Select();
                if (selectedIndex != -1)
                {
                    SObj d = Objects.FirstOrDefault(o => o.Index == CurrentObjects[selectedIndex]);
                    if (d != null)
                        d.Deselect();
                }
                if (!selectedByNode)
                    graphEditor.Camera.AnimateViewToPanToBounds(s.GlobalFullBounds, 0);
            }
            selectedIndex = n;
            selectedByNode = false;
        }
        private void SequenceEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(autoSaveViewToolStripMenuItem.Checked)
                saveView();

            Dictionary<string, object> options = new Dictionary<string, object>();
            options.Add("OutputNumbers", SObj.OutputNumbers);
            options.Add("AutoSave", autoSaveViewToolStripMenuItem.Checked);
            options.Add("GlobalSeqRefView", useGlobalSequenceRefSavesToolStripMenuItem.Checked);
            string outputFile = JsonConvert.SerializeObject(options);
            if (!System.IO.Directory.Exists(ME3Directory.cookedPath + @"\SequenceViews"))
                System.IO.Directory.CreateDirectory(ME3Directory.cookedPath + @"\SequenceViews");
            System.IO.File.WriteAllText(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON", outputFile);

          
            taskbar.RemoveTool(this);
        }

        private void saveViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Bmp Files (*.bmp)|*.bmp";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PNode r = graphEditor.Root;
                RectangleF rr = r.GlobalFullBounds;
                PNode p = PPath.CreateRectangle(rr.X, rr.Y, rr.Width, rr.Height);
                p.Brush = Brushes.White;
                graphEditor.addBack(p);
                graphEditor.Camera.Visible = false;
                Image image = graphEditor.Root.ToImage();
                graphEditor.Camera.Visible = true;
                image.Save(d.FileName, ImageFormat.Bmp);
                graphEditor.backLayer.RemoveAllChildren();
                MessageBox.Show("Done.");
            }
        }

        private void savePccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pcc.altSaveToFile(d.FileName, true);
                if (MessageBox.Show("Do you want to update TOC.bin?", "ME3Explorer", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    TOCUpdater.TOCUpdater tc = new TOCUpdater.TOCUpdater();
                    tc.MdiParent = this.ParentForm;
                    tc.Show();
                    tc.EasyUpdate();
                    tc.Close();
                }
            }
        }

        private void interpretToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (listBox1.SelectedIndex == -1)
            {
                if (treeView1.SelectedNode == null)
                    return;
                else
                    n = Convert.ToInt32(treeView1.SelectedNode.Name);
            }
            else
                n = CurrentObjects[n];
            Interpreter2.Interpreter2 ip = new Interpreter2.Interpreter2();
            ip.MdiParent = this.MdiParent;
            ip.pcc = pcc;
            ip.Index = n;
            ip.InitInterpreter(talkFile);
            ip.Show();
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            int n = ((SObj)sender).Index;
            n = CurrentObjects.IndexOf(n);
            if (n == -1)
                return;
            selectedByNode = true;
            listBox1.SelectedIndex = n;
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                contextMenuStrip1.Show(MousePosition);
                //open in InterpEditor
                string className = pcc.Exports[((SObj)sender).Index].ClassName;
                if (className == "SeqAct_Interp" || className == "InterpData")
                    openInInterpEditorToolStripMenuItem.Visible = true;
                else
                    openInInterpEditorToolStripMenuItem.Visible = false;
                //add inputs
                if (sender.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SAction"))
                {
                    addInputLinkToolStripMenuItem.Enabled = true;
                }
                else
                {
                    addInputLinkToolStripMenuItem.Enabled = false;
                }
                //break links
                breakLinksToolStripMenuItem.Enabled = false;
                breakLinksToolStripMenuItem.DropDown = null;
                if(sender.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SAction") || sender.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SEvent"))
                {
                    ToolStripMenuItem temp;
                    ArrayList array;
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    for(int i = 0; i < ((SBox)sender).Varlinks.Count; i++)
                    {
                        for (int j = 0; j < ((SBox)sender).Varlinks[i].Links.Count; j++)
                        {
                            if (((SBox)sender).Varlinks[i].Links[j] != -1)
                            {
                                temp = new ToolStripMenuItem("Break link from " + ((SBox)sender).Varlinks[i].Desc + " to " + ((SBox)sender).Varlinks[i].Links[j]);
                                temp.Click += new EventHandler(removeLink_handler);
                                array = new ArrayList();
                                array.Add(sender);
                                array.Add(0);
                                array.Add(i);
                                array.Add(j);
                                temp.Tag = array;
                                submenu.Items.Add(temp);
                            }
                        }
                    }
                    for (int i = 0; i < ((SBox)sender).Outlinks.Count; i++)
                    {
                        for (int j = 0; j < ((SBox)sender).Outlinks[i].Links.Count; j++)
                        {
                            if (((SBox)sender).Outlinks[i].Links[j] != -1)
                            {
                                temp = new ToolStripMenuItem("Break link from " + ((SBox)sender).Outlinks[i].Desc + " to " + ((SBox)sender).Outlinks[i].Links[j] + " :" + ((SBox)sender).Outlinks[i].InputIndices[j]);
                                temp.Click += new EventHandler(removeLink_handler);
                                array = new ArrayList();
                                array.Add(sender);
                                array.Add(1);
                                array.Add(i);
                                array.Add(j);
                                temp.Tag = array;
                                submenu.Items.Add(temp);
                            }
                        }
                    }
                    if(submenu.Items.Count > 0)
                    {
                        breakLinksToolStripMenuItem.Enabled = true;
                        breakLinksToolStripMenuItem.DropDown = submenu;
                    }
                }
            }
        }

        private void removeLink_handler(object sender, EventArgs e)
        {
            ArrayList array = (ArrayList)((ToolStripMenuItem)sender).Tag;
            if ((int)array[1] == 1)
                ((SBox)array[0]).RemoveOutlink((int)array[2], (int)array[3]);
            else
                ((SBox)array[0]).RemoveVarlink((int)array[2], (int)array[3]);

        }

        private void saveViewToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (CurrentObjects == null)
                return;
            saveView();
            MessageBox.Show("Done.");
        }

        private void saveView(bool toFile = true, List<SaveData> extra = null)
        {
            if (CurrentObjects == null || CurrentObjects.Count == 0)
                return;
            SavedPositions = new List<SaveData>();
            foreach (SObj p in graphEditor.nodeLayer)
            {
                SaveData s = new SaveData();
                if (p.Pickable)
                {
                    s.absoluteIndex = RefOrRefChild;
                    s.index = RefOrRefChild ? CurrentObjects.IndexOf(p.Index) : p.Index;
                    s.X = p.X + p.Offset.X;
                    s.Y = p.Y + p.Offset.Y;
                    SavedPositions.Add(s);
                }
            }
            if (extra != null)
                SavedPositions.AddRange(extra);
            if (toFile)
            {
                string outputFile = JsonConvert.SerializeObject(SavedPositions);
                if (!System.IO.Directory.Exists(JSONpath.Remove(JSONpath.LastIndexOf('\\'))))
                    System.IO.Directory.CreateDirectory(JSONpath.Remove(JSONpath.LastIndexOf('\\')));
                System.IO.File.WriteAllText(JSONpath, outputFile);
            }
            
        }

        public static PrivateFontCollection LoadFont(string file, int fontSize)
        {
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(file);
            if (fontCollection.Families.Length < 0)
            {
                throw new InvalidOperationException("No font familiy found when loading font");
            }
            return fontCollection;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            graphEditor.ScaleViewTo((float)Convert.ToDecimal(toolStripTextBox1.Text));
        }

        private void openInPCCEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int l = CurrentObjects[listBox1.SelectedIndex];
            if (l == -1)
                return;
            PCCEditor2 p = new PCCEditor2();
            p.MdiParent = this.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.pcc = new PCCObject(CurrentFile);
            p.SetView(2);
            p.RefreshView();
            p.InitStuff();
            p.listBox1.SelectedIndex = l;
        }

        private void addInputLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            SObj s = Objects.FirstOrDefault(o => o.Index == CurrentObjects[n]);
            if (s.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SAction"))
                ((SAction)s).AddInputLink();
        }

        private void addObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects == null)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter object index", "ME3Explorer");
            if (result == "")
                return;
            int i;
            if (int.TryParse(result, out i))
            {
                if(i < pcc.Exports.Count)
                {
                    if (!CurrentObjects.Contains(i))
                    {
                        byte[] buff = pcc.Exports[SequenceIndex].Data;
                        List<byte> ListBuff = new List<byte>(buff);
                        BitConverter.IsLittleEndian = true;
                        List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, buff);
                        for (int j = 0; j < p.Count(); j++)
                        {
                            if (pcc.getNameEntry(p[j].Name) == "SequenceObjects")
                            {
                                int count = BitConverter.ToInt32(p[j].raw, 24);
                                byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p[j].raw, 16) + 4);
                                byte[] countbuff = BitConverter.GetBytes(count + 1);
                                for (int k = 0; k < 4; k++)
                                {
                                    ListBuff[p[j].offsetval - 8 + k] = sizebuff[k];
                                    ListBuff[p[j].offsetval + k] = countbuff[k];
                                }
                                ListBuff.InsertRange(p[j].offsetval + 4 + count * 4, BitConverter.GetBytes(i + 1));
                                pcc.Exports[SequenceIndex].Data = ListBuff.ToArray();
                                SaveData s = new SaveData();
                                s.index = i;
                                s.X = graphEditor.Camera.Bounds.X + graphEditor.Camera.Bounds.Width / 2;
                                s.Y = graphEditor.Camera.Bounds.Y + graphEditor.Camera.Bounds.Height / 2;
                                List<SaveData> list = new List<SaveData>();
                                list.Add(s);
                                saveView(false, list);
                                LoadSequence(SequenceIndex, false);
                                break;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(i + " is already in the sequence.");
                    }
                }
                else
                {
                    MessageBox.Show(i + " is not in the export list.");
                }
            }
            else
            {
                MessageBox.Show(result + " is not an integer.");
            }
        }

        private void pg1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {

            int n = listBox1.SelectedIndex;
            if (n == -1 )
                return;
            n = CurrentObjects[n];
            string name = e.ChangedItem.Label;
            GridItem parent = e.ChangedItem.Parent;
            //if (parent != null) name = parent.Label;
            if (parent.Label == "data")
            {
                GridItem parent2 = parent.Parent;
                if (parent2 != null) name = parent2.Label;
            }
            if (name == "nameindex")
            {
                name = parent.Label;
            }
            byte[] buff = pcc.Exports[n].Data;
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, buff);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.Names[p[i].Name] == name)
                    m = i;
            if (m == -1)
                return;
            PCCObject.ExportEntry ent = pcc.Exports[n];
            byte[] buff2;
            switch (p[m].TypeVal)
            {
                case PropertyReader.Type.BoolProperty:
                    byte res = 0;
                    if ((bool)e.ChangedItem.Value == true)
                        res = 1;
                    ent.Data[p[m].offsetval] = res;
                    break;
                case PropertyReader.Type.FloatProperty:
                    buff2 = BitConverter.GetBytes((float)e.ChangedItem.Value);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyReader.Type.IntProperty:
                case PropertyReader.Type.StringRefProperty:
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyReader.Type.StructProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        if (e.ChangedItem.Label == "nameindex")
                        {
                            int val1 = Convert.ToInt32(e.ChangedItem.Value);
                            buff2 = BitConverter.GetBytes(val1);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + i] = buff2[i];
                            int t = listBox1.SelectedIndex;
                            listBox1.SelectedIndex = -1;
                            listBox1.SelectedIndex = t;
                        }
                        else
                        {
                            string sidx = e.ChangedItem.Label.Replace("[", "");
                            sidx = sidx.Replace("]", "");
                            int index = Convert.ToInt32(sidx);
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + i + index * 4 + 8] = buff2[i];
                            int t = listBox1.SelectedIndex;
                            listBox1.SelectedIndex = -1;
                            listBox1.SelectedIndex = t;
                        }
                    }
                    break;
                case PropertyReader.Type.ByteProperty:
                case PropertyReader.Type.NameProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            ent.Data[p[m].offsetval + i] = buff2[i];
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    break;
                case PropertyReader.Type.ObjectProperty:
                    if (e.ChangedItem.Value.GetType() == typeof(int))
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            ent.Data[p[m].offsetval + i] = buff2[i];
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    break;
                default:
                    return;
            }
            pcc.Exports[n] = ent;
            pg1.ExpandAllGridItems();
        }

        private void showOutputNumbersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SObj.OutputNumbers = showOutputNumbersToolStripMenuItem.Checked;
            saveView(false);
            if(CurrentObjects != null)
                LoadSequence(SequenceIndex, false);
        }

        private void openInInterpEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;
            InterpEditor.InterpEditor p = new InterpEditor.InterpEditor();
            p.MdiParent = this.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            p.LoadPCC(CurrentFile, talkFile);
            if (pcc.getClassName(Objects[listBox1.SelectedIndex].Index) == "InterpData")
            {
                p.toolStripComboBox1.SelectedIndex = p.objects.IndexOf(n);
                p.loadInterpData(n);
            }
            else
            {
                p.toolStripComboBox1.SelectedIndex = p.objects.IndexOf(((SAction)Objects[listBox1.SelectedIndex]).Varlinks[0].Links[0]);
                p.loadInterpData(((SAction)Objects[listBox1.SelectedIndex]).Varlinks[0].Links[0]);
            }
        }

        private void useGlobalSequenceRefSavesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects == null)
                return;
            SetupJSON(SequenceIndex);
        }

        private void graphEditor_Click(object sender, EventArgs e)
        {

        }

        private void loadAlternateTLKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.tlk|*.tlk";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                talkFile.LoadTlkData(d.FileName);
                RefreshView();
                MessageBox.Show("Done.");
            }
        }

    }

}
