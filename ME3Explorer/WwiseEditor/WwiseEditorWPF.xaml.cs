using System;
using System.Collections.Generic;
using System.Drawing;
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
using System.Windows.Threading;
using ByteSizeLib;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;
using Newtonsoft.Json;
using UMD.HCIL.GraphEditor;
using static UMD.HCIL.Piccolo.Extensions;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Path = System.IO.Path;

namespace ME3Explorer.WwiseEditor
{
    /// <summary>
    /// Interaction logic for WwiseEditorWPF.xaml
    /// </summary>
    public partial class WwiseEditorWPF : WPFBase
    {
        private struct SaveData
        {
            public uint ID;
            public float X;
            public float Y;
        }
        private readonly WwiseGraphEditor graphEditor;
        public WwiseEditorWPF()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Wwise Editor", new WeakReference(this));
            Analytics.TrackEvent("Used tool", new Dictionary<string, string>
            {
                { "Toolname", "Wwise Editor" }
            });
            DataContext = this;
            StatusText = "Select package file to load";
            LoadCommands();
            InitializeComponent();

            LoadRecentList();

            graphEditor = (WwiseGraphEditor)GraphHost.Child;
            graphEditor.BackColor = GraphEditorBackColor;
        }
        public WwiseEditorWPF(ExportEntry exportToLoad) : this()
        {
            FileQueuedForLoad = exportToLoad.FileRef.FilePath;
            ExportQueuedForFocusing = exportToLoad;
        }

        public WwiseEditorWPF(string filePath, int uIndex = 0) : this()
        {
            FileQueuedForLoad = filePath;
            ExportQueuedForFocusing = null;
            UIndexQueuedForFocusing = uIndex;
        }

        public ObservableCollectionExtended<ExportEntry> WwiseBankExports { get; } = new ObservableCollectionExtended<ExportEntry>();
        public ObservableCollectionExtended<WwiseHircObjNode> CurrentObjects { get; } = new ObservableCollectionExtended<WwiseHircObjNode>();

        private List<SaveData> SavedPositions;

        private string FileQueuedForLoad;
        private ExportEntry ExportQueuedForFocusing;
        private readonly int UIndexQueuedForFocusing;
        private static readonly Color GraphEditorBackColor = Color.FromArgb(167, 167, 167);
        public string CurrentFile;
        public string JSONpath;

        private ExportEntry _currentExport;
        public ExportEntry CurrentExport
        {
            get => _currentExport;
            set
            {
                if (SetProperty(ref _currentExport, value))
                {
                    LoadBank(value, true);
                }
            }
        }

