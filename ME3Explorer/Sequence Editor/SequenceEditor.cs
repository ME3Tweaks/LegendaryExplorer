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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Util;
using UMD.HCIL.GraphEditor;

using Newtonsoft.Json;
using KFreonLib.MEDirectories;
using Gibbed.IO;


namespace ME3Explorer
{
    public partial class SequenceEditor : Form
    {

        public SequenceEditor()
        {
            if (string.IsNullOrEmpty(ME3Directory.cookedPath))
            {
                MessageBox.Show("This tool requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                this.Close();
                return;
            }
            InitializeComponent();

            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += new PInputEventHandler(backMouseDown_Handler);
            zoomController = new ZoomController(graphEditor);
            
            SText.LoadFont();
            if (File.Exists(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"))
            {
                Dictionary<string, object> options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"));
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

        private const int CLONED_SEQREF_MAGIC = 0x05edf619;

        private bool selectedByNode;
        private bool haveCloned;
        private int selectedIndex;
        private ZoomController zoomController;
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
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        private void LoadFile(string fileName)
        {
            try
            {
                pcc = new PCCObject(fileName);
                haveCloned = false;
                CurrentFile = fileName;
                toolStripStatusLabel1.Text = CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1);
                LoadSequences();
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
                if (CurrentObjects != null)
                    CurrentObjects.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private void LoadSequences()
        {
            treeView1.Nodes.Clear();
            Dictionary<string, TreeNode> prefabs = new Dictionary<string, TreeNode>();
            for (int i = 0; i < pcc.Exports.Count; i++)
            {
                if (pcc.Exports[i].ClassName == "Sequence" && !pcc.getObjectClass(pcc.Exports[i].idxLink).Contains("Sequence"))
                {
                    treeView1.Nodes.Add(FindSequences(pcc, i, !(pcc.Exports[i].ObjectName == "Main_Sequence")));
                }
                if (pcc.Exports[i].ClassName == "Prefab")
                {
                    prefabs.Add(pcc.Exports[i].ObjectName, new TreeNode(pcc.Exports[i].GetFullPath));
                }
            }
            if (prefabs.Count > 0)
            {
                for (int i = 0; i < pcc.Exports.Count; i++)
                {
                    if (pcc.Exports[i].ClassName == "PrefabSequence" && pcc.getObjectClass(pcc.Exports[i].idxLink) == "Prefab")
                    {
                        string parentName = pcc.getObjectName(pcc.Exports[i].idxLink);
                        if (prefabs.ContainsKey(parentName))
                        {
                            prefabs[parentName].Nodes.Add(FindSequences(pcc, i, false));
                        }
                    }
                }
                foreach (var item in prefabs.Values)
                {
                    if (item.Nodes.Count > 0)
                    {
                        treeView1.Nodes.Add(item);
                    }
                }
            }
            if (treeView1.Nodes.Count == 0)
            {
                MessageBox.Show("No Sequences found!");
                return;
            }

            treeView1.ExpandAll();
            if (treeView1.Nodes.Count > 0)
            {
                treeView1.TopNode = treeView1.Nodes[0];
            }
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
                        TreeNode t = FindSequences(pcc, seq.SequenceObjects[i] - 1, false);
                        ret.Nodes.Add(t);
                    }
                    else if (pcc.Exports[seq.SequenceObjects[i] - 1].ClassName == "SequenceReference")
                    {
                        var props = PropertyReader.getPropList(pcc, pcc.Exports[seq.SequenceObjects[i] - 1]);
                        var propSequenceReference = props.FirstOrDefault(p => pcc.getNameEntry(p.Name).Equals("oSequenceReference"));
                        if (propSequenceReference != null)
                        {
                            TreeNode t = FindSequences(pcc, propSequenceReference.Value.IntValue - 1, false);
                            ret.Nodes.Add(t);
                        }
                    }
            return ret;
        }
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(autoSaveViewToolStripMenuItem.Checked)
                saveView();
            if (e.Node.Name != "")
            {
                LoadSequence(Convert.ToInt32(e.Node.Name));
            }
        }

        public void RefreshView()
        {
            saveView(false);
            LoadSequence(SequenceIndex, false);
        }

        private void LoadSequence(int index, bool fromFile = true)
        {
            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;
            SequenceIndex = index;
            toolStripStatusLabel2.Text = "\t#" + SequenceIndex + pcc.Exports[index].ObjectName;
            GetProperties(SequenceIndex);
            GetObjects(SequenceIndex);
            SetupJSON(index);
            if(SavedPositions == null)
                SavedPositions = new List<SaveData>();
            if (fromFile && File.Exists(JSONpath))
                SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
            GenerateGraph(SequenceIndex);
            selectedIndex = -1;
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
        }

        private void SetupJSON(int index)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(pcc.Exports[index].ObjectName, @"[<>:""/\\|?*]", "");
            bool isClonedSeqRef = false;
            PropertyReader.Property p = PropertyReader.getPropOrNull(pcc, pcc.Exports[index], "DefaultViewZoom");
            if (p != null && p.Value.IntValue == CLONED_SEQREF_MAGIC)
            {
                isClonedSeqRef = true;
            }
            if (useGlobalSequenceRefSavesToolStripMenuItem.Checked && pcc.Exports[index].PackageFullName.Contains("SequenceReference") && !isClonedSeqRef)
            {
                JSONpath = ME3Directory.cookedPath + @"\SequenceViews\" + pcc.Exports[index].PackageFullName.Substring(pcc.Exports[index].PackageFullName.LastIndexOf("SequenceReference")) + "." + objectName + ".JSON";
                RefOrRefChild = true;
            }
            else
            {

                JSONpath = ME3Directory.cookedPath + @"\SequenceViews\" + CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1) + ".#" + index + objectName + ".JSON";
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
                    LoadObject(CurrentObjects[i]);
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
        public void LoadObject(int index)
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
                foreach (PPath edge in graphEditor.edgeLayer)
                    GraphEditor.UpdateEdge(edge);
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
                    p = PropertyReader.getPropList(pcc, pcc.Exports[n]);
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
            if (!Directory.Exists(ME3Directory.cookedPath + @"\SequenceViews"))
                Directory.CreateDirectory(ME3Directory.cookedPath + @"\SequenceViews");
            File.WriteAllText(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON", outputFile);

          
            taskbar.RemoveTool(this);
        }

        private void saveViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Bmp Files (*.bmp)|*.bmp";
            if (d.ShowDialog() == DialogResult.OK)
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
            InterpreterHost ip = new InterpreterHost(pcc, n);
            ip.Text = "Interpreter (SequenceEditor)";
            ip.MdiParent = this.MdiParent;
            ip.interpreter1.PropertyValueChanged += Interpreter_PropertyValueChanged;
            ip.Show();
            taskbar.AddTool(ip, Properties.Resources.interpreter_icon_64x64);
        }

        private void Interpreter_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            RefreshView();
        }

        protected void node_MouseDown(object sender, PInputEventArgs e)
        {
            int n = ((SObj)sender).Index;
            n = CurrentObjects.IndexOf(n);
            if (n == -1)
                return;
            selectedByNode = true;
            listBox1.SelectedIndex = n;
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(MousePosition);
                //open in InterpEditor
                string className = pcc.Exports[((SObj)sender).Index].ClassName;
                if (className == "SeqAct_Interp" || className == "InterpData")
                    openInInterpEditorToolStripMenuItem.Visible = true;
                else
                    openInInterpEditorToolStripMenuItem.Visible = false;
                //break links
                breakLinksToolStripMenuItem.Enabled = false;
                breakLinksToolStripMenuItem.DropDown = null;
                if(sender.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SAction") || sender.GetType() == Type.GetType("ME3Explorer.SequenceObjects.SEvent"))
                {
                    ToolStripMenuItem temp;
                    ArrayList array;
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    ToolStripDropDown varLinkMenu = new ToolStripDropDown();
                    ToolStripDropDown outLinkMenu = new ToolStripDropDown();
                    for (int i = 0; i < ((SBox)sender).Varlinks.Count; i++)
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
                                varLinkMenu.Items.Add(temp);
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
                                outLinkMenu.Items.Add(temp);
                            }
                        }
                    }
                    if(varLinkMenu.Items.Count > 0)
                    {
                        temp = new ToolStripMenuItem("Variable Links");
                        temp.DropDown = varLinkMenu;
                        submenu.Items.Add(temp);
                    }
                    if (outLinkMenu.Items.Count > 0)
                    {
                        temp = new ToolStripMenuItem("Output Links");
                        temp.DropDown = outLinkMenu;
                        submenu.Items.Add(temp);
                    }
                    if (submenu.Items.Count > 0)
                    {
                        temp = new ToolStripMenuItem("Break all Links");
                        temp.Click += new EventHandler(removeAllLinks_handler);
                        temp.Tag = sender;
                        submenu.Items.Add(temp);
                        breakLinksToolStripMenuItem.Enabled = true;
                        breakLinksToolStripMenuItem.DropDown = submenu;
                    }
                }
            }
        }

