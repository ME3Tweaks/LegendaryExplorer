using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
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
using System.Diagnostics;
using ME3Explorer.SharedUI;

namespace ME3Explorer
{
    public partial class SequenceEditor : WinFormsBase
    {
        public List<string> RFiles;
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        public SequenceEditor()
        {
            InitializeComponent();
            LoadRecentList();
            RefreshRecent(false);
            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;
            zoomController = new ZoomController(graphEditor);
            
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
            string OldME3SequenceViews = Path.Combine(ME3Directory.cookedPath, @"SequenceViews\");
            string OldME2SequenceViews = Path.Combine(ME2Directory.cookedPath, @"SequenceViews\");
            string OldME1SequenceViews = Path.Combine(ME1Directory.cookedPath, @"SequenceViews\");

            #region Migrate data from legacy locations
            if (Directory.Exists(OldME3SequenceViews) ||
                    Directory.Exists(OldME2SequenceViews) ||
                    Directory.Exists(OldME1SequenceViews))
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

                }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
                catch
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
                {
                }
                var comp = new Microsoft.VisualBasic.Devices.Computer();
                try
                {
                    if (Directory.Exists(OldME3SequenceViews))
                    {
                        Directory.CreateDirectory(ME3ViewsPath);
                        comp.FileSystem.CopyDirectory(OldME3SequenceViews, ME3ViewsPath);
                        comp.FileSystem.DeleteDirectory(OldME3SequenceViews, Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                }
                catch
                {
                    MessageBox.Show($"Error migrating old ME3 data.\nPlease manually move all files from {OldME3SequenceViews} to {ME3ViewsPath}\nThen delete the original SequenceViews directory.");
                }
                try
                {
                    if (Directory.Exists(OldME2SequenceViews))
                    {
                        Directory.CreateDirectory(ME2ViewsPath);
                        comp.FileSystem.CopyDirectory(OldME2SequenceViews, ME2ViewsPath);
                        comp.FileSystem.DeleteDirectory(OldME2SequenceViews, Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                }
                catch
                {
                    MessageBox.Show($"Error migrating old ME2 data.\nPlease manually move all files from {OldME2SequenceViews} to {ME2ViewsPath}\nThen delete the original SequenceViews directory.");
                }
                try
                {
                    if (Directory.Exists(OldME1SequenceViews))
                    {
                        Directory.CreateDirectory(ME1ViewsPath);
                        comp.FileSystem.CopyDirectory(OldME1SequenceViews, ME1ViewsPath);
                        comp.FileSystem.DeleteDirectory(OldME1SequenceViews, Microsoft.VisualBasic.FileIO.DeleteDirectoryOption.DeleteAllContents);
                    }
                }
                catch
                {
                    MessageBox.Show($"Error migrating old ME1 data.\nPlease manually move all files from  {OldME1SequenceViews} to {ME1ViewsPath}\nThen delete the original SequenceViews directory.");
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

        private void LoadRecentList()
        {
            recentToolStripMenuItem.Enabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = SequenceEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
            {
                string[] recents = File.ReadAllLines(path);
                foreach (string recent in recents)
                {
                    if (File.Exists(recent))
                    {
                        AddRecent(recent, true);
                    }
                }
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s,StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (loadingList)
            {
                RFiles.Add(s); //in order
            }
            else
            {
                RFiles.Insert(0, s); //put at front
            }
            if (RFiles.Count > 10)
            {
                RFiles.RemoveRange(10, RFiles.Count - 10);
            }
            recentToolStripMenuItem.Enabled = true;
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(SequenceEditorDataFolder))
            {
                Directory.CreateDirectory(SequenceEditorDataFolder);
            }
            string path = SequenceEditorDataFolder + RECENTFILES_FILE;
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        private void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of packed
                var forms = Application.OpenForms;
                foreach (Form form in forms)
                {
                    if (form is SequenceEditor && this != form)
                    {
                        ((SequenceEditor)form).RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            recentToolStripMenuItem.DropDownItems.Clear();
            if (RFiles.Count <= 0)
            {
                recentToolStripMenuItem.Enabled = false;
                return;
            }
            recentToolStripMenuItem.Enabled = true;

            foreach (string filepath in RFiles)
            {
                ToolStripMenuItem fr = new ToolStripMenuItem(filepath, null, RecentFile_click);
                recentToolStripMenuItem.DropDownItems.Add(fr);
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = sender.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
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

        private const float CLONED_SEQREF_MAGIC = 2.237777E-35f;

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
        IExportEntry Sequence;
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

        public void LoadFile(string fileName)
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

                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
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
            var seqObjs = pcc.getExport(index).GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                IExportEntry exportEntry;
                foreach (ObjectProperty seqObj in seqObjs)
                {
                    exportEntry = pcc.getExport(seqObj.Value - 1);
                    if (exportEntry.ClassName == "Sequence" || exportEntry.ClassName.StartsWith("PrefabSequence"))
                    {
                        TreeNode t = FindSequences(pcc, seqObj.Value - 1, false);
                        ret.Nodes.Add(t);
                    }
                    else if (exportEntry.ClassName == "SequenceReference")
                    {
                        var propSequenceReference = exportEntry.GetProperty<ObjectProperty>("oSequenceReference");
                        if (propSequenceReference != null)
                        {
                            TreeNode t = FindSequences(pcc, propSequenceReference.Value - 1, false);
                            ret.Nodes.Add(t);
                        }
                    }
                }
            }
            return ret;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (autoSaveViewToolStripMenuItem.Checked)
                saveView();
            SavedPositions = null;
            if (e.Node.Name != "")
            {
                LoadSequence(pcc.getExport(Convert.ToInt32(e.Node.Name)));
            }
        }

        public void RefreshView()
        {
            saveView(false);
            LoadSequence(Sequence, false);
        }

        private void LoadSequence(IExportEntry seqExport, bool fromFile = true)
        {
            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;
            Sequence = seqExport;
            toolStripStatusLabel2.Text = "\t#" + Sequence.Index + Sequence.ObjectName;
            GetProperties(Sequence);
            GetObjects(Sequence);
            SetupJSON(Sequence);
            if (SavedPositions == null)
                SavedPositions = new List<SaveData>();
            if (fromFile && File.Exists(JSONpath))
                SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
            GenerateGraph();
            selectedIndex = -1;
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
        }

        public void GetObjects(IExportEntry export)
        {
            CurrentObjects = new List<int>();
            listBox1.Items.Clear();
            var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                var objIndices = seqObjs.Select(x => x.Value - 1).ToList();
                objIndices.Sort();
                foreach (int seqObj in objIndices)
                {
                    CurrentObjects.Add(seqObj);
                    IExportEntry exportEntry = pcc.getExport(seqObj);
                    listBox1.Items.Add("#" + seqObj + " :" + exportEntry.ObjectName + " class: " + exportEntry.ClassName);
                }
            }
        }

        private void SetupJSON(IExportEntry export)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(export.ObjectName, @"[<>:""/\\|?*]", "");
            bool isClonedSeqRef = false;
            var defaultViewZoomProp = export.GetProperty<FloatProperty>("DefaultViewZoom");
            if (defaultViewZoomProp != null && Math.Abs(defaultViewZoomProp.Value - CLONED_SEQREF_MAGIC) < 1.0E-30f)
            {
                isClonedSeqRef = true;
            }

            string packageFullName = export.PackageFullName;
            if (useGlobalSequenceRefSavesToolStripMenuItem.Checked && packageFullName.Contains("SequenceReference") && !isClonedSeqRef)
            {
                if (pcc.Game == MEGame.ME3)
                {
                    JSONpath = ME3ViewsPath + packageFullName.Substring(packageFullName.LastIndexOf("SequenceReference")) + "." + objectName + ".JSON";
                }
                else
                {
                    string packageName = export.PackageFullName.Substring(export.PackageFullName.LastIndexOf("SequenceReference"));
                    packageName = packageName.Replace("SequenceReference", "");
                    int idx = export.Index;
                    string ObjName = "";
                    while (idx > 0)
                    {
                        if (pcc.getExport(pcc.getExport(idx).idxLink - 1).ClassName == "SequenceReference")
                        {
                            var objNameProp = pcc.getExport(idx).GetProperty<StrProperty>("ObjName");
                            if (objNameProp != null)
                            {
                                ObjName = objNameProp.Value;
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
                JSONpath = viewsPath + CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1) + ".#" + export.Index + objectName + ".JSON";
                RefOrRefChild = false;
            }
        }

        public void GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
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
            PropertyCollection props = pcc.getExport(index).GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "ObjPosX")
                {
                    x = (prop as IntProperty).Value;
                }
                else if (prop.Name == "ObjPosY")
                    y = (prop as IntProperty).Value;
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

        public void GetProperties(IExportEntry export)
        {
            List<PropertyReader.Property> p;
            switch (export.ClassName)
            {
                default:
                    p = PropertyReader.getPropList(export);
                    break;
            }
            pg = new PropGrid();
            pg1.SelectedObject = pg;
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
            GetProperties(pcc.getExport(CurrentObjects[n]));
            selectedIndex = n;
            selectedByNode = false;
            graphEditor.Refresh();
        }

        private void SequenceEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (autoSaveViewToolStripMenuItem.Checked)
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
                if (sender is SAction || sender is SEvent)
                {
                    ToolStripMenuItem temp;
                    ToolStripDropDown submenu = new ToolStripDropDown();
                    ToolStripDropDown varLinkMenu = new ToolStripDropDown();
                    ToolStripDropDown outLinkMenu = new ToolStripDropDown();
                    SBox sBox = ((SBox)sender);
                    for (int i = 0; i < sBox.Varlinks.Count; i++)
                    {
                        for (int j = 0; j < sBox.Varlinks[i].Links.Count; j++)
                        {
                            if (sBox.Varlinks[i].Links[j] != -1)
                            {
                                temp = new ToolStripMenuItem("Break link from " + sBox.Varlinks[i].Desc + " to " + sBox.Varlinks[i].Links[j]);
                                int linkConnection = i;
                                int linkIndex = j;
                                temp.Click += (object o, EventArgs args) =>
                                {
                                    sBox.RemoveVarlink(linkConnection, linkIndex);
                                };
                                varLinkMenu.Items.Add(temp);
                            }
                        }
                    }
                    for (int i = 0; i < sBox.Outlinks.Count; i++)
                    {
                        for (int j = 0; j < sBox.Outlinks[i].Links.Count; j++)
                        {
                            if (sBox.Outlinks[i].Links[j] != -1)
                            {
                                temp = new ToolStripMenuItem("Break link from " + sBox.Outlinks[i].Desc + " to " + sBox.Outlinks[i].Links[j] + " :" + sBox.Outlinks[i].InputIndices[j]);
                                int linkConnection = i;
                                int linkIndex = j;
                                temp.Click += (object o, EventArgs args) =>
                                {
                                    sBox.RemoveOutlink(linkConnection, linkIndex);
                                };
                                outLinkMenu.Items.Add(temp);
                            }
                        }
                    }
                    if (varLinkMenu.Items.Count > 0)
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
            removeAllLinks(obj.Index);
        }

        private void removeAllLinks(int index)
        {
            IExportEntry export = pcc.getExport(index);
            var props = export.GetProperties();
            var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
            if (outLinksProp != null)
            {
                foreach (var prop in outLinksProp)
                {
                    prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                }
            }
            var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
            if (varLinksProp != null)
            {
                foreach (var prop in varLinksProp)
                {
                    prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                }
            }
            export.WriteProperties(props);
        }

        private void saveViewToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (CurrentObjects.Count == 0)
                return;
            saveView();
            MessageBox.Show("Done.");
        }

        List<SaveData> extraSaveData;
        private void saveView(bool toFile = true)
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
            if (extraSaveData != null)
            {
                SavedPositions.AddRange(extraSaveData);
                extraSaveData = null;
            }

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

            using (ExportSelectorWinForms form = new ExportSelectorWinForms(pcc, ExportSelectorWinForms.SUPPORTS_EXPORTS_ONLY))
            {
                DialogResult dr = form.ShowDialog(this);
                if (dr != DialogResult.Yes)
                {
                    return; //user cancel
                }

                int i = form.SelectedItemIndex;
                if (!CurrentObjects.Contains(i))
                {
                    if (pcc.getExport(i).inheritsFrom("SequenceObject"))
                    {
                        addObject(i);
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
        }

        private void addObject(int index, bool removeLinks = true)
        {
            SaveData s = new SaveData();
            s.index = index;
            s.X = graphEditor.Camera.Bounds.X + graphEditor.Camera.Bounds.Width / 2;
            s.Y = graphEditor.Camera.Bounds.Y + graphEditor.Camera.Bounds.Height / 2;
            List<SaveData> list = new List<SaveData>();
            list.Add(s);
            extraSaveData = list;
            addObjectToSequence(index, removeLinks, Sequence);
        }

        static void addObjectToSequence(int index, bool removeLinks, IExportEntry sequenceExport)
        {
            var seqObjs = sequenceExport.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                seqObjs.Add(new ObjectProperty(index + 1));
                sequenceExport.WriteProperty(seqObjs);
                PropertyCollection props = sequenceExport.GetProperties();

                IExportEntry newObject = sequenceExport.FileRef.getExport(index);
                PropertyCollection newObjectProps = newObject.GetProperties();
                newObjectProps.AddOrReplaceProp(new ObjectProperty(sequenceExport.UIndex, "ParentSequence"));
                if (removeLinks)
                {
                    var outLinksProp = newObjectProps.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                    if (outLinksProp != null)
                    {
                        foreach (var prop in outLinksProp)
                        {
                            prop.GetProp<ArrayProperty<StructProperty>>("Links").Clear();
                        }
                    }
                    var varLinksProp = newObjectProps.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var prop in varLinksProp)
                        {
                            prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").Clear();
                        }
                    }
                }
                newObject.WriteProperties(newObjectProps);
                newObject.idxLink = sequenceExport.UIndex;
            }
        }

        private void pg1_PropertyValueChanged(object o, PropertyValueChangedEventArgs e)
        {

            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            PropGrid.propGridPropertyValueChanged(e, CurrentObjects[n], pcc);
        }

        private void showOutputNumbersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SObj.OutputNumbers = showOutputNumbersToolStripMenuItem.Checked;
            if (CurrentObjects != null)
            {
                RefreshView();
            }

        }

        private void openInInterpEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc.Game != MEGame.ME3)
            {
                MessageBox.Show("InterpViewer does not support ME1 or ME2 yet.", "Sorry!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
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
            SetupJSON(Sequence);
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

        void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = CurrentObjects[listBox1.SelectedIndex];
            if (n == -1)
                return;
            cloneObject(n, Sequence);
        }

        static void cloneObject(int n, IExportEntry sequence, bool topLevel = true)
        {
            IMEPackage pcc = sequence.FileRef;
            IExportEntry exp = pcc.getExport(n).Clone();
            //needs to have the same index to work properly
            if (exp.ClassName == "SeqVar_External")
            {
                exp.indexValue = pcc.getExport(n).indexValue;
            }
            pcc.addExport(exp);
            addObjectToSequence(exp.Index, topLevel, sequence);
            cloneSequence(exp, sequence);
        }

        static void cloneSequence(IExportEntry exp, IExportEntry parentSequence)
        {
            IMEPackage pcc = exp.FileRef;
            if (exp.ClassName == "Sequence")
            {
                var seqObjs = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs == null || seqObjs.Count == 0)
                {
                    return;
                }

                //store original list of sequence objects;
                List<int> oldObjects = seqObjs.Select(x => x.Value).ToList();

                //clear original sequence objects
                seqObjs.Clear();
                exp.WriteProperty(seqObjs);

                //clone all children
                for (int i = 0; i < oldObjects.Count; i++)
                {
                    cloneObject(oldObjects[i] - 1, exp, false);
                }

                //re-point children's links to new objects
                seqObjs = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                foreach (var seqObj in seqObjs)
                {
                    IExportEntry obj = pcc.getExport(seqObj.Value - 1);
                    var props = obj.GetProperties();
                    var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                    if (outLinksProp != null)
                    {
                        foreach (var outLinkStruct in outLinksProp)
                        {
                            var links = outLinkStruct.GetProp<ArrayProperty<StructProperty>>("Links");
                            foreach (var link in links)
                            {
                                var linkedOp = link.GetProp<ObjectProperty>("LinkedOp");
                                linkedOp.Value = seqObjs[oldObjects.IndexOf(linkedOp.Value)].Value;
                            }
                        }
                    }
                    var varLinksProp = props.GetProp<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var varLinkStruct in varLinksProp)
                        {
                            var links = varLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                            foreach (var link in links)
                            {
                                link.Value = seqObjs[oldObjects.IndexOf(link.Value)].Value;
                            }
                        }
                    }
                    var eventLinksProp = props.GetProp<ArrayProperty<StructProperty>>("EventLinks");
                    if (eventLinksProp != null)
                    {
                        foreach (var eventLinkStruct in eventLinksProp)
                        {
                            var links = eventLinkStruct.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents");
                            foreach (var link in links)
                            {
                                link.Value = seqObjs[oldObjects.IndexOf(link.Value)].Value;
                            }
                        }
                    }
                    obj.WriteProperties(props);
                }

                //re-point sequence links to new objects
                int oldObj = 0;
                int newObj = 0;
                var propCollection = exp.GetProperties();
                var inputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inputLinksProp != null)
                {
                    foreach (var inLinkStruct in inputLinksProp)
                    {
                        var linkedOp = inLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjects.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = inLinkStruct.GetProp<NameProperty>("LinkAction");
                            var nameRef = linkAction.Value;
                            nameRef.Number = pcc.getExport(newObj - 1).indexValue;
                            linkAction.Value = nameRef;
                        }
                    }
                }
                var outputLinksProp = propCollection.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outputLinksProp != null)
                {
                    foreach (var outLinkStruct in outputLinksProp)
                    {
                        var linkedOp = outLinkStruct.GetProp<ObjectProperty>("LinkedOp");
                        oldObj = linkedOp.Value;
                        if (oldObj != 0)
                        {
                            newObj = seqObjs[oldObjects.IndexOf(oldObj)].Value;
                            linkedOp.Value = newObj;

                            NameProperty linkAction = outLinkStruct.GetProp<NameProperty>("LinkAction");
                            var nameRef = linkAction.Value;
                            nameRef.Number = pcc.getExport(newObj - 1).indexValue;
                            linkAction.Value = nameRef;
                        }
                    }
                }
                exp.WriteProperties(propCollection);
            }
            else if (exp.ClassName == "SequenceReference")
            {
                //set OSequenceReference to new sequence
                var oSeqRefProp = exp.GetProperty<ObjectProperty>("oSequenceReference");
                if (oSeqRefProp == null || oSeqRefProp.Value == 0)
                {
                    return;
                }
                int oldSeqIndex = oSeqRefProp.Value;
                oSeqRefProp.Value = exp.UIndex + 1;
                exp.WriteProperty(oSeqRefProp);

                //clone sequence
                cloneObject(oldSeqIndex - 1, parentSequence, false);

                //remove cloned sequence from SeqRef's parent's sequenceobjects
                var seqObjs = parentSequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                seqObjs.RemoveAt(seqObjs.Count - 1);
                parentSequence.WriteProperty(seqObjs);

                //set SequenceReference's linked name indices
                List<int> inputIndices = new List<int>();
                List<int> outputIndices = new List<int>();

                IExportEntry newSequence = pcc.getExport(exp.Index + 1);
                var props = newSequence.GetProperties();
                var inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    foreach (var inLink in inLinksProp)
                    {
                        inputIndices.Add(inLink.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }
                var outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    foreach (var outLinks in outLinksProp)
                    {
                        outputIndices.Add(outLinks.GetProp<NameProperty>("LinkAction").Value.Number);
                    }
                }

                props = exp.GetProperties();
                inLinksProp = props.GetProp<ArrayProperty<StructProperty>>("InputLinks");
                if (inLinksProp != null)
                {
                    for (int i = 0; i < inLinksProp.Count; i++)
                    {
                        NameProperty linkAction = inLinksProp[i].GetProp<NameProperty>("LinkAction");
                        var nameRef = linkAction.Value;
                        nameRef.Number = inputIndices[i];
                        linkAction.Value = nameRef;
                    }
                }
                outLinksProp = props.GetProp<ArrayProperty<StructProperty>>("OutputLinks");
                if (outLinksProp != null)
                {
                    for (int i = 0; i < outLinksProp.Count; i++)
                    {
                        NameProperty linkAction = outLinksProp[i].GetProp<NameProperty>("LinkAction");
                        var nameRef = linkAction.Value;
                        nameRef.Number = outputIndices[i];
                        linkAction.Value = nameRef;
                    }
                }
                exp.WriteProperties(props);

                //set new Sequence's link and ParentSequence prop to SeqRef
                newSequence.WriteProperty(new ObjectProperty(exp.UIndex, "ParentSequence"));
                newSequence.idxLink = exp.UIndex;

                //set DefaultViewZoom to magic number to flag that this is a cloned Sequence Reference and global saves cannot be used with it
                //ugly, but it should work
                newSequence.WriteProperty(new FloatProperty(CLONED_SEQREF_MAGIC, "DefaultViewZoom"));
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (updatedExports.Contains(Sequence.Index))
            {
                //loaded sequence is no longer a sequence
                if (!Sequence.ClassName.Contains("Sequence"))
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
