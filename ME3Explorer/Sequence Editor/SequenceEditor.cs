using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using ME3Explorer.SequenceObjects;

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Nodes;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.GraphEditor;

using Newtonsoft.Json;
using KFreonLib.MEDirectories;
using Gibbed.IO;


namespace ME3Explorer
{
    public partial class SequenceEditor : WinFormsBase
    {

        public SequenceEditor()
        {
            InitializeComponent();

            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            zoomController = new ZoomController(graphEditor);

            

            SText.LoadFont();
            if (SObj.talkfiles == null)
            {
                talkFiles = new ME1Explorer.TalkFiles();
                talkFiles.LoadGlobalTlk();
                SObj.talkfiles = talkFiles; 
            }
            else
            {
                talkFiles = SObj.talkfiles;
            }
        }

        private void SequenceEditor_Load(object sender, EventArgs e)
        {
            Dictionary<string, object> options = null;
            
            #region Migrate data from legacy locations
            if (Directory.Exists(ME3Directory.cookedPath + @"\SequenceViews\") ||
                    Directory.Exists(ME2Directory.cookedPath + @"\SequenceViews\") ||
                    Directory.Exists(ME1Directory.cookedPath + @"\SequenceViews\"))
            {
                try
                {
                    if (File.Exists(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"))
                    {
                        options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"));
                        File.Delete(ME3Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON");
                    }
                    if (File.Exists(ME2Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"))
                    {
                        File.Delete(ME2Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON");
                    }
                    if (File.Exists(ME1Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON"))
                    {
                        File.Delete(ME1Directory.cookedPath + @"\SequenceViews\SequenceEditorOptions.JSON");
                    }


                    var comp = new Microsoft.VisualBasic.Devices.Computer();
                    if (Directory.Exists(ME3Directory.cookedPath + @"\SequenceViews\"))
                    {
                        Directory.CreateDirectory(ME3ViewsPath);
                        comp.FileSystem.CopyDirectory(ME3Directory.cookedPath + @"\SequenceViews\", ME3ViewsPath);
                        comp.FileSystem.DeleteDirectory(ME3Directory.cookedPath + @"\SequenceViews\", Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                    if (Directory.Exists(ME2Directory.cookedPath + @"\SequenceViews\"))
                    {
                        Directory.CreateDirectory(ME2ViewsPath);
                        comp.FileSystem.CopyDirectory(ME2Directory.cookedPath + @"\SequenceViews\", ME2ViewsPath);
                        comp.FileSystem.DeleteDirectory(ME2Directory.cookedPath + @"\SequenceViews\", Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                    if (Directory.Exists(ME1Directory.cookedPath + @"\SequenceViews\"))
                    {
                        Directory.CreateDirectory(ME1ViewsPath);
                        comp.FileSystem.CopyDirectory(ME1Directory.cookedPath + @"\SequenceViews\", ME1ViewsPath);
                        comp.FileSystem.DeleteDirectory(ME1Directory.cookedPath + @"\SequenceViews\", Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error migrating old data, Previously created sequence views may not be available:\n{ex.Message}");
                }
            } 
            #endregion

            if (File.Exists(OptionsPath))
            {
                options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(OptionsPath));
            }
            if (options != null)
            {
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

            public SaveData(int i) : this()
            {
                index = i;
            }
        }

        public static readonly string SequenceEditorDataFolder = Path.Combine(App.AppDataFolder, @"SequenceEditor\");
        public static readonly string OptionsPath = Path.Combine(SequenceEditorDataFolder, "SequenceEditorOptions.JSON");
        public static readonly string ME3ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME3SequenceViews\");
        public static readonly string ME2ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME2SequenceViews\");
        public static readonly string ME1ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME1SequenceViews\");

        private const int CLONED_SEQREF_MAGIC = 0x05edf619;

        private ME1Explorer.TalkFiles talkFiles;
        private bool selectedByNode;
        private int selectedIndex;
        private ZoomController zoomController;
        public TreeNode SeqTree;
        public PropGrid pg;
        /// <summary>
        /// List of export-indices of exports in loaded sequence.
        /// </summary>
        public List<int> CurrentObjects = new List<int>();
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
            d.Filter = App.FileFilter;
            if (d.ShowDialog() == DialogResult.OK)
            {
                LoadFile(d.FileName);
            }
        }

        private void LoadFile(string fileName)
        {
            try
            {
                LoadMEPackage(fileName);
                CurrentFile = fileName;
                toolStripStatusLabel1.Text = CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1);
                LoadSequences();
                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();
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
            for (int i = 0; i < pcc.ExportCount; i++)
            {
                IExportEntry exportEntry = pcc.getExport(i);
                if (exportEntry.ClassName == "Sequence" && !pcc.getObjectClass(exportEntry.idxLink).Contains("Sequence"))
                {
                    treeView1.Nodes.Add(FindSequences(pcc, i, !(exportEntry.ObjectName == "Main_Sequence")));
                }
                if (exportEntry.ClassName == "Prefab")
                {
                    prefabs.Add(exportEntry.ObjectName, new TreeNode(exportEntry.GetFullPath));
                }
            }
            if (prefabs.Count > 0)
            {
                for (int i = 0; i < pcc.ExportCount; i++)
                {
                    IExportEntry exportEntry = pcc.getExport(i);
                    if (exportEntry.ClassName == "PrefabSequence" && pcc.getObjectClass(exportEntry.idxLink) == "Prefab")
                    {
                        string parentName = pcc.getObjectName(exportEntry.idxLink);
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
        public TreeNode FindSequences(IMEPackage pcc, int index, bool wantFullName = false)
        {
            TreeNode ret = new TreeNode("#" + index + ": " + (wantFullName ? pcc.getExport(index).GetFullPath : pcc.getExport(index).ObjectName));
            ret.Name = index.ToString();
            Sequence seq = new Sequence(pcc, index);
            if (seq.SequenceObjects != null)
            {
                IExportEntry exportEntry;
                for (int i = 0; i < seq.SequenceObjects.Count(); i++)
                {
                    exportEntry = pcc.getExport(seq.SequenceObjects[i] - 1);
                    if (exportEntry.ClassName == "Sequence" || exportEntry.ClassName.StartsWith("PrefabSequence"))
                    {
                        TreeNode t = FindSequences(pcc, seq.SequenceObjects[i] - 1, false);
                        ret.Nodes.Add(t);
                    }
                    else if (exportEntry.ClassName == "SequenceReference")
                    {
                        var props = PropertyReader.getPropList(exportEntry);
                        var propSequenceReference = props.FirstOrDefault(p => pcc.getNameEntry(p.Name).Equals("oSequenceReference"));
                        if (propSequenceReference != null)
                        {
                            TreeNode t = FindSequences(pcc, propSequenceReference.Value.IntValue - 1, false);
                            ret.Nodes.Add(t);
                        }
                    }
                }
            }
            return ret;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(autoSaveViewToolStripMenuItem.Checked)
                saveView();
            SavedPositions = null;
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
            toolStripStatusLabel2.Text = "\t#" + SequenceIndex + pcc.getExport(index).ObjectName;
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
            string objectName = System.Text.RegularExpressions.Regex.Replace(pcc.getExport(index).ObjectName, @"[<>:""/\\|?*]", "");
            bool isClonedSeqRef = false;
            PropertyReader.Property p = PropertyReader.getPropOrNull(pcc.getExport(index), "DefaultViewZoom");
            if (p != null && p.Value.IntValue == CLONED_SEQREF_MAGIC)
            {
                isClonedSeqRef = true;
            }

            string packageFullName = pcc.getExport(index).PackageFullName;
            if (useGlobalSequenceRefSavesToolStripMenuItem.Checked && packageFullName.Contains("SequenceReference") && !isClonedSeqRef)
            {
                if (pcc.Game == MEGame.ME3)
                {
                    JSONpath = ME3ViewsPath + packageFullName.Substring(packageFullName.LastIndexOf("SequenceReference")) + "." + objectName + ".JSON"; 
                }
                else
                {
                    string packageName = pcc.getExport(index).PackageFullName.Substring(pcc.getExport(index).PackageFullName.LastIndexOf("SequenceReference"));
                    packageName = packageName.Replace("SequenceReference", "");
                    int idx = index;
                    string ObjName = "";
                    while (idx > 0)
                    {
                        if (pcc.getExport(pcc.getExport(idx).idxLink - 1).ClassName == "SequenceReference")
                        {
                            PropertyReader.Property prop = PropertyReader.getPropOrNull(pcc.getExport(idx), "ObjName");
                            if (prop != null)
                            {
                                ObjName = prop.Value.StringValue;
                                break;
                            }
                        }
                        idx = pcc.getExport(idx).idxLink - 1;

                    }
                    if (objectName == "Sequence")
                    {
                        objectName = ObjName;
                        packageName = "." + packageName;
                    }
                    else
                        packageName = packageName.Replace("Sequence", ObjName) + ".";
                    if (pcc.Game == MEGame.ME2)
                    {
                        JSONpath = ME2ViewsPath + "SequenceReference" + packageName + objectName + ".JSON";
                    }
                    else
                    {
                        JSONpath = ME1ViewsPath + "SequenceReference" + packageName + objectName + ".JSON";
                    }
                }
                RefOrRefChild = true;
            }
            else
            {
                string viewsPath = ME3ViewsPath;
                if (pcc.Game == MEGame.ME2)
                {
                    viewsPath = ME2ViewsPath;
                }
                else if (pcc.Game == MEGame.ME1)
                {
                    viewsPath = ME1ViewsPath;
                }
                JSONpath = viewsPath + CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1) + ".#" + index + objectName + ".JSON";
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
                o.MouseDown += node_MouseDown;
            }
            if (SavedPositions.Count == 0 && CurrentFile.Contains("_LOC_INT") && pcc.Game != MEGame.ME1)
            {
                LoadDialogueObjects();
            }
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;
        public void LoadObject(int index)
        {
            string s = pcc.getExport(index).ObjectName;
            int x = 0, y = 0;
            SaveData savedInfo = new SaveData(-1);
            if (SavedPositions.Count > 0)
            {
                if (RefOrRefChild)
                    savedInfo = SavedPositions.FirstOrDefault(p => CurrentObjects.IndexOf(index) == p.index);
                else
                    savedInfo = SavedPositions.FirstOrDefault(p => index == p.index); 
            }
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc.getExport(index));
            foreach (PropertyReader.Property prop in props)
            {
                if (pcc.getNameEntry(prop.Name) == "ObjPosX")
                {
                    x = prop.Value.IntValue;
                }
                else if (pcc.getNameEntry(prop.Name) == "ObjPosY")
                    y = prop.Value.IntValue;
            }

            if (s.StartsWith("BioSeqEvt_") || s.StartsWith("SeqEvt_") || s.StartsWith("SFXSeqEvt_") || s.StartsWith("SeqEvent_"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(index) : index))
                {
                    Objects.Add(new SEvent(index, savedInfo.X, savedInfo.Y, pcc, graphEditor));
                }
                else
                {
                    if (pcc.Game == MEGame.ME1)
                    {
                        Objects.Add(new SEvent(index, x, y, pcc, graphEditor));
                    }
                    else
                    {
                        Objects.Add(new SEvent(index, StartPosEvents, 0, pcc, graphEditor));
                        StartPosEvents += Objects[Objects.Count - 1].Width + 20; 
                    }
                }
            }
             else if (s.StartsWith("SeqVar_") || s.StartsWith("BioSeqVar_") || s.StartsWith("SFXSeqVar_") || s.StartsWith("InterpData"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(index) : index))
                    Objects.Add(new SVar(index, savedInfo.X, savedInfo.Y, pcc, graphEditor));
                else
                {
                    if (pcc.Game == MEGame.ME1)
                    {
                        Objects.Add(new SVar(index, x, y, pcc, graphEditor));
                    }
                    else
                    {
                        Objects.Add(new SVar(index, StartPosVars, 500, pcc, graphEditor));
                        StartPosVars += Objects[Objects.Count - 1].Width + 20; 
                    }
                }
            }
            else if (pcc.getExport(index).ClassName == "SequenceFrame" && pcc.Game == MEGame.ME1)
            {
                Objects.Add(new SFrame(index, x, y, pcc, graphEditor));
            }
            else //if (s.StartsWith("BioSeqAct_") || s.StartsWith("SeqAct_") || s.StartsWith("SFXSeqAct_") || s.StartsWith("SeqCond_") || pcc.getExport(index).ClassName == "Sequence" || pcc.getExport(index).ClassName == "SequenceReference")
            {
                if (pcc.Game == MEGame.ME1)
                {
                    Objects.Add(new SAction(index, x, y, pcc, graphEditor));
                }
                else
                {
                    Objects.Add(new SAction(index, -1, -1, pcc, graphEditor)); 
                }
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
                    if (pcc.getExport(CurrentObjects[i]).ObjectName.StartsWith("BioSeqEvt_ConvNode"))
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
                    if (o is SAction)
                    {
                        SaveData savedInfo = new SaveData(-1);
                        if (SavedPositions.Count > 0)
                        {
                            if (RefOrRefChild)
                                savedInfo = SavedPositions.FirstOrDefault(p => CurrentObjects.IndexOf(o.Index) == p.index);
                            else
                                savedInfo = SavedPositions.FirstOrDefault(p => o.Index == p.index); 
                        }
                        if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(o.Index) : o.Index))
                            o.Layout(savedInfo.X, savedInfo.Y);
                        else
                        {
                            if (pcc.Game == MEGame.ME1)
                            {
                                o.Layout(-0.1f, -0.1f);
                            }
                            else
                            {
                                o.Layout(StartPosActions, 250);
                                StartPosActions += o.Width + 20; 
                            }
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
                    listBox1.Items.Add("#" + m + " :" + pcc.getExport(m).ObjectName + " class: " + pcc.getExport(m).ClassName);
                }
        }

        public void GetProperties(int n)
        {
            List<PropertyReader.Property> p;
            switch (pcc.getExport(n).ClassName)
            {
                default:
                    p = PropertyReader.getPropList(pcc.getExport(n));
                    break;
            }
            pg = new PropGrid();
            pg1.SelectedObject = pg;
            pg.Add(new CustomProperty("Name", "_Meta", pcc.getExport(n).ObjectName, typeof(string), true, true));
            pg.Add(new CustomProperty("Class", "_Meta", pcc.getExport(n).ClassName, typeof(string), true, true));
            pg.Add(new CustomProperty("Data Offset", "_Meta", pcc.getExport(n).DataOffset, typeof(int), true, true));
            pg.Add(new CustomProperty("Data Size", "_Meta", pcc.getExport(n).DataSize, typeof(int), true, true));
            for (int l = 0; l < p.Count; l++)
                pg.Add(PropertyReader.PropertyToGrid(p[l], pcc));
            pg1.Refresh();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1 || n < 0 || n >= CurrentObjects.Count())
                return;
            SObj s = Objects.FirstOrDefault(o => o.Index == CurrentObjects[n]);
            if (s != null)
            {
                if (selectedIndex != -1)
                {
                    SObj d = Objects.FirstOrDefault(o => o.Index == CurrentObjects[selectedIndex]);
                    if (d != null)
                        d.Deselect();
                }
                s.Select();
                if (!selectedByNode)
                    graphEditor.Camera.AnimateViewToPanToBounds(s.GlobalFullBounds, 0);
            }
            GetProperties(CurrentObjects[n]);
            selectedIndex = n;
            selectedByNode = false;
            graphEditor.Refresh();
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
            if (!Directory.Exists(SequenceEditorDataFolder))
                Directory.CreateDirectory(SequenceEditorDataFolder);
            File.WriteAllText(OptionsPath, outputFile);
        }

        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects.Count == 0)
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
            InterpreterHost ip = new InterpreterHost(pcc.FileName, n);
            ip.Text = "Interpreter (SequenceEditor)";
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
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(MousePosition);
                //open in InterpEditor
                string className = pcc.getExport(((SObj)sender).Index).ClassName;
                if (className == "SeqAct_Interp" || className == "InterpData")
                    openInInterpEditorToolStripMenuItem.Visible = true;
                else
                    openInInterpEditorToolStripMenuItem.Visible = false;
                //break links
                breakLinksToolStripMenuItem.Enabled = false;
                breakLinksToolStripMenuItem.DropDown = null;
                if(sender is SAction || sender is SEvent)
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
                                temp.Click += removeLink_handler;
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
                                temp.Click += removeLink_handler;
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
                        temp.Click += removeAllLinks_handler;
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
                            obj.RemoveOutlink(i, 0);
                        }
                    }
                }
                for (int i = 0; i < obj.Varlinks.Count; i++)
                {
                    if (obj.Varlinks[i].Links[0] != -1)
                    {
                        for (int j = 0; j < obj.Varlinks[i].Links.Count; j++)
                        {
                            obj.RemoveVarlink(i, 0);
                        }
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
            if (CurrentObjects.Count == 0)
                return;
            saveView();
            MessageBox.Show("Done.");
        }

        private void saveView(bool toFile = true, List<SaveData> extra = null)
        {
            if (CurrentObjects.Count == 0)
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
                if (!Directory.Exists(Path.GetDirectoryName(JSONpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(JSONpath));
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
            p.Show();
            p.LoadFile(CurrentFile);
            p.goToNumber(l);
        }
        
        private void addObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CurrentObjects.Count == 0)
                return;
            string result = Microsoft.VisualBasic.Interaction.InputBox("Please enter object index", "ME3Explorer");
            if (result == "")
                return;
            int i;
            if (int.TryParse(result, out i))
            {
                if(i < pcc.ExportCount)
                {
                    if (!CurrentObjects.Contains(i))
                    {
                        if (pcc.getExport(i).inheritsFrom("SequenceObject"))
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
            IExportEntry newObject = pcc.getExport(index);
            byte[] buff = newObject.Data;
            PropertyReader.Property p = PropertyReader.getPropOrNull(newObject, "ParentSequence");
            if (p != null)
            {
                byte[] val = BitConverter.GetBytes(SequenceIndex + 1);
                for (int j = 0; j < 4; j++)
                {
                    buff[p.offsetval + j] = val[j];
                }
                newObject.Data = buff;
            }
            newObject.idxLink = SequenceIndex + 1;
            IExportEntry sequenceExport = pcc.getExport(SequenceIndex);
            //add to sequence
            buff = sequenceExport.Data;
            List<byte> ListBuff = new List<byte>(buff);
            
            p = PropertyReader.getPropOrNull(sequenceExport, "SequenceObjects");
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
                sequenceExport.Data = ListBuff.ToArray();
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
            PropGrid.propGridPropertyValueChanged(e, CurrentObjects[n], pcc);
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
            Matinee.InterpEditor p = new Matinee.InterpEditor();
            p.Show();
            p.LoadPCC(CurrentFile);
            if (pcc.getExport(Objects[listBox1.SelectedIndex].Index).ObjectName == "InterpData")
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
            if (CurrentObjects.Count == 0)
                return;
            SetupJSON(SequenceIndex);
        }

        private void graphEditor_Click(object sender, EventArgs e)
        {
            graphEditor.Focus();
        }

        private void loadAlternateTLKToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {

                if (pcc.Game == MEGame.ME3)
                {
                    TlkManager tm = new TlkManager();
                    tm.InitTlkManager();
                    tm.Show();
                }
                else if (pcc.Game == MEGame.ME2)
                {
                    ME2Explorer.TlkManager tm = new ME2Explorer.TlkManager();
                    tm.InitTlkManager();
                    tm.Show();
                }
                else if (pcc.Game == MEGame.ME1)
                {
                    ME1Explorer.TlkManager tm = new ME1Explorer.TlkManager();
                    tm.InitTlkManager(talkFiles);
                    tm.Show();
                } 
            }
        }

        private void SequenceEditor_DragDrop(object sender, DragEventArgs e)
        {
            List<string> DroppedFiles = ((string[])e.Data.GetData(DataFormats.FileDrop)).ToList();
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
            pcc.save();
            MessageBox.Show("Done");
        }

        private void savePCCAsMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            string extension = Path.GetExtension(pcc.FileName);
            d.Filter = $"*{extension}|*{extension}";
            if (d.ShowDialog() == DialogResult.OK)
            {
                pcc.save(d.FileName);
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
            //TODO: support cloning in SeqEd for ME2 and ME1
            if (pcc.Game != MEGame.ME3)
            {
                MessageBox.Show("Cloning in the Sequence editor is currently supported for ME3 only. Try using the Package Editor instead.", "Sorry!");
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
            IExportEntry exp = pcc.getExport(n).Clone();
            //needs to have the same index to work properly
            if (exp.ClassName == "SeqVar_External")
            {
                exp.indexValue = pcc.getExport(n).indexValue;
            }
            pcc.addExport(exp);
            int expIndex = pcc.ExportCount - 1;
            addObjectToSequence(expIndex, topLevel);
            if (exp.ClassName == "Sequence")
            {
                int originalSequenceIndex = SequenceIndex;
                PropertyReader.Property p = PropertyReader.getPropOrNull(exp, "SequenceObjects");
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
                    IExportEntry obj = pcc.getExport(objIndex);
                    p = PropertyReader.getPropOrNull(obj, "OutputLinks");
                    if (p != null)
                    {
                        data = obj.Data;
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
                        obj.Data = data;
                    }
                    p = PropertyReader.getPropOrNull(obj, "VariableLinks");
                    if (p != null)
                    {
                        data = obj.Data;
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
                        obj.Data = data;
                    }
                    p = PropertyReader.getPropOrNull(obj, "EventLinks");
                    if (p != null)
                    {
                        data = obj.Data;
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
                        obj.Data = data;
                    }
                }

                //re-point sequence links to new objects
                int oldObj = 0;
                int newObj = 0;
                p = PropertyReader.getPropOrNull(exp, "InputLinks");
                if (p != null)
                {
                    data = exp.Data;
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
                                    data.OverwriteRange(p.offsetval - 24 + pos - 60, BitConverter.GetBytes(pcc.getExport(newObj).indexValue));
                                }
                            }
                        }
                    }
                    exp.Data = data;
                }
                p = PropertyReader.getPropOrNull(exp, "OutputLinks");
                if (p != null)
                {
                    data = exp.Data;
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
                                    data.OverwriteRange(p.offsetval - 24 + pos - 32, BitConverter.GetBytes(pcc.getExport(newObj).indexValue));
                                }
                            }
                        }
                    }
                    exp.Data = data;
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
                PropertyReader.Property p = PropertyReader.getPropOrNull(exp, "oSequenceReference");
                if (p == null || p.Value.IntValue == 0)
                {
                    return;
                }
                byte[] buff = exp.Data;
                buff.OverwriteRange(p.offsetval, BitConverter.GetBytes(expIndex + 1 + 1));
                exp.Data = buff;

                //clone sequence
                cloneObject(p.Value.IntValue - 1, false);

                //remove cloned sequence from SeqRef's parent's sequenceobjects
                p = PropertyReader.getPropOrNull(pcc.getExport(SequenceIndex), "SequenceObjects");
                List<byte> memList = pcc.getExport(SequenceIndex).Data.ToList();
                int count = BitConverter.ToInt32(pcc.getExport(SequenceIndex).Data, p.offsetval) - 1;
                buff = BitConverter.GetBytes(4 + count * 4);
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
                pcc.getExport(SequenceIndex).Data = memList.ToArray();

                //set SequenceReference's linked name indices
                List<int> inputIndices = new List<int>();
                List<int> outputIndices = new List<int>();
                p = PropertyReader.getPropOrNull(pcc.getExport(expIndex + 1), "InputLinks");
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
                p = PropertyReader.getPropOrNull(pcc.getExport(expIndex + 1), "OutputLinks");
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
                p = PropertyReader.getPropOrNull(exp, "InputLinks");
                if (p != null)
                {
                    buff = exp.Data;
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
                                buff.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(inputIndices[i]));
                            }
                        }
                    }
                    exp.Data = buff;
                }
                p = PropertyReader.getPropOrNull(exp, "OutputLinks");
                if (p != null)
                {
                    buff = exp.Data;
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
                                buff.OverwriteRange(p.offsetval - 24 + pos - 4, BitConverter.GetBytes(outputIndices[i]));
                            }
                        }
                    }
                    exp.Data = buff;
                }

                IExportEntry newSequence = pcc.getExport(expIndex + 1);
                //set new Sequence's link and ParentSequence prop to SeqRef
                p = PropertyReader.getPropOrNull(newSequence, "ParentSequence");
                if (p == null)
                {
                    throw new Exception();
                }
                buff = newSequence.Data;
                buff.OverwriteRange(p.offsetval, BitConverter.GetBytes(expIndex + 1));
                newSequence.Data = buff;
                newSequence.idxLink = expIndex + 1;

                //set DefaultViewZoom to magic number to flag that this is a cloned Sequence Reference and global saves cannot be used with it
                //ugly, but it should work
                p = PropertyReader.getPropOrNull(newSequence, "DefaultViewZoom");
                if (p != null)
                {
                    buff = newSequence.Data;
                    buff.OverwriteRange(p.offsetval, BitConverter.GetBytes(CLONED_SEQREF_MAGIC));
                    newSequence.Data = buff;
                }
                else
                {
                    p = PropertyReader.getPropOrNull(newSequence, "None");
                    memList = newSequence.Data.ToList();
                    memList.InsertRange(p.offsetval, BitConverter.GetBytes(pcc.FindNameOrAdd("DefaultViewZoom")));
                    memList.InsertRange(p.offsetval + 4, new byte[4]);
                    memList.InsertRange(p.offsetval + 8, BitConverter.GetBytes(pcc.FindNameOrAdd("FloatProperty")));
                    memList.InsertRange(p.offsetval + 12, new byte[4]);
                    memList.InsertRange(p.offsetval + 16, BitConverter.GetBytes(4));
                    memList.InsertRange(p.offsetval + 20, new byte[4]);
                    memList.InsertRange(p.offsetval + 24, BitConverter.GetBytes(CLONED_SEQREF_MAGIC));
                    newSequence.Data = memList.ToArray();
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

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (updatedExports.Contains(SequenceIndex))
            {
                //loaded sequence is no longer a sequence
                if (!pcc.getExport(SequenceIndex).ClassName.Contains("Sequence"))
                {
                    graphEditor.nodeLayer.RemoveAllChildren();
                    graphEditor.edgeLayer.RemoveAllChildren();
                    CurrentObjects.Clear();
                    listBox1.Items.Clear();
                }
                RefreshView();
                LoadSequences();
                return;
            }
            
            if (updatedExports.Intersect(CurrentObjects).Count() > 0)
            {
                RefreshView();
            }
            foreach (var i in updatedExports)
            {
                if (pcc.getExport(i).ClassName.Contains("Sequence"))
                {
                    LoadSequences();
                    break;
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
            camera.MouseWheel += OnMouseWheel;
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
