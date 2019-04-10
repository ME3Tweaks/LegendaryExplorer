using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SequenceObjects;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using UMD.HCIL.GraphEditor;
using UMD.HCIL.Piccolo.Event;
using UMD.HCIL.Piccolo.Nodes;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Color = System.Drawing.Color;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ME3Explorer.Sequence_Editor
{
    /// <summary>
    /// Interaction logic for SequenceEditorWPF.xaml
    /// </summary>
    public partial class SequenceEditorWPF : WPFBase, IBusyUIHost
    {
        private const float CLONED_SEQREF_MAGIC = 2.237777E-35f;

        private readonly GraphEditor graphEditor;
        public ObservableCollectionExtended<IExportEntry> CurrentObjects { get; } = new ObservableCollectionExtended<IExportEntry>();
        public ObservableCollectionExtended<IExportEntry> SequenceExports { get; } = new ObservableCollectionExtended<IExportEntry>();
        public ObservableCollectionExtended<TreeViewEntry> TreeViewRootNodes { get; set; } = new ObservableCollectionExtended<TreeViewEntry>();
        public string CurrentFile;
        public string JSONpath;
        private ZoomController zoomController;

        public List<SObj> Objects;
        private List<SaveData> SavedPositions;
        IExportEntry Sequence;
        public bool RefOrRefChild;

        public static readonly string SequenceEditorDataFolder = Path.Combine(App.AppDataFolder, @"SequenceEditor\");
        public static readonly string OptionsPath = Path.Combine(SequenceEditorDataFolder, "SequenceEditorOptions.JSON");
        public static readonly string ME3ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME3SequenceViews\");
        public static readonly string ME2ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME2SequenceViews\");
        public static readonly string ME1ViewsPath = Path.Combine(SequenceEditorDataFolder, @"ME1SequenceViews\");

        public SequenceEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Sequence Editor WPF", new WeakReference(this));
            LoadCommands();
            InitializeComponent();

            LoadRecentList();

            graphEditor = (GraphEditor)GraphHost.Child;
            graphEditor.BackColor = Color.FromArgb(167, 167, 167);
            graphEditor.Camera.MouseDown += backMouseDown_Handler;

            this.graphEditor.Click += this.graphEditor_Click;
            this.graphEditor.DragDrop += SequenceEditor_DragDrop;
            this.graphEditor.DragEnter += SequenceEditor_DragEnter;
            zoomController = new ZoomController(graphEditor);

            if (File.Exists(OptionsPath))
            {
                var options = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(OptionsPath));
                if (options.ContainsKey("AutoSave"))
                    AutoSaveView_MenuItem.IsChecked = (bool)options["AutoSave"];
                if (options.ContainsKey("OutputNumbers"))
                    ShowOutputNumbers_MenuItem.IsChecked = (bool)options["OutputNumbers"];
                if (options.ContainsKey("GlobalSeqRefView"))
                    GlobalSeqRefViewSavesMenuItem.IsChecked = (bool)options["GlobalSeqRefView"];
                SObj.OutputNumbers = ShowOutputNumbers_MenuItem.IsChecked;
            }
        }

        public SequenceEditorWPF(IExportEntry export) : this()
        {
            LoadFile(export.FileRef.FileName);
            foreach (IExportEntry exp in SequenceExports)
            {
                var seqObjs = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                if (seqObjs != null)
                {
                    foreach (ObjectProperty seqObj in seqObjs)
                    {
                        if (export.UIndex == seqObj.Value)
                        {
                            //This is our sequence
                            LoadSequence(exp);
                            CurrentObjects_ListBox.SelectedItem = export;
                            return;
                        }
                    }
                }
            }
        }


        #region Properties and Bindings

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        
        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        private void LoadCommands()
        {
            OpenCommand = new RelayCommand(OpenPackage, o => true);
            SaveCommand = new RelayCommand(SavePackage, PackageIsLoaded);
            SaveAsCommand = new RelayCommand(SavePackageAs, PackageIsLoaded);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }
        private TreeViewEntry _selectedItem;
        public TreeViewEntry SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    LoadSequence((IExportEntry)value.Entry);
                }
            }
        }

        private void SavePackageAs(object obj)
        {
            string extension = Path.GetExtension(Pcc.FileName);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.save(d.FileName);
                MessageBox.Show("Done.");
            }
        }
        

        private void SavePackage(object obj)
        {
            Pcc.save();
        }

        private void OpenPackage(object obj)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
            }
        }

        private bool PackageIsLoaded(object obj)
        {
            return Pcc != null;
        }

        #endregion

        public void LoadFile(string fileName)
        {
            try
            {
                IsBusy = true;
                CurrentObjects.ClearEx();
                LoadMEPackage(fileName);
                CurrentFile = System.IO.Path.GetFileName(fileName);
                LoadSequences();
                if (!TreeViewRootNodes.Any())
                {
                    Pcc.Release();
                    Pcc = null;
                    MessageBox.Show("This file does not contain any Sequences!");
                    return;
                }

                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                AddRecent(fileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);


                Title = $"Sequence Editor WPF - {fileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
                Title = "Sequence Editor WPF";
                CurrentFile = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadSequences()
        {
            TreeViewRootNodes.ClearEx();
            var prefabs = new Dictionary<string, TreeViewEntry>();
            foreach(var export in Pcc.Exports)
            {
                switch (export.ClassName)
                {
                    case "Sequence" when !Pcc.getObjectClass(export.idxLink).Contains("Sequence"):
                        TreeViewRootNodes.Add(FindSequences(export, export.ObjectName != "Main_Sequence"));
                        SequenceExports.Add(export);
                        break;
                    case "Prefab":
                        try
                        {
                            prefabs.Add(export.ObjectName, new TreeViewEntry(export, export.GetFullPath));
                        }
                        catch
                        {
                            // ignored
                        }
                        break;
                }
            }
            if (prefabs.Count > 0)
            {
                foreach (var export in Pcc.Exports)
                {
                    if (export.ClassName == "PrefabSequence" && Pcc.getObjectClass(export.idxLink) == "Prefab")
                    {
                        string parentName = Pcc.getObjectName(export.idxLink);
                        if (prefabs.ContainsKey(parentName))
                        {
                            prefabs[parentName].Sublinks.Add(FindSequences(export));
                        }
                    }
                }
                foreach (var item in prefabs.Values)
                {
                    if (item.Sublinks.Any())
                    {
                        TreeViewRootNodes.Add(item);
                    }
                }
            }
        }

        private TreeViewEntry FindSequences(IExportEntry rootSeq, bool wantFullName = false)
        {
            var root = new TreeViewEntry(rootSeq, $"#{rootSeq.UIndex}: {(wantFullName ? rootSeq.GetFullPath : rootSeq.ObjectName)}");
            var pcc = rootSeq.FileRef;
            var seqObjs = rootSeq.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                foreach (ObjectProperty seqObj in seqObjs)
                {
                    IExportEntry exportEntry = pcc.getUExport(seqObj.Value);
                    if (exportEntry.ClassName == "Sequence" || exportEntry.ClassName.StartsWith("PrefabSequence"))
                    {
                        TreeViewEntry t = FindSequences(exportEntry);
                        SequenceExports.Add(exportEntry);
                        root.Sublinks.Add(t);
                    }
                    else if (exportEntry.ClassName == "SequenceReference")
                    {
                        var propSequenceReference = exportEntry.GetProperty<ObjectProperty>("oSequenceReference");
                        if (propSequenceReference != null)
                        {
                            TreeViewEntry t = FindSequences(pcc.getUExport(propSequenceReference.Value));
                            SequenceExports.Add(exportEntry);
                            root.Sublinks.Add(t);
                        }
                    }
                }
            }
            return root;
        }

        private void LoadSequence(IExportEntry seqExport, bool fromFile = true)
        {
            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;
            Sequence = seqExport;
            GetObjects(Sequence);
            SetupJSON(Sequence);
            if (SavedPositions == null)
                SavedPositions = new List<SaveData>();
            if (fromFile && File.Exists(JSONpath))
                SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
            try
            {
                GenerateGraph();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading sequences from file:\n{e.Message}");
            }
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
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
            if (GlobalSeqRefViewSavesMenuItem.IsChecked && packageFullName.Contains("SequenceReference") && !isClonedSeqRef)
            {
                string packageName = packageFullName.Substring(packageFullName.LastIndexOf("SequenceReference"));
                if (Pcc.Game == MEGame.ME3)
                {
                    JSONpath = $"{ME3ViewsPath}{packageName}.{objectName}.JSON";
                }
                else
                {
                    packageName = packageName.Replace("SequenceReference", "");
                    int idx = export.UIndex;
                    string ObjName = "";
                    while (idx > 0)
                    {
                        if (Pcc.getUExport(Pcc.getUExport(idx).idxLink).ClassName == "SequenceReference")
                        {
                            var objNameProp = Pcc.getUExport(idx).GetProperty<StrProperty>("ObjName");
                            if (objNameProp != null)
                            {
                                ObjName = objNameProp.Value;
                                break;
                            }
                        }
                        idx = Pcc.getUExport(idx).idxLink;

                    }
                    if (objectName == "Sequence")
                    {
                        objectName = ObjName;
                        packageName = "." + packageName;
                    }
                    else
                        packageName = packageName.Replace("Sequence", ObjName) + ".";
                    if (Pcc.Game == MEGame.ME2)
                    {
                        JSONpath = $"{ME2ViewsPath}SequenceReference{packageName}{objectName}.JSON";
                    }
                    else
                    {
                        JSONpath = $"{ME1ViewsPath}SequenceReference{packageName}{objectName}.JSON";
                    }
                }
                RefOrRefChild = true;
            }
            else
            {
                string viewsPath = ME3ViewsPath;
                switch (Pcc.Game)
                {
                    case MEGame.ME2:
                        viewsPath = ME2ViewsPath;
                        break;
                    case MEGame.ME1:
                        viewsPath = ME1ViewsPath;
                        break;
                }
                JSONpath = $"{viewsPath}{CurrentFile.Substring(CurrentFile.LastIndexOf(@"\") + 1)}.#{export.Index}{objectName}.JSON";
                RefOrRefChild = false;
            }
        }

        public void GetObjects(IExportEntry export)
        {
            CurrentObjects.ClearEx();
            var seqObjs = export.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            if (seqObjs != null)
            {
                List<int> objIndices = seqObjs.Select(x => x.Value).ToList();
                objIndices.Sort();
                foreach (int seqObj in objIndices)
                {
                    CurrentObjects.Add(Pcc.getUExport(seqObj));
                }
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
                foreach (var exp in CurrentObjects)
                {
                    LoadObject(exp);
                }

                CreateConnections();
            }
            foreach (SObj o in Objects)
            {
                //o.MouseDown += node_MouseDown;
            }
            if (SavedPositions.Count == 0 && CurrentFile.Contains("_LOC_INT") && Pcc.Game != MEGame.ME1)
            {
                LoadDialogueObjects();
            }
        }

        public float StartPosEvents;
        public float StartPosActions;
        public float StartPosVars;
        public void LoadObject(IExportEntry export)
        {
            string s = export.ObjectName;
            int x = 0, y = 0;
            SaveData savedInfo = new SaveData(-1);
            if (SavedPositions.Count > 0)
            {
                savedInfo = SavedPositions.FirstOrDefault(p => p.index == (RefOrRefChild ? CurrentObjects.IndexOf(export) : export.Index));
            }
            foreach (var prop in export.GetProperties())
            {
                switch (prop)
                {
                    case IntProperty intProp when intProp.Name == "ObjPosX":
                        x = intProp.Value;
                        break;
                    case IntProperty intProp when intProp.Name == "ObjPosY":
                        y = intProp.Value;
                        break;
                }
            }

            int index = export.Index;
            if (s.StartsWith("BioSeqEvt_") || s.StartsWith("SeqEvt_") || s.StartsWith("SFXSeqEvt_") || s.StartsWith("SeqEvent_"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(export) : export.Index))
                {
                    Objects.Add(new SEvent(index, savedInfo.X, savedInfo.Y, Pcc, graphEditor));
                }
                else
                {
                    if (Pcc.Game == MEGame.ME1)
                    {
                        Objects.Add(new SEvent(index, x, y, Pcc, graphEditor));
                    }
                    else
                    {
                        Objects.Add(new SEvent(index, StartPosEvents, 0, Pcc, graphEditor));
                        StartPosEvents += Objects[Objects.Count - 1].Width + 20;
                    }
                }
            }
            else if (s.StartsWith("SeqVar_") || s.StartsWith("BioSeqVar_") || s.StartsWith("SFXSeqVar_") || s.StartsWith("InterpData"))
            {
                if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(export) : export.Index))
                    Objects.Add(new SVar(index, savedInfo.X, savedInfo.Y, Pcc, graphEditor));
                else
                {
                    if (Pcc.Game == MEGame.ME1)
                    {
                        Objects.Add(new SVar(index, x, y, Pcc, graphEditor));
                    }
                    else
                    {
                        Objects.Add(new SVar(index, StartPosVars, 500, Pcc, graphEditor));
                        StartPosVars += Objects[Objects.Count - 1].Width + 20;
                    }
                }
            }
            else if (export.ClassName == "SequenceFrame" && Pcc.Game == MEGame.ME1)
            {
                Objects.Add(new SFrame(index, x, y, Pcc, graphEditor));
            }
            else //if (s.StartsWith("BioSeqAct_") || s.StartsWith("SeqAct_") || s.StartsWith("SFXSeqAct_") || s.StartsWith("SeqCond_") || pcc.getExport(index).ClassName == "Sequence" || pcc.getExport(index).ClassName == "SequenceReference")
            {
                if (Pcc.Game == MEGame.ME1)
                {
                    Objects.Add(new SAction(index, x, y, Pcc, graphEditor));
                }
                else
                {
                    Objects.Add(new SAction(index, -1, -1, Pcc, graphEditor));
                }
            }
        }

        public bool LoadDialogueObjects()
        {
            float StartPosDialog = 0;
            try
            {
                for (int i = 0; i < CurrentObjects.Count; i++)
                {
                    if (CurrentObjects[i].ObjectName.StartsWith("BioSeqEvt_ConvNode"))
                    {
                        Objects[i].SetOffset(StartPosDialog, 600);//Startconv event
                        int InterpIndex = CurrentObjects.IndexOf(Pcc.getExport(((SEvent)Objects[i]).Outlinks[0].Links[0]));
                        Objects[InterpIndex].SetOffset(StartPosDialog + 150, 600);//Interp
                        Objects[CurrentObjects.IndexOf(Pcc.getExport(((SAction)Objects[InterpIndex]).Varlinks[0].Links[0]))].SetOffset(StartPosDialog + 165, 770);//Interpdata
                        StartPosDialog += Objects[InterpIndex].Width + 200;
                        Objects[CurrentObjects.IndexOf(Pcc.getExport(((SAction)Objects[InterpIndex]).Outlinks[0].Links[0]))].SetOffset(StartPosDialog, 600);//Endconv node
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
                foreach (SObj obj in Objects)
                {
                    graphEditor.addNode(obj);
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
                                savedInfo = SavedPositions.FirstOrDefault(p => CurrentObjects.IndexOf(o.Export) == p.index);
                            else
                                savedInfo = SavedPositions.FirstOrDefault(p => o.Index == p.index);
                        }
                        if (savedInfo.index == (RefOrRefChild ? CurrentObjects.IndexOf(o.Export) : o.Index))
                            o.Layout(savedInfo.X, savedInfo.Y);
                        else
                        {
                            if (Pcc.Game == MEGame.ME1)
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

        public void RefreshView()
        {
            saveView(false);
            LoadSequence(Sequence, false);
        }

        #region Recents
        private readonly List<Button> RecentButtons = new List<Button>();
        public List<string> RFiles;
        private readonly string RECENTFILES_FILE = "RECENTFILES";

        private void LoadRecentList()
        {
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
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

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                //we are posting an update to other instances of SeqEd

                //This code can be removed when non-WPF sequence editor is removed.
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (System.Windows.Forms.Form form in forms)
                {
                    if (form is SequenceEditor editor) //it will never be "this"
                    {
                        editor.RefreshRecent(false, RFiles);
                    }
                }
                foreach (var form in Application.Current.Windows)
                {
                    if (form is SequenceEditorWPF wpf && this != wpf)
                    {
                        wpf.RefreshRecent(false, RFiles);
                    }
                }
            }
            else if (recents != null)
            {
                //we are receiving an update
                RFiles = new List<string>(recents);
            }
            Recents_MenuItem.Items.Clear();
            if (RFiles.Count <= 0)
            {
                Recents_MenuItem.IsEnabled = false;
                return;
            }
            Recents_MenuItem.IsEnabled = true;

            int i = 0;
            foreach (string filepath in RFiles)
            {
                MenuItem fr = new MenuItem()
                {
                    Header = filepath.Replace("_", "__"),
                    Tag = filepath
                };
                RecentButtons[i].Visibility = Visibility.Visible;
                RecentButtons[i].Content = Path.GetFileName(filepath.Replace("_", "__"));
                RecentButtons[i].Click -= RecentFile_click;
                RecentButtons[i].Click += RecentFile_click;
                RecentButtons[i].Tag = filepath;
                RecentButtons[i].ToolTip = filepath;
                fr.Click += RecentFile_click;
                Recents_MenuItem.Items.Add(fr);
                i++;
            }
            while (i < 10)
            {
                RecentButtons[i].Visibility = Visibility.Collapsed;
                i++;
            }
        }

        private void RecentFile_click(object sender, EventArgs e)
        {
            string s = ((FrameworkElement)sender).Tag.ToString();
            if (File.Exists(s))
            {
                LoadFile(s);
            }
            else
            {
                MessageBox.Show("File does not exist: " + s);
            }
        }

        public void AddRecent(string s, bool loadingList)
        {
            RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
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
            Recents_MenuItem.IsEnabled = true;
        }

        #endregion


        private void backMouseDown_Handler(object sender, PInputEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                throw new NotImplementedException();
            }
        }

        private void graphEditor_Click(object sender, EventArgs e)
        {
            graphEditor.Focus();
        }

        private void SequenceEditor_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop))
                e.Effect = System.Windows.Forms.DragDropEffects.All;
            else
                e.Effect = System.Windows.Forms.DragDropEffects.None;
        }

        private void SequenceEditor_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetData(System.Windows.Forms.DataFormats.FileDrop) is string[] DroppedFiles)
            {
                if (DroppedFiles.Any())
                {
                    LoadFile(DroppedFiles[0]);
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            throw new NotImplementedException();
        }

        List<SaveData> extraSaveData;
        private void saveView(bool toFile = true)
        {
            if (CurrentObjects.Count == 0)
                return;
            SavedPositions = new List<SaveData>();
            foreach (SObj p in graphEditor.nodeLayer)
            {
                if (p.Pickable)
                {
                    SavedPositions.Add(new SaveData
                    {
                        absoluteIndex = RefOrRefChild,
                        index = RefOrRefChild ? CurrentObjects.IndexOf(p.Export) : p.Index,
                        X = p.X + p.Offset.X,
                        Y = p.Y + p.Offset.Y
                    });
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

        private void SequenceEditorWPF_Closing(object sender, CancelEventArgs e)
        {
            if (AutoSaveView_MenuItem.IsChecked)
                saveView();

            var options = new Dictionary<string, object>
            {
                { "OutputNumbers", SObj.OutputNumbers },
                { "AutoSave", AutoSaveView_MenuItem.IsChecked },
                { "GlobalSeqRefView", GlobalSeqRefViewSavesMenuItem.IsChecked }
            };
            string outputFile = JsonConvert.SerializeObject(options);
            if (!Directory.Exists(SequenceEditorDataFolder))
                Directory.CreateDirectory(SequenceEditorDataFolder);
            File.WriteAllText(OptionsPath, outputFile);


            //Code here remove these objects from leaking the window memory
            graphEditor.Camera.MouseDown -= backMouseDown_Handler;
            graphEditor.Click -= graphEditor_Click;
            graphEditor.DragDrop -= SequenceEditor_DragDrop;
            graphEditor.DragEnter -= SequenceEditor_DragEnter;
            zoomController.Dispose();
            //Objects?.ForEach(x => { x.MouseDown -= node_MouseDown; x.Dispose(); });
            Objects?.Clear();
            graphEditor.Dispose();
        }

        private void OpenInPackageEditor_Clicked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CloneObject_Clicked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CurrentObjectsList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