        private void removeAllLinks_handler(object sender, EventArgs e)
        {
            SBox obj = (SBox)((ToolStripMenuItem)sender).Tag;
            removeAllLinks(obj);
        }

        private void removeAllLinks(SBox obj)
        {
            if (obj is SBox)
            {
                for (int i = 0; i < obj.Outlinks.Count; i++)
                {
                    if (obj.Outlinks[i].Links[0] != -1)
                    {
                        for (int j = 0; j < obj.Outlinks[i].Links.Count; j++)
                        {
                            obj.RemoveOutlink(i, 0, false);
                        }
                    }
                }
                for (int i = 0; i < obj.Varlinks.Count; i++)
                {
                    if (obj.Varlinks[i].Links[0] != -1)
                    {
                        for (int j = 0; j < obj.Varlinks[i].Links.Count; j++)
                        {
                            obj.RemoveVarlink(i, 0, false);
                        }
                    }
                }
                RefreshView(); 
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
                if (!Directory.Exists(JSONpath.Remove(JSONpath.LastIndexOf('\\'))))
                    Directory.CreateDirectory(JSONpath.Remove(JSONpath.LastIndexOf('\\')));
                File.WriteAllText(JSONpath, outputFile);
                SavedPositions.Clear();
            }
            
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            graphEditor.ScaleViewTo((float)Convert.ToDecimal(toolStripTextBox1.Text));
        }