        private WwiseBank CurrentWwiseBank;

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand SaveImageCommand { get; set; }
        public ICommand SaveViewCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenFile);
            SaveCommand = new GenericCommand(SavePackage, IsPackageLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, IsPackageLoaded);
            //SaveImageCommand = new GenericCommand(SaveImage, CurrentObjects.Any);
            //SaveViewCommand = new GenericCommand(() => saveView(), CurrentObjects.Any);
        }

        private bool IsPackageLoaded() => Pcc != null;

        private void SavePackageAs()
        {
            string extension = Path.GetExtension(Pcc.FilePath);
            SaveFileDialog d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
            if (d.ShowDialog() == true)
            {
                Pcc.Save(d.FileName);
                MessageBox.Show(this, "Done.");
            }
        }

        private void SavePackage()
        {
            Pcc.Save();
        }

        private void OpenFile()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = App.FileFilter };
            if (d.ShowDialog() == true)
            {
#if !DEBUG
                try
                {
#endif
                LoadFile(d.FileName);
                AddRecent(d.FileName, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open file:\n" + ex.Message);
                }
#endif
            }
        }

        public void LoadFile(string s, int goToIndex = 0)
        {
            try
            {
                StatusBar_LeftMostText.Text =
                    $"Loading {Path.GetFileName(s)} ({ByteSize.FromBytes(new FileInfo(s).Length)})";
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
                LoadMEPackage(s);
                CurrentFile = Path.GetFileName(s);

                graphEditor.nodeLayer.RemoveAllChildren();
                graphEditor.edgeLayer.RemoveAllChildren();

                WwiseBankExports.ReplaceAll(Pcc.Exports.Where(exp => exp.ClassName == "WwiseBank"));

                StatusBar_LeftMostText.Text = Path.GetFileName(s);
                Title = $"Wwise Editor - {s}";

                AddRecent(s, false);
                SaveRecentList();
                RefreshRecent(true, RFiles);
                if (goToIndex != 0)
                {
                    CurrentExport = WwiseBankExports.FirstOrDefault(x => x.UIndex == goToIndex);
                    ExportQueuedForFocusing = CurrentExport;
                }
                else
                {
                    CurrentExport = null;
                }
            }
            catch (Exception e)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
                MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
                UnLoadMEPackage();
                Title = "Wwise Editor";
                CurrentFile = null;
            }
        }

        public void LoadBank(ExportEntry export, bool fromFile = false)
        {
            if (export == null)
            {
                return;
            }
            graphEditor.Enabled = false;
            graphEditor.UseWaitCursor = true;

            CurrentWwiseBank = export.GetBinaryData<WwiseBank>();
            SetupJSON(export);
            Properties_InterpreterWPF.LoadExport(export);

            if (fromFile)
            {
                if (File.Exists(JSONpath))
                {
                    SavedPositions = JsonConvert.DeserializeObject<List<SaveData>>(File.ReadAllText(JSONpath));
                }
                else
                {
                    SavedPositions = new List<SaveData>();
                }
            }
            try
            {
                GenerateGraph();
            }
            catch (Exception e) when (!App.IsDebug)
            {
                MessageBox.Show(this, $"Error loading WwiseBank:\n{e.Message}");
            }
            graphEditor.Enabled = true;
            graphEditor.UseWaitCursor = false;
        }

        private void GenerateGraph()
        {
            graphEditor.nodeLayer.RemoveAllChildren();
            graphEditor.edgeLayer.RemoveAllChildren();
            GetObjects(CurrentWwiseBank);
            Layout();
            foreach (var o in CurrentObjects)
            {
                //todo: mousedown handlers
            }

            if (SavedPositions.IsEmpty())
            {
                AutoLayout();
            }
        }

        private void GetObjects(WwiseBank bank)
        {
            var newObjs = new List<WwiseHircObjNode>();
            foreach ((uint id, WwiseBank.HIRCObject hircObject) in CurrentWwiseBank.HIRCObjects)
            {
                newObjs.Add(hircObject switch
                {
                    WwiseBank.Event evt => new WEvent(evt, 0, 0, graphEditor),
                    WwiseBank.EventAction evtAct => new WEventAction(evtAct, 0, 0, graphEditor),
                    WwiseBank.SoundSFXVoice sfxvoice => new WSoundSFXVoice(sfxvoice, 0, 0, graphEditor),
                    _ => new WGeneric(hircObject, 0, 0, graphEditor)
                });
            }

            CurrentObjects.ReplaceAll(newObjs);
        }

        public void Layout()
        {
            if (CurrentObjects != null && CurrentObjects.Any())
            {
                var wwiseEvents = new Dictionary<uint, List<ExportEntry>>();
                var wwiseStreams = new Dictionary<uint, ExportEntry>();
                foreach (ExportEntry exportEntry in Pcc.Exports)
                {
                    switch (exportEntry.ClassName)
                    {
                        case "WwiseEvent":
                            wwiseEvents.AddToListAt((exportEntry.GetProperty<IntProperty>("Id")?.Value ?? 0).ReinterpretAsUint(), exportEntry);
                            break;
                        case "WwiseStream":
                            wwiseStreams.Add((exportEntry.GetProperty<IntProperty>("Id")?.Value ?? 0).ReinterpretAsUint(), exportEntry);
                            break;
                    }
                }
                var referencedExports = new Dictionary<uint, List<WExport>>();
                foreach (var obj in CurrentObjects)
                {
                    graphEditor.addNode(obj);
                    switch (obj)
                    {
                        case WEvent wEvent:
                        {
                            if (!referencedExports.TryGetValue(wEvent.ID, out List<WExport> wExports))
                            {
                                if (!wwiseEvents.TryGetValue(wEvent.ID, out List<ExportEntry> wwiseEventExports))
                                {
                                    continue;
                                }

                                wExports = new List<WExport>();
                                foreach (var wwiseEventExp in wwiseEventExports)
                                {
                                    WExport wExp = new WExport(wwiseEventExp, 0, 0, graphEditor);
                                    wExports.Add(wExp);
                                    referencedExports.AddToListAt(wEvent.ID, wExp);
                                    graphEditor.addNode(wExp);
                                }
                            }
                            obj.Varlinks[0].Links.AddRange(wExports.Select(x => (uint)x.Export.UIndex));
                            break;
                        }
                        case WSoundSFXVoice wSound:
                        {
                            if (!referencedExports.TryGetValue(wSound.SoundSFXVoice.AudioID, out List<WExport> wExports))
                            {
                                if (!wwiseStreams.TryGetValue(wSound.SoundSFXVoice.AudioID, out ExportEntry wwiseSoundExport))
                                {
                                    continue;
                                }

                                wExports = new List<WExport>();
                                WExport wExp = new WExport(wwiseSoundExport, 0, 0, graphEditor);
                                wExports.Add(wExp);
                                referencedExports.AddToListAt(wSound.SoundSFXVoice.AudioID, wExp);
                                graphEditor.addNode(wExp);
                            }
                            obj.Varlinks[0].Links.Clear();
                            obj.Varlinks[0].Links.AddRange(wExports.Select(x => (uint)x.Export.UIndex));
                            break;
                        }
                    }
                }
                CurrentObjects.AddRange(referencedExports.Values.SelectMany(vals => vals));
                foreach (var obj in CurrentObjects)
                {
                    obj.CreateConnections(CurrentObjects);
                }

                float x = 0, y = 0;
                foreach (WwiseHircObjNode obj in CurrentObjects)
                {
                    SaveData savedInfo = new SaveData();
                    if (SavedPositions.Any())
                    {
                        savedInfo = SavedPositions.FirstOrDefault(p => obj.ID == p.ID);
                    }

                    bool hasSavedPosition = savedInfo.ID == obj.ID;
                    if (hasSavedPosition)
                    {
                        obj.Layout(savedInfo.X, savedInfo.Y);
                    }
                    obj.Layout();
                }

                foreach (WwiseEdEdge edge in graphEditor.edgeLayer)
                {
                    WwiseGraphEditor.UpdateEdge(edge);
                }
            }
        }

        private void AutoLayout()
        {
            foreach (WwiseHircObjNode obj in CurrentObjects)
            {
                obj.SetOffset(0, 0); //remove existing positioning
            }

            const float HORIZONTAL_SPACING = 40;
            const float VERTICAL_SPACING = 20;
            const float VAR_SPACING = 10;
            var visitedNodes = new HashSet<uint>();
            var eventNodes = CurrentObjects.OfType<WEvent>().ToList();
            WwiseHircObjNode firstNode = eventNodes.FirstOrDefault();
            var varNodeLookup = CurrentObjects.OfType<WExport>().ToDictionary(obj => obj.Export.UIndex);
            var opNodeLookup = CurrentObjects.OfType<WGeneric>().ToDictionary(obj => obj.ID);
            var rootTree = new List<WwiseHircObjNode>();
            //WEvents are natural root nodes. ALmost everything will proceed from one of these
            foreach (WEvent eventNode in eventNodes)
            {
                LayoutTree(eventNode, 5 * VERTICAL_SPACING);
            }

            //Find WGenerics with no inputs. These will not have been reached from an WEvent
            var orphanRoots = CurrentObjects.OfType<WGeneric>().Where(node => node.InputEdges.IsEmpty());
            foreach (WGeneric orphan in orphanRoots)
            {
                if (!visitedNodes.Contains(orphan.ID))
                {
                    LayoutTree(orphan, VERTICAL_SPACING);
                }
            }

            //It's possible that there are groups of otherwise unconnected WGenerics that form cycles.
            //Might be possible to make a better heuristic for choosing a root than sequence order, but this situation is so rare it's not worth the effort
            var cycleNodes = CurrentObjects.OfType<WGeneric>().Where(node => !visitedNodes.Contains(node.ID));
            foreach (WGeneric cycleNode in cycleNodes)
            {
                LayoutTree(cycleNode, VERTICAL_SPACING);
            }

            if (firstNode != null) CurrentObjects.OffsetBy(0, -firstNode.OffsetY);

            foreach (WwiseEdEdge edge in graphEditor.edgeLayer)
                WwiseGraphEditor.UpdateEdge(edge);


            void LayoutTree(WwiseHircObjNode WGeneric, float verticalSpacing)
            {
                if (firstNode == null) firstNode = WGeneric;
                visitedNodes.Add(WGeneric.ID);
                var subTree = LayoutSubTree(WGeneric);
                float width = subTree.BoundingRect().Width + HORIZONTAL_SPACING;
                //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
                float dy = rootTree.Where(node => node.GlobalFullBounds.Left < width).BoundingRect().Bottom;
                if (dy > 0) dy += verticalSpacing;
                subTree.OffsetBy(0, dy);
                rootTree.AddRange(subTree);
            }

            List<WwiseHircObjNode> LayoutSubTree(WwiseHircObjNode root)
            {
                var tree = new List<WwiseHircObjNode>();
                var vars = new List<WwiseHircObjNode>();
                foreach (var varLink in root.Varlinks)
                {
                    float dx = varLink.node.GlobalFullBounds.X - WExport.RADIUS;
                    float dy = root.GlobalFullHeight + VAR_SPACING;
                    foreach (uint id in varLink.Links.Where(id => !visitedNodes.Contains(id)))
                    {
                        visitedNodes.Add(id);
                        if (varNodeLookup.TryGetValue((int)id, out WExport WExport))
                        {
                            WExport.OffsetBy(dx, dy);
                            dy += WExport.GlobalFullHeight + VAR_SPACING;
                            vars.Add(WExport);
                        }
                        else if (opNodeLookup.TryGetValue(id, out WGeneric node))
                        {
                            node.OffsetBy(dx, dy);
                            dy += node.GlobalFullHeight + VAR_SPACING;
                            vars.Add(node);
                        }
                    }
                }

                var childTrees = new List<List<WwiseHircObjNode>>();
                var children = root.Outlinks.SelectMany(link => link.Links).Where(id => !visitedNodes.Contains(id));
                foreach (uint id in children)
                {
                    visitedNodes.Add(id);
                    if (opNodeLookup.TryGetValue(id, out WGeneric node))
                    {
                        List<WwiseHircObjNode> subTree = LayoutSubTree(node);
                        childTrees.Add(subTree);
                    }
                }

                if (childTrees.Any())
                {
                    float dx = root.GlobalFullWidth + (HORIZONTAL_SPACING * (1 + childTrees.Count * 0.4f));
                    foreach (List<WwiseHircObjNode> subTree in childTrees)
                    {
                        float subTreeWidth = subTree.BoundingRect().Width + HORIZONTAL_SPACING + dx;
                        //ignore nodes that are further to the right than this subtree is wide. This allows tighter spacing
                        float dy = tree.Where(node => node.GlobalFullBounds.Left < subTreeWidth).BoundingRect().Bottom;
                        if (dy > 0) dy += VERTICAL_SPACING;
                        subTree.OffsetBy(dx, dy);
                        //TODO: fix this so it doesn't screw up some sequences. eg: BioD_ProEar_310BigFall.pcc
                        /*float treeWidth = tree.BoundingRect().Width + HORIZONTAL_SPACING;
                        //tighten spacing when this subtree is wider than existing tree. 
                        dy -= subTree.Where(node => node.GlobalFullBounds.Left < treeWidth).BoundingRect().Top;
                        if (dy < 0) dy += VERTICAL_SPACING;
                        subTree.OffsetBy(0, dy);*/

                        tree.AddRange(subTree);
                    }

                    //center the root on its children
                    float centerOffset = tree.OfType<WGeneric>().BoundingRect().Height / 2 - root.GlobalFullHeight / 2;
                    root.OffsetBy(0, centerOffset);
                    vars.OffsetBy(0, centerOffset);
                }

                tree.AddRange(vars);
                tree.Add(root);
                return tree;
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //todo
        }

        private void WwiseEditorWPF_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileQueuedForLoad))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    //Wait for all children to finish loading
                    LoadFile(FileQueuedForLoad);
                    FileQueuedForLoad = null;

                    if (ExportQueuedForFocusing is null && Pcc.IsUExport(UIndexQueuedForFocusing))
                    {
                        ExportQueuedForFocusing = Pcc.GetUExport(UIndexQueuedForFocusing);
                    }

                    if (WwiseBankExports.Contains(ExportQueuedForFocusing))
                    {
                        CurrentExport = ExportQueuedForFocusing;
                    }
                    ExportQueuedForFocusing = null;

                    Activate();
                }));
            }
        }

        public static readonly string WwiseEditorDataFolder = Path.Combine(App.AppDataFolder, "WwiseEditor");
        public static readonly string ME3ViewsPath = Path.Combine(WwiseEditorDataFolder, "ME3Views");
        public static readonly string ME2ViewsPath = Path.Combine(WwiseEditorDataFolder, "ME2Views");

        private void SetupJSON(ExportEntry export)
        {
            string objectName = System.Text.RegularExpressions.Regex.Replace(export.ObjectName.Name, @"[<>:""/\\|?*]", "");

            var bankID = BitConverter.ToUInt32(BitConverter.GetBytes(export.GetProperty<IntProperty>("Id")), 0);
            string viewsPath = export.Game switch
            {
                MEGame.ME2 => ME2ViewsPath,
                _ => ME3ViewsPath
            };

            JSONpath = Path.Combine(viewsPath, $"{CurrentFile}.#{export.Index}.{bankID:X8}.{objectName}.JSON");
        }

        #region Recents
        private const string RECENTFILES_FILE = "RECENTFILES";
        public List<string> RFiles;
        readonly List<Button> RecentButtons = new List<Button>();
        private void LoadRecentList()
        {
            RecentButtons.Clear();
            RecentButtons.AddRange(new[] { RecentButton1, RecentButton2, RecentButton3, RecentButton4, RecentButton5, RecentButton6, RecentButton7, RecentButton8, RecentButton9, RecentButton10 });
            Recents_MenuItem.IsEnabled = false;
            RFiles = new List<string>();
            RFiles.Clear();
            string path = Path.Combine(WwiseEditorDataFolder, RECENTFILES_FILE);
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
            RefreshRecent(false);
        }

        private void SaveRecentList()
        {
            if (!Directory.Exists(WwiseEditorDataFolder))
            {
                Directory.CreateDirectory(WwiseEditorDataFolder);
            }
            string path = Path.Combine(WwiseEditorDataFolder, RECENTFILES_FILE);
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllLines(path, RFiles);
        }

        public void RefreshRecent(bool propogate, List<string> recents = null)
        {
            if (propogate && recents != null)
            {
                var forms = System.Windows.Forms.Application.OpenForms;
                foreach (var form in Application.Current.Windows)
                {
                    if (form is WwiseEditorWPF wpf && this != wpf)
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
            Recents_MenuItem.IsEnabled = RFiles.Count > 0;
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

        #region Busy

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

        void SetBusy(string text)
        {
            Image graphImage = graphEditor.Camera.ToImage((int)graphEditor.Camera.GlobalFullWidth, (int)graphEditor.Camera.GlobalFullHeight, new SolidBrush(GraphEditorBackColor));
            graphImageSub.Source = graphImage.ToBitmapImage();
            graphImageSub.Width = graphGrid.ActualWidth;
            graphImageSub.Height = graphGrid.ActualHeight;
            graphImageSub.Visibility = Visibility.Visible;
            BusyText = text;
            IsBusy = true;
        }

        void EndBusy()
        {
            IsBusy = false;
            graphImageSub.Visibility = Visibility.Collapsed;
        }

        #endregion

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, $"{CurrentFile} {value}");
        }
    }
}