        private void openInPackageEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int l = CurrentObjects[listBox1.SelectedIndex];
            if (l == -1)
                return;
            PackageEditor p = new PackageEditor();
            p.MdiParent = this.MdiParent;
            p.WindowState = FormWindowState.Maximized;
            p.Show();
            taskbar.AddTool(p, Properties.Resources.package_editor_64x64);
            p.LoadFile(CurrentFile);
            p.listBox1.SelectedIndex = l;
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
                        if (pcc.Exports[i].inheritsFrom("SequenceObject"))
                        {
                            addObjectToSequence(i);
                        }
                        else
                        {
                            MessageBox.Show(i + " is not a sequence object.");
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

        private void addObjectToSequence(int index, bool removeLinks = true)
        {
            byte[] buff = pcc.Exports[index].Data;
            PropertyReader.Property p = PropertyReader.getPropOrNull(pcc, pcc.Exports[index], "ParentSequence");
            if (p != null)
            {
                byte[] val = BitConverter.GetBytes(SequenceIndex + 1);
                for (int j = 0; j < 4; j++)
                {
                    buff[p.offsetval + j] = val[j];
                }
                pcc.Exports[index].Data = buff;
            }
            pcc.Exports[index].idxLink = SequenceIndex + 1;
            //add to sequence
            buff = pcc.Exports[SequenceIndex].Data;
            List<byte> ListBuff = new List<byte>(buff);
            BitConverter.IsLittleEndian = true;
            p = PropertyReader.getPropOrNull(pcc, pcc.Exports[SequenceIndex], "SequenceObjects");
            if (p != null)
            {
                int count = BitConverter.ToInt32(p.raw, 24);
                byte[] sizebuff = BitConverter.GetBytes(BitConverter.ToInt32(p.raw, 16) + 4);
                byte[] countbuff = BitConverter.GetBytes(count + 1);
                for (int j = 0; j < 4; j++)
                {
                    ListBuff[p.offsetval - 8 + j] = sizebuff[j];
                    ListBuff[p.offsetval + j] = countbuff[j];
                }
                ListBuff.InsertRange(p.offsetval + 4 + count * 4, BitConverter.GetBytes(index + 1));
                pcc.Exports[SequenceIndex].Data = ListBuff.ToArray();
                SaveData s = new SaveData();
                s.index = index;
                s.X = graphEditor.Camera.Bounds.X + graphEditor.Camera.Bounds.Width / 2;
                s.Y = graphEditor.Camera.Bounds.Y + graphEditor.Camera.Bounds.Height / 2;
                List<SaveData> list = new List<SaveData>();
                list.Add(s);
                saveView(false, list);
                LoadSequence(SequenceIndex, false);
                if (removeLinks)
                {
                    removeAllLinks(Objects.First(x => x.Index == index) as SBox); 
                }
            }
        }

        private void pg1_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
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
            Type parentVal = null;
            if (parent.Value != null)
	        {
                parentVal = parent.Value.GetType();
	        }
            if (name == "nameindex" || name == "index" || parentVal == typeof(Unreal.ColorProp) || parentVal == typeof(Unreal.VectorProp) || parentVal == typeof(Unreal.RotatorProp))
            {
                name = parent.Label;
            }
            PCCObject.ExportEntry ent = pcc.Exports[n];
            List<PropertyReader.Property> p = PropertyReader.getPropList(pcc, ent);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.Names[p[i].Name] == name)
                    m = i;
            if (m == -1)
                return;
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
                case PropertyReader.Type.StrProperty:
                    string s = Convert.ToString(e.ChangedItem.Value);
                    int oldLength = -(int)BitConverter.ToInt64(ent.Data, p[m].offsetval);
                    List<byte> stringBuff = new List<byte>(s.Length * 2);
                    for (int i = 0; i < s.Length; i++)
                    {
                        stringBuff.AddRange(BitConverter.GetBytes(s[i]));
                    }
                    stringBuff.Add(0);
                    stringBuff.Add(0);
                    buff2 = BitConverter.GetBytes((s.LongCount() + 1) * 2 + 4);
                    for (int i = 0; i < 4; i++)
                        ent.Data[p[m].offsetval - 8 + i] = buff2[i];
                    buff2 = BitConverter.GetBytes(-(s.LongCount() + 1));
                    for (int i = 0; i < 8; i++)
                        ent.Data[p[m].offsetval + i] = buff2[i];
                    buff2 = new byte[ent.Data.Length - (oldLength * 2) + stringBuff.Count];
                    int startLength = p[m].offsetval + 4;
                    int startLength2 = startLength + (oldLength * 2);
                    for (int i = 0; i < startLength; i++)
                    {
                        buff2[i] = ent.Data[i];
                    }
                    for (int i = 0; i < stringBuff.Count; i++)
                    {
                        buff2[i + startLength] = stringBuff[i];
                    }
                    startLength += stringBuff.Count;
                    for (int i = 0; i < ent.Data.Length - startLength2; i++)
                    {
                        buff2[i + startLength] = ent.Data[i + startLength2];
                    }
                    ent.Data = buff2;
                    break;
                case PropertyReader.Type.StructProperty:
                    if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.ColorProp))
                    {
                        switch (e.ChangedItem.Label)
                        {
                            case "Alpha":
                                ent.Data[p[m].offsetval + 11] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Red":
                                ent.Data[p[m].offsetval + 10] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Green":
                                ent.Data[p[m].offsetval + 9] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Blue":
                                ent.Data[p[m].offsetval + 8] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            default:
                                break;
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.VectorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "X":
                                offset = 8;
                                break;
                            case "Y":
                                offset = 12;
                                break;
                            case "Z":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.RotatorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Pitch":
                                offset = 8;
                                break;
                            case "Yaw":
                                offset = 12;
                                break;
                            case "Roll":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            int val = Convert.ToInt32(Convert.ToSingle(e.ChangedItem.Value) * 65536f / 360f);
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.LinearColorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Red":
                                offset = 8;
                                break;
                            case "Green":
                                offset = 12;
                                break;
                            case "Blue":
                                offset = 16;
                                break;
                            case "Alpha":
                                offset = 20;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                ent.Data[p[m].offsetval + offset + i] = buff2[i];
                        }
                        int t = listBox1.SelectedIndex;
                        listBox1.SelectedIndex = -1;
                        listBox1.SelectedIndex = t;
                    }
                    else if (e.ChangedItem.Value.GetType() == typeof(int))
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
            RefreshView();
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
            p.LoadPCC(CurrentFile);
            taskbar.AddTool(p, Properties.Resources.interp_viewer_icon_64x64);
            if (pcc.Exports[Objects[listBox1.SelectedIndex].Index].ObjectName == "InterpData")
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
            graphEditor.Focus();
        }

        private void loadAlternateTLKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TlkManager tm = new TlkManager();
            tm.InitTlkManager();
            tm.Show();
            taskbar.AddTool(tm, Properties.Resources.TLKManager_icon_64x64);
        }

        private void SequenceEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList().Where(f => f.EndsWith(".pcc")).ToList();
            if (DroppedFiles.Count > 0)
            {
                LoadFile(DroppedFiles[0]);
            }
        }

        private void SequenceEditor_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        private void savePccToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            if (haveCloned)
            {
                pcc.saveByReconstructing(pcc.pccFileName);
            }
            else
            {
                pcc.altSaveToFile(pcc.pccFileName, true);
            }
            MessageBox.Show("Done");
        }

        private void savePCCAsMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
            {
                if (haveCloned)
                {
                    pcc.saveByReconstructing(d.FileName);
                }
                else
                {
                    pcc.altSaveToFile(d.FileName, true);
                }
                MessageBox.Show("Done");
            }
        }

        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip2.Show(MousePosition);
            }
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!pcc.canClone())
            {
                return;
            }
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;
            treeView1.Enabled = false;
            graphEditor.updating = true;
            cloneObject(n);
            graphEditor.updating = false;
            graphEditor.Invalidate();
            treeView1.Enabled = true;
        }

        private void cloneObject(int n, bool topLevel = true)
        {
            PCCObject.ExportEntry exp = pcc.Exports[n].Clone();
            //needs to have the same index to work properly
            if (exp.ClassName == "SeqVar_External")
            {
                exp.indexValue = pcc.Exports[n].indexValue;
            }
            pcc.addExport(exp);
            haveCloned = true;
            int expIndex = pcc.Exports.Count - 1;
            addObjectToSequence(expIndex, topLevel);
            if (exp.ClassName == "Sequence")
            {
                int originalSequenceIndex = SequenceIndex;
                PropertyReader.Property p = PropertyReader.getPropOrNull(pcc, exp, "SequenceObjects");
                if (p == null)
                {
                    return;
                }

                //store original list of sequence objects;
                List<int> oldObjects = new List<int>();
                int count = BitConverter.ToInt32(p.raw, 24);
                if (count == 0)
                {
                    return;
                }
                for (int i = 0; i < count; i++)
                    oldObjects.Add(BitConverter.ToInt32(p.raw, 28 + i * 4) - 1);

                //refresh list of sequences and select just-cloned one
                LoadSequences();
                TreeNode[] nodes = treeView1.Nodes.Find(expIndex.ToString(), true);
                if (nodes.Length == 0)
                {
                    throw new Exception();
                }
                treeView1.SelectedNode = nodes[0];
                Application.DoEvents();
               SaveData[] positions = new SaveData[SavedPositions.Count];
                SavedPositions.CopyTo(positions); 

                //clone all children
                int index = 0;
                for (int i = 0; i < count; i++)
                {
                    index = oldObjects[i];
                    cloneObject(index, false);
                }

                //remove old objects
                List<byte> memList = exp.Data.ToList();
                byte[] buff = BitConverter.GetBytes(4 + count * 4);
                for (int i = 0; i < 4; i++)
                {
                    memList[p.offsetval - 8 + i] = buff[i];
                }
                buff = BitConverter.GetBytes(count);
                for (int i = 0; i < 4; i++)
                {
                    memList[p.offsetval + i] = buff[i];
                }
                memList.RemoveRange(p.offsetval + 4, 4 * count);
                exp.Data = memList.ToArray();

                //restore saved positions and refresh sequence
                SavedPositions = positions.ToList();
                LoadSequence(SequenceIndex, false);

                //for non-reference sequences, map saved positions to new objects
                if (!RefOrRefChild)
                {
                    SetupJSON(n);
                    if (File.Exists(JSONpath))
                    {
                        SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
                        if (SavedPositions.Count > 0 && SavedPositions[0].absoluteIndex == false)
                        {
                            try
                            {
                                SavedPositions = SavedPositions.Select((x, i) =>
                                {
                                    SaveData d = new SaveData();
                                    d.X = x.X;
                                    d.Y = x.Y;
                                    d.index = CurrentObjects[oldObjects.IndexOf(x.index)];
                                    return d;
                                }).ToList();
                            }
                            catch (Exception)
                            {
                                SavedPositions = null;
                            }
                            finally
                            {
                                LoadSequence(SequenceIndex, false);
                            }
                        }
                    } 
                }

                #region re-linking
                //re-point children's links to new objects
                byte[] data;
                foreach (int objIndex in CurrentObjects)
                {
                    p = PropertyReader.getPropOrNull(pcc, pcc.Exports[objIndex], "OutputLinks");
                    if (p != null)
                    {
                        data = pcc.Exports[objIndex].Data;
                        int pos = 28;
                        int linkCount = BitConverter.ToInt32(p.raw, 24);
                        for (int i = 0; i < linkCount; i++)
                        {
                            List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            for (int j = 0; j < p2.Count(); j++)
                            {
                                pos += p2[j].raw.Length;
                                if (pcc.getNameEntry(p2[j].Name) == "Links")
                                {
                                    int count2 = BitConverter.ToInt32(p2[j].raw, 24);
                                    if (count2 != 0)
                                    {
                                        for (int k = 0; k < count2; k += 1)
                                        {
                                            List<PropertyReader.Property> p3 = PropertyReader.ReadProp(pcc, p2[j].raw, 28 + k * 64);
                                            buff = BitConverter.GetBytes(CurrentObjects[oldObjects.IndexOf(p3[0].Value.IntValue - 1)] + 1);
                                            data.OverwriteRange(p.offsetval - 24 + pos - p2[j].raw.Length + p3[0].offsetval, buff);
                                        }
                                    }
                                }
                            }
                        }
                        pcc.Exports[objIndex].Data = data;
                    }
                    p = PropertyReader.getPropOrNull(pcc, pcc.Exports[objIndex], "VariableLinks");
                    if (p != null)
                    {
                        data = pcc.Exports[objIndex].Data;
                        int pos = 28;
                        int linkCount = BitConverter.ToInt32(p.raw, 24);
                        for (int j = 0; j < count; j++)
                        {
                            List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            for (int i = 0; i < p2.Count(); i++)
                            {
                                pos += p2[i].raw.Length;
                                if (pcc.getNameEntry(p2[i].Name) == "LinkedVariables")
                                {
                                    int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                                    if (count2 != 0)
                                    {
                                        for (int k = 0; k < count2; k += 1)
                                        {
                                            buff = BitConverter.GetBytes(CurrentObjects[oldObjects.IndexOf(BitConverter.ToInt32(p2[i].raw, 28 + k * 4) - 1)] + 1);
                                            data.OverwriteRange(p.offsetval - 24 + pos + 28 + k * 4 - p2[i].raw.Length, buff);
                                        }
                                    }
                                }
                            }
                        }
                        pcc.Exports[objIndex].Data = data;
                    }
                    p = PropertyReader.getPropOrNull(pcc, pcc.Exports[objIndex], "EventLinks");
                    if (p != null)
                    {
                        data = pcc.Exports[objIndex].Data;
                        int pos = 28;
                        int linkCount = BitConverter.ToInt32(p.raw, 24);
                        for (int j = 0; j < count; j++)
                        {
                            List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            for (int i = 0; i < p2.Count(); i++)
                            {
                                pos += p2[i].raw.Length;
                                if (pcc.getNameEntry(p2[i].Name) == "LinkedEvents")
                                {
                                    int count2 = BitConverter.ToInt32(p2[i].raw, 24);
                                    if (count2 != 0)
                                    {
                                        for (int k = 0; k < count2; k += 1)
                                        {
                                            buff = BitConverter.GetBytes(CurrentObjects[oldObjects.IndexOf(BitConverter.ToInt32(p2[i].raw, 28 + k * 4) - 1)] + 1);
                                            data.OverwriteRange(p.offsetval - 24 + pos + 28 + k * 4 - p2[i].raw.Length, buff);
                                        }
                                    }
                                }
                            }
                        }
                        pcc.Exports[objIndex].Data = data;
                    }
                }

                //re-point sequence links to new objects
                int oldObj = 0;
                int newObj = 0;
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex], "InputLinks");
                if (p != null)
                {
                    data = pcc.Exports[expIndex].Data;
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkedOp")
                            {
                                oldObj = p2[j].Value.IntValue;
                                if (oldObj != 0)
                                {
                                    newObj = CurrentObjects[oldObjects.IndexOf(oldObj - 1)];
                                    data.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(newObj + 1));
                                    //set index for LinkAction property
                                    data.OverwriteRange(p.offsetval - 24 + pos - 60, BitConverter.GetBytes(pcc.Exports[newObj].indexValue));
                                }
                            }
                        }
                    }
                    pcc.Exports[expIndex].Data = data;
                }
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex], "OutputLinks");
                if (p != null)
                {
                    data = pcc.Exports[expIndex].Data;
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkedOp")
                            {
                                oldObj = p2[j].Value.IntValue;
                                if (oldObj != 0)
                                {
                                    newObj = CurrentObjects[oldObjects.IndexOf(oldObj - 1)];
                                    data.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(newObj + 1));
                                    //set index for LinkAction property
                                    data.OverwriteRange(p.offsetval - 24 + pos - 32, BitConverter.GetBytes(pcc.Exports[newObj].indexValue));
                                }
                            }
                        }
                    }
                    pcc.Exports[expIndex].Data = data;
                }
                #endregion

                //reselect original sequence
                LoadSequences();
                nodes = treeView1.Nodes.Find(originalSequenceIndex.ToString(), true);
                if (nodes.Length == 0)
                {
                    throw new Exception();
                }
                treeView1.SelectedNode = nodes[0];
                Application.DoEvents();
            }
            else if (exp.ClassName == "SequenceReference")
            {
                int originalSequenceIndex = SequenceIndex;
                bool temp = useGlobalSequenceRefSavesToolStripMenuItem.Checked;
                useGlobalSequenceRefSavesToolStripMenuItem.Checked = false;

                //set OSequenceReference to new sequence
                PropertyReader.Property p = PropertyReader.getPropOrNull(pcc, exp, "oSequenceReference");
                if (p == null || p.Value.IntValue == 0)
                {
                    return;
                } 
                exp.Data.OverwriteRange(p.offsetval, BitConverter.GetBytes(expIndex + 1 + 1));

                //clone sequence
                cloneObject(p.Value.IntValue - 1, false);

                //remove cloned sequence from SeqRef's parent's sequenceobjects
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[SequenceIndex], "SequenceObjects");
                List<byte> memList = pcc.Exports[SequenceIndex].Data.ToList();
                int count = BitConverter.ToInt32(pcc.Exports[SequenceIndex].Data, p.offsetval) - 1;
                byte[] buff = BitConverter.GetBytes(4 + count * 4);
                for (int i = 0; i < 4; i++)
                {
                    memList[p.offsetval - 8 + i] = buff[i];
                }
                buff = BitConverter.GetBytes(count);
                for (int i = 0; i < 4; i++)
                {
                    memList[p.offsetval + i] = buff[i];
                }
                memList.RemoveRange(p.offsetval + 4 + (count * 4), 4);
                pcc.Exports[SequenceIndex].Data = memList.ToArray();

                //set SequenceReference's linked name indices
                List<int> inputIndices = new List<int>();
                List<int> outputIndices = new List<int>();
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex + 1], "InputLinks");
                if (p != null)
                {
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkAction")
                            {
                                inputIndices.Add(p2[j].Value.NameValue.count);
                            }
                        }
                    }
                }
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex + 1], "OutputLinks");
                if (p != null)
                {
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkAction")
                            {
                                outputIndices.Add(p2[j].Value.NameValue.count);
                            }
                        }
                    }
                }
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex], "InputLinks");
                if (p != null)
                {
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkAction")
                            {
                                pcc.Exports[expIndex].Data.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(inputIndices[i]));
                            }
                        }
                    }
                }
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex], "OutputLinks");
                if (p != null)
                {
                    int pos = 28;
                    int linkCount = BitConverter.ToInt32(p.raw, 24);
                    for (int i = 0; i < linkCount; i++)
                    {
                        List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                        for (int j = 0; j < p2.Count(); j++)
                        {
                            pos += p2[j].raw.Length;
                            if (pcc.getNameEntry(p2[j].Name) == "LinkAction")
                            {
                                pcc.Exports[expIndex].Data.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(outputIndices[i]));
                            }
                        }
                    }
                }


                //set new Sequence's link and ParentSequence prop to SeqRef
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex + 1], "ParentSequence");
                if (p == null)
                {
                    throw new Exception();
                }
                pcc.Exports[expIndex + 1].Data.OverwriteRange(p.offsetval, BitConverter.GetBytes(expIndex + 1));
                pcc.Exports[expIndex + 1].idxLink = expIndex + 1;

                //set DefaultViewZoom to magic number to flag that this is a cloned Sequence Reference and global saves cannot be used with it
                //ugly, but it should work
                p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex + 1], "DefaultViewZoom");
                if (p != null)
                {
                    pcc.Exports[expIndex + 1].Data.OverwriteRange(p.offsetval, BitConverter.GetBytes(CLONED_SEQREF_MAGIC));
                }
                else
                {
                    p = PropertyReader.getPropOrNull(pcc, pcc.Exports[expIndex + 1], "None");
                    memList = pcc.Exports[expIndex + 1].Data.ToList();
                    memList.InsertRange(p.offsetval, BitConverter.GetBytes(pcc.FindNameOrAdd("DefaultViewZoom")));
                    memList.InsertRange(p.offsetval + 4, new byte[4]);
                    memList.InsertRange(p.offsetval + 8, BitConverter.GetBytes(pcc.FindNameOrAdd("FloatProperty")));
                    memList.InsertRange(p.offsetval + 12, new byte[4]);
                    memList.InsertRange(p.offsetval + 16, BitConverter.GetBytes(4));
                    memList.InsertRange(p.offsetval + 20, new byte[4]);
                    memList.InsertRange(p.offsetval + 24, BitConverter.GetBytes(CLONED_SEQREF_MAGIC));
                    pcc.Exports[expIndex + 1].Data = memList.ToArray();
                }
                
                useGlobalSequenceRefSavesToolStripMenuItem.Checked = temp;

                if (topLevel)
                {
                    //reselect original sequence
                    LoadSequences();
                    TreeNode[] nodes = treeView1.Nodes.Find(originalSequenceIndex.ToString(), true);
                    if (nodes.Length == 0)
                    {
                        throw new Exception();
                    }
                    treeView1.SelectedNode = nodes[0];
                    Application.DoEvents();
                }
            }
        }
    }

    public class ZoomController
    {
        public static float MIN_SCALE = .005f;
        public static float MAX_SCALE = 15;
        PCamera camera;

        public ZoomController(GraphEditor graphEditor)
        {
            this.camera = graphEditor.Camera;
            camera.Canvas.ZoomEventHandler = null;
            camera.MouseWheel += new PInputEventHandler(OnMouseWheel);
            graphEditor.KeyDown += OnKeyDown;
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

        private void scaleView(float scaleDelta, PointF p)
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
